using System;
using System.Collections.Generic;

public sealed class ArenaAgentEconomicSnapshot
{
    public float Gold { get; }
    public float CurrentPayrollPerHour { get; }
    public float PayrollCoverageHours { get; }

    public ArenaAgentEconomicSnapshot(
        float gold,
        float currentPayrollPerHour,
        float payrollCoverageHours)
    {
        Gold = gold;
        CurrentPayrollPerHour = currentPayrollPerHour;
        PayrollCoverageHours = payrollCoverageHours;
    }
}

public sealed class ArenaCitizenOfferResolution
{
    public string CitizenId { get; }
    public ArenaEmploymentOffer OfferA { get; }
    public ArenaOfferEligibilityResult EligibilityA { get; }
    public ArenaEmploymentOffer OfferB { get; }
    public ArenaOfferEligibilityResult EligibilityB { get; }
    public bool HasOfferA => OfferA != null;
    public bool HasOfferB => OfferB != null;
    public bool HasWinner => WinnerSide.HasValue;
    public ArenaSide? WinnerSide { get; }
    public ArenaEmploymentOffer WinningOffer { get; }

    internal ArenaCitizenOfferResolution(
        string citizenId,
        ArenaEmploymentOffer offerA,
        ArenaOfferEligibilityResult eligibilityA,
        ArenaEmploymentOffer offerB,
        ArenaOfferEligibilityResult eligibilityB,
        ArenaSide? winnerSide,
        ArenaEmploymentOffer winningOffer)
    {
        CitizenId = citizenId;
        OfferA = offerA;
        EligibilityA = eligibilityA;
        OfferB = offerB;
        EligibilityB = eligibilityB;
        WinnerSide = winnerSide;
        WinningOffer = winningOffer;
    }
}

public sealed class ArenaRoundResolution
{
    public IReadOnlyList<ArenaCitizenOfferResolution> Citizens { get; }
    public float FinalProjectedPayrollA { get; }
    public float FinalProjectedPayrollB { get; }
    public ArenaSide FinalTiePriority { get; }

    internal ArenaRoundResolution(
        ArenaCitizenOfferResolution[] citizens,
        float finalProjectedPayrollA,
        float finalProjectedPayrollB,
        ArenaSide finalTiePriority)
    {
        Citizens = Array.AsReadOnly(citizens);
        FinalProjectedPayrollA = finalProjectedPayrollA;
        FinalProjectedPayrollB = finalProjectedPayrollB;
        FinalTiePriority = finalTiePriority;
    }
}

