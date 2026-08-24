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
    private Button singlePlayerButton;
    private Button localMultiplayerButton;
    private DropdownField aiProviderField;
    private TextField modelField;
    private TextField apiKeyField;
    private Label configurationErrorLabel;
    private Button backButton;
    private Button startMatchButton;
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

        root.Q<Button>("ai-arena-button")?.SetEnabled(false);
        root.Q<Button>("online-multiplayer-button")?.SetEnabled(false);
        modeSelectionView = root.Q<VisualElement>("mode-selection-view");
        singlePlayerConfigurationView =
            root.Q<VisualElement>("single-player-configuration-view");
        singlePlayerButton = root.Q<Button>("single-player-button");
        localMultiplayerButton =
            root.Q<Button>("local-multiplayer-button");
        aiProviderField = root.Q<DropdownField>("ai-provider-field");
        modelField = root.Q<TextField>("model-field");
        apiKeyField = root.Q<TextField>("api-key-field");
        configurationErrorLabel =
            root.Q<Label>("configuration-error-label");
        backButton = root.Q<Button>("configuration-back-button");
        startMatchButton = root.Q<Button>("start-match-button");

        if (modeSelectionView == null ||
            singlePlayerConfigurationView == null ||
            singlePlayerButton == null ||
            localMultiplayerButton == null ||
            aiProviderField == null ||
            modelField == null ||
            apiKeyField == null ||
            configurationErrorLabel == null ||
            backButton == null ||
            startMatchButton == null)
        {
            Debug.LogError(
                "MainMenuUI is missing required menu or configuration controls.",
                this);
            enabled = false;
            return;
        }

        aiProviderField.choices = new List<string>
        {
            OpenAIProviderLabel
        };
        aiProviderField.index = 0;
        modelField.value = OpenAiLlmProvider.DefaultModel;
        apiKeyField.isPasswordField = true;
        ShowModeSelection();

        singlePlayerButton.clicked += LoadSinglePlayer;
        localMultiplayerButton.clicked += LoadLocalMultiplayer;
        backButton.clicked += ShowModeSelection;
        startMatchButton.clicked += StartSinglePlayerMatch;
    }

    private void OnDisable()
    {
        if (singlePlayerButton != null)
        {
            singlePlayerButton.clicked -= LoadSinglePlayer;
        }

        if (localMultiplayerButton != null)
        {
            localMultiplayerButton.clicked -= LoadLocalMultiplayer;
        }

        if (backButton != null)
        {
            backButton.clicked -= ShowModeSelection;
        }

        if (startMatchButton != null)
        {
            startMatchButton.clicked -= StartSinglePlayerMatch;
        }

        ClearApiKeyField();
    }

    private void LoadSinglePlayer()
    {
        if (loadRequested)
        {
            return;
        }

        SetConfigurationError(null);
        modeSelectionView.style.display = DisplayStyle.None;
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

        if (!string.Equals(
                aiProviderField.value,
                OpenAIProviderLabel,
                System.StringComparison.Ordinal))
        {
            SetConfigurationError("Select a supported AI provider.");
            return;
        }

        string model = modelField.value?.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            SetConfigurationError("Model is required.");
            return;
        }

        MatchAiConfiguration aiConfiguration =
            new MatchAiConfiguration(
                MatchAiProvider.OpenAI,
                model,
                apiKeyField.value);
        MatchConfiguration configuration =
            MatchConfiguration.SinglePlayer(aiConfiguration);

        ClearApiKeyField();
        LoadMatch(configuration);
    }

    private void ShowModeSelection()
    {
        if (loadRequested || modeSelectionView == null ||
            singlePlayerConfigurationView == null)
        {
            return;
        }

        ClearApiKeyField();
        SetConfigurationError(null);
        aiProviderField.index = 0;
        modelField.value = OpenAiLlmProvider.DefaultModel;
        singlePlayerConfigurationView.style.display = DisplayStyle.None;
        modeSelectionView.style.display = DisplayStyle.Flex;
    }

    private void SetConfigurationError(string message)
    {
        if (configurationErrorLabel == null)
        {
            return;
        }

        configurationErrorLabel.text = message ?? string.Empty;
        configurationErrorLabel.style.display =
            string.IsNullOrEmpty(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
    }

    private void ClearApiKeyField()
    {
        if (apiKeyField != null)
        {
            apiKeyField.value = string.Empty;
        }
    }

    private void LoadMatch(MatchConfiguration configuration)
    {
        if (loadRequested)
        {
            return;
        }

        loadRequested = true;
        singlePlayerButton.SetEnabled(false);
        localMultiplayerButton.SetEnabled(false);
        backButton.SetEnabled(false);
        startMatchButton.SetEnabled(false);
        MatchConfigurationSession.SetPending(configuration);
        SceneManager.LoadScene(ArenaSceneName, LoadSceneMode.Single);
    }
}
