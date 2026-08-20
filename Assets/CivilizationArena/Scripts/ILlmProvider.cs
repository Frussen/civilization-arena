using System;
using UnityEngine;

public interface ILlmProvider
{
    void RequestAction(
        string observation,
        string[] citizenIds,
        string[] workplaceIds,
        Action<LlmProviderResult> onCompleted);

    void CancelRequest();
}

public abstract class LlmProviderBehaviour : MonoBehaviour, ILlmProvider
{
    public abstract void RequestAction(
        string observation,
        string[] citizenIds,
        string[] workplaceIds,
        Action<LlmProviderResult> onCompleted);

    public abstract void CancelRequest();
}

public sealed class LlmProviderResult
{
    public bool Success { get; }
    public string ActionJson { get; }
    public string Error { get; }

    private LlmProviderResult(
        bool success,
        string actionJson,
        string error)
    {
        Success = success;
        ActionJson = actionJson;
        Error = error;
    }

    public static LlmProviderResult Succeeded(string actionJson)
    {
        return new LlmProviderResult(true, actionJson, null);
    }

    public static LlmProviderResult Failed(string error)
    {
        return new LlmProviderResult(false, null, error);
    }
}
