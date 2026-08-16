using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAiAgentController : MonoBehaviour
{
    private const string ResponsesEndpoint =
        "https://api.openai.com/v1/responses";
    private const int RequestTimeoutSeconds = 180;

    [SerializeField] private AgentDecisionScheduler decisionScheduler;
    [SerializeField] private AgentTextInterface textInterface;
    [SerializeField] private MatchController matchController;

    [SerializeField] private string model = "gpt-5.6";
    [SerializeField] private string apiKeyEnvironmentVariable =
        "OPENAI_API_KEY";
    [TextArea(7, 15)]
    [SerializeField] private string systemInstructions =
        "You are the strategic controller of an agent in Civilization Arena.\n" +
        "Your objective is to complete your Wonder successfully.\n" +
        "You receive authoritative textual observations of the simulation.\n" +
        "Choose only the strategic controls allowed by the provided action schema.\n" +
        "Plan economically, account for wages, resource production, worker shifts, " +
        "reservation wages, and Wonder construction.\n" +
        "Maximum Offer Wage is a hard ceiling, not the wage automatically paid " +
        "to everyone.\n" +
        "Unemployed citizens are hired at their reservation wage when it is " +
        "within that ceiling.\n" +
        "Reassigning an already-employed citizen requires an offer of at least " +
        "current wage + 1.\n" +
        "A desired allocation can remain unapplied when Maximum Offer Wage is " +
        "too low.\n" +
        "Inspect actual allocations in later observations to verify whether " +
        "requested changes were executed.\n" +
        "Do not invent actions that are not available.\n" +
        "Return the strategic action required by the supplied schema.\n" +
        "strategyNote must contain only a short high-level strategy summary.";

    [SerializeField] private bool requestInFlight;
    [SerializeField] private int lastDecisionNumberRequested;
    [TextArea(8, 20)]
    [SerializeField] private string latestModelAction;
    [TextArea(2, 5)]
    [SerializeField] private string latestApiStatus;
    [TextArea(2, 8)]
    [SerializeField] private string latestApiError;

    public bool RequestInFlight => requestInFlight;
    public int LastDecisionNumberRequested => lastDecisionNumberRequested;
    public string LatestModelAction => latestModelAction;
    public string LatestApiStatus => latestApiStatus;
    public string LatestApiError => latestApiError;

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
            StopAllCoroutines();
            requestInFlight = false;
            latestApiStatus =
                "OpenAI request cancelled because the controller was disabled.";
        }
    }

    private void Update()
    {
        if (requestInFlight && !IsApiMode)
        {
            StopAllCoroutines();
            requestInFlight = false;
            latestApiStatus =
                "OpenAI request discarded because control mode is Manual.";
            latestApiError = string.Empty;
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
            ReportFailure("Retry Current Decision is available only during Play Mode.");
            return;
        }

        if (decisionScheduler == null || !decisionScheduler.AwaitingAction)
        {
            ReportFailure("There is no decision currently awaiting an action.");
            return;
        }

        if (requestInFlight)
        {
            latestApiError = "An OpenAI request is already in flight.";
            Debug.LogWarning(latestApiError, this);
            return;
        }

        if (matchController == null || matchController.IsEnded)
        {
            ReportFailure("The match has ended or MatchController is not configured.");
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
        latestApiError = string.Empty;

        if (matchController == null || matchController.IsEnded)
        {
            ReportFailure("The match has ended or MatchController is not configured.");
            return false;
        }

        if (textInterface == null)
        {
            ReportFailure("AgentTextInterface is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(observation))
        {
            ReportFailure("The scheduled observation is empty.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            ReportFailure("The OpenAI model is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable))
        {
            ReportFailure("The API key environment variable name is empty.");
            return false;
        }

        string apiKey = Environment.GetEnvironmentVariable(
            apiKeyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ReportFailure(
                $"API key environment variable '{apiKeyEnvironmentVariable}' is not set.");
            return false;
        }

        if (!TryGetWorkplaceIds(out string[] workplaceIds, out string error))
        {
            ReportFailure(error);
            return false;
        }

        OpenAiResponsesRequest requestBody = BuildRequest(
            observation,
            workplaceIds);

        string requestJson = JsonUtility.ToJson(requestBody);

        requestInFlight = true;
        latestApiStatus =
            $"Decision {decisionNumber}: OpenAI request in flight.";

        StartCoroutine(SendRequest(
            decisionNumber,
            apiKey,
            requestJson));

        return true;
    }

    private IEnumerator SendRequest(
        int decisionNumber,
        string apiKey,
        string requestJson)
    {
        using (UnityWebRequest request = new UnityWebRequest(
            ResponsesEndpoint,
            UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(requestJson));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = RequestTimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();

            if (!IsApiMode)
            {
                requestInFlight = false;
                latestApiStatus =
                    "OpenAI response discarded because control mode is Manual.";
                latestApiError = string.Empty;
                yield break;
            }

            if (matchController == null || matchController.IsEnded)
            {
                ReportFailure(
                    $"Decision {decisionNumber}: response discarded because the match ended.");
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string apiError = GetApiErrorMessage(
                    request.downloadHandler?.text);

                string detail = !string.IsNullOrWhiteSpace(apiError)
                    ? apiError
                    : request.error;

                ReportFailure(
                    $"Decision {decisionNumber}: HTTP {request.responseCode} " +
                    $"request failed: {detail}");
                yield break;
            }

            HandleSuccessfulHttpResponse(
                decisionNumber,
                request.downloadHandler?.text);
        }
    }

    private void HandleSuccessfulHttpResponse(
        int decisionNumber,
        string responseJson)
    {
        OpenAiResponsesResponse response;

        try
        {
            response = JsonUtility.FromJson<OpenAiResponsesResponse>(
                responseJson);
        }
        catch (Exception)
        {
            ReportFailure(
                $"Decision {decisionNumber}: malformed OpenAI response.");
            return;
        }

        if (response == null)
        {
            ReportFailure(
                $"Decision {decisionNumber}: empty OpenAI response.");
            return;
        }

        if (!string.Equals(
            response.status,
            "completed",
            StringComparison.Ordinal))
        {
            string detail = response.error != null &&
                !string.IsNullOrWhiteSpace(response.error.message)
                ? response.error.message
                : $"response status was '{response.status ?? "missing"}'";

            ReportFailure($"Decision {decisionNumber}: {detail}.");
            return;
        }

        string refusal = FindRefusal(response);
        if (!string.IsNullOrWhiteSpace(refusal))
        {
            ReportFailure(
                $"Decision {decisionNumber}: model refused the request: {refusal}");
            return;
        }

        string actionJson = FindOutputText(response);
        if (string.IsNullOrWhiteSpace(actionJson))
        {
            ReportFailure(
                $"Decision {decisionNumber}: completed response had no output_text.");
            return;
        }

        latestModelAction = actionJson;

        if (!IsApiMode)
        {
            requestInFlight = false;
            latestApiStatus =
                "OpenAI action discarded because control mode is Manual.";
            latestApiError = string.Empty;
            return;
        }

        if (matchController == null || matchController.IsEnded)
        {
            ReportFailure(
                $"Decision {decisionNumber}: action discarded because the match ended.");
            return;
        }

        if (!textInterface.TryApplyActionJson(actionJson))
        {
            ReportFailure(
                $"Decision {decisionNumber}: model action rejected. " +
                textInterface.LatestActionResult);
            return;
        }

        requestInFlight = false;
        latestApiError = string.Empty;
        latestApiStatus =
            $"Decision {decisionNumber}: action accepted.";
    }

    private bool TryGetWorkplaceIds(
        out string[] workplaceIds,
        out string error)
    {
        workplaceIds = textInterface.GetConfiguredWorkplaceIds();

        if (workplaceIds.Length == 0)
        {
            error = "No workplace IDs are configured in AgentTextInterface.";
            return false;
        }

        HashSet<string> uniqueIds = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (string workplaceId in workplaceIds)
        {
            if (string.IsNullOrWhiteSpace(workplaceId) ||
                !uniqueIds.Add(workplaceId))
            {
                error =
                    "AgentTextInterface workplace IDs must be non-empty and unique.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private OpenAiResponsesRequest BuildRequest(
        string observation,
        string[] workplaceIds)
    {
        AllocationObjectSchema allocationItem = new AllocationObjectSchema
        {
            type = "object",
            properties = new AllocationSchemaProperties
            {
                workplaceId = new EnumStringSchema
                {
                    type = "string",
                    @enum = workplaceIds
                },
                desiredWorkers = new IntegerSchema
                {
                    type = "integer",
                    minimum = 0
                }
            },
            required = new[] { "workplaceId", "desiredWorkers" },
            additionalProperties = false
        };

        ActionObjectSchema actionSchema = new ActionObjectSchema
        {
            type = "object",
            properties = new ActionSchemaProperties
            {
                maximumOfferWage = new IntegerSchema
                {
                    type = "integer",
                    minimum = 1
                },
                allocations = new AllocationArraySchema
                {
                    type = "array",
                    items = allocationItem,
                    minItems = workplaceIds.Length,
                    maxItems = workplaceIds.Length
                },
                strategyNote = new StringSchema
                {
                    type = "string"
                }
            },
            required = new[]
            {
                "maximumOfferWage",
                "allocations",
                "strategyNote"
            },
            additionalProperties = false
        };

        return new OpenAiResponsesRequest
        {
            model = model,
            instructions = systemInstructions,
            input = observation,
            text = new ResponseTextConfiguration
            {
                format = new ResponseTextFormat
                {
                    type = "json_schema",
                    name = "civilization_arena_action",
                    strict = true,
                    schema = actionSchema
                }
            }
        };
    }

    private static string FindRefusal(OpenAiResponsesResponse response)
    {
        if (response.output == null)
        {
            return null;
        }

        foreach (OpenAiOutputItem output in response.output)
        {
            if (output?.content == null)
            {
                continue;
            }

            foreach (OpenAiContentItem content in output.content)
            {
                if (content != null &&
                    content.type == "refusal" &&
                    !string.IsNullOrWhiteSpace(content.refusal))
                {
                    return content.refusal;
                }
            }
        }

        return null;
    }

    private static string FindOutputText(OpenAiResponsesResponse response)
    {
        if (response.output == null)
        {
            return null;
        }

        foreach (OpenAiOutputItem output in response.output)
        {
            if (output == null ||
                output.type != "message" ||
                output.role != "assistant" ||
                output.content == null)
            {
                continue;
            }

            foreach (OpenAiContentItem content in output.content)
            {
                if (content != null &&
                    content.type == "output_text" &&
                    !string.IsNullOrWhiteSpace(content.text))
                {
                    return content.text;
                }
            }
        }

        return null;
    }

    private static string GetApiErrorMessage(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return null;
        }

        try
        {
            OpenAiErrorEnvelope envelope =
                JsonUtility.FromJson<OpenAiErrorEnvelope>(responseJson);

            return envelope?.error?.message;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ReportFailure(string message)
    {
        requestInFlight = false;
        latestApiStatus = "OpenAI decision failed.";
        latestApiError = message;
        Debug.LogError(message, this);
    }

    private bool IsApiMode =>
        decisionScheduler != null &&
        decisionScheduler.ControlMode == AgentControlMode.Api;

    [Serializable]
    private class OpenAiResponsesRequest
    {
        public string model;
        public string instructions;
        public string input;
        public ResponseTextConfiguration text;
    }

    [Serializable]
    private class ResponseTextConfiguration
    {
        public ResponseTextFormat format;
    }

    [Serializable]
    private class ResponseTextFormat
    {
        public string type;
        public string name;
        public bool strict;
        public ActionObjectSchema schema;
    }

    [Serializable]
    private class ActionObjectSchema
    {
        public string type;
        public ActionSchemaProperties properties;
        public string[] required;
        public bool additionalProperties;
    }

    [Serializable]
    private class ActionSchemaProperties
    {
        public IntegerSchema maximumOfferWage;
        public AllocationArraySchema allocations;
        public StringSchema strategyNote;
    }

    [Serializable]
    private class AllocationArraySchema
    {
        public string type;
        public AllocationObjectSchema items;
        public int minItems;
        public int maxItems;
    }

    [Serializable]
    private class AllocationObjectSchema
    {
        public string type;
        public AllocationSchemaProperties properties;
        public string[] required;
        public bool additionalProperties;
    }

    [Serializable]
    private class AllocationSchemaProperties
    {
        public EnumStringSchema workplaceId;
        public IntegerSchema desiredWorkers;
    }

    [Serializable]
    private class IntegerSchema
    {
        public string type;
        public int minimum;
    }

    [Serializable]
    private class StringSchema
    {
        public string type;
    }

    [Serializable]
    private class EnumStringSchema
    {
        public string type;
        public string[] @enum;
    }

    [Serializable]
    private class OpenAiResponsesResponse
    {
        public string status;
        public OpenAiOutputItem[] output;
        public OpenAiError error;
    }

    [Serializable]
    private class OpenAiOutputItem
    {
        public string type;
        public string role;
        public OpenAiContentItem[] content;
    }

    [Serializable]
    private class OpenAiContentItem
    {
        public string type;
        public string text;
        public string refusal;
    }

    [Serializable]
    private class OpenAiErrorEnvelope
    {
        public OpenAiError error;
    }

    [Serializable]
    private class OpenAiError
    {
        public string message;
    }
}
