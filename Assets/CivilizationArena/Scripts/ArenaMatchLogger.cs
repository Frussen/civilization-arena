using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class ArenaMatchLogger : MonoBehaviour
{
    private const int SchemaVersion = 1;

    [SerializeField] private ArenaRoundSnapshotBuilder snapshotBuilder;
    [SerializeField] private ArenaMatchController matchController;
    [SerializeField] private ArenaLlmRoundController roundController;

    [SerializeField] private string runId;
    [SerializeField] private string currentLogPath;
    [SerializeField] private bool fileLoggingActive;

    private StreamWriter writer;
    private bool initializationAttempted;
    private bool loggingFailureReported;
    private bool matchEndRecorded;
    private int roundsOpened;
    private int roundsApplied;
    private int providerAttemptsA;
    private int providerAttemptsB;
    private int providerFailuresA;
    private int providerFailuresB;

    public string RunId => runId;
    public string CurrentLogPath => currentLogPath;
    public bool FileLoggingActive => fileLoggingActive;

    public void EnsureMatchLogStarted()
    {
        EnsureMatchStarted();
    }

    public void RecordRoundStart(
        int roundId,
        ArenaRoundSnapshot snapshot,
        string observationA,
        string observationB,
        ArenaSide tiePriorityBefore,
        AgentControlMode sourceA,
        AgentControlMode sourceB)
    {
        if (!EnsureMatchStarted())
        {
            return;
        }

        roundsOpened++;
        RoundStartEvent eventData = NewRoundStartEvent();
        eventData.roundId = roundId;
        SetSimulatedTime(eventData);
        eventData.tiePriorityBefore = tiePriorityBefore.ToString();
        eventData.sourceA = sourceA.ToString();
        eventData.sourceB = sourceB.ToString();
        eventData.observationA = observationA;
        eventData.observationB = observationB;
        eventData.state = TryBuildState(snapshot, out string stateError);
        eventData.stateError = stateError;
        WriteEvent(eventData);
    }

    public void RecordActionSubmitted(
        int roundId,
        ArenaSide side,
        AgentControlMode source,
        ArenaAction action)
    {
        if (!EnsureMatchStarted() || action == null)
        {
            return;
        }

        ActionSubmittedEvent eventData = NewActionSubmittedEvent();
        eventData.roundId = roundId;
        eventData.side = side.ToString();
        eventData.source = source.ToString();
        eventData.strategyNote = action.StrategyNote;
        eventData.offers = BuildActionOffers(action);
        WriteEvent(eventData);
    }

    public void RecordProviderResult(
        int roundId,
        ArenaSide side,
        int attempt,
        bool success,
        string actionJson,
        string error)
    {
        if (!EnsureMatchStarted())
        {
            return;
        }

        if (side == ArenaSide.A)
        {
            providerAttemptsA++;
            if (!success)
            {
                providerFailuresA++;
            }
        }
        else if (side == ArenaSide.B)
        {
            providerAttemptsB++;
            if (!success)
            {
                providerFailuresB++;
            }
        }

        ProviderResultEvent eventData = NewProviderResultEvent();
        eventData.roundId = roundId;
        eventData.side = side.ToString();
        eventData.attempt = attempt;
        eventData.success = success;
        eventData.actionJson = actionJson;
        eventData.error = error;
        WriteEvent(eventData);
    }

    public void RecordRoundResult(
        int roundId,
        ArenaRoundResolution resolution)
    {
        if (!EnsureMatchStarted() || resolution == null)
        {
            return;
        }

        roundsApplied++;
        RoundResultEvent eventData = NewRoundResultEvent();
        eventData.roundId = roundId;
        SetSimulatedTime(eventData);
        eventData.tiePriorityBefore =
            resolution.InitialTiePriority.ToString();
        eventData.tiePriorityAfter =
            resolution.FinalTiePriority.ToString();
        eventData.finalProjectedPayrollA =
            resolution.FinalProjectedPayrollA;
        eventData.finalProjectedPayrollB =
            resolution.FinalProjectedPayrollB;
        eventData.citizens = BuildResolutionEntries(resolution);

        string snapshotError = null;
        if (snapshotBuilder != null &&
            snapshotBuilder.TryBuild(
                out ArenaRoundSnapshot postSnapshot,
                out snapshotError))
        {
            eventData.postApplicationState =
                TryBuildState(postSnapshot, out string stateError);
            eventData.stateError = stateError;
        }
        else
        {
            eventData.stateError = snapshotBuilder == null
                ? "Snapshot builder is not configured."
                : snapshotError;
        }

        WriteEvent(eventData);
    }

    public void RecordRoundFailure(
        int roundId,
        string stage,
        string error)
    {
        if (!EnsureMatchStarted())
        {
            return;
        }

        RoundFailureEvent eventData = NewRoundFailureEvent();
        eventData.roundId = roundId;
        eventData.stage = stage;
        eventData.error = error;
        WriteEvent(eventData);
    }

    public void RecordMatchEnd(ArenaMatchResult result)
    {
        if (matchEndRecorded || !EnsureMatchStarted())
        {
            return;
        }

        matchEndRecorded = true;
        MatchEndEvent eventData = NewMatchEndEvent();
        eventData.result = FormatMatchResult(result);
        SetSimulatedTime(eventData);
        eventData.roundsOpened = roundsOpened;
        eventData.roundsApplied = roundsApplied;
        eventData.providerAttemptsA = providerAttemptsA;
        eventData.providerAttemptsB = providerAttemptsB;
        eventData.providerFailuresA = providerFailuresA;
        eventData.providerFailuresB = providerFailuresB;

        string snapshotError = null;
        if (snapshotBuilder != null &&
            snapshotBuilder.TryBuild(
                out ArenaRoundSnapshot finalSnapshot,
                out snapshotError))
        {
            eventData.finalState =
                TryBuildState(finalSnapshot, out string stateError);
            eventData.stateError = stateError;
        }
        else
        {
            eventData.stateError = snapshotBuilder == null
                ? "Snapshot builder is not configured."
                : snapshotError;
        }

        WriteEvent(eventData);
        CloseWriter();
    }

    private bool EnsureMatchStarted()
    {
        if (initializationAttempted)
        {
            if (loggingFailureReported || matchEndRecorded)
            {
                return false;
            }

            if (fileLoggingActive && writer != null)
            {
                return true;
            }

            return TryReopenMatchLog();
        }

        initializationAttempted = true;

        if (snapshotBuilder == null ||
            matchController == null ||
            roundController == null)
        {
            DisableLogging(
                "Arena match logger references are not fully configured.");
            return false;
        }

        if (roundController.SnapshotBuilder != snapshotBuilder ||
            roundController.ArenaMatchController != matchController ||
            matchController.SideATreasury != snapshotBuilder.SideATreasury ||
            matchController.SideBTreasury != snapshotBuilder.SideBTreasury)
        {
            DisableLogging(
                "Arena match logger integration mappings do not agree.");
            return false;
        }

        try
        {
            runId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string logDirectory = Path.Combine(
                projectRoot,
                "Logs",
                "Arena");
            Directory.CreateDirectory(logDirectory);

            string timestamp = DateTime.UtcNow.ToString(
                "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture);
            currentLogPath = Path.Combine(
                logDirectory,
                $"arena-match-{timestamp}-{runId}.jsonl");
            writer = new StreamWriter(
                currentLogPath,
                false,
                new UTF8Encoding(false));
            writer.AutoFlush = true;
            fileLoggingActive = true;
            Debug.Log(
                $"CIVILIZATION_ARENA_MATCH_LOG path={currentLogPath}",
                this);

            MatchStartEvent eventData = NewMatchStartEvent();
            eventData.wallClockUtc = DateTime.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);
            eventData.sideATreasuryId =
                snapshotBuilder.SideATreasury != null
                    ? snapshotBuilder.SideATreasury.gameObject.name
                    : null;
            eventData.sideBTreasuryId =
                snapshotBuilder.SideBTreasury != null
                    ? snapshotBuilder.SideBTreasury.gameObject.name
                    : null;
            eventData.wonderAId = matchController.WonderA != null
                ? matchController.WonderA.gameObject.name
                : null;
            eventData.wonderBId = matchController.WonderB != null
                ? matchController.WonderB.gameObject.name
                : null;
            eventData.roundIntervalMinutes =
                roundController.RoundIntervalMinutes;
            eventData.automaticRoundsEnabled =
                roundController.AutomaticRoundsEnabled;
            eventData.requestRoundOnStart =
                roundController.RequestRoundOnStart;
            eventData.initialTiePriority =
                roundController.NextTiePriority.ToString();
            AgentControlMode sourceA = roundController.SideAControlMode;
            AgentControlMode sourceB = roundController.SideBControlMode;
            eventData.sourceA = sourceA.ToString();
            eventData.sourceB = sourceB.ToString();
            eventData.providerA = sourceA == AgentControlMode.Api
                ? BuildProvider(roundController.SideAProvider)
                : null;
            eventData.providerB = sourceB == AgentControlMode.Api
                ? BuildProvider(roundController.SideBProvider)
                : null;
            SetSimulatedTime(eventData);

            if (snapshotBuilder.TryBuild(
                out ArenaRoundSnapshot initialSnapshot,
                out string snapshotError))
            {
                eventData.initialState =
                    TryBuildState(initialSnapshot, out string stateError);
                eventData.stateError = stateError;
            }
            else
            {
                eventData.stateError = snapshotError;
            }

            WriteEvent(eventData);
            return fileLoggingActive;
        }
        catch (Exception exception)
        {
            DisableLogging(
                $"Could not create Arena match log: {exception.Message}");
            return false;
        }
    }

    private bool TryReopenMatchLog()
    {
        if (string.IsNullOrWhiteSpace(currentLogPath))
        {
            DisableLogging(
                "Could not resume Arena match log because its path is missing.");
            return false;
        }

        FileStream stream = null;
        try
        {
            stream = new FileStream(
                currentLogPath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.Read);
            stream.Seek(0, SeekOrigin.End);
            writer = new StreamWriter(
                stream,
                new UTF8Encoding(false));
            stream = null;
            writer.AutoFlush = true;
            fileLoggingActive = true;
            return true;
        }
        catch (Exception exception)
        {
            stream?.Dispose();
            DisableLogging(
                $"Could not resume Arena match log: {exception.Message}");
            return false;
        }
    }

    private MatchStateLog TryBuildState(
        ArenaRoundSnapshot snapshot,
        out string error)
    {
        error = null;

        if (snapshot == null ||
            snapshotBuilder == null ||
            matchController == null ||
            roundController == null)
        {
            error = "Arena state logging references are incomplete.";
            return null;
        }

        if (!snapshotBuilder.TryGetConfiguredCitizens(
                out IReadOnlyDictionary<string, CitizenEmployment> citizens,
                out error))
        {
            return null;
        }

        MatchStateLog state = new MatchStateLog
        {
            sideA = BuildSideState(
                snapshot.SideA,
                snapshotBuilder.SideATreasury,
                matchController.WonderA),
            sideB = BuildSideState(
                snapshot.SideB,
                snapshotBuilder.SideBTreasury,
                matchController.WonderB)
        };

        List<string> citizenIds = new List<string>(snapshot.Citizens.Keys);
        citizenIds.Sort(StringComparer.Ordinal);
        state.citizens = new CitizenStateLog[citizenIds.Count];

        for (int i = 0; i < citizenIds.Count; i++)
        {
            string citizenId = citizenIds[i];
            ArenaCitizenEmploymentSnapshot citizenSnapshot =
                snapshot.Citizens[citizenId];
            citizens.TryGetValue(
                citizenId,
                out CitizenEmployment citizenComponent);
            Workplace workplace = citizenComponent != null
                ? citizenComponent
                    .GetComponent<CitizenWorkAssignment>()
                    ?.CurrentWorkplace
                : null;
            AgentTextInterface textInterface =
                citizenSnapshot.CurrentEmployerSide == ArenaSide.B
                    ? roundController.SideBTextInterface
                    : roundController.SideATextInterface;

            state.citizens[i] = new CitizenStateLog
            {
                citizenId = citizenId,
                employer = citizenSnapshot.CurrentEmployerSide.HasValue
                    ? citizenSnapshot.CurrentEmployerSide.Value.ToString()
                    : "none",
                wage = citizenSnapshot.CurrentWage,
                reservationWage = citizenSnapshot.ReservationWage,
                workplaceId = workplace == null
                    ? "none"
                    : textInterface?.GetConfiguredWorkplaceId(workplace)
            };
        }

        return state;
    }

    private static SideStateLog BuildSideState(
        ArenaAgentEconomicSnapshot economy,
        AgentTreasury treasury,
        WonderConstruction wonder)
    {
        AgentResourceStockpile stockpile = treasury != null
            ? treasury.GetComponent<AgentResourceStockpile>()
            : null;

        return new SideStateLog
        {
            gold = economy?.Gold ?? 0f,
            payrollPerHour = economy?.CurrentPayrollPerHour ?? 0f,
            payrollCoverageHours = economy?.PayrollCoverageHours ?? 0f,
            stone = stockpile != null ? stockpile.Stone : 0f,
            wood = stockpile != null ? stockpile.Wood : 0f,
            wonderLaborCompleted = wonder != null
                ? wonder.LaborHoursCompleted
                : 0f,
            wonderLaborRequired = wonder != null
                ? wonder.LaborHoursRequired
                : 0f,
            wonderCompleted = wonder != null && wonder.Completed
        };
    }

    private static ResolutionCitizenLog[] BuildResolutionEntries(
        ArenaRoundResolution resolution)
    {
        ResolutionCitizenLog[] entries =
            new ResolutionCitizenLog[resolution.Citizens.Count];

        for (int i = 0; i < resolution.Citizens.Count; i++)
        {
            ArenaCitizenOfferResolution citizen = resolution.Citizens[i];
            entries[i] = new ResolutionCitizenLog
            {
                citizenId = citizen.CitizenId,
                hasOfferA = citizen.HasOfferA,
                offerA = BuildOffer(citizen.OfferA),
                eligibilityA = BuildEligibility(citizen.EligibilityA),
                hasOfferB = citizen.HasOfferB,
                offerB = BuildOffer(citizen.OfferB),
                eligibilityB = BuildEligibility(citizen.EligibilityB),
                winner = citizen.WinnerSide.HasValue
                    ? citizen.WinnerSide.Value.ToString()
                    : "none"
            };
        }

        return entries;
    }

    private static OfferLog BuildOffer(ArenaEmploymentOffer offer)
    {
        return offer == null
            ? null
            : new OfferLog
            {
                workplaceId = offer.WorkplaceId,
                wage = offer.Wage
            };
    }

    private static ActionOfferLog[] BuildActionOffers(ArenaAction action)
    {
        ActionOfferLog[] offers =
            new ActionOfferLog[action.Offers.Count];

        for (int i = 0; i < action.Offers.Count; i++)
        {
            ArenaEmploymentOffer offer = action.Offers[i];
            offers[i] = new ActionOfferLog
            {
                citizenId = offer.CitizenId,
                workplaceId = offer.WorkplaceId,
                wage = offer.Wage
            };
        }

        return offers;
    }

    private static EligibilityLog BuildEligibility(
        ArenaOfferEligibilityResult eligibility)
    {
        return eligibility == null
            ? null
            : new EligibilityLog
            {
                reason = eligibility.Reason.ToString(),
                hasProjectedPayrollIfWon =
                    eligibility.ProjectedPayrollIfWon.HasValue,
                projectedPayrollIfWon =
                    eligibility.ProjectedPayrollIfWon ?? 0f
            };
    }

    private static ProviderLog BuildProvider(LlmProviderBehaviour provider)
    {
        return new ProviderLog
        {
            componentType = provider != null
                ? provider.GetType().FullName
                : null,
            gameObjectName = provider != null
                ? provider.gameObject.name
                : null,
            providerLabel = provider != null
                ? provider.ProviderLabel
                : null,
            modelLabel = provider != null
                ? provider.ModelLabel
                : null
        };
    }

    private void WriteEvent(object eventData)
    {
        if (!fileLoggingActive || writer == null)
        {
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(eventData, false);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    "JSON serialization returned no data.");
            }

            writer.WriteLine(json);
            writer.Flush();
        }
        catch (Exception exception)
        {
            DisableLogging(
                $"Arena match log write failed: {exception.Message}");
        }
    }

    private void DisableLogging(string error)
    {
        fileLoggingActive = false;
        if (!loggingFailureReported)
        {
            loggingFailureReported = true;
            Debug.LogError(error, this);
        }

        CloseWriter();
    }

    private void CloseWriter()
    {
        if (writer == null)
        {
            fileLoggingActive = false;
            return;
        }

        try
        {
            writer.Dispose();
        }
        catch (Exception exception)
        {
            if (!loggingFailureReported)
            {
                loggingFailureReported = true;
                Debug.LogError(
                    $"Arena match log close failed: {exception.Message}",
                    this);
            }
        }
        finally
        {
            writer = null;
            fileLoggingActive = false;
        }
    }

    private void OnDisable()
    {
        CloseWriter();
    }

    private void OnDestroy()
    {
        CloseWriter();
    }

    private void SetSimulatedTime(SimulatedTimeEvent eventData)
    {
        WorldClock clock = roundController?.WorldClock;
        if (clock == null)
        {
            return;
        }

        eventData.day = clock.Day;
        eventData.hour = clock.Hour;
        eventData.minute = clock.Minute;
    }

    private MatchStartEvent NewMatchStartEvent()
    {
        return new MatchStartEvent
        {
            schemaVersion = SchemaVersion,
            @event = "match_start",
            runId = runId
        };
    }

    private RoundStartEvent NewRoundStartEvent()
    {
        return new RoundStartEvent
        {
            schemaVersion = SchemaVersion,
            @event = "round_start",
            runId = runId
        };
    }

    private ProviderResultEvent NewProviderResultEvent()
    {
        return new ProviderResultEvent
        {
            schemaVersion = SchemaVersion,
            @event = "provider_result",
            runId = runId
        };
    }

    private ActionSubmittedEvent NewActionSubmittedEvent()
    {
        return new ActionSubmittedEvent
        {
            schemaVersion = SchemaVersion,
            @event = "action_submitted",
            runId = runId
        };
    }

    private RoundResultEvent NewRoundResultEvent()
    {
        return new RoundResultEvent
        {
            schemaVersion = SchemaVersion,
            @event = "round_result",
            runId = runId
        };
    }

    private RoundFailureEvent NewRoundFailureEvent()
    {
        return new RoundFailureEvent
        {
            schemaVersion = SchemaVersion,
            @event = "round_failure",
            runId = runId
        };
    }

    private MatchEndEvent NewMatchEndEvent()
    {
        return new MatchEndEvent
        {
            schemaVersion = SchemaVersion,
            @event = "match_end",
            runId = runId
        };
    }

    private static string FormatMatchResult(ArenaMatchResult result)
    {
        switch (result)
        {
            case ArenaMatchResult.SideA:
                return "A";
            case ArenaMatchResult.SideB:
                return "B";
            case ArenaMatchResult.Draw:
                return "DRAW";
            default:
                return "IN_PROGRESS";
        }
    }

    [Serializable]
    private class LogEvent
    {
        public int schemaVersion;
        public string @event;
        public string runId;
    }

    [Serializable]
    private class SimulatedTimeEvent : LogEvent
    {
        public int day;
        public int hour;
        public int minute;
    }

    [Serializable]
    private sealed class MatchStartEvent : SimulatedTimeEvent
    {
        public string wallClockUtc;
        public string sideATreasuryId;
        public string sideBTreasuryId;
        public string wonderAId;
        public string wonderBId;
        public bool automaticRoundsEnabled;
        public int roundIntervalMinutes;
        public bool requestRoundOnStart;
        public string initialTiePriority;
        public string sourceA;
        public string sourceB;
        public ProviderLog providerA;
        public ProviderLog providerB;
        public MatchStateLog initialState;
        public string stateError;
    }

    [Serializable]
    private sealed class RoundStartEvent : SimulatedTimeEvent
    {
        public int roundId;
        public string tiePriorityBefore;
        public string sourceA;
        public string sourceB;
        public string observationA;
        public string observationB;
        public MatchStateLog state;
        public string stateError;
    }

    [Serializable]
    private sealed class ProviderResultEvent : LogEvent
    {
        public int roundId;
        public string side;
        public int attempt;
        public bool success;
        public string actionJson;
        public string error;
    }

    [Serializable]
    private sealed class ActionSubmittedEvent : LogEvent
    {
        public int roundId;
        public string side;
        public string source;
        public string strategyNote;
        public ActionOfferLog[] offers;
    }

    [Serializable]
    private sealed class RoundResultEvent : SimulatedTimeEvent
    {
        public int roundId;
        public string tiePriorityBefore;
        public string tiePriorityAfter;
        public float finalProjectedPayrollA;
        public float finalProjectedPayrollB;
        public ResolutionCitizenLog[] citizens;
        public MatchStateLog postApplicationState;
        public string stateError;
    }

    [Serializable]
    private sealed class RoundFailureEvent : LogEvent
    {
        public int roundId;
        public string stage;
        public string error;
    }

    [Serializable]
    private sealed class MatchEndEvent : SimulatedTimeEvent
    {
        public string result;
        public MatchStateLog finalState;
        public string stateError;
        public int roundsOpened;
        public int roundsApplied;
        public int providerAttemptsA;
        public int providerAttemptsB;
        public int providerFailuresA;
        public int providerFailuresB;
    }

    [Serializable]
    private sealed class ProviderLog
    {
        public string componentType;
        public string gameObjectName;
        public string providerLabel;
        public string modelLabel;
    }

    [Serializable]
    private sealed class MatchStateLog
    {
        public SideStateLog sideA;
        public SideStateLog sideB;
        public CitizenStateLog[] citizens;
    }

    [Serializable]
    private sealed class SideStateLog
    {
        public float gold;
        public float payrollPerHour;
        public float payrollCoverageHours;
        public float stone;
        public float wood;
        public float wonderLaborCompleted;
        public float wonderLaborRequired;
        public bool wonderCompleted;
    }

    [Serializable]
    private sealed class CitizenStateLog
    {
        public string citizenId;
        public string employer;
        public int wage;
        public int reservationWage;
        public string workplaceId;
    }

    [Serializable]
    private sealed class ResolutionCitizenLog
    {
        public string citizenId;
        public bool hasOfferA;
        public OfferLog offerA;
        public EligibilityLog eligibilityA;
        public bool hasOfferB;
        public OfferLog offerB;
        public EligibilityLog eligibilityB;
        public string winner;
    }

    [Serializable]
    private sealed class OfferLog
    {
        public string workplaceId;
        public int wage;
    }

    [Serializable]
    private sealed class ActionOfferLog
    {
        public string citizenId;
        public string workplaceId;
        public int wage;
    }

    [Serializable]
    private sealed class EligibilityLog
    {
        public string reason;
        public bool hasProjectedPayrollIfWon;
        public float projectedPayrollIfWon;
    }
}
