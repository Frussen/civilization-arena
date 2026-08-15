using UnityEngine;

public class CitizenWorker : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private CitizenRoutine routine;

    private CitizenWorkAssignment workAssignment;
    private CitizenEmployment employment;

    private void Awake()
    {
        workAssignment = GetComponent<CitizenWorkAssignment>();
        employment = GetComponent<CitizenEmployment>();
    }

    private void Update()
    {
        Workplace workplace = workAssignment.CurrentWorkplace;
        AgentTreasury employer = employment.CurrentEmployer;

        if (!routine.IsWorkingTime ||
            workplace == null ||
            employer == null ||
            clock.MinutesAdvancedThisFrame <= 0 ||
            !workplace.IsWithinWorkArea(transform.position))
        {
            return;
        }

        AgentResourceStockpile stockpile =
            employer.GetComponent<AgentResourceStockpile>();

        if (stockpile == null)
        {
            return;
        }

        float producedAmount =
            workplace.Work(clock.MinutesAdvancedThisFrame);

        stockpile.Add(workplace.ResourceType, producedAmount);
    }
}
