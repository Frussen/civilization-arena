using System.Text;
using UnityEngine;

public enum ArenaMatchResult
{
    InProgress,
    SideA,
    SideB,
    Draw
}

[DefaultExecutionOrder(SimulationExecutionOrder.ArenaMatchEvaluation)]
public sealed class ArenaMatchController : MonoBehaviour
{
    [SerializeField] private AgentTreasury sideATreasury;
    [SerializeField] private AgentTreasury sideBTreasury;
    [SerializeField] private WonderConstruction wonderA;
    [SerializeField] private WonderConstruction wonderB;
    [SerializeField] private WorldClock worldClock;

    [SerializeField] private ArenaMatchResult result =
        ArenaMatchResult.InProgress;
    [SerializeField] private bool wonderACompleted;
    [SerializeField] private bool wonderBCompleted;
    [TextArea(5, 10)]
    [SerializeField] private string latestMatchResult;

    private SimulationPauseLease matchPauseLease;
    private bool configurationErrorReported;

    public AgentTreasury SideATreasury => sideATreasury;
    public AgentTreasury SideBTreasury => sideBTreasury;
    public bool IsMatchEnded => result != ArenaMatchResult.InProgress;
    public ArenaMatchResult Result => result;
    public string LatestMatchResult => latestMatchResult;

    private void Awake()
    {
        result = ArenaMatchResult.InProgress;
        wonderACompleted = false;
        wonderBCompleted = false;
        latestMatchResult = string.Empty;
        matchPauseLease = default;
        configurationErrorReported = false;
    }

    private void OnEnable()
    {
        if (IsMatchEnded && !matchPauseLease.IsValid)
        {
            matchPauseLease = SimulationPauseCoordinator.Acquire();
        }
    }

    private void Update()
    {
        if (IsMatchEnded)
        {
            return;
        }

        if (!TryValidateConfiguration(out string error))
        {
            if (!configurationErrorReported)
            {
                Debug.LogError(
                    $"ArenaMatchController configuration failed: {error}",
                    this);
                configurationErrorReported = true;
            }

            return;
        }

        configurationErrorReported = false;
        wonderACompleted = wonderA.Completed;
        wonderBCompleted = wonderB.Completed;

        if (!wonderACompleted && !wonderBCompleted)
        {
            return;
        }

        ArenaMatchResult finalResult = wonderACompleted
            ? wonderBCompleted
                ? ArenaMatchResult.Draw
                : ArenaMatchResult.SideA
            : ArenaMatchResult.SideB;

        EndMatch(finalResult);
    }

    public bool TryValidateConfiguration(out string error)
    {
        if (sideATreasury == null || sideBTreasury == null)
        {
            error = "Side A and Side B treasuries are required.";
            return false;
        }

        if (sideATreasury == sideBTreasury)
        {
            error = "Side A and Side B must use different treasuries.";
            return false;
        }

        if (wonderA == null || wonderB == null)
        {
            error = "Wonder_A and Wonder_B are required.";
            return false;
        }

        if (wonderA == wonderB)
        {
            error = "Wonder_A and Wonder_B must be different Wonders.";
            return false;
        }

        if (wonderA.Owner != sideATreasury)
        {
            error = "Wonder_A must be owned by the Side A treasury.";
            return false;
        }

        if (wonderB.Owner != sideBTreasury)
        {
            error = "Wonder_B must be owned by the Side B treasury.";
            return false;
        }

        error = null;
        return true;
    }

    private void EndMatch(ArenaMatchResult finalResult)
    {
        if (IsMatchEnded)
        {
            return;
        }

        result = finalResult;
        matchPauseLease = SimulationPauseCoordinator.Acquire();
        latestMatchResult = BuildMatchResult();
        Debug.Log(latestMatchResult, this);
    }

    private string BuildMatchResult()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("CIVILIZATION_ARENA_MATCH_RESULT");
        text.AppendLine($"result={FormatResult(result)}");
        text.AppendLine($"wonderACompleted={wonderACompleted.ToString().ToLowerInvariant()}");
        text.Append($"wonderBCompleted={wonderBCompleted.ToString().ToLowerInvariant()}");

        if (worldClock != null)
        {
            text.AppendLine();
            text.AppendLine($"day={worldClock.Day}");
            text.Append($"time={worldClock.Hour:00}:{worldClock.Minute:00}");
        }

        return text.ToString();
    }

    private void OnDisable()
    {
        ReleaseMatchPause();
    }

    private void OnDestroy()
    {
        ReleaseMatchPause();
    }

    private void ReleaseMatchPause()
    {
        if (!matchPauseLease.IsValid)
        {
            return;
        }

        SimulationPauseCoordinator.Release(matchPauseLease);
        matchPauseLease = default;
    }

    private static string FormatResult(ArenaMatchResult matchResult)
    {
        switch (matchResult)
        {
            case ArenaMatchResult.SideA:
                return "A";
            case ArenaMatchResult.SideB:
                return "B";
            case ArenaMatchResult.Draw:
                return "DRAW";
            default:
                return "IN_PROGRESS";
        }
    }
}
