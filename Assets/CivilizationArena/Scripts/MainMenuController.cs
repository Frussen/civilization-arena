using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class MainMenuController : MonoBehaviour
{
    private const string ArenaSceneName = "M0";
    private const string OpenAIProviderLabel = "OpenAI";

    private VisualElement modeSelectionView;
    private VisualElement singlePlayerConfigurationView;
    private VisualElement aiArenaConfigurationView;
    private Button aiArenaButton;
    private Button singlePlayerButton;
    private Button localMultiplayerButton;
    private AiSideFields singlePlayerAiFields;
    private AiSideFields aiArenaSideAFields;
    private AiSideFields aiArenaSideBFields;
    private Label singlePlayerErrorLabel;
    private Label aiArenaErrorLabel;
    private Button singlePlayerBackButton;
    private Button singlePlayerStartButton;
    private Button aiArenaBackButton;
    private Button aiArenaStartButton;
    private bool loadRequested;

    private void Start()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document != null
            ? document.rootVisualElement
            : null;

        if (root == null)
        {
            Debug.LogError("Main menu requires a UIDocument.", this);
            enabled = false;
            return;
        }

        root.Q<Button>("online-multiplayer-button")?.SetEnabled(false);
        modeSelectionView = root.Q<VisualElement>("mode-selection-view");
        singlePlayerConfigurationView =
            root.Q<VisualElement>("single-player-configuration-view");
        aiArenaConfigurationView =
            root.Q<VisualElement>("ai-arena-configuration-view");
        aiArenaButton = root.Q<Button>("ai-arena-button");
        singlePlayerButton = root.Q<Button>("single-player-button");
        localMultiplayerButton =
            root.Q<Button>("local-multiplayer-button");
        singlePlayerAiFields = new AiSideFields(
            root,
            "ai-provider-field",
            "model-field",
            "api-key-field");
        aiArenaSideAFields = new AiSideFields(
            root,
            "ai-arena-side-a-provider-field",
            "ai-arena-side-a-model-field",
            "ai-arena-side-a-api-key-field");
        aiArenaSideBFields = new AiSideFields(
            root,
            "ai-arena-side-b-provider-field",
            "ai-arena-side-b-model-field",
            "ai-arena-side-b-api-key-field");
        singlePlayerErrorLabel =
            root.Q<Label>("configuration-error-label");
        aiArenaErrorLabel =
            root.Q<Label>("ai-arena-configuration-error-label");
        singlePlayerBackButton =
            root.Q<Button>("configuration-back-button");
        singlePlayerStartButton = root.Q<Button>("start-match-button");
        aiArenaBackButton = root.Q<Button>("ai-arena-back-button");
        aiArenaStartButton =
            root.Q<Button>("ai-arena-start-match-button");

        if (modeSelectionView == null ||
            singlePlayerConfigurationView == null ||
            aiArenaConfigurationView == null ||
            aiArenaButton == null ||
            singlePlayerButton == null ||
            localMultiplayerButton == null ||
            !singlePlayerAiFields.IsValid ||
            !aiArenaSideAFields.IsValid ||
            !aiArenaSideBFields.IsValid ||
            singlePlayerErrorLabel == null ||
            aiArenaErrorLabel == null ||
            singlePlayerBackButton == null ||
            singlePlayerStartButton == null ||
            aiArenaBackButton == null ||
            aiArenaStartButton == null)
        {
            Debug.LogError(
                "MainMenuUI is missing required menu or configuration controls.",
                this);
            enabled = false;
            return;
        }

        singlePlayerAiFields.Initialize();
        aiArenaSideAFields.Initialize();
        aiArenaSideBFields.Initialize();
        ShowModeSelection();

        aiArenaButton.clicked += ShowAiArenaConfiguration;
        singlePlayerButton.clicked += ShowSinglePlayerConfiguration;
        localMultiplayerButton.clicked += LoadLocalMultiplayer;
        singlePlayerBackButton.clicked += ShowModeSelection;
        singlePlayerStartButton.clicked += StartSinglePlayerMatch;
        aiArenaBackButton.clicked += ShowModeSelection;
        aiArenaStartButton.clicked += StartAiArenaMatch;
    }

    private void OnDisable()
    {
        if (aiArenaButton != null)
        {
            aiArenaButton.clicked -= ShowAiArenaConfiguration;
        }

        if (singlePlayerButton != null)
        {
            singlePlayerButton.clicked -= ShowSinglePlayerConfiguration;
        }

        if (localMultiplayerButton != null)
        {
            localMultiplayerButton.clicked -= LoadLocalMultiplayer;
        }

        if (singlePlayerBackButton != null)
        {
            singlePlayerBackButton.clicked -= ShowModeSelection;
        }

        if (singlePlayerStartButton != null)
        {
            singlePlayerStartButton.clicked -= StartSinglePlayerMatch;
        }

        if (aiArenaBackButton != null)
        {
            aiArenaBackButton.clicked -= ShowModeSelection;
        }

        if (aiArenaStartButton != null)
        {
            aiArenaStartButton.clicked -= StartAiArenaMatch;
        }

        ClearApiKeyFields();
    }

    private void ShowAiArenaConfiguration()
    {
        if (loadRequested)
        {
            return;
        }

        SetConfigurationError(aiArenaErrorLabel, null);
        modeSelectionView.style.display = DisplayStyle.None;
        singlePlayerConfigurationView.style.display = DisplayStyle.None;
        aiArenaConfigurationView.style.display = DisplayStyle.Flex;
    }

    private void ShowSinglePlayerConfiguration()
    {
        if (loadRequested)
        {
            return;
        }

        SetConfigurationError(singlePlayerErrorLabel, null);
        modeSelectionView.style.display = DisplayStyle.None;
        aiArenaConfigurationView.style.display = DisplayStyle.None;
        singlePlayerConfigurationView.style.display = DisplayStyle.Flex;
    }

    private void LoadLocalMultiplayer()
    {
        LoadMatch(MatchConfiguration.LocalMultiplayer);
    }

    private void StartSinglePlayerMatch()
    {
        if (loadRequested)
        {
            return;
        }

        if (!singlePlayerAiFields.TryBuildConfiguration(
                "Side B",
                out MatchAiConfiguration aiConfiguration,
                out string error))
        {
            SetConfigurationError(singlePlayerErrorLabel, error);
            return;
        }

        MatchConfiguration configuration =
            MatchConfiguration.SinglePlayer(aiConfiguration);
        singlePlayerAiFields.ClearCredential();
        LoadMatch(configuration);
    }

    private void StartAiArenaMatch()
    {
        if (loadRequested)
        {
            return;
        }

        if (!aiArenaSideAFields.TryBuildConfiguration(
                "Side A",
                out MatchAiConfiguration sideAConfiguration,
                out string error) ||
            !aiArenaSideBFields.TryBuildConfiguration(
                "Side B",
                out MatchAiConfiguration sideBConfiguration,
                out error))
        {
            SetConfigurationError(aiArenaErrorLabel, error);
            return;
        }

        MatchConfiguration configuration = MatchConfiguration.AiArena(
            sideAConfiguration,
            sideBConfiguration);
        aiArenaSideAFields.ClearCredential();
        aiArenaSideBFields.ClearCredential();
        LoadMatch(configuration);
    }

    private void ShowModeSelection()
    {
        if (loadRequested || modeSelectionView == null ||
            singlePlayerConfigurationView == null ||
            aiArenaConfigurationView == null)
        {
            return;
        }

        ClearApiKeyFields();
        SetConfigurationError(singlePlayerErrorLabel, null);
        SetConfigurationError(aiArenaErrorLabel, null);
        singlePlayerAiFields.ResetDefaults();
        aiArenaSideAFields.ResetDefaults();
        aiArenaSideBFields.ResetDefaults();
        singlePlayerConfigurationView.style.display = DisplayStyle.None;
        aiArenaConfigurationView.style.display = DisplayStyle.None;
        modeSelectionView.style.display = DisplayStyle.Flex;
    }

    private static void SetConfigurationError(Label label, string message)
    {
        if (label == null)
        {
            return;
        }

        label.text = message ?? string.Empty;
        label.style.display = string.IsNullOrEmpty(message)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private void ClearApiKeyFields()
    {
        singlePlayerAiFields?.ClearCredential();
        aiArenaSideAFields?.ClearCredential();
        aiArenaSideBFields?.ClearCredential();
    }

    private void LoadMatch(MatchConfiguration configuration)
    {
        if (loadRequested)
        {
            return;
        }

        loadRequested = true;
        aiArenaButton.SetEnabled(false);
        singlePlayerButton.SetEnabled(false);
        localMultiplayerButton.SetEnabled(false);
        singlePlayerBackButton.SetEnabled(false);
        singlePlayerStartButton.SetEnabled(false);
        aiArenaBackButton.SetEnabled(false);
        aiArenaStartButton.SetEnabled(false);
        MatchConfigurationSession.SetPending(configuration);
        SceneManager.LoadScene(ArenaSceneName, LoadSceneMode.Single);
    }

    private sealed class AiSideFields
    {
        private readonly DropdownField providerField;
        private readonly TextField modelField;
        private readonly TextField apiKeyField;

        public bool IsValid => providerField != null &&
            modelField != null && apiKeyField != null;

        public AiSideFields(
            VisualElement root,
            string providerFieldName,
            string modelFieldName,
            string apiKeyFieldName)
        {
            providerField = root.Q<DropdownField>(providerFieldName);
            modelField = root.Q<TextField>(modelFieldName);
            apiKeyField = root.Q<TextField>(apiKeyFieldName);
        }

        public void Initialize()
        {
            providerField.choices = new List<string>
            {
                OpenAIProviderLabel
            };
            apiKeyField.isPasswordField = true;
            ResetDefaults();
        }

        public void ResetDefaults()
        {
            providerField.index = 0;
            modelField.value = OpenAiLlmProvider.DefaultModel;
        }

        public void ClearCredential()
        {
            if (apiKeyField != null)
            {
                apiKeyField.value = string.Empty;
            }
        }

        public bool TryBuildConfiguration(
            string sideName,
            out MatchAiConfiguration configuration,
            out string error)
        {
            configuration = default;

            if (!string.Equals(
                    providerField.value,
                    OpenAIProviderLabel,
                    System.StringComparison.Ordinal))
            {
                error = $"{sideName}: select a supported AI provider.";
                return false;
            }

            string model = modelField.value?.Trim();
            if (string.IsNullOrWhiteSpace(model))
            {
                error = $"{sideName} model is required.";
                return false;
            }

            configuration = new MatchAiConfiguration(
                MatchAiProvider.OpenAI,
                model,
                apiKeyField.value);
            error = null;
            return true;
        }
    }
}
