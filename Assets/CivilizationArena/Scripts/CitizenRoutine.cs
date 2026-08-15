using UnityEngine;

public class CitizenRoutine : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private CitizenMover mover;

    [SerializeField] private Transform home;
    [SerializeField] private Transform work;

    [SerializeField] private int workStartHour = 8;
    [SerializeField] private int workEndHour = 18;

    private Transform currentDestination;

    private void Start()
    {
        currentDestination = home;
    }

    private void Update()
    {
        Transform desiredDestination =
            IsWorkingHours() ? work : home;

        if (desiredDestination == currentDestination)
        {
            return;
        }

        currentDestination = desiredDestination;
        mover.MoveTo(currentDestination);
    }

    private bool IsWorkingHours()
    {
        return clock.Hour >= workStartHour &&
               clock.Hour < workEndHour;
    }
}
