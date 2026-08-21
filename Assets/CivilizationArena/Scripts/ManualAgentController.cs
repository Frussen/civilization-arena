using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class WorkplaceAllocation
{
    [SerializeField] private Workplace workplace;
    [SerializeField] private int desiredWorkers;

    public Workplace Workplace => workplace;
    public int DesiredWorkers => desiredWorkers;

    internal void SetDesiredWorkers(int value)
    {
        desiredWorkers = value;
    }
}

[DefaultExecutionOrder(SimulationExecutionOrder.ManualDecisions)]
public class ManualAgentController : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private AgentTreasury employer;
    [SerializeField] private CitizenEmployment[] citizens;
    [SerializeField] private WorkplaceAllocation[] workplaceAllocations;
    [FormerlySerializedAs("offerWage")]
    [SerializeField] private int maximumOfferWage = 5;
    [SerializeField] private int decisionIntervalMinutes = 30;

    private int accumulatedMinutes;
    private bool executionEnabled = true;
    private AgentDecisionScheduler controlModeAuthority;

    public AgentTreasury Employer => employer;
    public int MaximumOfferWage => maximumOfferWage;
    public bool ExecutionEnabled => executionEnabled;

    internal void SetExecutionEnabled(
        bool value,
        AgentDecisionScheduler authority)
    {
        executionEnabled = value;
        controlModeAuthority = authority;
    }

    public bool SetMaximumOfferWage(int value)
    {
        if (value <= 0)
        {
            return false;
        }

        maximumOfferWage = value;
        return true;
    }

    public bool TryGetDesiredWorkers(
        Workplace workplace,
        out int desiredWorkers)
    {
        int allocationIndex = FindAllocationIndex(workplace);
        if (allocationIndex < 0)
        {
            desiredWorkers = 0;
            return false;
        }

        desiredWorkers = workplaceAllocations[allocationIndex].DesiredWorkers;
        return true;
    }

    public bool SetDesiredWorkers(Workplace workplace, int desiredWorkers)
    {
        if (desiredWorkers < 0)
        {
            return false;
        }

        int allocationIndex = FindAllocationIndex(workplace);
        if (allocationIndex < 0)
        {
            return false;
        }

        workplaceAllocations[allocationIndex].SetDesiredWorkers(desiredWorkers);
        return true;
    }

    private void Update()
    {
        if (!executionEnabled ||
            (controlModeAuthority != null &&
             controlModeAuthority.ControlMode != AgentControlMode.Manual))
        {
            return;
        }

        int simulatedMinutes = clock.MinutesAdvancedThisFrame;
        if (simulatedMinutes <= 0)
        {
            return;
        }

        accumulatedMinutes += simulatedMinutes;

        int interval = Mathf.Max(1, decisionIntervalMinutes);
        if (accumulatedMinutes < interval)
        {
            return;
        }

        accumulatedMinutes %= interval;
        ApplyWorkplaceAllocations();
    }

    private void ApplyWorkplaceAllocations()
    {
        if (employer == null ||
            workplaceAllocations == null ||
            citizens == null)
        {
            return;
        }

        int[] assignedWorkers = new int[workplaceAllocations.Length];

        for (int i = 0; i < workplaceAllocations.Length; i++)
        {
            WorkplaceAllocation allocation = workplaceAllocations[i];
            if (allocation != null && allocation.Workplace != null)
            {
                assignedWorkers[i] =
                    CountAssignedWorkers(allocation.Workplace);
            }
        }

        HireUnemployedWorkers(assignedWorkers);
        ReallocateSurplusWorkers(assignedWorkers);
    }

    private void HireUnemployedWorkers(int[] assignedWorkers)
    {
        for (int i = 0; i < workplaceAllocations.Length; i++)
        {
            WorkplaceAllocation allocation = workplaceAllocations[i];
            if (allocation == null || allocation.Workplace == null)
            {
                continue;
            }

            int desiredWorkers = Mathf.Max(0, allocation.DesiredWorkers);

            foreach (CitizenEmployment citizen in citizens)
            {
                if (assignedWorkers[i] >= desiredWorkers)
                {
                    break;
                }

                if (citizen == null || citizen.IsEmployed)
                {
                    continue;
                }

                int offeredWage = citizen.ReservationWage;
                if (offeredWage > maximumOfferWage)
                {
                    continue;
                }

                if (citizen.TryAcceptOffer(
                    employer,
                    allocation.Workplace,
                    offeredWage))
                {
                    assignedWorkers[i]++;
                }
            }
        }
    }

    private void ReallocateSurplusWorkers(int[] assignedWorkers)
    {
        for (int targetIndex = 0;
             targetIndex < workplaceAllocations.Length;
             targetIndex++)
        {
            WorkplaceAllocation target = workplaceAllocations[targetIndex];
            if (target == null || target.Workplace == null)
            {
                continue;
            }

            int desiredWorkers = Mathf.Max(0, target.DesiredWorkers);

            while (assignedWorkers[targetIndex] < desiredWorkers)
            {
                bool reassignedWorker = false;

                foreach (CitizenEmployment citizen in citizens)
                {
                    if (!TryGetSurplusSource(
                        citizen,
                        target.Workplace,
                        assignedWorkers,
                        out int sourceIndex))
                    {
                        continue;
                    }

                    if (citizen.CurrentWage == int.MaxValue)
                    {
                        continue;
                    }

                    int reassignmentWage = citizen.CurrentWage + 1;
                    if (reassignmentWage > maximumOfferWage)
                    {
                        continue;
                    }

                    if (citizen.TryAcceptOffer(
                        employer,
                        target.Workplace,
                        reassignmentWage))
                    {
                        assignedWorkers[sourceIndex]--;
                        assignedWorkers[targetIndex]++;
                        reassignedWorker = true;
                        break;
                    }
                }

                if (!reassignedWorker)
                {
                    break;
                }
            }
        }
    }

    private bool TryGetSurplusSource(
        CitizenEmployment citizen,
        Workplace targetWorkplace,
        int[] assignedWorkers,
        out int sourceIndex)
    {
        sourceIndex = -1;

        if (citizen == null || citizen.CurrentEmployer != employer)
        {
            return false;
        }

        CitizenWorkAssignment assignment =
            citizen.GetComponent<CitizenWorkAssignment>();

        if (assignment == null ||
            assignment.CurrentWorkplace == null ||
            assignment.CurrentWorkplace == targetWorkplace)
        {
            return false;
        }

        sourceIndex = FindAllocationIndex(assignment.CurrentWorkplace);
        if (sourceIndex < 0)
        {
            return false;
        }

        int desiredWorkers = Mathf.Max(
            0,
            workplaceAllocations[sourceIndex].DesiredWorkers);

        return assignedWorkers[sourceIndex] > desiredWorkers;
    }

    private int FindAllocationIndex(Workplace workplace)
    {
        if (workplace == null || workplaceAllocations == null)
        {
            return -1;
        }

        for (int i = 0; i < workplaceAllocations.Length; i++)
        {
            WorkplaceAllocation allocation = workplaceAllocations[i];
            if (allocation != null && allocation.Workplace == workplace)
            {
                return i;
            }
        }

        return -1;
    }

    private int CountAssignedWorkers(Workplace workplace)
    {
        int assignedWorkers = 0;

        foreach (CitizenEmployment citizen in citizens)
        {
            if (citizen == null || citizen.CurrentEmployer != employer)
            {
                continue;
            }

            CitizenWorkAssignment assignment =
                citizen.GetComponent<CitizenWorkAssignment>();

            if (assignment != null &&
                assignment.CurrentWorkplace == workplace)
            {
                assignedWorkers++;
            }
        }

        return assignedWorkers;
    }
}
