using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class ArenaLlmRoundController : MonoBehaviour
{
    [SerializeField] private ArenaRoundSnapshotBuilder snapshotBuilder;
    [SerializeField] private ArenaRoundApplier roundApplier;

    [SerializeField] private AgentTextInterface sideATextInterface;
    [SerializeField] private LlmProviderBehaviour sideAProvider;
    [SerializeField] private AgentTextInterface sideBTextInterface;
    [SerializeField] private LlmProviderBehaviour sideBProvider;

    [SerializeField] private int currentArenaRoundId;
    [SerializeField] private bool roundActive;
    [SerializeField] private bool sideARequestInFlight;
    [SerializeField] private bool sideBRequestInFlight;
    [SerializeField] private bool sideASubmitted;
    [SerializeField] private bool sideBSubmitted;
    [SerializeField] private ArenaSide nextTiePriority = ArenaSide.A;

    [TextArea(8, 24)]
    [SerializeField] private string latestSideAActionJson;
    [TextArea(8, 24)]
    [SerializeField] private string latestSideBActionJson;
    [TextArea(2, 6)]
    [SerializeField] private string latestStatus;
    [TextArea(2, 8)]
    [SerializeField] private string latestError;

    private readonly ArenaDecisionCoordinator coordinator =
        new ArenaDecisionCoordinator();

    private ArenaRoundSnapshot activeSnapshot;
    private string sideAObservation;
    private string sideBObservation;
    private string[] sideACitizenIds;
    private string[] sideAWorkplaceIds;
    private string[] sideBCitizenIds;
    private string[] sideBWorkplaceIds;
    private SimulationPauseLease pauseLease;
    private bool resolvingRound;
    private int lifecycleVersion;
    private int sideARequestVersion;
    private int sideBRequestVersion;

    public int CurrentArenaRoundId => currentArenaRoundId;
    public bool RoundActive => roundActive;
    public bool SideARequestInFlight => sideARequestInFlight;
    public bool SideBRequestInFlight => sideBRequestInFlight;
    public bool SideASubmitted => sideASubmitted;
    public bool SideBSubmitted => sideBSubmitted;
    public ArenaSide NextTiePriority => nextTiePriority;
    public string LatestSideAActionJson => latestSideAActionJson;
    public string LatestSideBActionJson => latestSideBActionJson;
    public string LatestStatus => latestStatus;
    public string LatestError => latestError;

    [ContextMenu("Request Arena LLM Round (Debug)")]
    private void RequestArenaLlmRoundDebug()
    {
        if (!Application.isPlaying)
        {
            ReportFailure(
                "Arena LLM rounds are available only during Play Mode.");
            return;
        }

        if (roundActive)
        {
            ReportFailure("An Arena LLM round is already active.");
            return;
        }

        if (!TryValidateConfiguration(
            out string[] configuredSideACitizenIds,
            out string[] configuredSideAWorkplaceIds,
            out string[] configuredSideBCitizenIds,
            out string[] configuredSideBWorkplaceIds,
            out string error))
        {
            ReportFailure(error);
            return;
        }

        pauseLease = SimulationPauseCoordinator.Acquire();

        if (!snapshotBuilder.TryBuild(out activeSnapshot, out error))
        {
            FailBeforeRoundOpened($"Snapshot failed: {error}");
            return;
        }

        if (!CitizenIdsMatchSnapshot(
                configuredSideACitizenIds,
                activeSnapshot) ||
            !CitizenIdsMatchSnapshot(
                configuredSideBCitizenIds,
                activeSnapshot))
        {
            FailBeforeRoundOpened(
                "Both Arena text interfaces must configure exactly the " +
                "citizens in the shared snapshot.");
            return;
        }

        if (!sideATextInterface.GenerateObservation())
        {
            FailBeforeRoundOpened(
                "Side A observation generation failed.");
            return;
        }

        sideAObservation = sideATextInterface.LatestObservation;

        if (!sideBTextInterface.GenerateObservation())
        {
            FailBeforeRoundOpened(
                "Side B observation generation failed.");
            return;
        }

        sideBObservation = sideBTextInterface.LatestObservation;

        if (string.IsNullOrWhiteSpace(sideAObservation) ||
            string.IsNullOrWhiteSpace(sideBObservation))
        {
            FailBeforeRoundOpened(
                "Both Arena observations must be non-empty.");
            return;
        }

        if (!coordinator.TryBeginRound(out int roundId))
        {
            FailBeforeRoundOpened(
                "Arena decision coordinator could not open a round.");
            return;
        }

        currentArenaRoundId = roundId;
        roundActive = true;
        sideACitizenIds = configuredSideACitizenIds;
        sideAWorkplaceIds = configuredSideAWorkplaceIds;
        sideBCitizenIds = configuredSideBCitizenIds;
        sideBWorkplaceIds = configuredSideBWorkplaceIds;
        latestSideAActionJson = string.Empty;
        latestSideBActionJson = string.Empty;
        latestError = string.Empty;
        latestStatus = $"Arena round {roundId}: requesting both actions.";
        SyncSubmissionState();

        StartRequest(ArenaSide.A, roundId);
        StartRequest(ArenaSide.B, roundId);
    }

    [ContextMenu("Retry Pending Arena Requests (Debug)")]
    private void RetryPendingArenaRequestsDebug()
    {
        if (!Application.isPlaying)
        {
            ReportFailure(
                "Arena LLM request retry is available only during Play Mode.");
            return;
        }

        if (!roundActive || !coordinator.IsRoundOpen)
        {
            ReportFailure("There is no active Arena round to retry.");
            return;
        }

        bool requested = false;

        if (!coordinator.HasActionA && !sideARequestInFlight)
        {
            requested |= StartRequest(ArenaSide.A, currentArenaRoundId);
        }

        if (!coordinator.HasActionB && !sideBRequestInFlight)
        {
            requested |= StartRequest(ArenaSide.B, currentArenaRoundId);
        }

        if (!requested)
        {
            latestStatus = coordinator.IsReady
                ? $"Arena round {currentArenaRoundId}: both actions are " +
                  "already submitted; no request was sent."
                : $"Arena round {currentArenaRoundId}: pending requests " +
                  "are already in flight.";
        }
    }

    private bool TryValidateConfiguration(
        out string[] configuredSideACitizenIds,
        out string[] configuredSideAWorkplaceIds,
        out string[] configuredSideBCitizenIds,
        out string[] configuredSideBWorkplaceIds,
        out string error)
    {
        configuredSideACitizenIds = Array.Empty<string>();
        configuredSideAWorkplaceIds = Array.Empty<string>();
        configuredSideBCitizenIds = Array.Empty<string>();
        configuredSideBWorkplaceIds = Array.Empty<string>();

        if (snapshotBuilder == null ||
            roundApplier == null ||
            sideATextInterface == null ||
            sideAProvider == null ||
            sideBTextInterface == null ||
            sideBProvider == null)
        {
            error = "Arena LLM round references are not fully configured.";
            return false;
        }

        if (sideATextInterface == sideBTextInterface ||
            sideAProvider == sideBProvider)
        {
            error =
                "Arena sides must use different text interfaces and providers.";
            return false;
        }

        if (!sideAProvider.isActiveAndEnabled ||
            !sideBProvider.isActiveAndEnabled)
        {
            error = "Both Arena LLM providers must be active and enabled.";
            return false;
        }

        if (nextTiePriority != ArenaSide.A &&
            nextTiePriority != ArenaSide.B)
        {
            error = "The next Arena tie priority is invalid.";
            return false;
        }

        if (snapshotBuilder.SideATreasury == null ||
            snapshotBuilder.SideBTreasury == null ||
            snapshotBuilder.SideATreasury == snapshotBuilder.SideBTreasury)
        {
            error = "Arena snapshot side treasuries are not valid.";
            return false;
        }

        if (sideATextInterface.Treasury != snapshotBuilder.SideATreasury ||
            sideBTextInterface.Treasury != snapshotBuilder.SideBTreasury)
        {
            error =
                "Arena text-interface and snapshot-builder side mappings differ.";
            return false;
        }

        if (!roundApplier.TryValidateConfiguration(
                snapshotBuilder,
                sideATextInterface,
                sideBTextInterface,
                out error))
        {
            error = $"ArenaRoundApplier configuration failed: {error}";
            return false;
        }

        if (!sideATextInterface.TryValidateArenaSideConfiguration(
                snapshotBuilder.SideATreasury,
                out error))
        {
            error = $"Side A observation configuration failed: {error}";
            return false;
        }

        if (!sideBTextInterface.TryValidateArenaSideConfiguration(
                snapshotBuilder.SideBTreasury,
                out error))
        {
            error = $"Side B observation configuration failed: {error}";
            return false;
        }

        if (!sideATextInterface.TryGetOfferConfiguration(
                out configuredSideACitizenIds,
                out CitizenEmployment[] configuredSideACitizens,
                out configuredSideAWorkplaceIds,
                out _,
                out error))
        {
            error = $"Side A offer configuration failed: {error}";
            return false;
        }

        if (!sideBTextInterface.TryGetOfferConfiguration(
                out configuredSideBCitizenIds,
                out CitizenEmployment[] configuredSideBCitizens,
                out configuredSideBWorkplaceIds,
                out _,
                out error))
        {
            error = $"Side B offer configuration failed: {error}";
            return false;
        }

        if (!snapshotBuilder.TryGetConfiguredCitizens(
                out IReadOnlyDictionary<string, CitizenEmployment>
                    authoritativeCitizens,
                out error))
        {
            error = $"Snapshot citizen configuration failed: {error}";
            return false;
        }

        if (!CitizenReferencesMatch(
                configuredSideACitizens,
                authoritativeCitizens))
        {
            error =
                "Side A must configure exactly the authoritative Arena " +
                "CitizenEmployment references.";
            return false;
        }

        if (!CitizenReferencesMatch(
                configuredSideBCitizens,
                authoritativeCitizens))
        {
            error =
                "Side B must configure exactly the authoritative Arena " +
                "CitizenEmployment references.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool CitizenReferencesMatch(
        CitizenEmployment[] configuredCitizens,
        IReadOnlyDictionary<string, CitizenEmployment>
            authoritativeCitizens)
    {
        if (configuredCitizens == null ||
            authoritativeCitizens == null ||
            configuredCitizens.Length != authoritativeCitizens.Count)
        {
            return false;
        }

        for (int i = 0; i < configuredCitizens.Length; i++)
        {
            CitizenEmployment configuredCitizen = configuredCitizens[i];

            if (configuredCitizen == null)
            {
                return false;
            }

            for (int earlierIndex = 0; earlierIndex < i; earlierIndex++)
            {
                if (ReferenceEquals(
                    configuredCitizen,
                    configuredCitizens[earlierIndex]))
                {
                    return false;
                }
            }

            bool foundAuthoritativeReference = false;

            foreach (CitizenEmployment authoritativeCitizen in
                authoritativeCitizens.Values)
            {
                if (ReferenceEquals(
                    configuredCitizen,
                    authoritativeCitizen))
                {
                    foundAuthoritativeReference = true;
                    break;
                }
            }

            if (!foundAuthoritativeReference)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CitizenIdsMatchSnapshot(
        string[] citizenIds,
        ArenaRoundSnapshot snapshot)
    {
        if (citizenIds == null ||
            snapshot == null ||
            citizenIds.Length != snapshot.Citizens.Count)
        {
            return false;
        }

        for (int i = 0; i < citizenIds.Length; i++)
        {
            if (!snapshot.Citizens.ContainsKey(citizenIds[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool StartRequest(ArenaSide side, int roundId)
    {
        if (!roundActive ||
            !coordinator.IsRoundOpen ||
            coordinator.CurrentRoundId != roundId ||
            !isActiveAndEnabled)
        {
            return false;
        }

        LlmProviderBehaviour provider;
        string observation;
        string[] citizenIds;
        string[] workplaceIds;
        int requestAttempt;

        if (side == ArenaSide.A)
        {
            if (sideARequestInFlight || coordinator.HasActionA)
            {
                return false;
            }

            sideARequestInFlight = true;
            requestAttempt = ++sideARequestVersion;
            provider = sideAProvider;
            observation = sideAObservation;
            citizenIds = sideACitizenIds;
            workplaceIds = sideAWorkplaceIds;
        }
        else if (side == ArenaSide.B)
        {
            if (sideBRequestInFlight || coordinator.HasActionB)
            {
                return false;
            }

            sideBRequestInFlight = true;
            requestAttempt = ++sideBRequestVersion;
            provider = sideBProvider;
            observation = sideBObservation;
            citizenIds = sideBCitizenIds;
            workplaceIds = sideBWorkplaceIds;
        }
        else
        {
            ReportFailure("Cannot request an action for an invalid Arena side.");
            return false;
        }

        if (!provider.isActiveAndEnabled)
        {
            SetRequestInFlight(side, false);
            ReportSideFailure(side, "configured provider is not active.");
            return false;
        }

        int requestLifecycleVersion = lifecycleVersion;
        latestStatus = $"Arena round {roundId}: Side {side} request in flight.";

        try
        {
            provider.RequestAction(
                observation,
                citizenIds,
                workplaceIds,
                result => HandleProviderResult(
                    requestLifecycleVersion,
                    requestAttempt,
                    roundId,
                    side,
                    result));
        }
        catch (Exception exception)
        {
            if (IsCurrentRequest(
                requestLifecycleVersion,
                requestAttempt,
                roundId,
                side))
            {
                SetRequestInFlight(side, false);
                ReportSideFailure(
                    side,
                    $"provider request threw: {exception.Message}");
            }

            return false;
        }

        return true;
    }

    private void HandleProviderResult(
        int requestLifecycleVersion,
        int requestAttempt,
        int roundId,
        ArenaSide side,
        LlmProviderResult result)
    {
        if (!IsCurrentRequest(
            requestLifecycleVersion,
            requestAttempt,
            roundId,
            side))
        {
            return;
        }

        SetRequestInFlight(side, false);

        if (result == null)
        {
            ReportSideFailure(side, "provider returned no result.");
            return;
        }

        if (!result.Success)
        {
            string detail = string.IsNullOrWhiteSpace(result.Error)
                ? "provider request failed."
                : result.Error;
            ReportSideFailure(side, detail);
            return;
        }

        if (string.IsNullOrWhiteSpace(result.ActionJson) ||
            !coordinator.TrySubmit(roundId, side, result.ActionJson))
        {
            ReportSideFailure(
                side,
                "provider returned an invalid or duplicate action payload.");
            return;
        }

        if (side == ArenaSide.A)
        {
            latestSideAActionJson = result.ActionJson;
        }
        else
        {
            latestSideBActionJson = result.ActionJson;
        }

        SyncSubmissionState();
        latestStatus = $"Arena round {roundId}: Side {side} submitted.";

        if (coordinator.IsReady)
        {
            latestError = string.Empty;
            ResolveAndApply(roundId);
        }
    }

    private bool IsCurrentRequest(
        int requestLifecycleVersion,
        int requestAttempt,
        int roundId,
        ArenaSide side)
    {
        if (!isActiveAndEnabled ||
            requestLifecycleVersion != lifecycleVersion ||
            !roundActive ||
            !coordinator.IsRoundOpen ||
            coordinator.CurrentRoundId != roundId)
        {
            return false;
        }

        return side == ArenaSide.A
            ? requestAttempt == sideARequestVersion && sideARequestInFlight
            : side == ArenaSide.B &&
              requestAttempt == sideBRequestVersion &&
              sideBRequestInFlight;
    }

    private void ResolveAndApply(int roundId)
    {
        if (resolvingRound ||
            !roundActive ||
            !coordinator.IsReady ||
            coordinator.CurrentRoundId != roundId)
        {
            return;
        }

        resolvingRound = true;
        ArenaSide tiePriorityBefore = nextTiePriority;

        try
        {
            if (!ArenaActionParser.TryParse(
                coordinator.ActionA,
                out ArenaAction actionA,
                out string error))
            {
                FailResolvedRound($"Side A action parse failed: {error}");
                return;
            }

            if (!ArenaActionParser.TryParse(
                coordinator.ActionB,
                out ArenaAction actionB,
                out error))
            {
                FailResolvedRound($"Side B action parse failed: {error}");
                return;
            }

            if (!ArenaOfferPairing.TryBuild(
                actionA,
                actionB,
                out IReadOnlyList<ArenaCitizenOfferPair> pairs,
                out error))
            {
                FailResolvedRound($"Offer pairing failed: {error}");
                return;
            }

            OfferConflictResolver temporaryConflictResolver =
                new OfferConflictResolver(tiePriorityBefore);

            if (!ArenaRoundResolver.TryResolve(
                pairs,
                activeSnapshot.Citizens,
                activeSnapshot.SideA,
                activeSnapshot.SideB,
                temporaryConflictResolver,
                out ArenaRoundResolution resolution,
                out error))
            {
                FailResolvedRound($"Round resolution failed: {error}");
                return;
            }

            if (!roundApplier.TryApply(activeSnapshot, resolution, out error))
            {
                FailResolvedRound($"Round application failed: {error}");
                return;
            }

            nextTiePriority = resolution.FinalTiePriority;

            if (!coordinator.TryCloseRound())
            {
                FailResolvedRound(
                    "Round applied, but the decision coordinator could not close it.");
                return;
            }

            currentArenaRoundId = roundId;
            roundActive = false;
            SyncSubmissionState();
            latestError = string.Empty;
            latestStatus = $"Arena round {roundId}: applied successfully.";

            Debug.Log(
                BuildSuccessSummary(
                    roundId,
                    tiePriorityBefore,
                    resolution),
                this);

            ClearActiveRoundData();
            ReleasePauseLease();
        }
        finally
        {
            resolvingRound = false;
        }
    }

    private void FailBeforeRoundOpened(string error)
    {
        activeSnapshot = null;
        ReportFailure(error);
        ReleasePauseLease();
    }

    private void FailResolvedRound(string error)
    {
        latestStatus =
            $"Arena round {currentArenaRoundId}: resolution/application failed.";
        latestError = error;
        Debug.LogError(error, this);
    }

    private void ReportSideFailure(ArenaSide side, string detail)
    {
        latestStatus = $"Arena round {currentArenaRoundId}: Side {side} failed.";
        latestError = $"Side {side}: {detail}";
        Debug.LogError(latestError, this);
    }

    private void ReportFailure(string error)
    {
        latestStatus = "Arena LLM round failed.";
        latestError = error;
        Debug.LogError(error, this);
    }

    private void SyncSubmissionState()
    {
        sideASubmitted = coordinator.HasActionA;
        sideBSubmitted = coordinator.HasActionB;
    }

    private void SetRequestInFlight(ArenaSide side, bool value)
    {
        if (side == ArenaSide.A)
        {
            sideARequestInFlight = value;
        }
        else
        {
            sideBRequestInFlight = value;
        }
    }

    private void ClearActiveRoundData()
    {
        activeSnapshot = null;
        sideAObservation = null;
        sideBObservation = null;
        sideACitizenIds = null;
        sideAWorkplaceIds = null;
        sideBCitizenIds = null;
        sideBWorkplaceIds = null;
        sideARequestInFlight = false;
        sideBRequestInFlight = false;
    }

    private void ReleasePauseLease()
    {
        if (!pauseLease.IsValid)
        {
            return;
        }

        SimulationPauseCoordinator.Release(pauseLease);
        pauseLease = default;
    }

    private void OnDisable()
    {
        lifecycleVersion++;
        sideARequestVersion++;
        sideBRequestVersion++;

        if (sideARequestInFlight && sideAProvider != null)
        {
            sideAProvider.CancelRequest();
        }

        if (sideBRequestInFlight && sideBProvider != null)
        {
            sideBProvider.CancelRequest();
        }

        sideARequestInFlight = false;
        sideBRequestInFlight = false;

        if (coordinator.IsRoundOpen)
        {
            coordinator.TryAbortRound();
        }

        if (roundActive)
        {
            roundActive = false;
            SyncSubmissionState();
            latestStatus =
                $"Arena round {currentArenaRoundId}: aborted because the " +
                "controller was disabled.";
            latestError = string.Empty;
        }

        ClearActiveRoundData();
        ReleasePauseLease();
    }

    private static string BuildSuccessSummary(
        int roundId,
        ArenaSide tiePriorityBefore,
        ArenaRoundResolution resolution)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("CIVILIZATION_ARENA_LLM_ROUND_RESULT");
        text.AppendLine("success=true");
        text.AppendLine(
            $"roundId={roundId.ToString(CultureInfo.InvariantCulture)}");
        text.AppendLine($"tiePriorityBefore={tiePriorityBefore}");
        text.AppendLine(
            $"tiePriorityAfter={resolution.FinalTiePriority}");
        text.AppendLine(
            $"payrollA={Format(resolution.FinalProjectedPayrollA)}");
        text.Append(
            $"payrollB={Format(resolution.FinalProjectedPayrollB)}");
        return text.ToString();
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
