using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class ArenaRoundSnapshot
{
    public IReadOnlyDictionary<string, ArenaCitizenEmploymentSnapshot>
        Citizens { get; }
    public ArenaAgentEconomicSnapshot SideA { get; }
    public ArenaAgentEconomicSnapshot SideB { get; }

    internal ArenaRoundSnapshot(
        Dictionary<string, ArenaCitizenEmploymentSnapshot> citizens,
        ArenaAgentEconomicSnapshot sideA,
        ArenaAgentEconomicSnapshot sideB)
    {
        Citizens = new ReadOnlyDictionary<
            string,
            ArenaCitizenEmploymentSnapshot>(
                new Dictionary<string, ArenaCitizenEmploymentSnapshot>(
                    citizens,
                    StringComparer.Ordinal));
        SideA = sideA;
        SideB = sideB;
    }
}

public sealed class ArenaRoundSnapshotBuilder : MonoBehaviour
{
    [SerializeField] private AgentTreasury sideATreasury;
    [SerializeField] private AgentTreasury sideBTreasury;
    [SerializeField] private CitizenEmployment[] citizens;

    public AgentTreasury SideATreasury => sideATreasury;
    public AgentTreasury SideBTreasury => sideBTreasury;

    public bool TryGetConfiguredCitizens(
        out IReadOnlyDictionary<string, CitizenEmployment>
            configuredCitizens,
        out string error)
    {
        configuredCitizens = null;

        if (citizens == null)
        {
            error = "Configured citizens are required.";
            return false;
        }

        Dictionary<string, CitizenEmployment> citizensById =
            new Dictionary<string, CitizenEmployment>(
                StringComparer.Ordinal);

        for (int i = 0; i < citizens.Length; i++)
        {
            CitizenEmployment citizen = citizens[i];
            string citizenId = citizen != null
                ? citizen.gameObject.name
                : null;

            if (citizen == null ||
                string.IsNullOrWhiteSpace(citizenId) ||
                !citizensById.TryAdd(citizenId, citizen))
            {
                error =
                    "Configured citizens must have unique, non-empty IDs.";
                return false;
            }
        }

        configuredCitizens = new ReadOnlyDictionary<
            string,
            CitizenEmployment>(citizensById);
        error = null;
        return true;
    }

    [ContextMenu("Build Snapshot (Debug)")]
    private void BuildSnapshotDebug()
    {
        if (!TryBuild(out ArenaRoundSnapshot snapshot, out string error))
        {
            Debug.LogError($"Arena snapshot build failed: {error}", this);
            return;
        }

        StringBuilder text = new StringBuilder();
        text.AppendLine("CIVILIZATION_ARENA_ROUND_SNAPSHOT");
        AppendEconomicSnapshot(text, "sideA", snapshot.SideA);
        text.AppendLine();
        AppendEconomicSnapshot(text, "sideB", snapshot.SideB);
        text.AppendLine();
        text.AppendLine("citizens:");

        List<string> citizenIds = new List<string>(
            snapshot.Citizens.Keys);
        citizenIds.Sort(StringComparer.Ordinal);

        for (int i = 0; i < citizenIds.Count; i++)
        {
            ArenaCitizenEmploymentSnapshot citizen =
                snapshot.Citizens[citizenIds[i]];
            string employer = citizen.CurrentEmployerSide.HasValue
                ? citizen.CurrentEmployerSide.Value.ToString()
                : "none";

            text.Append(citizen.CitizenId);
            text.Append(": employed=");
            text.Append(citizen.IsEmployed ? "true" : "false");
            text.Append(" employer=");
            text.Append(employer);
            text.Append(" wage=");
            text.Append(citizen.CurrentWage.ToString(
                CultureInfo.InvariantCulture));
            text.Append(" reservation=");
            text.AppendLine(citizen.ReservationWage.ToString(
                CultureInfo.InvariantCulture));
        }

        Debug.Log(text.ToString(), this);
    }

