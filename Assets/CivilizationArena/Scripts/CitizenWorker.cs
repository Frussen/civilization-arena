using UnityEngine;

public class CitizenWorker : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private CitizenMover mover;
    [SerializeField] private CitizenRoutine routine;
    [SerializeField] private Workplace workplace;

    private void Update()
    {
        if (!routine.IsWorkingTime ||
            !mover.HasArrivedAt(routine.WorkDestination))
        {
            return;
        }

        workplace.Work(clock.MinutesAdvancedThisFrame);
    }
}
