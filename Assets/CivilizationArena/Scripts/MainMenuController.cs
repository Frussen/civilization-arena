using System.Collections.Generic;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class MainMenuController : MonoBehaviour
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    [DllImport("CivilizationArenaMacFullscreen")]
    private static extern void
        CivilizationArena_InstallNativeFullscreenShortcut();

    [DllImport("CivilizationArenaMacFullscreen")]
    private static extern void CivilizationArena_SetNativeFullscreen(
        int enabled);
#endif

    private const string ArenaSceneName = "M0";
    private const string OpenAIProviderLabel = "OpenAI";
    private const string LocalProviderLabel = "Local (LM Studio)";
    private const string FullscreenPreferenceKey =
        "CivilizationArena.Fullscreen";
    private const int DefaultWindowedWidth = 1200;
    private const int DefaultWindowedHeight = 800;
    private static readonly List<string> OpenAIModelChoices =
        new List<string>
        {
            OpenAiLlmProvider.DefaultModel,
            "gpt-5.6-sol",
            "gpt-5.6-terra",
            "gpt-5.6-luna",
            "gpt-5.5",
            "gpt-5.4",
            "gpt-5.4-mini",
            "gpt-5.4-nano"
        };
    private static int windowedWidth;
    private static int windowedHeight;
    private static bool hasCapturedWindowedResolution;

    private VisualElement modeSelectionView;
    private VisualElement singlePlayerConfigurationView;
    private VisualElement aiArenaConfigurationView;
    private VisualElement settingsView;
    private Button aiArenaButton;
    private Button singlePlayerButton;
    private Button localMultiplayerButton;
    private Button settingsButton;
    private AiSideFields singlePlayerAiFields;
    private AiSideFields aiArenaSideAFields;
    private AiSideFields aiArenaSideBFields;
    private Label singlePlayerErrorLabel;
    private Label aiArenaErrorLabel;
    private Button singlePlayerBackButton;
    private Button singlePlayerStartButton;
    private Button aiArenaBackButton;
    private Button aiArenaStartButton;
    private Slider musicVolumeSlider;
    private Label musicVolumeValueLabel;
    private Slider sfxVolumeSlider;
    private Label sfxVolumeValueLabel;
    private Toggle fullscreenToggle;
    private Button settingsBackButton;
    private bool loadRequested;

    private void Start()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        CivilizationArena_InstallNativeFullscreenShortcut();
