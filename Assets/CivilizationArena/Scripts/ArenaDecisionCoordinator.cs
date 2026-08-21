public sealed class ArenaDecisionCoordinator
{
    private readonly ArenaActionBatch actionBatch = new ArenaActionBatch();
    private int currentRoundId;
    private bool isRoundOpen;

    public int CurrentRoundId => currentRoundId;
    public bool IsRoundOpen => isRoundOpen;
    public bool IsReady => isRoundOpen && actionBatch.IsComplete;
    public string ActionA => actionBatch.ActionA;
    public string ActionB => actionBatch.ActionB;
    public bool HasActionA => actionBatch.HasActionA;
    public bool HasActionB => actionBatch.HasActionB;

    public bool TryBeginRound(out int roundId)
    {
        if (isRoundOpen || currentRoundId == int.MaxValue)
        {
            roundId = currentRoundId;
            return false;
        }

        actionBatch.Reset();
        currentRoundId++;
        isRoundOpen = true;
        roundId = currentRoundId;
        return true;
    }

    public bool TrySubmit(
        int roundId,
        ArenaSide side,
        string actionJson)
    {
        if (!isRoundOpen || roundId != currentRoundId)
        {
            return false;
        }

        return actionBatch.TrySubmit(side, actionJson);
    }

    public bool TryCloseRound()
    {
        if (!IsReady)
        {
            return false;
        }

        isRoundOpen = false;
        actionBatch.Reset();
        return true;
    }

    public bool TryAbortRound()
    {
        if (!isRoundOpen)
        {
            return false;
        }

        isRoundOpen = false;
        actionBatch.Reset();
        return true;
    }
}
