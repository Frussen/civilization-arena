using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[Serializable]
public class AgentWorkplaceBinding
{
    [SerializeField] private string id;
    [SerializeField] private Workplace workplace;

    public string Id => id;
    public Workplace Workplace => workplace;
}

[Serializable]
public class AgentAllocationAction
{
    public string workplaceId;
    public int desiredWorkers;
}

[Serializable]
public class AgentStrategicAction
{
    public int maximumOfferWage;
    public AgentAllocationAction[] allocations;
    public string strategyNote;
}

public class AgentTextInterface : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private AgentTreasury treasury;
    [SerializeField] private AgentResourceStockpile stockpile;
    [SerializeField] private ManualAgentController manualController;
    [SerializeField] private WonderConstruction wonder;
    [SerializeField] private CitizenEmployment[] citizens;
    [SerializeField] private AgentWorkplaceBinding[] workplaceBindings;

    [TextArea(15, 40)]
    [SerializeField] private string latestObservation;

    [TextArea(10, 30)]
    [SerializeField] private string actionJson;

    [TextArea(2, 5)]
    [SerializeField] private string latestActionResult;

    [ContextMenu("Generate Observation")]
    private void GenerateObservation()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Generate Observation is available only during Play Mode.",
                this);
            return;
        }

        if (!TryBuildObservation(out string observation, out string error))
        {
            Debug.LogWarning($"Observation failed: {error}", this);
            return;
        }

        latestObservation = observation;
        Debug.Log(latestObservation, this);
    }

    [ContextMenu("Apply Action JSON")]
    private void ApplyActionJson()
    {
        if (!Application.isPlaying)
        {
            RejectAction("Apply Action JSON is available only during Play Mode.");
            return;
        }

        AgentStrategicAction action;

        try
        {
            action = JsonUtility.FromJson<AgentStrategicAction>(actionJson);
        }
        catch (Exception)
        {
            RejectAction("Malformed JSON.");
            return;
        }

        if (!TryValidateAction(
            action,
            out int[] desiredWorkers,
            out string error))
        {
            RejectAction(error);
            return;
        }

        manualController.SetMaximumOfferWage(action.maximumOfferWage);

        for (int i = 0; i < workplaceBindings.Length; i++)
        {
            manualController.SetDesiredWorkers(
                workplaceBindings[i].Workplace,
                desiredWorkers[i]);
        }

        latestActionResult =
            "Accepted: strategic targets updated; execution will occur " +
            "through ManualAgentController decision cycles.";

        Debug.Log(latestActionResult, this);
    }

    private bool TryBuildObservation(
        out string observation,
        out string error)
    {
        observation = null;

        if (!TryValidateConfiguration(out _, out error))
        {
            return false;
        }

        float payrollPerHour = 0f;
        foreach (CitizenEmployment citizen in citizens)
        {
            if (citizen.CurrentEmployer == treasury)
            {
                payrollPerHour += citizen.CurrentWage;
            }
        }

        float remainingLabor = Mathf.Max(
            0f,
            wonder.LaborHoursRequired - wonder.LaborHoursCompleted);

        float remainingFraction = wonder.LaborHoursRequired > 0f
            ? Mathf.Clamp01(remainingLabor / wonder.LaborHoursRequired)
            : 0f;

        float stoneStillRequired = wonder.StoneRequired * remainingFraction;
        float woodStillRequired = wonder.WoodRequired * remainingFraction;

        StringBuilder text = new StringBuilder();

        text.AppendLine("CIVILIZATION_ARENA_OBSERVATION");
        text.AppendLine(
            $"time: day={clock.Day} hour={clock.Hour:D2} " +
            $"minute={clock.Minute:D2}");

        text.AppendLine("economy:");
        text.AppendLine($"gold={Format(treasury.CurrentGold)}");
        text.AppendLine(
            $"goldIncomePerHour={Format(treasury.GoldIncomePerHour)}");
        text.AppendLine($"payrollPerHour={Format(payrollPerHour)}");
        text.AppendLine(
            $"netGoldPerHour={Format(treasury.GoldIncomePerHour - payrollPerHour)}");
        text.AppendLine($"stone={Format(stockpile.Stone)}");
        text.AppendLine($"wood={Format(stockpile.Wood)}");
        text.AppendLine(
            $"maximumOfferWage={manualController.MaximumOfferWage}");

        text.AppendLine("wonder:");
        text.AppendLine(
            $"completed={wonder.Completed.ToString().ToLowerInvariant()}");
        text.AppendLine(
            $"laborHoursCompleted={Format(wonder.LaborHoursCompleted)}");
        text.AppendLine(
            $"laborHoursRequired={Format(wonder.LaborHoursRequired)}");
        text.AppendLine($"stoneRequired={Format(wonder.StoneRequired)}");
        text.AppendLine($"woodRequired={Format(wonder.WoodRequired)}");
        text.AppendLine(
            $"stoneStillRequired={Format(stoneStillRequired)}");
        text.AppendLine(
            $"woodStillRequired={Format(woodStillRequired)}");

        text.AppendLine("allocations:");
        foreach (AgentWorkplaceBinding binding in workplaceBindings)
        {
            manualController.TryGetDesiredWorkers(
                binding.Workplace,
                out int desiredWorkers);

            int actualWorkers = CountActualWorkers(binding.Workplace);
            text.AppendLine(
                $"{binding.Id}: desired={desiredWorkers} actual={actualWorkers}");
        }

        text.AppendLine("citizens:");
        foreach (CitizenEmployment citizen in citizens)
        {
            AppendCitizenObservation(text, citizen);
        }

        AgentStrategicAction actionReminder = BuildActionReminder();
        text.AppendLine("availableActionJson:");
        text.AppendLine(JsonUtility.ToJson(actionReminder));
        text.Append(
            "actionSemantics: set strategic targets only; hiring and " +
            "reallocation use the existing simulation and may take effect " +
            "on the next ManualAgentController decision cycle.");

        observation = text.ToString();
        error = null;
        return true;
    }

    private void AppendCitizenObservation(
        StringBuilder text,
        CitizenEmployment citizen)
    {
        string status = citizen.IsEmployed ? "employed" : "unemployed";
        string employerRelation = citizen.CurrentEmployer == null
            ? "none"
            : citizen.CurrentEmployer == treasury ? "this_agent" : "other";

        CitizenRoutine routine = citizen.GetComponent<CitizenRoutine>();
        string shift = routine != null ? routine.Shift.ToString() : "unknown";

        CitizenWorkAssignment assignment =
            citizen.GetComponent<CitizenWorkAssignment>();

        Workplace workplace = assignment != null
            ? assignment.CurrentWorkplace
            : null;

        text.AppendLine(
            $"{citizen.name}: status={status}, employer={employerRelation}, " +
            $"wage={citizen.CurrentWage}, " +
            $"reservation={citizen.ReservationWage}, shift={shift}, " +
            $"workplace={GetWorkplaceId(workplace)}");
    }

    private AgentStrategicAction BuildActionReminder()
    {
        AgentAllocationAction[] allocations =
            new AgentAllocationAction[workplaceBindings.Length];

        for (int i = 0; i < workplaceBindings.Length; i++)
        {
            AgentWorkplaceBinding binding = workplaceBindings[i];
            manualController.TryGetDesiredWorkers(
                binding.Workplace,
                out int desiredWorkers);

            allocations[i] = new AgentAllocationAction
            {
                workplaceId = binding.Id,
                desiredWorkers = desiredWorkers
            };
        }

        return new AgentStrategicAction
        {
            maximumOfferWage = manualController.MaximumOfferWage,
            allocations = allocations,
            strategyNote = string.Empty
        };
    }

    private bool TryValidateAction(
        AgentStrategicAction action,
        out int[] desiredWorkers,
        out string error)
    {
        desiredWorkers = null;

        if (!TryValidateConfiguration(
            out Dictionary<string, int> bindingIndices,
            out error))
        {
            return false;
        }

        if (action == null)
        {
            error = "Malformed JSON.";
            return false;
        }

        if (action.maximumOfferWage <= 0)
        {
            error = "maximumOfferWage must be greater than zero.";
            return false;
        }

        if (action.allocations == null)
        {
            error = "allocations is required.";
            return false;
        }

        if (action.allocations.Length != workplaceBindings.Length)
        {
            error = "Exactly one allocation is required for every configured workplace ID.";
            return false;
        }

        desiredWorkers = new int[workplaceBindings.Length];
        bool[] receivedAllocations = new bool[workplaceBindings.Length];
        long totalDesiredWorkers = 0;

        foreach (AgentAllocationAction allocation in action.allocations)
        {
            if (allocation == null)
            {
                error = "Allocation entries cannot be null.";
                return false;
            }

            if (!bindingIndices.TryGetValue(
                allocation.workplaceId,
                out int bindingIndex))
            {
                error = $"Unknown workplaceId: {allocation.workplaceId ?? "null"}.";
                return false;
            }

            if (receivedAllocations[bindingIndex])
            {
                error = $"Duplicate workplaceId: {allocation.workplaceId}.";
                return false;
            }

            if (allocation.desiredWorkers < 0)
            {
                error =
                    $"desiredWorkers cannot be negative for {allocation.workplaceId}.";
                return false;
            }

            receivedAllocations[bindingIndex] = true;
            desiredWorkers[bindingIndex] = allocation.desiredWorkers;
            totalDesiredWorkers += allocation.desiredWorkers;
        }

        for (int i = 0; i < receivedAllocations.Length; i++)
        {
            if (!receivedAllocations[i])
            {
                error = $"Missing workplaceId: {workplaceBindings[i].Id}.";
                return false;
            }
        }

        if (totalDesiredWorkers > citizens.Length)
        {
            error =
                "Total desired workers cannot exceed the configured citizen population.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryValidateConfiguration(
        out Dictionary<string, int> bindingIndices,
        out string error)
    {
        bindingIndices = new Dictionary<string, int>(StringComparer.Ordinal);

        if (clock == null ||
            treasury == null ||
            stockpile == null ||
            manualController == null ||
            wonder == null)
        {
            error = "Required simulation references are not fully configured.";
            return false;
        }

        if (manualController.Employer != treasury)
        {
            error = "ManualAgentController and AgentTextInterface must use the same employer.";
            return false;
        }

        if (citizens == null || workplaceBindings == null)
        {
            error = "Citizens and workplace bindings must be configured.";
            return false;
        }

        HashSet<CitizenEmployment> configuredCitizens =
            new HashSet<CitizenEmployment>();

        foreach (CitizenEmployment citizen in citizens)
        {
            if (citizen == null || !configuredCitizens.Add(citizen))
            {
                error = "Configured citizens must be non-null and unique.";
                return false;
            }
        }

        HashSet<Workplace> configuredWorkplaces = new HashSet<Workplace>();

        for (int i = 0; i < workplaceBindings.Length; i++)
        {
            AgentWorkplaceBinding binding = workplaceBindings[i];

            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.Id) ||
                binding.Workplace == null)
            {
                error = "Every workplace binding needs a non-empty ID and Workplace.";
                return false;
            }

            if (!bindingIndices.TryAdd(binding.Id, i))
            {
                error = $"Duplicate configured workplace ID: {binding.Id}.";
                return false;
            }

            if (!configuredWorkplaces.Add(binding.Workplace))
            {
                error = "Each configured Workplace may have only one stable ID.";
                return false;
            }

            if (!manualController.TryGetDesiredWorkers(
                binding.Workplace,
                out _))
            {
                error =
                    $"ManualAgentController has no allocation for {binding.Id}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private int CountActualWorkers(Workplace workplace)
    {
        int count = 0;

        foreach (CitizenEmployment citizen in citizens)
        {
            if (citizen.CurrentEmployer != treasury)
            {
                continue;
            }

            CitizenWorkAssignment assignment =
                citizen.GetComponent<CitizenWorkAssignment>();

            if (assignment != null &&
                assignment.CurrentWorkplace == workplace)
            {
                count++;
            }
        }

        return count;
    }

    private string GetWorkplaceId(Workplace workplace)
    {
        if (workplace == null)
        {
            return "none";
        }

        foreach (AgentWorkplaceBinding binding in workplaceBindings)
        {
            if (binding.Workplace == workplace)
            {
                return binding.Id;
            }
        }

        return "unconfigured";
    }

    private void RejectAction(string reason)
    {
        latestActionResult = $"Rejected: {reason}";
        Debug.LogWarning(latestActionResult, this);
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
