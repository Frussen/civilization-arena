using UnityEngine;

[DefaultExecutionOrder(SimulationExecutionOrder.WorldClock)]
public class WorldClock : MonoBehaviour
{
    [SerializeField] private float realSecondsPerGameMinute = 0.1f;
    [SerializeField] private int startHour = 7;

    private float accumulatedTime;
    private int totalMinutes;
    private bool startupFrameComplete;

    public int Hour => (totalMinutes / 60) % 24;
    public int Minute => totalMinutes % 60;
    public int Day => totalMinutes / (24 * 60) + 1;

    public int MinutesAdvancedThisFrame { get; private set; }
    public bool StartupFrameComplete => startupFrameComplete;

    private void Start()
    {
        accumulatedTime = 0f;
        totalMinutes = startHour * 60;
        MinutesAdvancedThisFrame = 0;
        startupFrameComplete = false;
    }

    private void Update()
    {
        MinutesAdvancedThisFrame = 0;

        // Scene loading can produce a larger-than-normal first delta. Treat the
        // clock's first Update as initialization so loading time never becomes
        // elapsed simulation time.
        if (!startupFrameComplete)
        {
            startupFrameComplete = true;
            return;
        }

        // A pause lease is the authoritative simulation boundary. Unity may
        // still expose the previous scaled delta on the frame where timeScale
        // changes, so do not let that transition advance or accumulate time.
        if (SimulationPauseCoordinator.ActiveLeaseCount > 0)
        {
            return;
        }

        accumulatedTime += Time.deltaTime;

        while (accumulatedTime >= realSecondsPerGameMinute)
        {
            accumulatedTime -= realSecondsPerGameMinute;

            totalMinutes++;
            MinutesAdvancedThisFrame++;
        }
    }
}

public static class SimulationExecutionOrder
{
    public const int WorldClock = -500;
    public const int TreasuryEconomy = -400;
    public const int ArenaMatchEvaluation = 0;
    public const int ManualDecisions = 100;
    public const int SingleAgentDecisions = 200;
}
