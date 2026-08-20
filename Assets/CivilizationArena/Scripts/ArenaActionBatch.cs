using System;

public sealed class ArenaActionBatch
{
    private string actionA;
    private string actionB;
    private bool hasActionA;
    private bool hasActionB;

    public string ActionA => actionA;
    public string ActionB => actionB;
    public bool HasActionA => hasActionA;
    public bool HasActionB => hasActionB;
    public bool IsComplete => hasActionA && hasActionB;

    public bool TrySubmit(ArenaSide side, string actionJson)
    {
        if (string.IsNullOrWhiteSpace(actionJson))
        {
            return false;
        }

        switch (side)
        {
            case ArenaSide.A:
                if (hasActionA)
                {
                    return false;
                }

                actionA = actionJson;
                hasActionA = true;
                return true;

            case ArenaSide.B:
                if (hasActionB)
                {
                    return false;
                }

                actionB = actionJson;
                hasActionB = true;
                return true;

            default:
                return false;
        }
    }

    public void Reset()
    {
        actionA = null;
        actionB = null;
        hasActionA = false;
        hasActionB = false;
    }
}
