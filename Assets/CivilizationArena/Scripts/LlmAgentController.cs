using System;
using UnityEngine;
using UnityEngine.Serialization;

public class LlmAgentController : MonoBehaviour
{
    [SerializeField] private AgentDecisionScheduler decisionScheduler;
    [SerializeField] private AgentTextInterface textInterface;
    [SerializeField] private AgentOfferExecutor offerExecutor;
    [SerializeField] private MatchController matchController;
    [SerializeField] private LlmProviderBehaviour provider;

    [SerializeField] private bool requestInFlight;
    [SerializeField] private int lastDecisionNumberRequested;
    [TextArea(8, 20)]
    [SerializeField] private string latestModelAction;
    [FormerlySerializedAs("latestApiStatus")]
    [TextArea(2, 5)]
    [SerializeField] private string latestRequestStatus;
    [FormerlySerializedAs("latestApiError")]
    [TextArea(2, 8)]
    [SerializeField] private string latestRequestError;

    private int requestVersion;

    public bool RequestInFlight => requestInFlight;
    public int LastDecisionNumberRequested => lastDecisionNumberRequested;
    public string LatestModelAction => latestModelAction;
    public string LatestRequestStatus => latestRequestStatus;
    public string LatestRequestError => latestRequestError;
    private ILlmProvider Provider => provider;

    private void OnEnable()
    {
        if (decisionScheduler != null)
        {
            decisionScheduler.DecisionRequested += HandleDecisionRequested;
        }
    }

    private void OnDisable()
    {
        if (decisionScheduler != null)
        {
            decisionScheduler.DecisionRequested -= HandleDecisionRequested;
        }

        if (requestInFlight)
        {
            CancelActiveRequest(
                "LLM request cancelled because the controller was disabled.");
        }
    }

    private void Update()
    {
        if (requestInFlight && !IsApiMode)
        {
            CancelActiveRequest(
                "LLM request discarded because control mode is Manual.");
        }
    }

    private void HandleDecisionRequested(
        int decisionNumber,
        string observation)
    {
        TryStartRequest(decisionNumber, observation);
    }

    [ContextMenu("Retry Current Decision")]
    private void RetryCurrentDecision()
    {
        if (!IsApiMode)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            ReportFailure(
                "Retry Current Decision is available only during Play Mode.");
            return;
        }

        if (decisionScheduler == null || !decisionScheduler.AwaitingAction)
        {
            ReportFailure("There is no decision currently awaiting an action.");
            return;
        }

        if (requestInFlight)
        {
            latestRequestError = "An LLM request is already in flight.";
            Debug.LogWarning(latestRequestError, this);
            return;
        }

        if (matchController == null || matchController.IsEnded)
        {
            ReportFailure(
                "The match has ended or MatchController is not configured.");
            return;
        }

        if (textInterface == null ||
            string.IsNullOrWhiteSpace(textInterface.LatestObservation))
        {
            ReportFailure("No current observation is available for retry.");
            return;
        }

        TryStartRequest(
            decisionScheduler.DecisionsRequested,
            textInterface.LatestObservation);
    }

    private bool TryStartRequest(int decisionNumber, string observation)
    {
        if (!IsApiMode || requestInFlight)
        {
            return false;
        }

        lastDecisionNumberRequested = decisionNumber;
        latestModelAction = string.Empty;
        latestRequestError = string.Empty;

        if (matchController == null || matchController.IsEnded)
        {
            ReportFailure(
                "The match has ended or MatchController is not configured.");
            return false;
        }

        if (textInterface == null)
        {
            ReportFailure("AgentTextInterface is not configured.");
            return false;
        }

        if (offerExecutor == null)
        {
            ReportFailure("AgentOfferExecutor is not configured.");
            return false;
        }

        if (provider == null || !provider.isActiveAndEnabled)
        {
            ReportFailure("An active LLM provider is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(observation))
        {
            ReportFailure("The scheduled observation is empty.");
            return false;
        }

        if (!textInterface.TryGetOfferConfiguration(
            out string[] citizenIds,
            out _,
            out string[] workplaceIds,
            out _,
            out string error))
        {
            ReportFailure(error);
            return false;
        }

        requestInFlight = true;
        int currentRequestVersion = ++requestVersion;
        latestRequestStatus =
            $"Decision {decisionNumber}: LLM request in flight.";

        try
        {
            Provider.RequestAction(
                observation,
                citizenIds,
                workplaceIds,
                result => HandleProviderResult(
                    currentRequestVersion,
                    decisionNumber,
                    result));
        }
        catch (Exception exception)
        {
            if (currentRequestVersion == requestVersion)
            {
                ReportFailure(
                    $"Decision {decisionNumber}: provider request failed: " +
                    exception.Message);
            }

            return false;
        }

        return true;
    }

    private void HandleProviderResult(
        int completedRequestVersion,
        int decisionNumber,
        LlmProviderResult result)
    {
        if (completedRequestVersion != requestVersion)
        {
            return;
        }

        requestInFlight = false;

        if (!IsApiMode)
        {
            latestRequestStatus =
                "LLM result discarded because control mode is Manual.";
            latestRequestError = string.Empty;
            return;
        }

        if (matchController == null || matchController.IsEnded)
        {
            ReportFailure(
                $"Decision {decisionNumber}: result discarded because the match ended.");
            return;
        }

        if (decisionScheduler == null ||
            !decisionScheduler.AwaitingAction ||
            decisionScheduler.DecisionsRequested != decisionNumber)
        {
            ReportFailure(
                $"Decision {decisionNumber}: result discarded because it is stale.");
            return;
        }

        if (result == null)
        {
            ReportFailure(
                $"Decision {decisionNumber}: provider returned no result.");
            return;
        }

        if (!result.Success)
        {
            string detail = string.IsNullOrWhiteSpace(result.Error)
                ? "provider request failed"
                : result.Error;

            ReportFailure($"Decision {decisionNumber}: {detail}");
            return;
        }

        if (string.IsNullOrWhiteSpace(result.ActionJson))
        {
            ReportFailure(
                $"Decision {decisionNumber}: provider returned no action JSON.");
            return;
        }

        latestModelAction = result.ActionJson;

        if (!offerExecutor.TryExecuteActionJson(result.ActionJson))
        {
            ReportFailure(
                $"Decision {decisionNumber}: model action rejected. " +
                offerExecutor.LatestExecutionResult);
            return;
        }

        latestRequestError = string.Empty;
        latestRequestStatus =
            $"Decision {decisionNumber}: action executed.";
    }

    private void CancelActiveRequest(string status)
    {
        requestVersion++;

        if (provider != null)
        {
            Provider.CancelRequest();
        }

        requestInFlight = false;
        latestRequestStatus = status;
        latestRequestError = string.Empty;
    }

    private void ReportFailure(string message)
    {
        requestInFlight = false;
        latestRequestStatus = "LLM decision failed.";
        latestRequestError = message;
        Debug.LogError(message, this);
    }

    private bool IsApiMode =>
        decisionScheduler != null &&
        decisionScheduler.ControlMode == AgentControlMode.Api;
}
