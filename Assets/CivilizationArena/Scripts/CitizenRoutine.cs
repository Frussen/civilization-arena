using UnityEngine;

public class CitizenRoutine : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private CitizenMover mover;

    [SerializeField] private ResidentialArea residentialArea;
    [SerializeField] private WorkShift workShift = WorkShift.Day;

    private CitizenWorkAssignment workAssignment;
    private Transform currentDestination;

    public WorkShift Shift => workShift;
    public bool IsWorkingTime
    {
        get
        {
            if (workShift == WorkShift.Night)
            {
                return clock.Hour >= 18 || clock.Hour < 4;
            }

            return clock.Hour >= 8 && clock.Hour < 18;
        }
    }

    private void Awake()
    {
        workAssignment = GetComponent<CitizenWorkAssignment>();
    }

    private void Update()
    {
        Workplace workplace = workAssignment.CurrentWorkplace;
        bool shouldWork = IsWorkingTime && workplace != null;

        Transform desiredDestination =
            shouldWork ? workplace.transform : residentialArea.transform;

        if (desiredDestination == currentDestination)
        {
            return;
        }

        currentDestination = desiredDestination;
        float stoppingDistance =
            shouldWork ? workplace.WorkRadius : residentialArea.RestRadius;

        mover.MoveTo(currentDestination, stoppingDistance);
    }
}