public static class ArenaRoundResolver
{
    public static bool TryResolve(
        IReadOnlyList<ArenaCitizenOfferPair> pairs,
        IReadOnlyDictionary<string, ArenaCitizenEmploymentSnapshot>
            citizenSnapshots,
        ArenaAgentEconomicSnapshot sideA,
        ArenaAgentEconomicSnapshot sideB,
        OfferConflictResolver conflictResolver,
        out ArenaRoundResolution resolution,
        out string error)
    {
        resolution = null;

        if (!TryValidateAndOrderPairs(
            pairs,
            out ArenaCitizenOfferPair[] orderedPairs,
            out error) ||
            !TryIndexSnapshots(
                citizenSnapshots,
                orderedPairs,
                out Dictionary<string, ArenaCitizenEmploymentSnapshot>
                    snapshotsByCitizen,
                out error) ||
            !TryValidateEconomicSnapshot(sideA, "Side A", out error) ||
            !TryValidateEconomicSnapshot(sideB, "Side B", out error))
        {
            return false;
        }

        if (conflictResolver == null)
        {
            error = "Offer conflict resolver is required.";
            return false;
        }

        OfferConflictResolver temporaryConflictResolver;

        try
        {
            temporaryConflictResolver = new OfferConflictResolver(
                conflictResolver.TiePriority);
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "Offer conflict resolver tie priority is invalid.";
            return false;
        }

        float projectedPayrollA = sideA.CurrentPayrollPerHour;
        float projectedPayrollB = sideB.CurrentPayrollPerHour;
        ArenaCitizenOfferResolution[] citizenResults =
            new ArenaCitizenOfferResolution[orderedPairs.Length];
        List<ArenaCitizenOfferPair> conflictComparisons =
            new List<ArenaCitizenOfferPair>();

        for (int i = 0; i < orderedPairs.Length; i++)
        {
            ArenaCitizenOfferPair pair = orderedPairs[i];
            ArenaCitizenEmploymentSnapshot citizenSnapshot =
                snapshotsByCitizen[pair.CitizenId];

            ArenaOfferEligibilityResult eligibilityA =
                EvaluateOffer(
                    ArenaSide.A,
                    pair.OfferA,
                    citizenSnapshot,
                    sideA,
                    projectedPayrollA);
            ArenaOfferEligibilityResult eligibilityB =
                EvaluateOffer(
                    ArenaSide.B,
                    pair.OfferB,
                    citizenSnapshot,
                    sideB,
                    projectedPayrollB);

            if (HasInvalidInput(eligibilityA) ||
                HasInvalidInput(eligibilityB))
            {
                error =
                    $"Eligibility input is invalid for {pair.CitizenId}.";
                return false;
            }

            ArenaSide? winnerSide = null;
            ArenaEmploymentOffer winningOffer = null;
            ArenaOfferEligibilityResult winningEligibility = null;

            if (eligibilityA?.IsEligible == true &&
                eligibilityB?.IsEligible == true)
            {
                winnerSide = temporaryConflictResolver.Resolve(
                    pair.OfferA.Wage,
                    pair.OfferB.Wage);
                conflictComparisons.Add(pair);
            }
            else if (eligibilityA?.IsEligible == true)
            {
                winnerSide = ArenaSide.A;
            }
            else if (eligibilityB?.IsEligible == true)
            {
                winnerSide = ArenaSide.B;
            }

            if (winnerSide == ArenaSide.A)
            {
                winningOffer = pair.OfferA;
                winningEligibility = eligibilityA;
            }
            else if (winnerSide == ArenaSide.B)
            {
                winningOffer = pair.OfferB;
                winningEligibility = eligibilityB;
            }

            if (winnerSide.HasValue &&
                !TryApplyWinnerToProjectedPayroll(
                    winnerSide.Value,
                    winningEligibility,
                    citizenSnapshot,
                    ref projectedPayrollA,
                    ref projectedPayrollB,
                    out error))
            {
                return false;
            }

            citizenResults[i] = new ArenaCitizenOfferResolution(
                pair.CitizenId,
                pair.OfferA,
                eligibilityA,
                pair.OfferB,
                eligibilityB,
                winnerSide,
                winningOffer);
        }

        resolution = new ArenaRoundResolution(
            citizenResults,
            projectedPayrollA,
            projectedPayrollB,
            temporaryConflictResolver.TiePriority);

        for (int i = 0; i < conflictComparisons.Count; i++)
        {
            ArenaCitizenOfferPair pair = conflictComparisons[i];
            conflictResolver.Resolve(pair.OfferA.Wage, pair.OfferB.Wage);
        }

        error = null;
        return true;
    }

    private static ArenaOfferEligibilityResult EvaluateOffer(
        ArenaSide bidderSide,
        ArenaEmploymentOffer offer,
        ArenaCitizenEmploymentSnapshot citizenSnapshot,
        ArenaAgentEconomicSnapshot economicSnapshot,
        float projectedPayrollPerHour)
    {
        if (offer == null)
        {
            return null;
        }

        return ArenaOfferEligibilityEvaluator.Evaluate(
            bidderSide,
            offer,
            citizenSnapshot,
            economicSnapshot.Gold,
            projectedPayrollPerHour,
            economicSnapshot.PayrollCoverageHours);
    }

    private static bool HasInvalidInput(
        ArenaOfferEligibilityResult eligibility)
    {
        return eligibility != null &&
            eligibility.Reason == ArenaOfferEligibilityReason.InvalidInput;
    }

