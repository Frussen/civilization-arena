using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAiLlmProvider : LlmProviderBehaviour
{
    public const string DefaultModel = "gpt-5.6";
    public const string DefaultLocalBaseUrl =
        "http://localhost:1234/v1";

    private const string ResponsesEndpoint =
        "https://api.openai.com/v1/responses";
    private const int RequestTimeoutSeconds = 180;

    [SerializeField] private string model = DefaultModel;
    [SerializeField] private string apiKeyEnvironmentVariable =
        "OPENAI_API_KEY";
    [TextArea(7, 15)]
    [SerializeField] private string systemInstructions =
        "You are the strategic controller of an agent in Civilization Arena.\n" +
        "Your objective is to complete your Wonder successfully.\n" +
        "You receive authoritative textual observations of the simulation.\n" +
        "Make explicit employment offers to individual citizens. Each offer " +
        "specifies citizen, Workplace, and wage.\n" +
        "You may make zero or more offers per decision. Citizens decide whether " +
        "to accept according to the simulation rules.\n" +
        "An employed citizen requires a strictly higher wage for a new offer. " +
        "An unemployed citizen requires at least their reservation wage.\n" +
        "Every new or renegotiated contract must satisfy the employer's payroll " +
        "coverage requirement.\n" +
        "Offers are resolved in ordinal CitizenId order, and rejection " +
        "is a possible gameplay outcome.\n" +
        "Consider current time, work shift, activity, wage, reservation wage, " +
        "current employer and Workplace, resources, payroll, and Wonder " +
        "requirements.\n" +
        "Do not invent actions that are not available.\n" +
        "Return the strategic action required by the supplied schema.\n" +
        "strategyNote must contain only a short high-level strategy summary.";

    private Coroutine activeRequest;
    private int requestVersion;
    private string runtimeApiKey;
    private RuntimeProviderMode runtimeProviderMode =
        RuntimeProviderMode.OpenAICloud;
    private string runtimeResponsesEndpoint = ResponsesEndpoint;

    public override string ModelLabel => model;

    public bool TryConfigureRuntime(
        string configuredModel,
        string configuredApiKey,
        out string error)
    {
        if (!Application.isPlaying)
        {
            error =
                "OpenAI runtime configuration is available only in Play Mode.";
            return false;
        }

        if (activeRequest != null)
        {
            error =
                "OpenAI runtime configuration cannot change during a request.";
            return false;
        }

        string normalizedModel = configuredModel?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            error = "The OpenAI model must not be blank.";
            return false;
        }

        model = normalizedModel;
        runtimeApiKey = string.IsNullOrWhiteSpace(configuredApiKey)
            ? null
            : configuredApiKey;
        runtimeProviderMode = RuntimeProviderMode.OpenAICloud;
        runtimeResponsesEndpoint = ResponsesEndpoint;
        error = null;
        return true;
    }

    public bool TryConfigureLocalRuntime(
        string configuredModel,
        string configuredBaseUrl,
        out string error)
    {
        if (!Application.isPlaying)
        {
            error =
                "Local AI runtime configuration is available only in Play " +
                "Mode.";
            return false;
        }

        if (activeRequest != null)
        {
            error =
                "Local AI runtime configuration cannot change during a " +
                "request.";
            return false;
        }

        string normalizedModel = configuredModel?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            error = "The local model must not be blank.";
            return false;
        }

        if (!TryBuildLocalResponsesEndpoint(
                configuredBaseUrl,
                out string responsesEndpoint))
        {
            error =
                "The local endpoint must be an absolute HTTP or HTTPS URL.";
            return false;
        }

        model = normalizedModel;
        runtimeApiKey = null;
        runtimeProviderMode = RuntimeProviderMode.LocalOpenAICompatible;
        runtimeResponsesEndpoint = responsesEndpoint;
        error = null;
        return true;
    }

    public override void RequestAction(
        string observation,
        string[] citizenIds,
        string[] workplaceIds,
        Action<LlmProviderResult> onCompleted)
    {
        if (onCompleted == null)
        {
            throw new ArgumentNullException(nameof(onCompleted));
        }

        if (activeRequest != null)
        {
            onCompleted(LlmProviderResult.Failed(
                "An OpenAI request is already in flight."));
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            onCompleted(LlmProviderResult.Failed(
                "The OpenAI model is not configured."));
            return;
        }

        string apiKey = null;
        if (runtimeProviderMode == RuntimeProviderMode.OpenAICloud)
        {
            apiKey = runtimeApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable))
                {
                    onCompleted(LlmProviderResult.Failed(
                        "The API key environment variable name is empty."));
                    return;
                }

                apiKey = Environment.GetEnvironmentVariable(
                    apiKeyEnvironmentVariable);

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    onCompleted(LlmProviderResult.Failed(
                        $"API key environment variable " +
                        $"'{apiKeyEnvironmentVariable}' is not set."));
                    return;
                }
            }
        }

        ActionObjectSchema actionSchema = BuildActionSchema(
            citizenIds,
            workplaceIds);
        string requestJson = runtimeProviderMode ==
            RuntimeProviderMode.LocalOpenAICompatible
            ? JsonUtility.ToJson(BuildLocalChatCompletionsRequest(
                observation,
                actionSchema))
            : JsonUtility.ToJson(BuildRequest(
                observation,
                actionSchema));
        int currentRequestVersion = ++requestVersion;
        activeRequest = StartCoroutine(SendRequest(
            currentRequestVersion,
            runtimeResponsesEndpoint,
            runtimeProviderMode,
            apiKey,
            requestJson,
            onCompleted));
    }

    public override void CancelRequest()
    {
        requestVersion++;

        if (activeRequest != null)
        {
            StopCoroutine(activeRequest);
            activeRequest = null;
        }
    }

    private void OnDisable()
    {
        CancelRequest();
        runtimeApiKey = null;
        runtimeProviderMode = RuntimeProviderMode.OpenAICloud;
        runtimeResponsesEndpoint = ResponsesEndpoint;
    }

    private IEnumerator SendRequest(
        int currentRequestVersion,
        string responsesEndpoint,
        RuntimeProviderMode providerMode,
        string apiKey,
        string requestJson,
        Action<LlmProviderResult> onCompleted)
    {
        using (UnityWebRequest request = new UnityWebRequest(
            responsesEndpoint,
            UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(requestJson));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = RequestTimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            if (providerMode == RuntimeProviderMode.OpenAICloud &&
                !string.IsNullOrWhiteSpace(apiKey))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            }

            yield return request.SendWebRequest();

            if (currentRequestVersion != requestVersion)
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string apiError = GetApiErrorMessage(
                    request.downloadHandler?.text);

                string detail = !string.IsNullOrWhiteSpace(apiError)
                    ? apiError
                    : request.error;

                CompleteRequest(
                    currentRequestVersion,
                    onCompleted,
                    LlmProviderResult.Failed(
                        $"HTTP {request.responseCode} request failed: {detail}"));
                yield break;
            }

            CompleteRequest(
                currentRequestVersion,
                onCompleted,
                providerMode == RuntimeProviderMode.LocalOpenAICompatible
                    ? ParseLocalChatCompletionsResponse(
                        request.downloadHandler?.text)
                    : ParseResponse(request.downloadHandler?.text));
        }
    }

    internal static bool TryBuildLocalResponsesEndpoint(
        string configuredBaseUrl,
        out string responsesEndpoint)
    {
        responsesEndpoint = null;
        string normalizedBaseUrl = configuredBaseUrl?.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(normalizedBaseUrl) ||
            !Uri.TryCreate(
                normalizedBaseUrl,
                UriKind.Absolute,
                out Uri baseUri) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment) ||
            (!string.Equals(
                baseUri.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(
                baseUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        responsesEndpoint = baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/') +
            "/chat/completions";
        return true;
    }

    private void CompleteRequest(
        int completedRequestVersion,
        Action<LlmProviderResult> onCompleted,
        LlmProviderResult result)
    {
        if (completedRequestVersion != requestVersion)
        {
            return;
        }

        activeRequest = null;
        onCompleted(result);
    }

    private static LlmProviderResult ParseResponse(string responseJson)
    {
        OpenAiResponsesResponse response;

        try
        {
            response = JsonUtility.FromJson<OpenAiResponsesResponse>(
                responseJson);
        }
        catch (Exception)
        {
            return LlmProviderResult.Failed("Malformed OpenAI response.");
        }

        if (response == null)
        {
            return LlmProviderResult.Failed("Empty OpenAI response.");
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

            return LlmProviderResult.Failed(detail + ".");
        }

        string refusal = FindRefusal(response);
        if (!string.IsNullOrWhiteSpace(refusal))
        {
            return LlmProviderResult.Failed(
                $"Model refused the request: {refusal}");
        }

        string actionJson = FindOutputText(response);
        if (string.IsNullOrWhiteSpace(actionJson))
        {
            return LlmProviderResult.Failed(
                "Completed response had no output_text.");
        }

        return LlmProviderResult.Succeeded(actionJson);
    }

    private OpenAiResponsesRequest BuildRequest(
        string observation,
        ActionObjectSchema actionSchema)
    {
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

    private LocalChatCompletionsRequest BuildLocalChatCompletionsRequest(
        string observation,
        ActionObjectSchema actionSchema)
    {
        return new LocalChatCompletionsRequest
        {
            model = model,
            messages = new[]
            {
                new ChatMessage
                {
                    role = "system",
                    content = systemInstructions
                },
                new ChatMessage
                {
                    role = "user",
                    content = observation
                }
            },
            response_format = new ChatResponseFormat
            {
                type = "json_schema",
                json_schema = new ChatJsonSchema
                {
                    name = "civilization_arena_action",
                    strict = true,
                    schema = actionSchema
                }
            },
            stream = false
        };
    }

    private static ActionObjectSchema BuildActionSchema(
        string[] citizenIds,
        string[] workplaceIds)
    {
        OfferObjectSchema offerItem = new OfferObjectSchema
        {
            type = "object",
            properties = new OfferSchemaProperties
            {
                citizenId = new EnumStringSchema
                {
                    type = "string",
                    @enum = citizenIds
                },
                workplaceId = new EnumStringSchema
                {
                    type = "string",
                    @enum = workplaceIds
                },
                wage = new IntegerSchema
                {
                    type = "integer",
                    minimum = 1
                }
            },
            required = new[] { "citizenId", "workplaceId", "wage" },
            additionalProperties = false
        };

        ActionObjectSchema actionSchema = new ActionObjectSchema
        {
            type = "object",
            properties = new ActionSchemaProperties
            {
                offers = new OfferArraySchema
                {
                    type = "array",
                    items = offerItem,
                    minItems = 0,
                    maxItems = citizenIds.Length
                },
                strategyNote = new StringSchema
                {
                    type = "string"
                }
            },
            required = new[]
            {
                "offers",
                "strategyNote"
            },
            additionalProperties = false
        };

        return actionSchema;
    }

    private static LlmProviderResult ParseLocalChatCompletionsResponse(
        string responseJson)
    {
        LocalChatCompletionsResponse response;

        try
        {
            response = JsonUtility.FromJson<LocalChatCompletionsResponse>(
                responseJson);
        }
        catch (Exception)
        {
            return LlmProviderResult.Failed(
                "Malformed local chat completion response.");
        }

        if (response == null)
        {
            return LlmProviderResult.Failed(
                "Empty local chat completion response.");
        }

        if (response.choices == null || response.choices.Length == 0)
        {
            return LlmProviderResult.Failed(
                "Local chat completion returned no choices.");
        }

        if (response.choices[0]?.message == null)
        {
            return LlmProviderResult.Failed(
                "Local chat completion returned no message.");
        }

        string actionJson = response.choices[0].message.content;
        if (string.IsNullOrWhiteSpace(actionJson))
        {
            return LlmProviderResult.Failed(
                "Local chat completion returned no message content.");
        }

        return LlmProviderResult.Succeeded(actionJson);
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

    [Serializable]
    private class OpenAiResponsesRequest
    {
        public string model;
        public string instructions;
        public string input;
        public ResponseTextConfiguration text;
    }

    [Serializable]
    private class LocalChatCompletionsRequest
    {
        public string model;
        public ChatMessage[] messages;
        public ChatResponseFormat response_format;
        public bool stream;
    }

    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class ChatResponseFormat
    {
        public string type;
        public ChatJsonSchema json_schema;
    }

    [Serializable]
    private class ChatJsonSchema
    {
        public string name;
        public bool strict;
        public ActionObjectSchema schema;
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
        public OfferArraySchema offers;
        public StringSchema strategyNote;
    }

    [Serializable]
    private class OfferArraySchema
    {
        public string type;
        public OfferObjectSchema items;
        public int minItems;
        public int maxItems;
    }

    [Serializable]
    private class OfferObjectSchema
    {
        public string type;
        public OfferSchemaProperties properties;
        public string[] required;
        public bool additionalProperties;
    }

    [Serializable]
    private class OfferSchemaProperties
    {
        public EnumStringSchema citizenId;
        public EnumStringSchema workplaceId;
        public IntegerSchema wage;
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
    private class LocalChatCompletionsResponse
    {
        public LocalChatChoice[] choices;
    }

    [Serializable]
    private class LocalChatChoice
    {
        public ChatMessage message;
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

    private enum RuntimeProviderMode
    {
        OpenAICloud,
        LocalOpenAICompatible
    }
}