    public bool TryBuild(
        out ArenaRoundSnapshot snapshot,
        out string error)
    {
        snapshot = null;

        if (sideATreasury == null)
        {
            error = "Side A treasury is required.";
            return false;
        }

        if (sideBTreasury == null)
        {
            error = "Side B treasury is required.";
            return false;
        }

        if (sideATreasury == sideBTreasury)
        {
            error = "Side A and Side B must use different treasuries.";
            return false;
        }

        if (citizens == null)
        {
            error = "Configured citizens are required.";
            return false;
        }

        if (!TryBuildEconomicSnapshot(
                sideATreasury,
                "Side A",
                out ArenaAgentEconomicSnapshot sideA,
                out error) ||
            !TryBuildEconomicSnapshot(
                sideBTreasury,
                "Side B",
                out ArenaAgentEconomicSnapshot sideB,
                out error))
        {
            return false;
        }

        Dictionary<string, ArenaCitizenEmploymentSnapshot>
            citizenSnapshots =
                new Dictionary<string, ArenaCitizenEmploymentSnapshot>(
                    StringComparer.Ordinal);

        for (int i = 0; i < citizens.Length; i++)
        {
            CitizenEmployment citizen = citizens[i];

            if (citizen == null)
            {
                error = $"Configured citizen entry {i} is required.";
                return false;
            }

            string citizenId = citizen.gameObject.name;

            if (string.IsNullOrWhiteSpace(citizenId))
            {
                error = $"Configured citizen entry {i} requires a name.";
                return false;
            }

            AgentTreasury currentEmployer = citizen.CurrentEmployer;
            bool isEmployed = citizen.IsEmployed;
            int currentWage = citizen.CurrentWage;
            int reservationWage = citizen.ReservationWage;
            ArenaSide? currentEmployerSide = null;

            if (reservationWage < 0)
            {
                error = $"Citizen {citizenId} has an invalid reservation wage.";
                return false;
            }

            if (isEmployed != (currentEmployer != null))
            {
                error = $"Citizen {citizenId} has inconsistent employment state.";
                return false;
            }

            if (!isEmployed)
            {
                if (currentWage != 0)
                {
                    error =
                        $"Unemployed citizen {citizenId} must have wage zero.";
                    return false;
                }
            }
            else
            {
                if (currentWage <= 0)
                {
                    error =
                        $"Employed citizen {citizenId} requires a positive wage.";
                    return false;
                }

                if (currentEmployer == sideATreasury)
                {
                    currentEmployerSide = ArenaSide.A;
                }
                else if (currentEmployer == sideBTreasury)
                {
                    currentEmployerSide = ArenaSide.B;
                }
                else
                {
                    error =
                        $"Citizen {citizenId} has an unknown Arena employer.";
                    return false;
                }
            }

            ArenaCitizenEmploymentSnapshot citizenSnapshot =
                new ArenaCitizenEmploymentSnapshot(
                    citizenId,
                    isEmployed,
                    currentEmployerSide,
                    currentWage,
                    reservationWage);

            if (!citizenSnapshots.TryAdd(citizenId, citizenSnapshot))
            {
                error = $"Duplicate citizen ID: {citizenId}.";
                return false;
            }
        }

        snapshot = new ArenaRoundSnapshot(
            citizenSnapshots,
            sideA,
            sideB);
        error = null;
        return true;
    }

    private static bool TryBuildEconomicSnapshot(
        AgentTreasury treasury,
        string sideName,
        out ArenaAgentEconomicSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        float currentGold = treasury.CurrentGold;
        float currentPayrollPerHour = treasury.CurrentPayrollPerHour;
        float payrollCoverageHours = treasury.PayrollCoverageHours;

        if (!IsFiniteNonNegative(currentGold) ||
            !IsFiniteNonNegative(currentPayrollPerHour) ||
            !IsFinitePositive(payrollCoverageHours))
        {
            error = $"{sideName} treasury state is invalid.";
            return false;
        }

        snapshot = new ArenaAgentEconomicSnapshot(
            currentGold,
            currentPayrollPerHour,
            payrollCoverageHours);
        error = null;
        return true;
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
            $"payrollCoverageHours={Format(snapshot.PayrollCoverageHours)}");
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
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
