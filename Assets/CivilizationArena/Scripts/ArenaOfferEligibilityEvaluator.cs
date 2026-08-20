using System;

public sealed class ArenaCitizenEmploymentSnapshot
{
    public string CitizenId { get; }
    public bool IsEmployed { get; }
    public ArenaSide? CurrentEmployerSide { get; }
    public int CurrentWage { get; }
    public int ReservationWage { get; }

    public ArenaCitizenEmploymentSnapshot(
        string citizenId,
        bool isEmployed,
        ArenaSide? currentEmployerSide,
        int currentWage,
        int reservationWage)
    {
        CitizenId = citizenId;
        IsEmployed = isEmployed;
        CurrentEmployerSide = currentEmployerSide;
        CurrentWage = currentWage;
        ReservationWage = reservationWage;
    }
}

public enum ArenaOfferEligibilityReason
{
    Eligible,
    WageTooLow,
    InsufficientPayrollCoverage,
    InvalidInput
}

public sealed class ArenaOfferEligibilityResult
{
    public ArenaOfferEligibilityReason Reason { get; }
    public bool IsEligible => Reason == ArenaOfferEligibilityReason.Eligible;
    public float? ProjectedPayrollIfWon { get; }

    internal ArenaOfferEligibilityResult(
        ArenaOfferEligibilityReason reason,
        float? projectedPayrollIfWon)
    {
        Reason = reason;
        ProjectedPayrollIfWon = projectedPayrollIfWon;
    }
}

public static class ArenaOfferEligibilityEvaluator
{
    public static ArenaOfferEligibilityResult Evaluate(
        ArenaSide bidderSide,
        ArenaEmploymentOffer offer,
        ArenaCitizenEmploymentSnapshot citizenSnapshot,
        float bidderGold,
        float bidderProjectedPayrollPerHour,
        float payrollCoverageHours)
    {
        if (!IsValidSide(bidderSide) ||
            offer == null ||
            citizenSnapshot == null ||
            string.IsNullOrWhiteSpace(offer.CitizenId) ||
            string.IsNullOrWhiteSpace(citizenSnapshot.CitizenId) ||
            !string.Equals(
                offer.CitizenId,
                citizenSnapshot.CitizenId,
                StringComparison.Ordinal) ||
            offer.Wage <= 0 ||
            !IsFiniteNonNegative(bidderGold) ||
            !IsFiniteNonNegative(bidderProjectedPayrollPerHour) ||
            !IsFinitePositive(payrollCoverageHours) ||
            !IsValidSnapshot(citizenSnapshot))
        {
            return Result(ArenaOfferEligibilityReason.InvalidInput);
        }

        bool wageIsEligible = citizenSnapshot.IsEmployed
            ? offer.Wage > citizenSnapshot.CurrentWage
            : offer.Wage >= citizenSnapshot.ReservationWage;

        if (!wageIsEligible)
        {
            return Result(ArenaOfferEligibilityReason.WageTooLow);
        }

        bool bidderIsCurrentEmployer = citizenSnapshot.IsEmployed &&
            citizenSnapshot.CurrentEmployerSide == bidderSide;

        if (bidderIsCurrentEmployer &&
            bidderProjectedPayrollPerHour < citizenSnapshot.CurrentWage)
        {
            return Result(ArenaOfferEligibilityReason.InvalidInput);
        }

        float projectedPayrollIfWon = bidderProjectedPayrollPerHour;

        if (bidderIsCurrentEmployer)
        {
            projectedPayrollIfWon -= citizenSnapshot.CurrentWage;
        }

        projectedPayrollIfWon += offer.Wage;

        if (!IsFiniteNonNegative(projectedPayrollIfWon))
        {
            return Result(ArenaOfferEligibilityReason.InvalidInput);
        }

        float requiredGold =
            projectedPayrollIfWon * payrollCoverageHours;

        if (!IsFiniteNonNegative(requiredGold))
        {
            return Result(ArenaOfferEligibilityReason.InvalidInput);
        }

        if (bidderGold < requiredGold)
        {
            return Result(
                ArenaOfferEligibilityReason.InsufficientPayrollCoverage,
                projectedPayrollIfWon);
        }

        return Result(
            ArenaOfferEligibilityReason.Eligible,
            projectedPayrollIfWon);
    }

    private static bool IsValidSnapshot(
        ArenaCitizenEmploymentSnapshot snapshot)
    {
        if (snapshot.ReservationWage < 0)
        {
            return false;
        }

        if (snapshot.IsEmployed)
        {
            return snapshot.CurrentEmployerSide.HasValue &&
                IsValidSide(snapshot.CurrentEmployerSide.Value) &&
                snapshot.CurrentWage > 0;
        }

        return !snapshot.CurrentEmployerSide.HasValue &&
            snapshot.CurrentWage == 0;
    }

    private static bool IsValidSide(ArenaSide side)
    {
        return side == ArenaSide.A || side == ArenaSide.B;
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

    private static ArenaOfferEligibilityResult Result(
        ArenaOfferEligibilityReason reason,
        float? projectedPayrollIfWon = null)
    {
        return new ArenaOfferEligibilityResult(
            reason,
            projectedPayrollIfWon);
    }
}
