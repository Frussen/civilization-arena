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

    public Transform WorkDestination => work;
    public bool IsWorkingTime =>
        clock.Hour >= workStartHour &&
        clock.Hour < workEndHour;

    private void Start()
    {
        currentDestination = home;
    }

    private void Update()
    {
        Transform desiredDestination =
            IsWorkingTime ? work : home;

        if (desiredDestination == currentDestination)
        {
            return;
        }

        currentDestination = desiredDestination;
        mover.MoveTo(currentDestination);
    }
}
