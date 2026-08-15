using UnityEngine;

public class CitizenWorker : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private CitizenRoutine routine;

    private CitizenWorkAssignment workAssignment;

    private void Awake()
    {
        workAssignment = GetComponent<CitizenWorkAssignment>();
    }

    private void Update()
    {
        Workplace workplace = workAssignment.CurrentWorkplace;

        if (!routine.IsWorkingTime ||
            workplace == null ||
            clock.MinutesAdvancedThisFrame <= 0 ||
            !workplace.IsWithinWorkArea(transform.position))
        {
            return;
        }

        workplace.Work(clock.MinutesAdvancedThisFrame);
    }
}