    private static bool TryApplyWinnerToProjectedPayroll(
        ArenaSide winnerSide,
        ArenaOfferEligibilityResult winningEligibility,
        ArenaCitizenEmploymentSnapshot citizenSnapshot,
        ref float projectedPayrollA,
        ref float projectedPayrollB,
        out string error)
    {
        if (winningEligibility?.ProjectedPayrollIfWon == null)
        {
            error =
                $"Winning payroll is unavailable for {citizenSnapshot.CitizenId}.";
            return false;
        }

        float winningPayroll =
            winningEligibility.ProjectedPayrollIfWon.Value;

        if (!IsFiniteNonNegative(winningPayroll))
        {
            error =
                $"Winning payroll is invalid for {citizenSnapshot.CitizenId}.";
            return false;
        }

        if (winnerSide == ArenaSide.A)
        {
            projectedPayrollA = winningPayroll;
        }
        else if (winnerSide == ArenaSide.B)
        {
            projectedPayrollB = winningPayroll;
        }
        else
        {
            error = "Winning side is invalid.";
            return false;
        }

        if (citizenSnapshot.IsEmployed &&
            citizenSnapshot.CurrentEmployerSide != winnerSide)
        {
            if (citizenSnapshot.CurrentEmployerSide == ArenaSide.A)
            {
                if (!TryRemoveFormerWage(
                    ref projectedPayrollA,
                    citizenSnapshot.CurrentWage))
                {
                    error =
                        $"Side A payroll cannot release " +
                        $"{citizenSnapshot.CitizenId}.";
                    return false;
                }
            }
            else if (citizenSnapshot.CurrentEmployerSide == ArenaSide.B)
            {
                if (!TryRemoveFormerWage(
                    ref projectedPayrollB,
                    citizenSnapshot.CurrentWage))
                {
                    error =
                        $"Side B payroll cannot release " +
                        $"{citizenSnapshot.CitizenId}.";
                    return false;
                }
            }
            else
            {
                error =
                    $"Current employer is invalid for " +
                    $"{citizenSnapshot.CitizenId}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryRemoveFormerWage(
        ref float projectedPayroll,
        int currentWage)
    {
        if (!IsFiniteNonNegative(projectedPayroll) ||
            currentWage <= 0 ||
            projectedPayroll < currentWage)
        {
            return false;
        }

        float updatedPayroll = projectedPayroll - currentWage;

        if (!IsFiniteNonNegative(updatedPayroll))
        {
            return false;
        }

        projectedPayroll = updatedPayroll;
        return true;
    }

    private static bool TryValidateAndOrderPairs(
        IReadOnlyList<ArenaCitizenOfferPair> pairs,
        out ArenaCitizenOfferPair[] orderedPairs,
        out string error)
    {
        orderedPairs = Array.Empty<ArenaCitizenOfferPair>();

        if (pairs == null)
        {
            error = "Citizen offer pairs are required.";
            return false;
        }

        ArenaCitizenOfferPair[] result =
            new ArenaCitizenOfferPair[pairs.Count];
        HashSet<string> citizenIds = new HashSet<string>(
            StringComparer.Ordinal);

        for (int i = 0; i < pairs.Count; i++)
        {
            ArenaCitizenOfferPair pair = pairs[i];

            if (pair == null ||
                string.IsNullOrWhiteSpace(pair.CitizenId) ||
                (!pair.HasOfferA && !pair.HasOfferB) ||
                !IsOfferValidForCitizen(pair.OfferA, pair.CitizenId) ||
                !IsOfferValidForCitizen(pair.OfferB, pair.CitizenId))
            {
                error = $"Citizen offer pair {i} is invalid.";
                return false;
            }

            if (!citizenIds.Add(pair.CitizenId))
            {
                error = $"Duplicate citizen offer pair: {pair.CitizenId}.";
                return false;
            }

            result[i] = pair;
        }

        Array.Sort(
            result,
            (left, right) => StringComparer.Ordinal.Compare(
                left.CitizenId,
                right.CitizenId));

        orderedPairs = result;
        error = null;
        return true;
    }

    private static bool IsOfferValidForCitizen(
        ArenaEmploymentOffer offer,
        string citizenId)
    {
        if (offer == null)
        {
            return true;
        }

        return string.Equals(
                offer.CitizenId,
                citizenId,
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(offer.WorkplaceId) &&
            offer.Wage > 0;
    }

    private static bool TryIndexSnapshots(
        IReadOnlyDictionary<string, ArenaCitizenEmploymentSnapshot>
            citizenSnapshots,
        IReadOnlyList<ArenaCitizenOfferPair> orderedPairs,
        out Dictionary<string, ArenaCitizenEmploymentSnapshot>
            snapshotsByCitizen,
        out string error)
    {
        snapshotsByCitizen =
            new Dictionary<string, ArenaCitizenEmploymentSnapshot>(
                StringComparer.Ordinal);

        if (citizenSnapshots == null)
        {
            error = "Citizen employment snapshots are required.";
            return false;
        }

        foreach (KeyValuePair<string, ArenaCitizenEmploymentSnapshot> entry
            in citizenSnapshots)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) ||
                entry.Value == null ||
                !string.Equals(
                    entry.Key,
                    entry.Value.CitizenId,
                    StringComparison.Ordinal) ||
                !snapshotsByCitizen.TryAdd(entry.Key, entry.Value))
            {
                error = "Citizen employment snapshot mapping is invalid.";
                return false;
            }
        }

        for (int i = 0; i < orderedPairs.Count; i++)
        {
            if (!snapshotsByCitizen.ContainsKey(orderedPairs[i].CitizenId))
            {
                error =
                    $"Missing employment snapshot for " +
                    $"{orderedPairs[i].CitizenId}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateEconomicSnapshot(
        ArenaAgentEconomicSnapshot snapshot,
        string sideName,
        out string error)
    {
        if (snapshot == null ||
            !IsFiniteNonNegative(snapshot.Gold) ||
            !IsFiniteNonNegative(snapshot.CurrentPayrollPerHour) ||
            !IsFinitePositive(snapshot.PayrollCoverageHours))
        {
            error = $"{sideName} economic snapshot is invalid.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value >= 0f;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value > 0f;
    }
}
