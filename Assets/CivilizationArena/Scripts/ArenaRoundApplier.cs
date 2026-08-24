using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class ArenaRoundApplier : MonoBehaviour
{
    [SerializeField] private ArenaRoundSnapshotBuilder snapshotBuilder;
    [SerializeField] private AgentTreasury sideATreasury;
    [SerializeField] private AgentTreasury sideBTreasury;
    [SerializeField] private AgentTextInterface sideATextInterface;
    [SerializeField] private AgentTextInterface sideBTextInterface;

    public bool TryApply(
        ArenaRoundSnapshot expectedSnapshot,
        ArenaRoundResolution resolution,
        out string error)
    {
        if (!TryValidateInternalConfiguration(out error) ||
            expectedSnapshot == null ||
            resolution == null)
        {
            if (error == null)
            {
                error = expectedSnapshot == null
                    ? "Expected Arena round snapshot is required."
                    : "Arena round resolution is required.";
            }

            return false;
        }

        if (!snapshotBuilder.TryBuild(
            out ArenaRoundSnapshot currentSnapshot,
            out error))
        {
            error = $"Current snapshot failed: {error}";
            return false;
        }

        if (!TryVerifySnapshot(
            expectedSnapshot,
            currentSnapshot,
            out error))
        {
            return false;
        }

        if (!snapshotBuilder.TryGetConfiguredCitizens(
            out IReadOnlyDictionary<string, CitizenEmployment> citizens,
            out error))
        {
            return false;
        }

        if (!TryBuildWorkplaceLookup(
                sideATextInterface,
                sideATreasury,
                "Side A",
                out Dictionary<string, Workplace> workplacesA,
                out error) ||
            !TryBuildWorkplaceLookup(
                sideBTextInterface,
                sideBTreasury,
                "Side B",
                out Dictionary<string, Workplace> workplacesB,
                out error) ||
            !TryPreflight(
                expectedSnapshot,
                resolution,
                citizens,
                workplacesA,
                workplacesB,
                out List<PendingWinner> pendingWinners,
                out error))
        {
            return false;
        }

        if (!TryValidateResolutionAgainstSnapshot(
            expectedSnapshot,
            resolution,
            out error))
        {
            return false;
        }

        for (int i = 0; i < pendingWinners.Count; i++)
        {
            PendingWinner pending = pendingWinners[i];

            if (!pending.Citizen.TryAcceptOffer(
                pending.Employer,
                pending.Workplace,
                pending.Wage))
            {
                error = i > 0
                    ? $"Unexpected gameplay rejection for " +
                      $"{pending.CitizenId} by Side {pending.Side}. " +
                      $"Round application stopped after {i} earlier " +
                      $"winning offer(s) were applied; partial state remains."
                    : $"Unexpected gameplay rejection for " +
                      $"{pending.CitizenId} by Side {pending.Side}; " +
                      $"no winning offers were applied.";
                return false;
            }
        }

        if (sideATreasury.CurrentPayrollPerHour !=
                resolution.FinalProjectedPayrollA ||
            sideBTreasury.CurrentPayrollPerHour !=
                resolution.FinalProjectedPayrollB)
        {
            error =
                "Post-application payroll mismatch: " +
                $"A expected={Format(resolution.FinalProjectedPayrollA)} " +
                $"actual={Format(sideATreasury.CurrentPayrollPerHour)}, " +
                $"B expected={Format(resolution.FinalProjectedPayrollB)} " +
                $"actual={Format(sideBTreasury.CurrentPayrollPerHour)}.";
            return false;
        }

        error = null;
        return true;
    }

    public bool TryValidateConfiguration(
        ArenaRoundSnapshotBuilder expectedSnapshotBuilder,
        AgentTextInterface expectedSideATextInterface,
        AgentTextInterface expectedSideBTextInterface,
        out string error)
    {
        if (!TryValidateInternalConfiguration(out error))
        {
            return false;
        }

        if (expectedSnapshotBuilder == null ||
            expectedSideATextInterface == null ||
            expectedSideBTextInterface == null)
        {
            error = "Expected Arena application references are required.";
            return false;
        }

        if (snapshotBuilder != expectedSnapshotBuilder ||
            sideATextInterface != expectedSideATextInterface ||
            sideBTextInterface != expectedSideBTextInterface)
        {
            error =
                "ArenaRoundApplier does not use the controller's snapshot " +
                "builder and side text interfaces.";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryValidateInternalConfiguration(out string error)
    {
        if (snapshotBuilder == null ||
            sideATreasury == null ||
            sideBTreasury == null ||
            sideATextInterface == null ||
            sideBTextInterface == null)
        {
            error = "ArenaRoundApplier references are not fully configured.";
            return false;
        }

        if (sideATreasury == sideBTreasury)
        {
            error = "Side A and Side B must use different treasuries.";
            return false;
        }

        if (sideATextInterface == sideBTextInterface ||
            sideATextInterface.Treasury != sideATreasury ||
            sideBTextInterface.Treasury != sideBTreasury)
        {
            error =
                "ArenaRoundApplier text-interface side mappings are invalid.";
            return false;
        }

        if (snapshotBuilder.SideATreasury != sideATreasury ||
            snapshotBuilder.SideBTreasury != sideBTreasury)
        {
            error =
                "ArenaRoundApplier and snapshot builder side mappings differ.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryVerifySnapshot(
        ArenaRoundSnapshot expected,
        ArenaRoundSnapshot current,
        out string error)
    {
        if (expected.SideA == null ||
            expected.SideB == null ||
            expected.Citizens == null)
        {
            error = "Expected Arena round snapshot is malformed.";
            return false;
        }

        if (!EconomicSnapshotsMatch(expected.SideA, current.SideA))
        {
            error = BuildEconomicSnapshotMismatch(
                "A",
                expected.SideA,
                current.SideA);
            return false;
        }

        if (!EconomicSnapshotsMatch(expected.SideB, current.SideB))
        {
            error = BuildEconomicSnapshotMismatch(
                "B",
                expected.SideB,
                current.SideB);
            return false;
        }

        if (expected.Citizens.Count != current.Citizens.Count)
        {
            error = "Configured citizen set changed after resolution.";
            return false;
        }

        foreach (KeyValuePair<string, ArenaCitizenEmploymentSnapshot> entry
            in expected.Citizens)
        {
            ArenaCitizenEmploymentSnapshot expectedCitizen = entry.Value;

            if (expectedCitizen == null ||
                !string.Equals(
                    entry.Key,
                    expectedCitizen.CitizenId,
                    StringComparison.Ordinal) ||
                !current.Citizens.TryGetValue(
                    entry.Key,
                    out ArenaCitizenEmploymentSnapshot currentCitizen))
            {
                error = $"Citizen snapshot mismatch: {entry.Key}.";
                return false;
            }

            if (expectedCitizen.IsEmployed != currentCitizen.IsEmployed ||
                expectedCitizen.CurrentEmployerSide !=
                    currentCitizen.CurrentEmployerSide ||
                expectedCitizen.CurrentWage != currentCitizen.CurrentWage ||
                expectedCitizen.ReservationWage !=
                    currentCitizen.ReservationWage)
            {
                error =
                    $"Citizen state changed after resolution: {entry.Key}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool EconomicSnapshotsMatch(
        ArenaAgentEconomicSnapshot expected,
        ArenaAgentEconomicSnapshot current)
    {
        return current != null &&
            expected.Gold == current.Gold &&
            expected.CurrentPayrollPerHour == current.CurrentPayrollPerHour &&
            expected.PayrollCoverageHours == current.PayrollCoverageHours;
    }

    private static string BuildEconomicSnapshotMismatch(
        string side,
        ArenaAgentEconomicSnapshot expected,
        ArenaAgentEconomicSnapshot current)
    {
        if (current == null)
        {
            return $"Side {side} economic snapshot is missing.";
        }

        return $"Side {side} economic snapshot mismatch: " +
            $"gold expected={Format(expected.Gold)} " +
            $"actual={Format(current.Gold)}, " +
            $"payroll expected={Format(expected.CurrentPayrollPerHour)} " +
            $"actual={Format(current.CurrentPayrollPerHour)}, " +
            $"coverage expected={Format(expected.PayrollCoverageHours)} " +
            $"actual={Format(current.PayrollCoverageHours)}.";
    }

    private static bool TryBuildWorkplaceLookup(
        AgentTextInterface textInterface,
        AgentTreasury expectedTreasury,
        string sideName,
        out Dictionary<string, Workplace> workplacesById,
        out string error)
    {
        workplacesById = new Dictionary<string, Workplace>(
            StringComparer.Ordinal);

        if (textInterface.Treasury != expectedTreasury)
        {
            error = $"{sideName} Workplace configuration uses another treasury.";
            return false;
        }

        if (!textInterface.TryGetOfferConfiguration(
            out _,
            out _,
            out string[] workplaceIds,
            out Workplace[] workplaces,
            out error))
        {
            error = $"{sideName} Workplace configuration failed: {error}";
            return false;
        }

        for (int i = 0; i < workplaceIds.Length; i++)
        {
            if (!workplacesById.TryAdd(workplaceIds[i], workplaces[i]))
            {
                error =
                    $"{sideName} contains duplicate Workplace ID " +
                    $"{workplaceIds[i]}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private bool TryPreflight(
        ArenaRoundSnapshot expectedSnapshot,
        ArenaRoundResolution resolution,
        IReadOnlyDictionary<string, CitizenEmployment> citizens,
        IReadOnlyDictionary<string, Workplace> workplacesA,
        IReadOnlyDictionary<string, Workplace> workplacesB,
        out List<PendingWinner> pendingWinners,
        out string error)
    {
        pendingWinners = new List<PendingWinner>();

        if (resolution.Citizens == null ||
            !IsFiniteNonNegative(resolution.FinalProjectedPayrollA) ||
            !IsFiniteNonNegative(resolution.FinalProjectedPayrollB) ||
            !IsValidSide(resolution.InitialTiePriority) ||
            !IsValidSide(resolution.FinalTiePriority))
        {
            error = "Arena round resolution is malformed.";
            return false;
        }

        string previousCitizenId = null;
        HashSet<string> resultCitizenIds = new HashSet<string>(
            StringComparer.Ordinal);

        for (int i = 0; i < resolution.Citizens.Count; i++)
        {
            ArenaCitizenOfferResolution citizenResult =
                resolution.Citizens[i];

            if (citizenResult == null ||
                string.IsNullOrWhiteSpace(citizenResult.CitizenId) ||
                (previousCitizenId != null &&
                 StringComparer.Ordinal.Compare(
                    previousCitizenId,
                    citizenResult.CitizenId) >= 0) ||
                !resultCitizenIds.Add(citizenResult.CitizenId) ||
                !expectedSnapshot.Citizens.ContainsKey(
                    citizenResult.CitizenId) ||
                !citizens.TryGetValue(
                    citizenResult.CitizenId,
                    out CitizenEmployment citizen) ||
                citizen == null ||
                citizen.GetComponent<CitizenWorkAssignment>() == null ||
                !TryValidateOfferResult(
                    citizenResult.CitizenId,
                    citizenResult.OfferA,
                    citizenResult.EligibilityA) ||
                !TryValidateOfferResult(
                    citizenResult.CitizenId,
                    citizenResult.OfferB,
                    citizenResult.EligibilityB))
            {
                error = $"Citizen resolution entry {i} is malformed.";
                return false;
            }

            previousCitizenId = citizenResult.CitizenId;

            if (!citizenResult.HasWinner)
            {
                if (citizenResult.WinningOffer != null ||
                    citizenResult.EligibilityA?.IsEligible == true ||
                    citizenResult.EligibilityB?.IsEligible == true)
                {
                    error =
                        $"Winner state is malformed for " +
                        $"{citizenResult.CitizenId}.";
                    return false;
                }

                continue;
            }

            ArenaSide winnerSide = citizenResult.WinnerSide.Value;
            ArenaEmploymentOffer expectedWinningOffer;
            ArenaOfferEligibilityResult winningEligibility;
            AgentTreasury employer;
            IReadOnlyDictionary<string, Workplace> workplaces;

            if (winnerSide == ArenaSide.A)
            {
                expectedWinningOffer = citizenResult.OfferA;
                winningEligibility = citizenResult.EligibilityA;
                employer = sideATreasury;
                workplaces = workplacesA;
            }
            else if (winnerSide == ArenaSide.B)
            {
                expectedWinningOffer = citizenResult.OfferB;
                winningEligibility = citizenResult.EligibilityB;
                employer = sideBTreasury;
                workplaces = workplacesB;
            }
            else
            {
                error =
                    $"Winner side is invalid for {citizenResult.CitizenId}.";
                return false;
            }

            ArenaEmploymentOffer winningOffer = citizenResult.WinningOffer;

            if (winningOffer == null ||
                !ReferenceEquals(winningOffer, expectedWinningOffer) ||
                winningEligibility?.IsEligible != true ||
                !winningEligibility.ProjectedPayrollIfWon.HasValue ||
                !IsFiniteNonNegative(
                    winningEligibility.ProjectedPayrollIfWon.Value) ||
                !workplaces.TryGetValue(
                    winningOffer.WorkplaceId,
                    out Workplace workplace))
            {
                error =
                    $"Winning offer cannot be mapped for " +
                    $"{citizenResult.CitizenId}.";
                return false;
            }

            WonderConstruction wonder =
                workplace.GetComponent<WonderConstruction>();

            if (wonder != null && wonder.Owner != employer)
            {
                error =
                    $"Winning Workplace {winningOffer.WorkplaceId} for " +
                    $"Side {winnerSide} belongs to another treasury.";
                return false;
            }

            pendingWinners.Add(new PendingWinner(
                citizenResult.CitizenId,
                winnerSide,
                citizen,
                employer,
                workplace,
                winningOffer.Wage));
        }

        error = null;
        return true;
    }

    private static bool TryValidateResolutionAgainstSnapshot(
        ArenaRoundSnapshot expectedSnapshot,
        ArenaRoundResolution suppliedResolution,
        out string error)
    {
        ArenaCitizenOfferPair[] pairs =
            new ArenaCitizenOfferPair[suppliedResolution.Citizens.Count];

        for (int i = 0; i < suppliedResolution.Citizens.Count; i++)
        {
            ArenaCitizenOfferResolution citizen =
                suppliedResolution.Citizens[i];
            pairs[i] = new ArenaCitizenOfferPair(
                citizen.CitizenId,
                citizen.OfferA,
                citizen.OfferB);
        }

        OfferConflictResolver conflictResolver =
            new OfferConflictResolver(
                suppliedResolution.InitialTiePriority);

        if (!ArenaRoundResolver.TryResolve(
                pairs,
                expectedSnapshot.Citizens,
                expectedSnapshot.SideA,
                expectedSnapshot.SideB,
                conflictResolver,
                out ArenaRoundResolution recomputedResolution,
                out _) ||
            !ResolutionsMatch(
                suppliedResolution,
                recomputedResolution))
        {
            error =
                "Arena round resolution does not match the expected snapshot.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool ResolutionsMatch(
        ArenaRoundResolution supplied,
        ArenaRoundResolution recomputed)
    {
        if (recomputed == null ||
            supplied.InitialTiePriority != recomputed.InitialTiePriority ||
            supplied.FinalTiePriority != recomputed.FinalTiePriority ||
            supplied.FinalProjectedPayrollA !=
                recomputed.FinalProjectedPayrollA ||
            supplied.FinalProjectedPayrollB !=
                recomputed.FinalProjectedPayrollB ||
            supplied.Citizens.Count != recomputed.Citizens.Count)
        {
            return false;
        }

        for (int i = 0; i < supplied.Citizens.Count; i++)
        {
            ArenaCitizenOfferResolution suppliedCitizen =
                supplied.Citizens[i];
            ArenaCitizenOfferResolution recomputedCitizen =
                recomputed.Citizens[i];

            if (!string.Equals(
                    suppliedCitizen.CitizenId,
                    recomputedCitizen.CitizenId,
                    StringComparison.Ordinal) ||
                !OffersMatch(
                    suppliedCitizen.OfferA,
                    recomputedCitizen.OfferA) ||
                !OffersMatch(
                    suppliedCitizen.OfferB,
                    recomputedCitizen.OfferB) ||
                !EligibilityMatches(
                    suppliedCitizen.EligibilityA,
                    recomputedCitizen.EligibilityA) ||
                !EligibilityMatches(
                    suppliedCitizen.EligibilityB,
                    recomputedCitizen.EligibilityB) ||
                suppliedCitizen.WinnerSide !=
                    recomputedCitizen.WinnerSide ||
                !OffersMatch(
                    suppliedCitizen.WinningOffer,
                    recomputedCitizen.WinningOffer))
            {
                return false;
            }
        }

        return true;
    }

    private static bool OffersMatch(
        ArenaEmploymentOffer left,
        ArenaEmploymentOffer right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        return string.Equals(
                left.CitizenId,
                right.CitizenId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.WorkplaceId,
                right.WorkplaceId,
                StringComparison.Ordinal) &&
            left.Wage == right.Wage;
    }

    private static bool EligibilityMatches(
        ArenaOfferEligibilityResult left,
        ArenaOfferEligibilityResult right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        return left.Reason == right.Reason &&
            left.ProjectedPayrollIfWon.Equals(
                right.ProjectedPayrollIfWon);
    }

    private static bool TryValidateOfferResult(
        string citizenId,
        ArenaEmploymentOffer offer,
        ArenaOfferEligibilityResult eligibility)
    {
        if (offer == null)
        {
            return eligibility == null;
        }

        if (eligibility == null ||
            eligibility.Reason == ArenaOfferEligibilityReason.InvalidInput ||
            !string.Equals(
                offer.CitizenId,
                citizenId,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(offer.WorkplaceId) ||
            offer.Wage <= 0)
        {
            return false;
        }

        if (eligibility.Reason == ArenaOfferEligibilityReason.WageTooLow)
        {
            return !eligibility.ProjectedPayrollIfWon.HasValue;
        }

        return eligibility.ProjectedPayrollIfWon.HasValue &&
            IsFiniteNonNegative(
                eligibility.ProjectedPayrollIfWon.Value);
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

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed class PendingWinner
    {
        public string CitizenId { get; }
        public ArenaSide Side { get; }
        public CitizenEmployment Citizen { get; }
        public AgentTreasury Employer { get; }
        public Workplace Workplace { get; }
        public int Wage { get; }

        public PendingWinner(
            string citizenId,
            ArenaSide side,
            CitizenEmployment citizen,
            AgentTreasury employer,
            Workplace workplace,
            int wage)
        {
            CitizenId = citizenId;
            Side = side;
            Citizen = citizen;
            Employer = employer;
            Workplace = workplace;
            Wage = wage;
        }
    }
}