#endif
        ApplySavedFullscreenPreference();

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
        settingsView = root.Q<VisualElement>("settings-view");
        aiArenaButton = root.Q<Button>("ai-arena-button");
        singlePlayerButton = root.Q<Button>("single-player-button");
        localMultiplayerButton =
            root.Q<Button>("local-multiplayer-button");
        settingsButton = root.Q<Button>("settings-button");
        singlePlayerAiFields = new AiSideFields(
            root.Q<VisualElement>("single-player-ai-fields"));
        aiArenaSideAFields = new AiSideFields(
            root.Q<VisualElement>("ai-arena-side-a-ai-fields"));
        aiArenaSideBFields = new AiSideFields(
            root.Q<VisualElement>("ai-arena-side-b-ai-fields"));
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
        musicVolumeSlider =
            root.Q<Slider>("music-volume-slider");
        musicVolumeValueLabel =
            root.Q<Label>("music-volume-value-label");
        sfxVolumeSlider = root.Q<Slider>("sfx-volume-slider");
        sfxVolumeValueLabel =
            root.Q<Label>("sfx-volume-value-label");
        fullscreenToggle = root.Q<Toggle>("fullscreen-toggle");
        settingsBackButton = root.Q<Button>("settings-back-button");

        if (modeSelectionView == null ||
            singlePlayerConfigurationView == null ||
            aiArenaConfigurationView == null ||
            settingsView == null ||
            aiArenaButton == null ||
            singlePlayerButton == null ||
            localMultiplayerButton == null ||
            settingsButton == null ||
            !singlePlayerAiFields.IsValid ||
            !aiArenaSideAFields.IsValid ||
            !aiArenaSideBFields.IsValid ||
            singlePlayerErrorLabel == null ||
            aiArenaErrorLabel == null ||
            singlePlayerBackButton == null ||
            singlePlayerStartButton == null ||
            aiArenaBackButton == null ||
            aiArenaStartButton == null ||
            musicVolumeSlider == null ||
            musicVolumeValueLabel == null ||
            sfxVolumeSlider == null ||
            sfxVolumeValueLabel == null ||
            fullscreenToggle == null ||
            settingsBackButton == null)
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
        RefreshSettingsControls();
        ShowModeSelection();

        aiArenaButton.clicked += ShowAiArenaConfiguration;
        singlePlayerButton.clicked += ShowSinglePlayerConfiguration;
        localMultiplayerButton.clicked += LoadLocalMultiplayer;
        settingsButton.clicked += ShowSettings;
        singlePlayerBackButton.clicked += ReturnToModeSelection;
        singlePlayerStartButton.clicked += StartSinglePlayerMatch;
        aiArenaBackButton.clicked += ReturnToModeSelection;
        aiArenaStartButton.clicked += StartAiArenaMatch;
        settingsBackButton.clicked += ReturnFromSettings;
        musicVolumeSlider.RegisterValueChangedCallback(
            HandleMusicVolumeChanged);
        sfxVolumeSlider.RegisterValueChangedCallback(
            HandleSfxVolumeChanged);
        fullscreenToggle.RegisterValueChangedCallback(
            HandleFullscreenChanged);
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

        if (settingsButton != null)
        {
            settingsButton.clicked -= ShowSettings;
        }

        if (singlePlayerBackButton != null)
        {
            singlePlayerBackButton.clicked -= ReturnToModeSelection;
        }

        if (singlePlayerStartButton != null)
        {
            singlePlayerStartButton.clicked -= StartSinglePlayerMatch;
        }

        if (aiArenaBackButton != null)
        {
            aiArenaBackButton.clicked -= ReturnToModeSelection;
        }

        if (aiArenaStartButton != null)
        {
            aiArenaStartButton.clicked -= StartAiArenaMatch;
        }

        if (settingsBackButton != null)
        {
            settingsBackButton.clicked -= ReturnFromSettings;
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.UnregisterValueChangedCallback(
                HandleMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.UnregisterValueChangedCallback(
                HandleSfxVolumeChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.UnregisterValueChangedCallback(
                HandleFullscreenChanged);
        }

        singlePlayerAiFields?.UnregisterCallbacks();
        aiArenaSideAFields?.UnregisterCallbacks();
        aiArenaSideBFields?.UnregisterCallbacks();
        ClearApiKeyFields();
    }

    private void ShowAiArenaConfiguration()
    {
        if (loadRequested)
        {
            return;
        }

        ArenaAudioManager.PlayUiClick();
        SetConfigurationError(aiArenaErrorLabel, null);
        modeSelectionView.style.display = DisplayStyle.None;
        singlePlayerConfigurationView.style.display = DisplayStyle.None;
        settingsView.style.display = DisplayStyle.None;
        aiArenaConfigurationView.style.display = DisplayStyle.Flex;
    }

    private void ShowSinglePlayerConfiguration()
    {
        if (loadRequested)
        {
            return;
        }

        ArenaAudioManager.PlayUiClick();
        SetConfigurationError(singlePlayerErrorLabel, null);
        modeSelectionView.style.display = DisplayStyle.None;
        aiArenaConfigurationView.style.display = DisplayStyle.None;
        settingsView.style.display = DisplayStyle.None;
        singlePlayerConfigurationView.style.display = DisplayStyle.Flex;
    }

    private void LoadLocalMultiplayer()
    {
        if (loadRequested)
        {
            return;
        }

        ArenaAudioManager.PlayUiClick();
        LoadMatch(MatchConfiguration.LocalMultiplayer);
    }

    private void StartSinglePlayerMatch()
    {
        if (loadRequested)
        {
            return;
        }

        ArenaAudioManager.PlayUiClick();
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

        ArenaAudioManager.PlayUiClick();
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

    private void ReturnToModeSelection()
    {
        if (loadRequested)
        {
            return;
        }

        ArenaAudioManager.PlayUiClick();
        ShowModeSelection();
    }

    private void ShowSettings()
    {
        if (loadRequested)
        {
            return;
        }

        ArenaAudioManager.PlayUiClick();
        RefreshSettingsControls();
        modeSelectionView.style.display = DisplayStyle.None;
        singlePlayerConfigurationView.style.display = DisplayStyle.None;
        aiArenaConfigurationView.style.display = DisplayStyle.None;
        settingsView.style.display = DisplayStyle.Flex;
    }

    private void ReturnFromSettings()
    {
        if (loadRequested)
        {
            return;
        }

        ArenaAudioManager.PlayUiClick();
        PlayerPrefs.Save();
        ShowModeSelection();
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
        settingsView.style.display = DisplayStyle.None;
        modeSelectionView.style.display = DisplayStyle.Flex;
    }

    private void RefreshSettingsControls()
    {
        float musicPercent =
            ArenaAudioManager.CurrentMusicVolume * 100f;
        float sfxPercent = ArenaAudioManager.CurrentSfxVolume * 100f;
        musicVolumeSlider.SetValueWithoutNotify(musicPercent);
        sfxVolumeSlider.SetValueWithoutNotify(sfxPercent);
        fullscreenToggle.SetValueWithoutNotify(Screen.fullScreen);
        UpdateVolumeLabel(musicVolumeValueLabel, musicPercent);
        UpdateVolumeLabel(sfxVolumeValueLabel, sfxPercent);
    }

    private void HandleMusicVolumeChanged(ChangeEvent<float> change)
    {
        ArenaAudioManager.SetMusicVolume(change.newValue / 100f);
        UpdateVolumeLabel(musicVolumeValueLabel, change.newValue);
    }

    private void HandleSfxVolumeChanged(ChangeEvent<float> change)
    {
        ArenaAudioManager.SetSfxVolume(change.newValue / 100f);
        UpdateVolumeLabel(sfxVolumeValueLabel, change.newValue);
    }

    private static void HandleFullscreenChanged(ChangeEvent<bool> change)
    {
        ApplyFullscreen(change.newValue);
        PlayerPrefs.SetInt(
            FullscreenPreferenceKey,
            change.newValue ? 1 : 0);
    }

    private static void UpdateVolumeLabel(Label label, float percent)
    {
        label.text = $"{Mathf.RoundToInt(percent)}%";
    }

    private static void ApplySavedFullscreenPreference()
    {
        if (!PlayerPrefs.HasKey(FullscreenPreferenceKey))
        {
            return;
        }

        ApplyFullscreen(PlayerPrefs.GetInt(FullscreenPreferenceKey) != 0);
    }

    private static void ApplyFullscreen(bool enabled)
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        CivilizationArena_SetNativeFullscreen(enabled ? 1 : 0);
#else
        if (enabled)
        {
            if (!Screen.fullScreen)
            {
                windowedWidth = Screen.width;
                windowedHeight = Screen.height;
                hasCapturedWindowedResolution = true;
            }

            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            return;
        }

        int targetWidth = hasCapturedWindowedResolution
            ? windowedWidth
            : DefaultWindowedWidth;
        int targetHeight = hasCapturedWindowedResolution
            ? windowedHeight
            : DefaultWindowedHeight;
        Screen.SetResolution(
            targetWidth,
            targetHeight,
            FullScreenMode.Windowed);
#endif
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
        settingsButton.SetEnabled(false);
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
        private readonly VisualElement openAiModelRow;
        private readonly DropdownField openAiModelField;
        private readonly VisualElement apiKeyRow;
        private readonly TextField apiKeyField;
        private readonly VisualElement localEndpointRow;
        private readonly TextField localEndpointField;
        private readonly VisualElement localModelRow;
        private readonly TextField localModelField;

        public bool IsValid => providerField != null &&
            openAiModelRow != null && openAiModelField != null &&
            apiKeyRow != null && apiKeyField != null &&
            localEndpointRow != null && localEndpointField != null &&
            localModelRow != null && localModelField != null;

        public AiSideFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            providerField = container.Q<DropdownField>(
                className: "ai-provider-field");
            openAiModelRow = container.Q<VisualElement>(
                className: "openai-model-row");
            openAiModelField = container.Q<DropdownField>(
                className: "openai-model-field");
            apiKeyRow = container.Q<VisualElement>(
                className: "api-key-row");
            apiKeyField = container.Q<TextField>(
                className: "api-key-field");
            localEndpointRow = container.Q<VisualElement>(
                className: "local-endpoint-row");
            localEndpointField = container.Q<TextField>(
                className: "local-endpoint-field");
            localModelRow = container.Q<VisualElement>(
                className: "local-model-row");
            localModelField = container.Q<TextField>(
                className: "local-model-field");
        }

        public void Initialize()
        {
            providerField.choices = new List<string>
            {
                OpenAIProviderLabel,
                LocalProviderLabel
            };
            openAiModelField.choices = OpenAIModelChoices;
            apiKeyField.isPasswordField = true;
            providerField.RegisterValueChangedCallback(
                HandleProviderChanged);
            ResetDefaults();
        }

        public void UnregisterCallbacks()
        {
            providerField?.UnregisterValueChangedCallback(
                HandleProviderChanged);
        }

        public void ResetDefaults()
        {
            providerField.SetValueWithoutNotify(OpenAIProviderLabel);
            openAiModelField.SetValueWithoutNotify(
                OpenAiLlmProvider.DefaultModel);
            localEndpointField.SetValueWithoutNotify(
                OpenAiLlmProvider.DefaultLocalBaseUrl);
            localModelField.SetValueWithoutNotify(string.Empty);
            ClearCredential();
            UpdateProviderPresentation();
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

            if (string.Equals(
                    providerField.value,
                    OpenAIProviderLabel,
                    System.StringComparison.Ordinal))
            {
                string model = openAiModelField.value;
                if (string.IsNullOrWhiteSpace(model) ||
                    !OpenAIModelChoices.Contains(model))
                {
                    error =
                        $"{sideName}: select a supported OpenAI model.";
                    return false;
                }

                configuration = new MatchAiConfiguration(
                    MatchAiProvider.OpenAI,
                    model,
                    apiKeyField.value);
                error = null;
                return true;
            }

            if (string.Equals(
                    providerField.value,
                    LocalProviderLabel,
                    System.StringComparison.Ordinal))
            {
                string localModel = localModelField.value?.Trim();
                if (string.IsNullOrWhiteSpace(localModel))
                {
                    error = $"{sideName}: local model is required.";
                    return false;
                }

                string localEndpoint = localEndpointField.value?.Trim();
                if (!IsValidLocalEndpoint(localEndpoint))
                {
                    error = $"{sideName}: enter a valid local endpoint.";
                    return false;
                }

                configuration = new MatchAiConfiguration(
                    MatchAiProvider.LocalOpenAICompatible,
                    localModel,
                    null,
                    localEndpoint);
                error = null;
                return true;
            }

            error = $"{sideName}: select a supported AI provider.";
            return false;
        }

        private void HandleProviderChanged(ChangeEvent<string> change)
        {
            UpdateProviderPresentation();
        }

        private void UpdateProviderPresentation()
        {
            bool showLocal = string.Equals(
                providerField.value,
                LocalProviderLabel,
                System.StringComparison.Ordinal);

            openAiModelRow.style.display = showLocal
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            apiKeyRow.style.display = showLocal
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            localEndpointRow.style.display = showLocal
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            localModelRow.style.display = showLocal
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static bool IsValidLocalEndpoint(string endpoint)
        {
            return OpenAiLlmProvider.TryBuildLocalResponsesEndpoint(
                endpoint,
                out _);
        }
    }
}
