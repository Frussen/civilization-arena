using System;

public enum ArenaSide
{
    A,
    B
}

public sealed class OfferConflictResolver
{
    public ArenaSide TiePriority { get; private set; }

    public OfferConflictResolver(
        ArenaSide initialTiePriority = ArenaSide.A)
    {
        if (initialTiePriority != ArenaSide.A &&
            initialTiePriority != ArenaSide.B)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialTiePriority));
        }

        TiePriority = initialTiePriority;
    }

    public ArenaSide Resolve(int agentAWage, int agentBWage)
    {
        if (agentAWage > agentBWage)
        {
            return ArenaSide.A;
        }

        if (agentBWage > agentAWage)
        {
            return ArenaSide.B;
        }

        ArenaSide winner = TiePriority;
        TiePriority = TiePriority == ArenaSide.A
            ? ArenaSide.B
            : ArenaSide.A;

        return winner;
    }
}
