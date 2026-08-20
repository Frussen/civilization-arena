using System;
using System.Collections.Generic;

public sealed class ArenaCitizenOfferPair
{
    public string CitizenId { get; }
    public ArenaEmploymentOffer OfferA { get; }
    public ArenaEmploymentOffer OfferB { get; }
    public bool HasOfferA => OfferA != null;
    public bool HasOfferB => OfferB != null;

    internal ArenaCitizenOfferPair(
        string citizenId,
        ArenaEmploymentOffer offerA,
        ArenaEmploymentOffer offerB)
    {
        CitizenId = citizenId;
        OfferA = offerA;
        OfferB = offerB;
    }
}

public static class ArenaOfferPairing
{
    public static bool TryBuild(
        ArenaAction actionA,
        ArenaAction actionB,
        out IReadOnlyList<ArenaCitizenOfferPair> pairs,
        out string error)
    {
        pairs = Array.Empty<ArenaCitizenOfferPair>();

        if (actionA == null)
        {
            error = "Side A action is required.";
            return false;
        }

        if (actionB == null)
        {
            error = "Side B action is required.";
            return false;
        }

        if (!TryIndexOffers(
            actionA,
            "Side A",
            out Dictionary<string, ArenaEmploymentOffer> offersA,
            out error))
        {
            return false;
        }

        if (!TryIndexOffers(
            actionB,
            "Side B",
            out Dictionary<string, ArenaEmploymentOffer> offersB,
            out error))
        {
            return false;
        }

        SortedSet<string> citizenIds = new SortedSet<string>(
            offersA.Keys,
            StringComparer.Ordinal);
        citizenIds.UnionWith(offersB.Keys);

        ArenaCitizenOfferPair[] result =
            new ArenaCitizenOfferPair[citizenIds.Count];
        int index = 0;

        foreach (string citizenId in citizenIds)
        {
            offersA.TryGetValue(citizenId, out ArenaEmploymentOffer offerA);
            offersB.TryGetValue(citizenId, out ArenaEmploymentOffer offerB);

            result[index] = new ArenaCitizenOfferPair(
                citizenId,
                offerA,
                offerB);
            index++;
        }

        pairs = Array.AsReadOnly(result);
        error = null;
        return true;
    }

    private static bool TryIndexOffers(
        ArenaAction action,
        string sideName,
        out Dictionary<string, ArenaEmploymentOffer> offersByCitizen,
        out string error)
    {
        offersByCitizen = new Dictionary<string, ArenaEmploymentOffer>(
            StringComparer.Ordinal);

        for (int i = 0; i < action.Offers.Count; i++)
        {
            ArenaEmploymentOffer offer = action.Offers[i];

            if (offer == null || string.IsNullOrWhiteSpace(offer.CitizenId))
            {
                error = $"{sideName} offer entry {i} is invalid.";
                return false;
            }

            if (!offersByCitizen.TryAdd(offer.CitizenId, offer))
            {
                error =
                    $"{sideName} contains duplicate citizenId: " +
                    $"{offer.CitizenId}.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
