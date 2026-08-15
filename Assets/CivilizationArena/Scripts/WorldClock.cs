using UnityEngine;

public class WorldClock : MonoBehaviour
{
    [SerializeField] private float realSecondsPerGameMinute = 0.1f;
    [SerializeField] private int startHour = 7;

    private float accumulatedTime;
    private int totalMinutes;

    public int Hour => (totalMinutes / 60) % 24;
    public int Minute => totalMinutes % 60;

    private void Start()
    {
        totalMinutes = startHour * 60;
    }

    private void Update()
    {
        accumulatedTime += Time.deltaTime;

        while (accumulatedTime >= realSecondsPerGameMinute)
        {
            accumulatedTime -= realSecondsPerGameMinute;
            totalMinutes++;
        }
    }
}