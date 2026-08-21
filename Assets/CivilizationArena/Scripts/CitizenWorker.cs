using UnityEngine;

public class CitizenWorker : MonoBehaviour
{
    [SerializeField] private CitizenRoutine routine;

    private CitizenWorkAssignment workAssignment;
    private CitizenEmployment employment;

    private void Awake()
    {
        workAssignment = GetComponent<CitizenWorkAssignment>();
        employment = GetComponent<CitizenEmployment>();
    }

    internal void ProcessResourceWork(
        AgentTreasury expectedEmployer,
        int simulatedMinutes)
    {
        Workplace workplace = workAssignment.CurrentWorkplace;
        AgentTreasury employer = employment.CurrentEmployer;

        if (!routine.IsWorkingTime ||
            workplace == null ||
            employer != expectedEmployer ||
            simulatedMinutes <= 0 ||
            !workplace.IsWithinWorkArea(transform.position))
        {
            return;
        }

        WonderConstruction wonder =
            workplace.GetComponent<WonderConstruction>();

        if (wonder != null)
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
            workplace.Work(simulatedMinutes);

        stockpile.Add(workplace.ResourceType, producedAmount);
    }

    internal void ProcessWonderWork(
        AgentTreasury expectedEmployer,
        int simulatedMinutes)
    {
        Workplace workplace = workAssignment.CurrentWorkplace;
        AgentTreasury employer = employment.CurrentEmployer;

        if (!routine.IsWorkingTime ||
            workplace == null ||
            employer != expectedEmployer ||
            simulatedMinutes <= 0 ||
            !workplace.IsWithinWorkArea(transform.position))
        {
            return;
        }

        WonderConstruction wonder =
            workplace.GetComponent<WonderConstruction>();

        if (wonder != null)
        {
            wonder.ContributeLabor(employer, simulatedMinutes);
        }
    }
}
