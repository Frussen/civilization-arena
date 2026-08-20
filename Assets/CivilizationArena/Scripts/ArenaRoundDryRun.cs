using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class ArenaRoundDryRun : MonoBehaviour
{
    [SerializeField] private ArenaRoundSnapshotBuilder snapshotBuilder;

    [TextArea(8, 24)]
    [SerializeField] private string sideAActionJson;

    [TextArea(8, 24)]
    [SerializeField] private string sideBActionJson;

    [ContextMenu("Resolve Round (Dry Run)")]
    private void ResolveRoundDryRun()
    {
        if (snapshotBuilder == null)
        {
            Debug.LogError(
                "Arena dry run configuration failed: snapshot builder is required.",
                this);
            return;
        }

        if (!snapshotBuilder.TryBuild(
            out ArenaRoundSnapshot snapshot,
            out string error))
        {
            Debug.LogError($"Arena dry run snapshot failed: {error}", this);
            return;
        }

        if (!ArenaActionParser.TryParse(
            sideAActionJson,
            out ArenaAction actionA,
            out error))
        {
            Debug.LogError($"Arena dry run Side A parse failed: {error}", this);
            return;
        }

        if (!ArenaActionParser.TryParse(
            sideBActionJson,
            out ArenaAction actionB,
            out error))
        {
            Debug.LogError($"Arena dry run Side B parse failed: {error}", this);
            return;
        }

        if (!ArenaOfferPairing.TryBuild(
            actionA,
            actionB,
            out IReadOnlyList<ArenaCitizenOfferPair> pairs,
            out error))
        {
            Debug.LogError($"Arena dry run pairing failed: {error}", this);
            return;
        }

        OfferConflictResolver conflictResolver =
            new OfferConflictResolver(ArenaSide.A);

        if (!ArenaRoundResolver.TryResolve(
            pairs,
            snapshot.Citizens,
            snapshot.SideA,
            snapshot.SideB,
            conflictResolver,
            out ArenaRoundResolution resolution,
            out error))
        {
            Debug.LogError($"Arena dry run resolution failed: {error}", this);
            return;
        }

        Debug.Log(BuildResolutionSummary(snapshot, resolution), this);
    }

    private static string BuildResolutionSummary(
        ArenaRoundSnapshot snapshot,
        ArenaRoundResolution resolution)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("CIVILIZATION_ARENA_ROUND_DRY_RUN");
        text.AppendLine();
        text.AppendLine("initial:");
        AppendEconomicSnapshot(text, "sideA", snapshot.SideA);
        text.AppendLine();
        AppendEconomicSnapshot(text, "sideB", snapshot.SideB);
        text.AppendLine();
        text.AppendLine("resolution:");

        for (int i = 0; i < resolution.Citizens.Count; i++)
        {
            ArenaCitizenOfferResolution citizen = resolution.Citizens[i];
            text.AppendLine($"{citizen.CitizenId}:");
            AppendOffer(text, "A", citizen.OfferA, citizen.EligibilityA);
            AppendOffer(text, "B", citizen.OfferB, citizen.EligibilityB);

            if (citizen.HasWinner)
            {
                text.Append("winner=");
                text.Append(citizen.WinnerSide.Value);
                text.Append(' ');
                text.Append(citizen.WinningOffer.WorkplaceId);
                text.Append(" @");
                text.AppendLine(citizen.WinningOffer.Wage.ToString(
                    CultureInfo.InvariantCulture));
            }
            else
            {
                text.AppendLine("winner=none");
            }

            text.AppendLine();
        }

        text.AppendLine("finalProjectedPayroll:");
        text.AppendLine($"A={Format(resolution.FinalProjectedPayrollA)}");
        text.AppendLine($"B={Format(resolution.FinalProjectedPayrollB)}");
        text.Append("finalTiePriority=");
        text.Append(resolution.FinalTiePriority);
        return text.ToString();
    }

    private static void AppendEconomicSnapshot(
        StringBuilder text,
        string sideName,
        ArenaAgentEconomicSnapshot snapshot)
    {
        text.AppendLine($"{sideName}:");
        text.AppendLine($"gold={Format(snapshot.Gold)}");
        text.AppendLine(
            $"payrollPerHour={Format(snapshot.CurrentPayrollPerHour)}");
        text.AppendLine(
            $"coverageHours={Format(snapshot.PayrollCoverageHours)}");
    }

    private static void AppendOffer(
        StringBuilder text,
        string sideName,
        ArenaEmploymentOffer offer,
        ArenaOfferEligibilityResult eligibility)
    {
        text.Append(sideName);
        text.Append(": ");

        if (offer == null)
        {
            text.AppendLine("none");
            return;
        }

        text.Append(offer.WorkplaceId);
        text.Append(" @");
        text.Append(offer.Wage.ToString(CultureInfo.InvariantCulture));
        text.Append(" -> ");
        text.AppendLine(eligibility.Reason.ToString());
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
