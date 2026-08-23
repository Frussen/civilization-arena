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
        IReadOnlyList<ArenaEmploymentOffer> offers,
        string strategyNote)
    {
        ArenaEmploymentOffer[] offersCopy =
            new ArenaEmploymentOffer[offers.Count];

        for (int i = 0; i < offers.Count; i++)
        {
            offersCopy[i] = offers[i];
        }

        Offers = Array.AsReadOnly(offersCopy);
        StrategyNote = strategyNote;
    }
}

public static class ArenaActionFactory
{
    public static bool TryCreate(
        IReadOnlyList<ArenaEmploymentOffer> offers,
        string strategyNote,
        out ArenaAction action,
        out string error)
    {
        action = null;

        if (offers == null)
        {
            error = "offers is required.";
            return false;
        }

        if (strategyNote == null)
        {
            error = "strategyNote is required.";
            return false;
        }

        HashSet<string> citizenIds = new HashSet<string>(
            StringComparer.Ordinal);

        for (int i = 0; i < offers.Count; i++)
        {
            ArenaEmploymentOffer offer = offers[i];

            if (offer == null)
            {
                error = $"Offer entry {i} cannot be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(offer.CitizenId))
            {
                error = $"Offer entry {i} requires citizenId.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(offer.WorkplaceId))
            {
                error = $"Offer entry {i} requires workplaceId.";
                return false;
            }

            if (offer.Wage <= 0)
            {
                error = $"Offer entry {i} wage must be greater than zero.";
                return false;
            }

            if (!citizenIds.Add(offer.CitizenId))
            {
                error = $"Duplicate citizenId: {offer.CitizenId}.";
                return false;
            }
        }

        action = new ArenaAction(offers, strategyNote);
        error = null;
        return true;
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

        ArenaEmploymentOffer[] offers = null;

        if (serializedAction.offers != null)
        {
            offers = new ArenaEmploymentOffer[serializedAction.offers.Length];

            for (int i = 0; i < serializedAction.offers.Length; i++)
            {
                SerializedOffer serializedOffer = serializedAction.offers[i];

                if (serializedOffer != null)
                {
                    offers[i] = new ArenaEmploymentOffer(
                        serializedOffer.citizenId,
                        serializedOffer.workplaceId,
                        serializedOffer.wage);
                }
            }
        }

        return ArenaActionFactory.TryCreate(
            offers,
            serializedAction.strategyNote,
            out action,
            out error);
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
