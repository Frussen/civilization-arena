using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class ArenaRoundDryRun : MonoBehaviour
{
    [SerializeField] private ArenaRoundSnapshotBuilder snapshotBuilder;
    [SerializeField] private ArenaRoundApplier arenaRoundApplier;

    [TextArea(8, 24)]
    [SerializeField] private string sideAActionJson;

    [TextArea(8, 24)]
    [SerializeField] private string sideBActionJson;

    [ContextMenu("Resolve Round (Dry Run)")]
    private void ResolveRoundDryRun()
    {
        if (!TryResolveRound(
            out ArenaRoundSnapshot snapshot,
            out ArenaRoundResolution resolution,
            out string error))
        {
            Debug.LogError($"Arena dry run failed: {error}", this);
            return;
        }

        Debug.Log(BuildResolutionSummary(snapshot, resolution), this);
    }

    [ContextMenu("Resolve + Apply (Debug)")]
    private void ResolveAndApplyDebug()
    {
        if (arenaRoundApplier == null)
        {
            Debug.LogError(
                "Arena round apply configuration failed: applier is required.",
                this);
            return;
        }

        if (!TryResolveRound(
            out ArenaRoundSnapshot originalSnapshot,
            out ArenaRoundResolution resolution,
            out string error))
        {
            Debug.LogError($"Arena round apply preparation failed: {error}", this);
            return;
        }

        if (!arenaRoundApplier.TryApply(
            originalSnapshot,
            resolution,
            out string applyError))
        {
            Debug.LogError($"Arena round application failed: {applyError}", this);
            return;
        }

        if (!snapshotBuilder.TryBuild(
            out ArenaRoundSnapshot postApplicationSnapshot,
            out string snapshotError))
        {
            Debug.LogWarning(
                BuildPostApplicationSnapshotFailureSummary(
                    resolution,
                    snapshotError),
                this);
            return;
        }

        Debug.Log(
            BuildApplicationSummary(resolution, postApplicationSnapshot),
            this);
    }

    private bool TryResolveRound(
        out ArenaRoundSnapshot snapshot,
        out ArenaRoundResolution resolution,
        out string error)
    {
        snapshot = null;
        resolution = null;

        if (snapshotBuilder == null)
        {
            error = "snapshot builder is required.";
            return false;
        }

        if (!snapshotBuilder.TryBuild(out snapshot, out error))
        {
            error = $"snapshot failed: {error}";
            return false;
        }

        if (!ArenaActionParser.TryParse(
            sideAActionJson,
            out ArenaAction actionA,
            out error))
        {
            error = $"Side A parse failed: {error}";
            return false;
        }

        if (!ArenaActionParser.TryParse(
            sideBActionJson,
            out ArenaAction actionB,
            out error))
        {
            error = $"Side B parse failed: {error}";
            return false;
        }

        if (!ArenaOfferPairing.TryBuild(
            actionA,
            actionB,
            out IReadOnlyList<ArenaCitizenOfferPair> pairs,
            out error))
        {
            error = $"pairing failed: {error}";
            return false;
        }

        OfferConflictResolver conflictResolver =
            new OfferConflictResolver(ArenaSide.A);

        if (!ArenaRoundResolver.TryResolve(
            pairs,
            snapshot.Citizens,
            snapshot.SideA,
            snapshot.SideB,
            conflictResolver,
            out resolution,
            out error))
        {
            error = $"resolution failed: {error}";
            return false;
        }

        error = null;
        return true;
    }

    private static string BuildApplicationSummary(
        ArenaRoundResolution resolution,
        ArenaRoundSnapshot postApplicationSnapshot)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("CIVILIZATION_ARENA_ROUND_APPLY_RESULT");
        text.AppendLine("success=true");
        AppendProjectedPayroll(text, resolution);
        text.AppendLine();
        text.AppendLine("postApplicationPayroll:");
        text.AppendLine(
            $"A={Format(postApplicationSnapshot.SideA.CurrentPayrollPerHour)}");
        text.AppendLine(
            $"B={Format(postApplicationSnapshot.SideB.CurrentPayrollPerHour)}");
        AppendPostApplicationCitizens(text, postApplicationSnapshot);
        return text.ToString().TrimEnd();
    }

    private static string BuildPostApplicationSnapshotFailureSummary(
        ArenaRoundResolution resolution,
        string snapshotError)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("CIVILIZATION_ARENA_ROUND_APPLY_RESULT");
        text.AppendLine("success=true");
        AppendProjectedPayroll(text, resolution);
        text.AppendLine("postApplicationSnapshot=failed");
        text.Append("verificationError=");
        text.Append(snapshotError);
        return text.ToString();
    }

    private static void AppendProjectedPayroll(
        StringBuilder text,
        ArenaRoundResolution resolution)
    {
        text.AppendLine("finalProjectedPayroll:");
        text.AppendLine($"A={Format(resolution.FinalProjectedPayrollA)}");
        text.AppendLine($"B={Format(resolution.FinalProjectedPayrollB)}");
    }

    private static void AppendPostApplicationCitizens(
        StringBuilder text,
        ArenaRoundSnapshot snapshot)
    {
        text.AppendLine();
        text.AppendLine("citizens:");

        List<string> citizenIds = new List<string>(snapshot.Citizens.Keys);
        citizenIds.Sort(System.StringComparer.Ordinal);

        for (int i = 0; i < citizenIds.Count; i++)
        {
            ArenaCitizenEmploymentSnapshot citizen =
                snapshot.Citizens[citizenIds[i]];
            string employer = citizen.CurrentEmployerSide.HasValue
                ? citizen.CurrentEmployerSide.Value.ToString()
                : "none";

            text.Append(citizen.CitizenId);
            text.Append(": employer=");
            text.Append(employer);
            text.Append(" wage=");
            text.AppendLine(citizen.CurrentWage.ToString(
                CultureInfo.InvariantCulture));
        }
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
