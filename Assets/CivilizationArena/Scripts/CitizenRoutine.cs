using UnityEngine;

public class CitizenRoutine : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private CitizenMover mover;

    [SerializeField] private ResidentialArea residentialArea;

    [SerializeField] private int workStartHour = 8;
    [SerializeField] private int workEndHour = 18;

    private CitizenWorkAssignment workAssignment;
    private Transform currentDestination;

    public bool IsWorkingTime =>
        clock.Hour >= workStartHour &&
        clock.Hour < workEndHour;

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
