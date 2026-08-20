using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OpenAiLlmProvider : LlmProviderBehaviour
{
    private const string ResponsesEndpoint =
        "https://api.openai.com/v1/responses";
    private const int RequestTimeoutSeconds = 180;

    [SerializeField] private string model = "gpt-5.6";
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
        "Offers are processed sequentially in the supplied order, and rejection " +
        "is a possible gameplay outcome.\n" +
        "Consider current time, work shift, activity, wage, reservation wage, " +
        "current employer and Workplace, resources, payroll, and Wonder " +
        "requirements.\n" +
        "Do not invent actions that are not available.\n" +
        "Return the strategic action required by the supplied schema.\n" +
        "strategyNote must contain only a short high-level strategy summary.";

    private Coroutine activeRequest;
    private int requestVersion;

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

        if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable))
        {
            onCompleted(LlmProviderResult.Failed(
                "The API key environment variable name is empty."));
            return;
        }

        string apiKey = Environment.GetEnvironmentVariable(
            apiKeyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onCompleted(LlmProviderResult.Failed(
                $"API key environment variable " +
                $"'{apiKeyEnvironmentVariable}' is not set."));
            return;
        }

        OpenAiResponsesRequest requestBody = BuildRequest(
            observation,
            citizenIds,
            workplaceIds);

        string requestJson = JsonUtility.ToJson(requestBody);
        int currentRequestVersion = ++requestVersion;
        activeRequest = StartCoroutine(SendRequest(
            currentRequestVersion,
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

    private IEnumerator SendRequest(
        int currentRequestVersion,
        string apiKey,
        string requestJson,
        Action<LlmProviderResult> onCompleted)
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
                ParseResponse(request.downloadHandler?.text));
        }
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
