using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class AgentEmploymentOffer
{
    public string citizenId;
    public string workplaceId;
    public int wage;
}

[Serializable]
public class AgentEmploymentOfferAction
{
    public AgentEmploymentOffer[] offers;
    public string strategyNote;
}

public class AgentOfferExecutor : MonoBehaviour
{
    [SerializeField] private AgentTreasury employer;
    [SerializeField] private AgentTextInterface textInterface;
    [SerializeField] private AgentDecisionScheduler decisionScheduler;

    [TextArea(8, 20)]
    [SerializeField] private string latestActionJson;
    [TextArea(4, 20)]
    [SerializeField] private string latestExecutionResult;

    public string LatestActionJson => latestActionJson;
    public string LatestExecutionResult => latestExecutionResult;

    [ContextMenu("Execute Action JSON")]
    private void ExecuteActionJsonFromContextMenu()
    {
        TryExecuteActionJson(latestActionJson);
    }

    public bool TryExecuteActionJson(string json)
    {
        latestActionJson = json;

        AgentEmploymentOfferAction action;

        try
        {
            action = JsonUtility.FromJson<AgentEmploymentOfferAction>(json);
        }
        catch (Exception)
        {
            RejectAction("Malformed JSON.");
            return false;
        }

        if (!TryValidateAction(
            action,
            out CitizenEmployment[] offerCitizens,
            out Workplace[] offerWorkplaces,
            out string error))
        {
            RejectAction(error);
            return false;
        }

        StringBuilder result = new StringBuilder();
        result.AppendLine("API_OFFER_RESULT");

        if (action.offers.Length == 0)
        {
            result.Append("offers=0");
        }
        else
        {
            for (int i = 0; i < action.offers.Length; i++)
            {
                AgentEmploymentOffer offer = action.offers[i];
                bool accepted = offerCitizens[i].TryAcceptOffer(
                    employer,
                    offerWorkplaces[i],
                    offer.wage);

                result.Append(
                    $"{offer.citizenId} -> {offer.workplaceId} " +
                    $"@{offer.wage}: {(accepted ? "accepted" : "rejected")}");

                if (i < action.offers.Length - 1)
                {
                    result.AppendLine();
                }
            }
        }

        latestExecutionResult = result.ToString();
        Debug.Log(latestExecutionResult, this);

        if (!decisionScheduler.TryCompletePendingDecision())
        {
            latestExecutionResult +=
                "\nDecision execution completed, but the scheduler could not resume.";
            Debug.LogError(latestExecutionResult, this);
            return false;
        }

        return true;
    }

    private bool TryValidateAction(
        AgentEmploymentOfferAction action,
        out CitizenEmployment[] offerCitizens,
        out Workplace[] offerWorkplaces,
        out string error)
    {
        offerCitizens = Array.Empty<CitizenEmployment>();
        offerWorkplaces = Array.Empty<Workplace>();

        if (!Application.isPlaying)
        {
            error = "API employment offers are available only during Play Mode.";
            return false;
        }

        if (employer == null ||
            textInterface == null ||
            decisionScheduler == null)
        {
            error = "AgentOfferExecutor references are not fully configured.";
            return false;
        }

        if (textInterface.Treasury != employer)
        {
            error =
                "AgentOfferExecutor and AgentTextInterface must use the same employer.";
            return false;
        }

        if (decisionScheduler.ControlMode != AgentControlMode.Api)
        {
            error = "Employment offer actions are available only in Api mode.";
            return false;
        }

        if (decisionScheduler.IsMatchEnded)
        {
            error = "Employment offers cannot execute after the match has ended.";
            return false;
        }

        if (!decisionScheduler.AwaitingAction)
        {
            error = "No API decision is currently awaiting an action.";
            return false;
        }

        if (!textInterface.TryGetOfferConfiguration(
            out string[] citizenIds,
            out CitizenEmployment[] configuredCitizens,
            out string[] workplaceIds,
            out Workplace[] configuredWorkplaces,
            out error))
        {
            return false;
        }

        if (action == null)
        {
            error = "Malformed JSON.";
            return false;
        }

        if (action.offers == null)
        {
            error = "offers is required.";
            return false;
        }

        if (action.strategyNote == null)
        {
            error = "strategyNote is required.";
            return false;
        }

        Dictionary<string, CitizenEmployment> citizensById =
            new Dictionary<string, CitizenEmployment>(StringComparer.Ordinal);
        Dictionary<string, Workplace> workplacesById =
            new Dictionary<string, Workplace>(StringComparer.Ordinal);

        for (int i = 0; i < citizenIds.Length; i++)
        {
            citizensById.Add(citizenIds[i], configuredCitizens[i]);
        }

        for (int i = 0; i < workplaceIds.Length; i++)
        {
            workplacesById.Add(workplaceIds[i], configuredWorkplaces[i]);
        }

        offerCitizens = new CitizenEmployment[action.offers.Length];
        offerWorkplaces = new Workplace[action.offers.Length];
        HashSet<string> offeredCitizenIds = new HashSet<string>(
            StringComparer.Ordinal);

        for (int i = 0; i < action.offers.Length; i++)
        {
            AgentEmploymentOffer offer = action.offers[i];

            if (offer == null)
            {
                error = $"Offer entry {i} cannot be null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(offer.citizenId))
            {
                error = $"Offer entry {i} requires citizenId.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(offer.workplaceId))
            {
                error = $"Offer entry {i} requires workplaceId.";
                return false;
            }

            if (offer.wage <= 0)
            {
                error = $"Offer entry {i} wage must be greater than zero.";
                return false;
            }

            if (!citizensById.TryGetValue(
                offer.citizenId,
                out offerCitizens[i]))
            {
                error = $"Unknown citizenId: {offer.citizenId}.";
                return false;
            }

            if (!workplacesById.TryGetValue(
                offer.workplaceId,
                out offerWorkplaces[i]))
            {
                error = $"Unknown workplaceId: {offer.workplaceId}.";
                return false;
            }

            if (!offeredCitizenIds.Add(offer.citizenId))
            {
                error = $"Duplicate citizenId: {offer.citizenId}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private void RejectAction(string reason)
    {
        latestExecutionResult = $"Rejected: {reason}";
        Debug.LogWarning(latestExecutionResult, this);
    }
}
