using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ArenaEmploymentOffer
{
    public string CitizenId { get; }
    public string WorkplaceId { get; }
    public int Wage { get; }

    internal ArenaEmploymentOffer(
        string citizenId,
        string workplaceId,
        int wage)
    {
        CitizenId = citizenId;
        WorkplaceId = workplaceId;
        Wage = wage;
    }
}

public sealed class ArenaAction
{
    public IReadOnlyList<ArenaEmploymentOffer> Offers { get; }
    public string StrategyNote { get; }

    internal ArenaAction(
        ArenaEmploymentOffer[] offers,
        string strategyNote)
    {
        Offers = Array.AsReadOnly(offers);
        StrategyNote = strategyNote;
    }
}

public static class ArenaActionParser
{
    public static bool TryParse(
        string actionJson,
        out ArenaAction action,
        out string error)
    {
        action = null;

        if (string.IsNullOrWhiteSpace(actionJson))
        {
            error = "Action JSON is required.";
            return false;
        }

        SerializedAction serializedAction;

        try
        {
            serializedAction = JsonUtility.FromJson<SerializedAction>(
                actionJson);
        }
        catch (Exception)
        {
            error = "Malformed action JSON.";
            return false;
        }

        if (serializedAction == null)
        {
            error = "Malformed action JSON.";
            return false;
        }

        if (serializedAction.offers == null)
        {
            error = "offers is required.";
            return false;
        }

        if (serializedAction.strategyNote == null)
        {
            error = "strategyNote is required.";
            return false;
        }

        ArenaEmploymentOffer[] offers =
            new ArenaEmploymentOffer[serializedAction.offers.Length];
        HashSet<string> citizenIds = new HashSet<string>(
            StringComparer.Ordinal);

        for (int i = 0; i < serializedAction.offers.Length; i++)
        {
            SerializedOffer serializedOffer = serializedAction.offers[i];

            if (serializedOffer == null)
            {
                error = $"Offer entry {i} cannot be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(serializedOffer.citizenId))
            {
                error = $"Offer entry {i} requires citizenId.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(serializedOffer.workplaceId))
            {
                error = $"Offer entry {i} requires workplaceId.";
                return false;
            }

            if (serializedOffer.wage <= 0)
            {
                error = $"Offer entry {i} wage must be greater than zero.";
                return false;
            }

            if (!citizenIds.Add(serializedOffer.citizenId))
            {
                error = $"Duplicate citizenId: {serializedOffer.citizenId}.";
                return false;
            }

            offers[i] = new ArenaEmploymentOffer(
                serializedOffer.citizenId,
                serializedOffer.workplaceId,
                serializedOffer.wage);
        }

        action = new ArenaAction(offers, serializedAction.strategyNote);
        error = null;
        return true;
    }

    [Serializable]
    private sealed class SerializedAction
    {
        public SerializedOffer[] offers;
        public string strategyNote;
    }

    [Serializable]
    private sealed class SerializedOffer
    {
        public string citizenId;
        public string workplaceId;
        public int wage;
    }
}
