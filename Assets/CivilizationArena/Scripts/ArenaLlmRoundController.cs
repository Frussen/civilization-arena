using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class ArenaLlmRoundController : MonoBehaviour
{
    [SerializeField] private ArenaRoundSnapshotBuilder snapshotBuilder;
    [SerializeField] private ArenaRoundApplier roundApplier;
    [SerializeField] private ArenaMatchController arenaMatchController;
    [SerializeField] private ArenaMatchLogger matchLogger;

    [SerializeField] private AgentControlMode sideAControlMode =
        AgentControlMode.Api;
    [SerializeField] private AgentTextInterface sideATextInterface;
    [SerializeField] private LlmProviderBehaviour sideAProvider;
    [SerializeField] private ArenaManualDecisionController
        sideAManualDecisionController;
    [SerializeField] private AgentControlMode sideBControlMode =
        AgentControlMode.Api;
    [SerializeField] private AgentTextInterface sideBTextInterface;
    [SerializeField] private LlmProviderBehaviour sideBProvider;
    [SerializeField] private ArenaManualDecisionController
        sideBManualDecisionController;

    [SerializeField] private WorldClock worldClock;
    [SerializeField] private bool automaticRoundsEnabled;
    [Min(1)]
    [SerializeField] private int roundIntervalMinutes = 360;
    [SerializeField] private bool requestRoundOnStart;

    [SerializeField] private int currentArenaRoundId;
    [SerializeField] private bool roundActive;
    [SerializeField] private bool sideARequestInFlight;
    [SerializeField] private bool sideBRequestInFlight;
    [SerializeField] private bool sideASubmitted;
    [SerializeField] private bool sideBSubmitted;
    [SerializeField] private ArenaSide nextTiePriority = ArenaSide.A;
    [SerializeField] private int minutesUntilNextRound = 360;

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
    private AgentControlMode activeSideAControlMode;
    private AgentControlMode activeSideBControlMode;
    private LlmProviderBehaviour activeSideAProvider;
    private LlmProviderBehaviour activeSideBProvider;
    private ArenaManualDecisionController activeSideAManualController;
    private ArenaManualDecisionController activeSideBManualController;
    private SimulationPauseLease pauseLease;
    private bool resolvingRound;
    private int lifecycleVersion;
    private int sideARequestVersion;
    private int sideBRequestVersion;
    private int sideAProviderAttempt;
    private int sideBProviderAttempt;
    private bool firstAutomaticUpdatePending;
    private bool automaticSchedulingWasEnabled;
    private bool automaticConfigurationErrorReported;

    public int CurrentArenaRoundId => currentArenaRoundId;
    public bool RoundActive => roundActive;
    public bool SideARequestInFlight => sideARequestInFlight;
    public bool SideBRequestInFlight => sideBRequestInFlight;
    public bool SideASubmitted => sideASubmitted;
    public bool SideBSubmitted => sideBSubmitted;
    public ArenaSide NextTiePriority => nextTiePriority;
    public int MinutesUntilNextRound => minutesUntilNextRound;
    public string LatestSideAActionJson => latestSideAActionJson;
    public string LatestSideBActionJson => latestSideBActionJson;
    public string LatestStatus => latestStatus;
    public string LatestError => latestError;
    public ArenaRoundSnapshotBuilder SnapshotBuilder => snapshotBuilder;
    public ArenaMatchController ArenaMatchController => arenaMatchController;
    public bool AutomaticRoundsEnabled => automaticRoundsEnabled;
    public int RoundIntervalMinutes => roundIntervalMinutes;
    public bool RequestRoundOnStart => requestRoundOnStart;
    public WorldClock WorldClock => worldClock;
    public AgentTextInterface SideATextInterface => sideATextInterface;
    public AgentTextInterface SideBTextInterface => sideBTextInterface;
    public AgentControlMode SideAControlMode => sideAControlMode;
    public AgentControlMode SideBControlMode => sideBControlMode;
    public LlmProviderBehaviour SideAProvider => sideAProvider;
    public LlmProviderBehaviour SideBProvider => sideBProvider;
    public ArenaManualDecisionController SideAManualDecisionController =>
        sideAManualDecisionController;
    public ArenaManualDecisionController SideBManualDecisionController =>
        sideBManualDecisionController;

    private void OnEnable()
    {
        automaticSchedulingWasEnabled = automaticRoundsEnabled;
        firstAutomaticUpdatePending = automaticRoundsEnabled;
        automaticConfigurationErrorReported = false;
        ResetAutomaticCountdown();
    }

    private void LateUpdate()
    {
        if (automaticRoundsEnabled != automaticSchedulingWasEnabled)
        {
            automaticSchedulingWasEnabled = automaticRoundsEnabled;
            firstAutomaticUpdatePending = automaticRoundsEnabled;
            automaticConfigurationErrorReported = false;
            ResetAutomaticCountdown();
        }

        if (!automaticRoundsEnabled || roundActive)
        {
            return;
        }

        if (arenaMatchController != null && arenaMatchController.IsMatchEnded)
        {
            return;
        }

        if (!TryValidateAutomaticScheduling(out string error))
        {
            if (!automaticConfigurationErrorReported)
            {
                ReportFailure(error);
                automaticConfigurationErrorReported = true;
            }

            return;
        }

        if (automaticConfigurationErrorReported)
        {
            automaticConfigurationErrorReported = false;
            firstAutomaticUpdatePending = false;
            ResetAutomaticCountdown();
            return;
        }

        if (firstAutomaticUpdatePending)
        {
            firstAutomaticUpdatePending = false;
            ResetAutomaticCountdown();

            if (requestRoundOnStart)
            {
                if (!TryStartArenaRound())
                {
                    ResetAutomaticCountdown();
                }

                return;
            }
        }

        int simulatedMinutes = worldClock.MinutesAdvancedThisFrame;
        if (simulatedMinutes <= 0)
        {
            return;
        }

        minutesUntilNextRound = Mathf.Max(
            0,
            minutesUntilNextRound - simulatedMinutes);

        if (minutesUntilNextRound == 0 && !TryStartArenaRound())
        {
            ResetAutomaticCountdown();
        }
    }

    [ContextMenu("Request Arena LLM Round (Debug)")]
    private void RequestArenaLlmRoundDebug()
    {
        TryStartArenaRound();
    }

    private bool TryStartArenaRound()
    {
        if (!Application.isPlaying)
        {
            ReportFailure(
                "Arena LLM rounds are available only during Play Mode.");
            return false;
        }

        if (roundActive)
        {
            ReportFailure("An Arena LLM round is already active.");
            return false;
        }

        if (arenaMatchController != null && arenaMatchController.IsMatchEnded)
        {
            ReportFailure("The Arena match has already ended.");
            return false;
        }

        if (!TryValidateConfiguration(
            out string[] configuredSideACitizenIds,
            out string[] configuredSideAWorkplaceIds,
            out string[] configuredSideBCitizenIds,
            out string[] configuredSideBWorkplaceIds,
            out string error))
        {
            ReportFailure(error);
            return false;
        }

        pauseLease = SimulationPauseCoordinator.Acquire();

        if (!snapshotBuilder.TryBuild(out activeSnapshot, out error))
        {
            FailBeforeRoundOpened($"Snapshot failed: {error}");
            return false;
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
            return false;
        }

        if (!sideATextInterface.GenerateArenaObservation())
        {
            FailBeforeRoundOpened(
                "Side A observation generation failed.");
            return false;
        }

        sideAObservation = sideATextInterface.LatestObservation;

        if (!sideBTextInterface.GenerateArenaObservation())
        {
            FailBeforeRoundOpened(
                "Side B observation generation failed.");
            return false;
        }

        sideBObservation = sideBTextInterface.LatestObservation;

        if (string.IsNullOrWhiteSpace(sideAObservation) ||
            string.IsNullOrWhiteSpace(sideBObservation))
        {
            FailBeforeRoundOpened(
                "Both Arena observations must be non-empty.");
            return false;
        }

        string[] roundSideACitizenIds =
            (string[])configuredSideACitizenIds.Clone();
        string[] roundSideAWorkplaceIds =
            (string[])configuredSideAWorkplaceIds.Clone();
        string[] roundSideBCitizenIds =
            (string[])configuredSideBCitizenIds.Clone();
        string[] roundSideBWorkplaceIds =
            (string[])configuredSideBWorkplaceIds.Clone();

        if (!coordinator.TryBeginRound(out int roundId))
        {
            FailBeforeRoundOpened(
                "Arena decision coordinator could not open a round.");
            return false;
        }

        currentArenaRoundId = roundId;
        roundActive = true;
        sideACitizenIds = roundSideACitizenIds;
        sideAWorkplaceIds = roundSideAWorkplaceIds;
        sideBCitizenIds = roundSideBCitizenIds;
        sideBWorkplaceIds = roundSideBWorkplaceIds;
        activeSideAControlMode = sideAControlMode;
        activeSideBControlMode = sideBControlMode;
        activeSideAProvider =
            activeSideAControlMode == AgentControlMode.Api
                ? sideAProvider
                : null;
        activeSideBProvider =
            activeSideBControlMode == AgentControlMode.Api
                ? sideBProvider
                : null;
        activeSideAManualController =
            activeSideAControlMode == AgentControlMode.Manual
                ? sideAManualDecisionController
                : null;
        activeSideBManualController =
            activeSideBControlMode == AgentControlMode.Manual
                ? sideBManualDecisionController
                : null;
        latestSideAActionJson = string.Empty;
        latestSideBActionJson = string.Empty;
        latestError = string.Empty;
        latestStatus = $"Arena round {roundId}: awaiting both actions.";
        sideAProviderAttempt = 0;
        sideBProviderAttempt = 0;
        SyncSubmissionState();

        if (matchLogger != null && matchLogger.isActiveAndEnabled)
        {
            matchLogger.RecordRoundStart(
                roundId,
                activeSnapshot,
                sideAObservation,
                sideBObservation,
                nextTiePriority,
                activeSideAControlMode,
                activeSideBControlMode);
        }

        if (!TryArmManualSide(ArenaSide.A, roundId, out error) ||
            !TryArmManualSide(ArenaSide.B, roundId, out error))
        {
            AbortStartedRound($"Manual Arena arming failed: {error}");
            return false;
        }

        if (activeSideAControlMode == AgentControlMode.Api)
        {
            StartRequest(ArenaSide.A, roundId);
        }

        if (activeSideBControlMode == AgentControlMode.Api)
        {
            StartRequest(ArenaSide.B, roundId);
        }

        return true;
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

        if (activeSideAControlMode == AgentControlMode.Api &&
            !coordinator.HasActionA &&
            !sideARequestInFlight)
        {
            requested |= StartRequest(ArenaSide.A, currentArenaRoundId);
        }

        if (activeSideBControlMode == AgentControlMode.Api &&
            !coordinator.HasActionB &&
            !sideBRequestInFlight)
        {
            requested |= StartRequest(ArenaSide.B, currentArenaRoundId);
        }

        if (!requested)
        {
            latestStatus = coordinator.IsReady
                ? $"Arena round {currentArenaRoundId}: both actions are " +
                  "already submitted; no request was sent."
                : $"Arena round {currentArenaRoundId}: waiting for " +
                  "pending Manual submissions or Api requests.";
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
            arenaMatchController == null ||
            sideATextInterface == null ||
            sideBTextInterface == null)
        {
            error = "Arena round references are not fully configured.";
            return false;
        }

        if (!arenaMatchController.isActiveAndEnabled)
        {
            error = "ArenaMatchController must be active and enabled.";
            return false;
        }

        if (!IsValidControlMode(sideAControlMode) ||
            !IsValidControlMode(sideBControlMode))
        {
            error = "Arena side control modes are invalid.";
            return false;
        }

        if (sideATextInterface == sideBTextInterface)
        {
            error = "Arena sides must use different text interfaces.";
            return false;
        }

        if (sideAControlMode == AgentControlMode.Api &&
            (sideAProvider == null || !sideAProvider.isActiveAndEnabled))
        {
            error = "Side A Api provider must be active and enabled.";
            return false;
        }

        if (sideBControlMode == AgentControlMode.Api &&
            (sideBProvider == null || !sideBProvider.isActiveAndEnabled))
        {
            error = "Side B Api provider must be active and enabled.";
            return false;
        }

        if (sideAControlMode == AgentControlMode.Api &&
            sideBControlMode == AgentControlMode.Api &&
            sideAProvider == sideBProvider)
        {
            error = "Api Arena sides must use different providers.";
            return false;
        }

        if (sideAControlMode == AgentControlMode.Manual &&
            !CanArmManualController(sideAManualDecisionController))
        {
            error =
                "Side A Manual decision controller must be active, enabled, " +
                "and available for a new round.";
            return false;
        }

        if (sideBControlMode == AgentControlMode.Manual &&
            !CanArmManualController(sideBManualDecisionController))
        {
            error =
                "Side B Manual decision controller must be active, enabled, " +
                "and available for a new round.";
            return false;
        }

        if (sideAControlMode == AgentControlMode.Manual &&
            sideBControlMode == AgentControlMode.Manual &&
            sideAManualDecisionController == sideBManualDecisionController)
        {
            error =
                "Manual Arena sides must use different decision controllers.";
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

        if (!arenaMatchController.TryValidateConfiguration(out error))
        {
            error = $"ArenaMatchController configuration failed: {error}";
            return false;
        }

        if (arenaMatchController.SideATreasury !=
                snapshotBuilder.SideATreasury ||
            arenaMatchController.SideBTreasury !=
                snapshotBuilder.SideBTreasury)
        {
            error =
                "Arena match and snapshot-builder side mappings differ.";
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

    private bool TryValidateAutomaticScheduling(out string error)
    {
        if (worldClock == null)
        {
            error = "Automatic Arena rounds require a WorldClock.";
            return false;
        }

        if (roundIntervalMinutes <= 0)
        {
            error =
                "Arena round interval minutes must be greater than zero.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsValidControlMode(AgentControlMode controlMode)
    {
        return controlMode == AgentControlMode.Api ||
            controlMode == AgentControlMode.Manual;
    }

    private static bool CanArmManualController(
        ArenaManualDecisionController manualController)
    {
        if (manualController == null ||
            !manualController.isActiveAndEnabled)
        {
            return false;
        }

        return manualController.Status == ArenaManualDecisionStatus.Idle ||
            manualController.Status == ArenaManualDecisionStatus.Completed ||
            manualController.Status == ArenaManualDecisionStatus.Aborted;
    }

    private bool TryArmManualSide(
        ArenaSide side,
        int roundId,
        out string error)
    {
        AgentControlMode controlMode;
        ArenaManualDecisionController manualController;
        string observation;
        string[] citizenIds;
        string[] workplaceIds;

        if (side == ArenaSide.A)
        {
            controlMode = activeSideAControlMode;
            manualController = activeSideAManualController;
            observation = sideAObservation;
            citizenIds = sideACitizenIds;
            workplaceIds = sideAWorkplaceIds;
        }
        else if (side == ArenaSide.B)
        {
            controlMode = activeSideBControlMode;
            manualController = activeSideBManualController;
            observation = sideBObservation;
            citizenIds = sideBCitizenIds;
            workplaceIds = sideBWorkplaceIds;
        }
        else
        {
            error = "Cannot arm an invalid Arena side.";
            return false;
        }

        if (controlMode == AgentControlMode.Api)
        {
            error = null;
            return true;
        }

        return manualController.TryArmForRound(
            roundId,
            side,
            observation,
            citizenIds,
            workplaceIds,
            TrySubmitCanonicalAction,
            out error);
    }

    private bool CompleteManualSides(int roundId, out string error)
    {
        if (activeSideAControlMode == AgentControlMode.Manual &&
            !activeSideAManualController.CompleteRound(roundId, out error))
        {
            error = $"Side A Manual completion failed: {error}";
            return false;
        }

        if (activeSideBControlMode == AgentControlMode.Manual &&
            !activeSideBManualController.CompleteRound(roundId, out error))
        {
            error = $"Side B Manual completion failed: {error}";
            return false;
        }

        error = null;
        return true;
    }

    private void AbortManualSides(int roundId)
    {
        AbortManualSide(activeSideAManualController, roundId);
        AbortManualSide(activeSideBManualController, roundId);
    }

    private static void AbortManualSide(
        ArenaManualDecisionController manualController,
        int roundId)
    {
        if (manualController == null ||
            manualController.CurrentRoundId != roundId ||
            (manualController.Status !=
                ArenaManualDecisionStatus.WaitingForSubmission &&
             manualController.Status != ArenaManualDecisionStatus.Submitted))
        {
            return;
        }

        manualController.AbortRound(roundId, out _);
    }

    private void AbortStartedRound(string error)
    {
        AbortManualSides(currentArenaRoundId);

        if (coordinator.IsRoundOpen)
        {
            coordinator.TryAbortRound();
        }

        roundActive = false;
        SyncSubmissionState();
        ClearActiveRoundData();
        ReportFailure(error);
        ReleasePauseLease();
    }

    private void ResetAutomaticCountdown()
    {
        minutesUntilNextRound = roundIntervalMinutes > 0
            ? roundIntervalMinutes
            : 0;
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

        if ((side == ArenaSide.A &&
             activeSideAControlMode != AgentControlMode.Api) ||
            (side == ArenaSide.B &&
             activeSideBControlMode != AgentControlMode.Api))
        {
            return false;
        }

        LlmProviderBehaviour provider;
        string observation;
        string[] citizenIds;
        string[] workplaceIds;
        int requestAttempt;
        int providerAttempt;

        if (side == ArenaSide.A)
        {
            if (sideARequestInFlight || coordinator.HasActionA)
            {
                return false;
            }

            sideARequestInFlight = true;
            requestAttempt = ++sideARequestVersion;
            providerAttempt = ++sideAProviderAttempt;
            provider = activeSideAProvider;
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
            providerAttempt = ++sideBProviderAttempt;
            provider = activeSideBProvider;
            observation = sideBObservation;
            citizenIds = sideBCitizenIds;
            workplaceIds = sideBWorkplaceIds;
        }
        else
        {
            ReportFailure("Cannot request an action for an invalid Arena side.");
            return false;
        }

        if (provider == null || !provider.isActiveAndEnabled)
        {
            SetRequestInFlight(side, false);
            RecordProviderResult(
                roundId,
                side,
                providerAttempt,
                false,
                null,
                "configured provider is not active.");
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
                    providerAttempt,
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
                RecordProviderResult(
                    roundId,
                    side,
                    providerAttempt,
                    false,
                    null,
                    $"provider request threw: {exception.Message}");
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
        int providerAttempt,
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
            RecordProviderResult(
                roundId,
                side,
                providerAttempt,
                false,
                null,
                "provider returned no result.");
            ReportSideFailure(side, "provider returned no result.");
            return;
        }

        if (!result.Success)
        {
            string detail = string.IsNullOrWhiteSpace(result.Error)
                ? "provider request failed."
                : result.Error;
            RecordProviderResult(
                roundId,
                side,
                providerAttempt,
                false,
                null,
                detail);
            ReportSideFailure(side, detail);
            return;
        }

        RecordProviderResult(
            roundId,
            side,
            providerAttempt,
            true,
            result.ActionJson,
            null);

        if (string.IsNullOrWhiteSpace(result.ActionJson))
        {
            ReportSideFailure(
                side,
                "provider returned an invalid or duplicate action payload.");
            return;
        }

        if (!ArenaActionParser.TryParse(
                result.ActionJson,
                out ArenaAction action,
                out string error))
        {
            ReportSideFailure(
                side,
                $"provider action parse failed: {error}");
            return;
        }

        string previousActionJson;

        if (side == ArenaSide.A)
        {
            previousActionJson = latestSideAActionJson;
            latestSideAActionJson = result.ActionJson;
        }
        else
        {
            previousActionJson = latestSideBActionJson;
            latestSideBActionJson = result.ActionJson;
        }

        if (!TrySubmitCanonicalAction(
                roundId,
                side,
                action,
                out error))
        {
            if (side == ArenaSide.A)
            {
                latestSideAActionJson = previousActionJson;
            }
            else
            {
                latestSideBActionJson = previousActionJson;
            }

            ReportSideFailure(
                side,
                $"provider action submission failed: {error}");
            return;
        }
    }

    private bool TrySubmitCanonicalAction(
        int roundId,
        ArenaSide side,
        ArenaAction action,
        out string error)
    {
        if (!roundActive ||
            !coordinator.IsRoundOpen ||
            coordinator.CurrentRoundId != roundId)
        {
            error = "Arena round is not active or the round ID is stale.";
            return false;
        }

        string[] allowedCitizenIds;
        string[] allowedWorkplaceIds;

        if (side == ArenaSide.A)
        {
            allowedCitizenIds = sideACitizenIds;
            allowedWorkplaceIds = sideAWorkplaceIds;
        }
        else if (side == ArenaSide.B)
        {
            allowedCitizenIds = sideBCitizenIds;
            allowedWorkplaceIds = sideBWorkplaceIds;
        }
        else
        {
            error = "Arena action submission side must be A or B.";
            return false;
        }

        if (!ArenaActionValidator.TryValidate(
                action,
                allowedCitizenIds,
                allowedWorkplaceIds,
                out error))
        {
            return false;
        }

        if (!coordinator.TrySubmit(roundId, side, action))
        {
            error = "Arena action is duplicate or could not be submitted.";
            return false;
        }

        if (matchLogger != null && matchLogger.isActiveAndEnabled)
        {
            AgentControlMode source = side == ArenaSide.A
                ? activeSideAControlMode
                : activeSideBControlMode;
            matchLogger.RecordActionSubmitted(
                roundId,
                side,
                source,
                action);
        }

        SyncSubmissionState();
        latestStatus = $"Arena round {roundId}: Side {side} submitted.";

        if (coordinator.IsReady)
        {
            latestError = string.Empty;
            ResolveAndApply(roundId);
        }

        error = null;
        return true;
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
            if (!ArenaOfferPairing.TryBuild(
                coordinator.ActionA,
                coordinator.ActionB,
                out IReadOnlyList<ArenaCitizenOfferPair> pairs,
                out string error))
            {
                FailResolvedRound("pairing", $"Offer pairing failed: {error}");
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
                FailResolvedRound(
                    "resolution",
                    $"Round resolution failed: {error}");
                return;
            }

            if (!roundApplier.TryApply(activeSnapshot, resolution, out error))
            {
                FailResolvedRound(
                    "application",
                    $"Round application failed: {error}");
                return;
            }

            nextTiePriority = resolution.FinalTiePriority;

            if (!CompleteManualSides(roundId, out error))
            {
                FailResolvedRound("manual_complete", error);
                return;
            }

            if (!coordinator.TryCloseRound())
            {
                FailResolvedRound(
                    "coordinator_close",
                    "Round applied, but the decision coordinator could not close it.");
                return;
            }

            currentArenaRoundId = roundId;
            roundActive = false;
            SyncSubmissionState();
            ResetAutomaticCountdown();
            latestError = string.Empty;
            latestStatus = $"Arena round {roundId}: applied successfully.";

            Debug.Log(
                BuildSuccessSummary(
                    roundId,
                    tiePriorityBefore,
                    resolution),
                this);

            if (matchLogger != null && matchLogger.isActiveAndEnabled)
            {
                matchLogger.RecordRoundResult(roundId, resolution);
            }

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

    private void FailResolvedRound(string stage, string error)
    {
        latestStatus =
            $"Arena round {currentArenaRoundId}: resolution/application failed.";
        latestError = error;
        Debug.LogError(error, this);

        if (matchLogger != null && matchLogger.isActiveAndEnabled)
        {
            matchLogger.RecordRoundFailure(
                currentArenaRoundId,
                stage,
                error);
        }
    }

    private void RecordProviderResult(
        int roundId,
        ArenaSide side,
        int attempt,
        bool success,
        string actionJson,
        string error)
    {
        if (matchLogger != null && matchLogger.isActiveAndEnabled)
        {
            matchLogger.RecordProviderResult(
                roundId,
                side,
                attempt,
                success,
                actionJson,
                error);
        }
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
        activeSideAProvider = null;
        activeSideBProvider = null;
        activeSideAManualController = null;
        activeSideBManualController = null;
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

        if (sideARequestInFlight && activeSideAProvider != null)
        {
            activeSideAProvider.CancelRequest();
        }

        if (sideBRequestInFlight && activeSideBProvider != null)
        {
            activeSideBProvider.CancelRequest();
        }

        sideARequestInFlight = false;
        sideBRequestInFlight = false;

        if (roundActive)
        {
            AbortManualSides(currentArenaRoundId);
        }

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
