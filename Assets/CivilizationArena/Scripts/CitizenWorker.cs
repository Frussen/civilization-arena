using UnityEngine;

public class CitizenWorker : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private CitizenRoutine routine;
    [SerializeField] private Workplace workplace;

    private void Update()
    {
        if (!routine.IsWorkingTime ||
            clock.MinutesAdvancedThisFrame <= 0 ||
            !workplace.IsWithinWorkArea(transform.position))
        {
            return;
        }

        workplace.Work(clock.MinutesAdvancedThisFrame);
    }
}
