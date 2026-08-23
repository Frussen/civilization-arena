public sealed class ArenaActionBatch
{
    private ArenaAction actionA;
    private ArenaAction actionB;

    public ArenaAction ActionA => actionA;
    public ArenaAction ActionB => actionB;
    public bool HasActionA => actionA != null;
    public bool HasActionB => actionB != null;
    public bool IsComplete => HasActionA && HasActionB;

    public bool TrySubmit(ArenaSide side, ArenaAction action)
    {
        if (action == null)
        {
            return false;
        }

        switch (side)
        {
            case ArenaSide.A:
                if (HasActionA)
                {
                    return false;
                }

                actionA = action;
                return true;

            case ArenaSide.B:
                if (HasActionB)
                {
                    return false;
                }

                actionB = action;
                return true;

            default:
                return false;
        }
    }

    public void Reset()
    {
        actionA = null;
        actionB = null;
    }
}
