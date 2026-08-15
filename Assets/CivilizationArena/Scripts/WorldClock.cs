using UnityEngine;

public class WorldClock : MonoBehaviour
{
    [SerializeField] private float realSecondsPerGameMinute = 0.1f;
    [SerializeField] private int startHour = 7;

    private float accumulatedTime;
    private int totalMinutes;

    public int Hour => (totalMinutes / 60) % 24;
    public int Minute => totalMinutes % 60;
    public int Day => totalMinutes / (24 * 60) + 1;

    public int MinutesAdvancedThisFrame { get; private set; }

    private void Start()
    {
        totalMinutes = startHour * 60;
    }

    private void Update()
    {
        MinutesAdvancedThisFrame = 0;

        accumulatedTime += Time.deltaTime;

        while (accumulatedTime >= realSecondsPerGameMinute)
        {
            accumulatedTime -= realSecondsPerGameMinute;

            totalMinutes++;
            MinutesAdvancedThisFrame++;
        }
    }
}
