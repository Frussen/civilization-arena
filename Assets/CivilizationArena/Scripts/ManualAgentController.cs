using UnityEngine;

[System.Serializable]
public class WorkplaceAllocation
{
    [SerializeField] private Workplace workplace;
    [SerializeField] private int desiredWorkers;

    public Workplace Workplace => workplace;
    public int DesiredWorkers => desiredWorkers;
}

public class ManualAgentController : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private AgentTreasury employer;
    [SerializeField] private CitizenEmployment[] citizens;
    [SerializeField] private WorkplaceAllocation[] workplaceAllocations;
    [SerializeField] private int offerWage = 5;
    [SerializeField] private int decisionIntervalMinutes = 30;

    private int accumulatedMinutes;

    private void Update()
    {
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

        foreach (WorkplaceAllocation allocation in workplaceAllocations)
        {
            if (allocation == null ||
                allocation.Workplace == null ||
                allocation.DesiredWorkers <= 0)
            {
                continue;
            }

            int assignedWorkers = CountAssignedWorkers(allocation.Workplace);

            foreach (CitizenEmployment citizen in citizens)
            {
                if (assignedWorkers >= allocation.DesiredWorkers)
                {
                    break;
                }

                if (citizen == null || citizen.IsEmployed)
                {
                    continue;
                }

                if (citizen.TryAcceptOffer(
                    employer,
                    allocation.Workplace,
                    offerWage))
                {
                    assignedWorkers++;
                }
            }
        }
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
