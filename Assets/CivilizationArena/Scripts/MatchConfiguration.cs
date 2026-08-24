using UnityEngine;
using UnityEngine.SceneManagement;

public enum MatchSideControlMode
{
    Manual,
    AI,
    Remote
}

public enum MatchAiProvider
{
    OpenAI
}

public readonly struct MatchAiConfiguration
{
    public MatchAiProvider Provider { get; }
    public string Model { get; }
    internal string RuntimeCredential { get; }

    public MatchAiConfiguration(
        MatchAiProvider provider,
        string model,
        string runtimeCredential)
    {
        Provider = provider;
        Model = model;
        RuntimeCredential = runtimeCredential;
    }
}

public readonly struct MatchSideConfiguration
{
    public MatchSideControlMode Controller { get; }
    public MatchAiConfiguration? AI { get; }

    public MatchSideConfiguration(
        MatchSideControlMode controller,
        MatchAiConfiguration? ai = null)
    {
        Controller = controller;
        AI = ai;
    }

    public static MatchSideConfiguration Manual =>
        new MatchSideConfiguration(MatchSideControlMode.Manual);
}

public readonly struct MatchConfiguration
{
    public MatchSideConfiguration SideA { get; }
    public MatchSideConfiguration SideB { get; }

    public MatchConfiguration(
        MatchSideConfiguration sideA,
        MatchSideConfiguration sideB)
    {
        SideA = sideA;
        SideB = sideB;
    }

    public static MatchConfiguration LocalMultiplayer =>
        new MatchConfiguration(
            MatchSideConfiguration.Manual,
            MatchSideConfiguration.Manual);

    public static MatchConfiguration SinglePlayer(
        MatchAiConfiguration aiConfiguration) =>
        new MatchConfiguration(
            MatchSideConfiguration.Manual,
            new MatchSideConfiguration(
                MatchSideControlMode.AI,
                aiConfiguration));
}

public static class MatchConfigurationSession
{
    private static bool hasPendingConfiguration;
    private static MatchConfiguration pendingConfiguration;

    public static bool HasPendingConfiguration => hasPendingConfiguration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        hasPendingConfiguration = false;
        pendingConfiguration = default;
    }

    public static void SetPending(MatchConfiguration configuration)
    {
        pendingConfiguration = configuration;
        hasPendingConfiguration = true;
    }

    internal static bool TryConsumePending(
        out MatchConfiguration configuration)
    {
        if (!hasPendingConfiguration)
        {
            configuration = default;
            return false;
        }

        configuration = pendingConfiguration;
        hasPendingConfiguration = false;
        pendingConfiguration = default;
        return true;
    }
}

internal static class MatchConfigurationArenaBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeSceneApplication()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryApplyPendingConfiguration();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryApplyPendingConfiguration();
    }

    private static void TryApplyPendingConfiguration()
    {
        if (!MatchConfigurationSession.HasPendingConfiguration)
        {
            return;
        }

        ArenaLlmRoundController[] controllers =
            Object.FindObjectsByType<ArenaLlmRoundController>(
                FindObjectsInactive.Exclude);

        if (controllers.Length == 0)
        {
            return;
        }

        if (!MatchConfigurationSession.TryConsumePending(
                out MatchConfiguration configuration))
        {
            return;
        }

        if (controllers.Length != 1)
        {
            Debug.LogError(
                "Pending MatchConfiguration requires exactly one active " +
                $"ArenaLlmRoundController, but found {controllers.Length}.");
            DisableControllers(controllers);
            return;
        }

        ArenaLlmRoundController controller = controllers[0];
        if (!TryPrepareSide(
                configuration.SideA,
                controller.SideAProvider,
                "Side A",
                out AgentControlMode sideAControlMode,
                out string error) ||
            !TryPrepareSide(
                configuration.SideB,
                controller.SideBProvider,
                "Side B",
                out AgentControlMode sideBControlMode,
                out error) ||
            !controller.TryConfigureControlModes(
                sideAControlMode,
                sideBControlMode,
                out error))
        {
            Debug.LogError(
                $"MatchConfiguration could not be applied: {error}",
                controller);
            controller.enabled = false;
        }
    }

    private static bool TryPrepareSide(
        MatchSideConfiguration side,
        LlmProviderBehaviour arenaProvider,
        string sideName,
        out AgentControlMode arenaMode,
        out string error)
    {
        switch (side.Controller)
        {
            case MatchSideControlMode.Manual:
                if (side.AI.HasValue)
                {
                    arenaMode = default;
                    error =
                        $"{sideName} Manual control cannot use AI settings.";
                    return false;
                }

                arenaMode = AgentControlMode.Manual;
                error = null;
                return true;

            case MatchSideControlMode.AI:
                if (!side.AI.HasValue)
                {
                    arenaMode = default;
                    error = $"{sideName} AI configuration is missing.";
                    return false;
                }

                if (!TryConfigureAiProvider(
                        side.AI.Value,
                        arenaProvider,
                        sideName,
                        out error))
                {
                    arenaMode = default;
                    return false;
                }

                arenaMode = AgentControlMode.Api;
                return true;

            case MatchSideControlMode.Remote:
                arenaMode = default;
                error = $"{sideName} Remote control is not supported yet.";
                return false;

            default:
                arenaMode = default;
                error =
                    $"{sideName} control mode '{side.Controller}' is invalid.";
                return false;
        }
    }

    private static bool TryConfigureAiProvider(
        MatchAiConfiguration configuration,
        LlmProviderBehaviour arenaProvider,
        string sideName,
        out string error)
    {
        switch (configuration.Provider)
        {
            case MatchAiProvider.OpenAI:
                if (!(arenaProvider is OpenAiLlmProvider openAiProvider))
                {
                    error =
                        $"{sideName} requires an OpenAiLlmProvider component.";
                    return false;
                }

                if (!openAiProvider.TryConfigureRuntime(
                        configuration.Model,
                        configuration.RuntimeCredential,
                        out error))
                {
                    error = $"{sideName} OpenAI configuration failed: {error}";
                    return false;
                }

                return true;

            default:
                error =
                    $"{sideName} AI provider '{configuration.Provider}' " +
                    "is not supported.";
                return false;
        }
    }

    private static void DisableControllers(
        ArenaLlmRoundController[] controllers)
    {
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].enabled = false;
            }
        }
    }
}
