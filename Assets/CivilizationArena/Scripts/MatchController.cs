using UnityEngine;

public class MatchController : MonoBehaviour
{
    [SerializeField] private WorldClock worldClock;
    [SerializeField] private WonderConstruction objectiveWonder;
    [SerializeField] private bool pauseSimulationOnEnd = true;

    [SerializeField] private bool isEnded;
    [SerializeField] private bool success;
    [SerializeField] private int finalDay;
    [SerializeField] private int finalHour;
    [SerializeField] private int finalMinute;
    [TextArea(4, 8)]
    [SerializeField] private string latestMatchResult;

    public bool IsEnded => isEnded;
    public bool Success => success;
    public int FinalDay => finalDay;
    public int FinalHour => finalHour;
    public int FinalMinute => finalMinute;
    public string LatestMatchResult => latestMatchResult;

    private void Awake()
    {
        isEnded = false;
        success = false;
        finalDay = 0;
        finalHour = 0;
        finalMinute = 0;
        latestMatchResult = string.Empty;
    }

    private void Update()
    {
        if (isEnded || worldClock == null || objectiveWonder == null)
        {
            return;
        }

        if (objectiveWonder.Completed)
        {
            EndMatchSuccessfully();
        }
    }

    private void EndMatchSuccessfully()
    {
        if (isEnded)
        {
            return;
        }

        isEnded = true;
        success = true;
        finalDay = worldClock.Day;
        finalHour = worldClock.Hour;
        finalMinute = worldClock.Minute;

        latestMatchResult =
            "CIVILIZATION_ARENA_MATCH_RESULT\n" +
            "success=true\n" +
            "reason=wonder_completed\n" +
            $"time: day={finalDay} hour={finalHour} minute={finalMinute}";

        Debug.Log(latestMatchResult, this);

        if (pauseSimulationOnEnd)
        {
            SimulationPauseCoordinator.Acquire();
        }
    }
}
