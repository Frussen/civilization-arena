using System;
using System.Collections.Generic;

public static class ArenaActionValidator
{
    public static bool TryValidate(
        ArenaAction action,
        IReadOnlyCollection<string> allowedCitizenIds,
        IReadOnlyCollection<string> allowedWorkplaceIds,
        out string error)
    {
        if (action == null)
        {
            error = "Arena action is required.";
            return false;
        }

        if (allowedCitizenIds == null)
        {
            error = "Allowed citizen IDs are required.";
            return false;
        }

        if (allowedWorkplaceIds == null)
        {
            error = "Allowed Workplace IDs are required.";
            return false;
        }

        if (action.Offers == null)
        {
            error = "Arena action offers are required.";
            return false;
        }

        if (action.Offers.Count > allowedCitizenIds.Count)
        {
            error = "Offer count cannot exceed the allowed citizen count.";
            return false;
        }

        HashSet<string> citizens = new HashSet<string>(
            allowedCitizenIds,
            StringComparer.Ordinal);
        HashSet<string> workplaces = new HashSet<string>(
            allowedWorkplaceIds,
            StringComparer.Ordinal);
        HashSet<string> offeredCitizenIds = new HashSet<string>(
            StringComparer.Ordinal);

        for (int i = 0; i < action.Offers.Count; i++)
        {
            ArenaEmploymentOffer offer = action.Offers[i];

            if (offer == null)
            {
                error = $"Offer entry {i} cannot be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(offer.CitizenId) ||
                !citizens.Contains(offer.CitizenId))
            {
                error = $"Unknown citizenId: {offer.CitizenId ?? "null"}.";
                return false;
            }

            if (!offeredCitizenIds.Add(offer.CitizenId))
            {
                error = $"Duplicate citizenId: {offer.CitizenId}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(offer.WorkplaceId) ||
                !workplaces.Contains(offer.WorkplaceId))
            {
                error =
                    $"Unknown workplaceId: {offer.WorkplaceId ?? "null"}.";
                return false;
            }

            if (offer.Wage <= 0)
            {
                error = $"Offer entry {i} wage must be greater than zero.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
