using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class ArenaRuntimeUIController : MonoBehaviour
{
    private const float PollIntervalSeconds = 0.1f;
    private const float SubmitDebounceSeconds = 0.35f;
    private const string ArenaSceneName = "M0";
    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private ArenaLlmRoundController roundController;

    private VisualElement mainPanel;
    private VisualElement headerRow;
    private Label turnLabel;
    private Label timeLabel;
    private Label goldLabel;
    private Label incomeLabel;
    private Label payrollLabel;
    private Label netLabel;
    private Label woodLabel;
    private Label stoneLabel;
    private Label wonderLabel;
    private VisualElement economyRow;
    private VisualElement tableHeader;
    private ScrollView citizenScroll;
    private VisualElement citizenViewport;
    private VisualElement citizenRows;
    private Button submitButton;
    private Label statusLabel;
    private VisualElement matchResultPanel;
    private Label matchResultTitle;
    private Label matchResultDetail;
    private Button rematchButton;
    private Button mainMenuButton;

    private ArenaManualDecisionController displayedController;
    private int displayedRoundId;
    private float nextPollTime;
    private float submitUnlockTime;
    private bool showingMatchResult;
    private bool initialized;
    private Camera gameplayCamera;
    private Rect originalCameraRect;
    private bool originalCameraRectCaptured;
    private bool sceneLoadRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeRuntimeAttachment()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AttachToExistingArenaDocument();
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        AttachToExistingArenaDocument();
    }

    private static void AttachToExistingArenaDocument()
    {
        UIDocument[] documents = FindObjectsByType<UIDocument>(
            FindObjectsInactive.Exclude);

        for (int i = 0; i < documents.Length; i++)
        {
            UIDocument document = documents[i];

            if (document != null &&
                document.gameObject.name == "ArenaRuntimeUI" &&
                document.GetComponent<ArenaRuntimeUIController>() == null)
            {
                document.gameObject.AddComponent<ArenaRuntimeUIController>();
            }
        }
    }

    private void Start()
    {
        Initialize();
        RefreshSelectedDecision(true);
    }

    private void Update()
    {
        if (!initialized || Time.unscaledTime < nextPollTime)
        {
            return;
        }

        nextPollTime = Time.unscaledTime + PollIntervalSeconds;
        RefreshSelectedDecision(false);
    }

    private void OnDisable()
    {
        if (citizenScroll != null)
        {
            citizenScroll.UnregisterCallback<GeometryChangedEvent>(
                HandleCitizenScrollGeometryChanged);
        }

        if (citizenViewport != null)
        {
            citizenViewport.UnregisterCallback<GeometryChangedEvent>(
                HandleCitizenScrollGeometryChanged);
        }

        if (submitButton != null)
        {
            submitButton.clicked -= SubmitTurn;
        }

        if (rematchButton != null)
        {
            rematchButton.clicked -= Rematch;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.clicked -= ReturnToMainMenu;
        }

        RestoreGameplayCameraViewport();

        initialized = false;
        displayedController = null;
        displayedRoundId = 0;
    }

    private void Initialize()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document != null
            ? document.rootVisualElement
            : null;

        if (root == null)
        {
            Debug.LogError("Arena runtime UI requires a UIDocument.", this);
            enabled = false;
            return;
        }

        mainPanel = root.Q<VisualElement>("main-panel");
        headerRow = root.Q<VisualElement>(className: "header-row");
        turnLabel = root.Q<Label>("turn-label");
        timeLabel = root.Q<Label>("time-label");
        goldLabel = root.Q<Label>("gold-label");
        incomeLabel = root.Q<Label>("income-label");
        payrollLabel = root.Q<Label>("payroll-label");
        netLabel = root.Q<Label>("net-label");
        woodLabel = root.Q<Label>("wood-label");
        stoneLabel = root.Q<Label>("stone-label");
        wonderLabel = root.Q<Label>("wonder-label");
        economyRow = root.Q<VisualElement>("economy-row");
        tableHeader = root.Q<VisualElement>("table-header");
        citizenScroll = root.Q<ScrollView>("citizen-scroll");
        citizenViewport = citizenScroll != null
            ? citizenScroll.contentViewport
            : null;
        citizenRows = root.Q<VisualElement>("citizen-rows");
        submitButton = root.Q<Button>("submit-button");
        statusLabel = root.Q<Label>("status-label");
        matchResultPanel = root.Q<VisualElement>("match-result-panel");
        matchResultTitle = root.Q<Label>("match-result-title");
        matchResultDetail = root.Q<Label>("match-result-detail");
        rematchButton = root.Q<Button>("rematch-button");
        mainMenuButton = root.Q<Button>("main-menu-button");

        if (mainPanel == null ||
            headerRow == null ||
            turnLabel == null ||
            timeLabel == null ||
            goldLabel == null ||
            incomeLabel == null ||
            payrollLabel == null ||
            netLabel == null ||
            woodLabel == null ||
            stoneLabel == null ||
            wonderLabel == null ||
            economyRow == null ||
            tableHeader == null ||
            citizenScroll == null ||
            citizenViewport == null ||
            citizenRows == null ||
            submitButton == null ||
            statusLabel == null ||
            matchResultPanel == null ||
            matchResultTitle == null ||
            matchResultDetail == null ||
            rematchButton == null ||
            mainMenuButton == null)
        {
            Debug.LogError(
                "ArenaManualUI is missing one or more required named elements.",
                this);
            enabled = false;
            return;
        }

        citizenScroll.RegisterCallback<GeometryChangedEvent>(
            HandleCitizenScrollGeometryChanged);
        citizenViewport.RegisterCallback<GeometryChangedEvent>(
            HandleCitizenScrollGeometryChanged);
        citizenScroll.schedule.Execute(UpdateTableHeaderGutter);
        submitButton.clicked += SubmitTurn;
        rematchButton.clicked += Rematch;
        mainMenuButton.clicked += ReturnToMainMenu;
        initialized = true;
    }

    private void HandleCitizenScrollGeometryChanged(
        GeometryChangedEvent change)
    {
        UpdateTableHeaderGutter();
    }

    private void UpdateTableHeaderGutter()
    {
        if (tableHeader == null ||
            citizenScroll == null ||
            citizenViewport == null)
        {
            return;
        }

        float scrollbarGutter = Mathf.Max(
            0f,
            citizenScroll.worldBound.xMax -
            citizenViewport.worldBound.xMax);
        tableHeader.style.marginRight = scrollbarGutter;
    }

    private void RefreshSelectedDecision(bool force)
    {
        if (roundController == null)
        {
            roundController = FindAnyObjectByType<ArenaLlmRoundController>();
        }

        ArenaMatchController matchController = roundController != null
            ? roundController.ArenaMatchController
            : null;
        bool matchEnded = matchController != null &&
            matchController.IsMatchEnded;

        bool apiVsApi = IsApiVsApi();

        if (apiVsApi)
        {
            SetGameplayCameraFullScreen();
            mainPanel.EnableInClassList(
                "spectator-result-overlay",
                matchEnded);
            headerRow.style.display = matchEnded
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            mainPanel.style.display = matchEnded
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (!matchEnded)
            {
                displayedController = null;
                displayedRoundId = 0;
                showingMatchResult = false;
                return;
            }
        }
        else
        {
            RestoreGameplayCameraViewport();
            mainPanel.RemoveFromClassList("spectator-result-overlay");
            headerRow.style.display = DisplayStyle.Flex;
            mainPanel.style.display = DisplayStyle.Flex;
        }

        if (matchEnded)
        {
            displayedController = null;
            displayedRoundId = 0;

            if (force || !showingMatchResult)
            {
                ShowMatchResult(matchController);
            }

            return;
        }

        showingMatchResult = false;

        ArenaManualDecisionController selectedController =
            SelectWaitingController();
        int selectedRoundId = selectedController != null
            ? selectedController.CurrentRoundId
            : 0;

        if (force ||
            selectedController != displayedController ||
            selectedRoundId != displayedRoundId)
        {
            displayedController = selectedController;
            displayedRoundId = selectedRoundId;

            if (displayedController == null)
            {
                ShowPassiveState();
            }
            else
            {
                ShowDecision(displayedController);
            }

            return;
        }

        if (displayedController != null)
        {
            submitButton.SetEnabled(
                IsWaiting(displayedController) &&
                Time.unscaledTime >= submitUnlockTime);
            RefreshStatus(displayedController);
        }
        else
        {
            RefreshPassiveState();
        }
    }

    private bool IsApiVsApi()
    {
        return roundController != null &&
            roundController.SideAControlMode == AgentControlMode.Api &&
            roundController.SideBControlMode == AgentControlMode.Api;
    }

    private void SetGameplayCameraFullScreen()
    {
        if (!TryCaptureGameplayCamera())
        {
            return;
        }

        gameplayCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private void RestoreGameplayCameraViewport()
    {
        if (!originalCameraRectCaptured || gameplayCamera == null)
        {
            return;
        }

        gameplayCamera.rect = originalCameraRect;
    }

    private bool TryCaptureGameplayCamera()
    {
        if (originalCameraRectCaptured && gameplayCamera != null)
        {
            return true;
        }

        Camera candidate = Camera.main;

        if (candidate == null ||
            candidate.gameObject.scene != gameObject.scene)
        {
            return false;
        }

        gameplayCamera = candidate;
        originalCameraRect = candidate.rect;
        originalCameraRectCaptured = true;
        return true;
    }

    private ArenaManualDecisionController SelectWaitingController()
    {
        if (roundController == null)
        {
            return null;
        }

        ArenaManualDecisionController sideA =
            roundController.SideAManualDecisionController;
        ArenaManualDecisionController sideB =
            roundController.SideBManualDecisionController;

        if (IsWaiting(displayedController))
        {
            return displayedController;
        }

        if (IsWaiting(sideA))
        {
            return sideA;
        }

        return IsWaiting(sideB) ? sideB : null;
    }

    private static bool IsWaiting(ArenaManualDecisionController controller)
    {
        return controller != null &&
            controller.isActiveAndEnabled &&
            controller.Status ==
                ArenaManualDecisionStatus.WaitingForSubmission;
    }

    private void ShowDecision(ArenaManualDecisionController controller)
    {
        SetHudVisibility(
            economyVisible: true,
            decisionVisible: true,
            matchResultVisible: false);
        ArenaManualObservationView observation =
            ArenaManualObservationView.Parse(controller.CapturedObservation);

        turnLabel.text = $"Agent {controller.ActiveSide} turn";
        ApplyObservationToHud(
            observation,
            $"Round {controller.CurrentRoundId}");

        BuildCitizenRows(controller, observation);
        submitButton.SetEnabled(Time.unscaledTime >= submitUnlockTime);
        RefreshStatus(controller);
    }

    private void ApplyObservationToHud(
        ArenaManualObservationView observation,
        string fallbackTime)
    {
        timeLabel.text = observation.HasTime
            ? $"Day {observation.Day} — {observation.Hour:D2}:" +
              $"{observation.Minute:D2}"
            : fallbackTime;

        goldLabel.text = FormatWholeValue("Gold", observation.Gold);
        incomeLabel.text = FormatRate(
            "Income",
            observation.GoldIncomePerHour,
            false);
        payrollLabel.text = FormatRate(
            "Payroll",
            observation.PayrollPerHour,
            true);
        netLabel.text = FormatRate(
            "Net",
            observation.NetGoldPerHour,
            false);
        woodLabel.text = FormatWholeValue("Wood", observation.Wood);
        stoneLabel.text = FormatWholeValue("Stone", observation.Stone);
        wonderLabel.text = FormatWonder(observation);
    }

    private void ShowPassiveState()
    {
        if (TryGetPersistentHumanInterface(out _))
        {
            SetHudVisibility(
                economyVisible: true,
                decisionVisible: false,
                matchResultVisible: false);
            citizenRows.Clear();
            submitButton.SetEnabled(false);
            RefreshPersistentHumanHud();
            return;
        }

        SetHudVisibility(
            economyVisible: true,
            decisionVisible: true,
            matchResultVisible: false);
        turnLabel.text = "No manual decision";
        timeLabel.text = "—";
        goldLabel.text = "Gold: —";
        incomeLabel.text = "Income: —";
        payrollLabel.text = "Payroll: —";
        netLabel.text = "Net: —";
        woodLabel.text = "Wood: —";
        stoneLabel.text = "Stone: —";
        wonderLabel.text = "Wonder: —";
        citizenRows.Clear();
        Label emptyLabel = new Label(
            "No manual decision is currently pending.");
        emptyLabel.AddToClassList("empty-row");
        citizenRows.Add(emptyLabel);
        submitButton.SetEnabled(false);
        RefreshPassiveStatus();
    }

    private void RefreshPassiveState()
    {
        if (TryGetPersistentHumanInterface(out _))
        {
            RefreshPersistentHumanHud();
            return;
        }

        RefreshPassiveStatus();
    }

    private void RefreshPersistentHumanHud()
    {
        if (!TryGetPersistentHumanInterface(
                out AgentTextInterface textInterface) ||
            textInterface == null)
        {
            ShowUnavailablePersistentHud(
                "The human observation interface is unavailable.");
            return;
        }

        bool waitingForAi = roundController.RoundActive;
        turnLabel.text = waitingForAi
            ? "Waiting for AI"
            : "Simulation running";

        if (!textInterface.TryCaptureArenaObservation(
                out string observationText,
                out string error))
        {
            ShowUnavailablePersistentHud(error);
            return;
        }

        ArenaManualObservationView observation =
            ArenaManualObservationView.Parse(observationText);
        ApplyObservationToHud(observation, "—");
        SetStatus(
            waitingForAi
                ? "Waiting for the AI action."
                : "Waiting for next decision.",
            false);
    }

    private void ShowUnavailablePersistentHud(string error)
    {
        timeLabel.text = "—";
        goldLabel.text = "Gold: —";
        incomeLabel.text = "Income: —";
        payrollLabel.text = "Payroll: —";
        netLabel.text = "Net: —";
        woodLabel.text = "Wood: —";
        stoneLabel.text = "Stone: —";
        wonderLabel.text = "Wonder: —";
        SetStatus(
            string.IsNullOrWhiteSpace(error)
                ? "Live human statistics are unavailable."
                : error,
            true);
    }

    private bool TryGetPersistentHumanInterface(
        out AgentTextInterface textInterface)
    {
        textInterface = null;

        if (roundController == null)
        {
            return false;
        }

        bool sideAManual = roundController.SideAControlMode ==
            AgentControlMode.Manual;
        bool sideBManual = roundController.SideBControlMode ==
            AgentControlMode.Manual;

        if (sideAManual == sideBManual)
        {
            return false;
        }

        textInterface = sideAManual
            ? roundController.SideATextInterface
            : roundController.SideBTextInterface;
        return true;
    }

    private void RefreshPassiveStatus()
    {
        SetStatus("Waiting for a manual Arena decision.", false);
    }

    private void ShowMatchResult(ArenaMatchController matchController)
    {
        showingMatchResult = true;
        SetHudVisibility(
            economyVisible: false,
            decisionVisible: false,
            matchResultVisible: true);
        submitButton.SetEnabled(false);
        turnLabel.text = "Match complete";
        timeLabel.text = "—";

        ArenaMatchResult result = matchController.Result;
        string title = "MATCH OVER";
        string detail;

        if (result == ArenaMatchResult.Draw)
        {
            detail = "Both agents built the Wonder";
        }
        else if (result == ArenaMatchResult.SideA ||
                 result == ArenaMatchResult.SideB)
        {
            ArenaSide winner = result == ArenaMatchResult.SideA
                ? ArenaSide.A
                : ArenaSide.B;
            detail = $"Agent {winner} built the Wonder";

            bool sideAManual = roundController.SideAControlMode ==
                AgentControlMode.Manual;
            bool sideBManual = roundController.SideBControlMode ==
                AgentControlMode.Manual;

            if (sideAManual != sideBManual)
            {
                ArenaSide humanSide = sideAManual
                    ? ArenaSide.A
                    : ArenaSide.B;
                title = winner == humanSide ? "VICTORY" : "DEFEAT";
            }
        }
        else
        {
            detail = "Match ended";
        }

        matchResultTitle.text = title;
        matchResultDetail.text = detail;
        SetPostMatchButtonsEnabled(!sceneLoadRequested);
        SetStatus(string.Empty, false);
    }

    private void Rematch()
    {
        if (sceneLoadRequested || !showingMatchResult)
        {
            return;
        }

        sceneLoadRequested = true;
        SetPostMatchButtonsEnabled(false);

        if (!MatchConfigurationSession.TryPrepareRematch())
        {
            MatchConfigurationSession.Clear();
        }

        SceneManager.LoadScene(ArenaSceneName, LoadSceneMode.Single);
    }

    private void ReturnToMainMenu()
    {
        if (sceneLoadRequested || !showingMatchResult)
        {
            return;
        }

        sceneLoadRequested = true;
        SetPostMatchButtonsEnabled(false);
        MatchConfigurationSession.Clear();
        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
    }

    private void SetPostMatchButtonsEnabled(bool enabledState)
    {
        rematchButton.SetEnabled(enabledState);
        mainMenuButton.SetEnabled(enabledState);
    }

    private void SetHudVisibility(
        bool economyVisible,
        bool decisionVisible,
        bool matchResultVisible)
    {
        economyRow.style.display = economyVisible
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        DisplayStyle decisionDisplay = decisionVisible
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        tableHeader.style.display = decisionDisplay;
        citizenScroll.style.display = decisionDisplay;
        submitButton.style.display = decisionDisplay;
        matchResultPanel.style.display = matchResultVisible
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    private void BuildCitizenRows(
        ArenaManualDecisionController controller,
        ArenaManualObservationView observation)
    {
        citizenRows.Clear();
        IReadOnlyList<ArenaManualOfferDraftRow> draftRows =
            controller.DraftRows;

        if (draftRows == null || draftRows.Count == 0)
        {
            Label emptyLabel = new Label("No citizens are available.");
            emptyLabel.AddToClassList("empty-row");
            citizenRows.Add(emptyLabel);
            return;
        }

        List<string> workplaceOptions = new List<string>();
        IReadOnlyList<string> allowedWorkplaces =
            controller.AllowedWorkplaceIds;

        if (allowedWorkplaces != null)
        {
            for (int i = 0; i < allowedWorkplaces.Count; i++)
            {
                workplaceOptions.Add(allowedWorkplaces[i]);
            }
        }

        for (int i = 0; i < draftRows.Count; i++)
        {
            ArenaManualOfferDraftRow draftRow = draftRows[i];

            if (draftRow == null)
            {
                continue;
            }

            observation.Citizens.TryGetValue(
                draftRow.CitizenId,
                out ArenaManualCitizenView citizen);

            VisualElement rowElement = new VisualElement();
            rowElement.AddToClassList("table-row");
            rowElement.AddToClassList("citizen-row");

            if ((i & 1) != 0)
            {
                rowElement.AddToClassList("alternate");
            }

            rowElement.Add(CreateCell(
                draftRow.CitizenId,
                "citizen-cell"));
            rowElement.Add(CreateCell(
                FormatOwner(citizen, controller.ActiveSide),
                "owner-cell"));
            rowElement.Add(CreateCell(
                FormatCurrentWage(citizen),
                "wage-cell"));
            rowElement.Add(CreateCell(
                citizen != null ? citizen.ReservationWage : "—",
                "reservation-cell"));
            rowElement.Add(CreateCell(
                citizen == null
                    ? "—"
                    : citizen.Activity == "Resting" ? "Yes" : "No",
                "rest-cell"));
            rowElement.Add(CreateCell(
                citizen != null &&
                    !string.IsNullOrWhiteSpace(citizen.WorkplaceId)
                        ? citizen.WorkplaceId
                        : "none",
                "current-job-cell"));

            DropdownField workplaceField = new DropdownField(
                workplaceOptions,
                FindWorkplaceIndex(workplaceOptions, draftRow.WorkplaceId));
            workplaceField.AddToClassList("cell");
            workplaceField.AddToClassList("offer-job-cell");
            workplaceField.AddToClassList("offer-job-field");
            workplaceField.SetEnabled(workplaceOptions.Count > 0);
            workplaceField.RegisterValueChangedCallback(change =>
            {
                if (!controller.TrySetWorkplace(
                        draftRow.CitizenId,
                        change.newValue))
                {
                    workplaceField.SetValueWithoutNotify(change.previousValue);
                    RefreshStatus(controller);
                }
            });
            rowElement.Add(workplaceField);

            IntegerField wageField = new IntegerField
            {
                value = draftRow.Wage
            };
            wageField.AddToClassList("cell");
            wageField.AddToClassList("offer-cell");
            wageField.AddToClassList("offer-wage-field");
            wageField.RegisterValueChangedCallback(change =>
            {
                if (!controller.TrySetWage(
                        draftRow.CitizenId,
                        change.newValue))
                {
                    wageField.SetValueWithoutNotify(change.previousValue);
                    RefreshStatus(controller);
                }
            });
            rowElement.Add(wageField);
            citizenRows.Add(rowElement);
        }
    }

    private static Label CreateCell(string text, string widthClass)
    {
        Label label = new Label(text ?? "—");
        label.AddToClassList("cell");
        label.AddToClassList(widthClass);
        return label;
    }

    private void SubmitTurn()
    {
        ArenaManualDecisionController controller = displayedController;

        if (!IsWaiting(controller) || Time.unscaledTime < submitUnlockTime)
        {
            submitButton.SetEnabled(false);
            return;
        }

        submitUnlockTime = Time.unscaledTime + SubmitDebounceSeconds;
        submitButton.SetEnabled(false);
        SetCitizenInputsEnabled(false);

        if (!controller.TrySubmit(out string error))
        {
            submitButton.SetEnabled(
                Time.unscaledTime >= submitUnlockTime);
            SetCitizenInputsEnabled(true);
            SetStatus(error, true);
            return;
        }

        SetStatus("Turn submitted.", false);
        RefreshSelectedDecision(true);
    }

    private void SetCitizenInputsEnabled(bool enabledState)
    {
        citizenRows.Query<DropdownField>().ForEach(
            field => field.SetEnabled(
                enabledState && field.choices.Count > 0));
        citizenRows.Query<IntegerField>().ForEach(
            field => field.SetEnabled(enabledState));
    }

    private void RefreshStatus(ArenaManualDecisionController controller)
    {
        string error = controller != null
            ? controller.ValidationError
            : null;

        SetStatus(
            string.IsNullOrWhiteSpace(error)
                ? string.Empty
                : error,
            !string.IsNullOrWhiteSpace(error));
    }

    private void SetStatus(string message, bool isError)
    {
        statusLabel.text = message ?? string.Empty;
        statusLabel.EnableInClassList("error", isError);
        statusLabel.style.display = string.IsNullOrWhiteSpace(message)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private static int FindWorkplaceIndex(
        List<string> workplaceIds,
        string workplaceId)
    {
        int index = workplaceIds.FindIndex(
            id => string.Equals(id, workplaceId, StringComparison.Ordinal));
        return index >= 0 ? index : 0;
    }

    private static string FormatOwner(
        ArenaManualCitizenView citizen,
        ArenaSide activeSide)
    {
        if (citizen == null || citizen.Employer == "none")
        {
            return "—";
        }

        if (citizen.Employer == "this_agent")
        {
            return activeSide.ToString();
        }

        if (citizen.Employer == "other")
        {
            return activeSide == ArenaSide.A ? "B" : "A";
        }

        return "—";
    }

    private static string FormatCurrentWage(ArenaManualCitizenView citizen)
    {
        return citizen == null || citizen.Status == "unemployed"
            ? "—"
            : citizen.Wage;
    }

    private static string FormatWholeValue(string label, string value)
    {
        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float number))
        {
            return $"{label}: —";
        }

        float wholeUnits = number >= 0f
            ? Mathf.Floor(number)
            : Mathf.Ceil(number);
        return $"{label}: " +
            wholeUnits.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatRate(
        string label,
        string value,
        bool displayAsCost)
    {
        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float number))
        {
            return $"{label}: —";
        }

        float displayedNumber = displayAsCost ? -number : number;
        string sign = displayedNumber > 0f ? "+" : string.Empty;
        return $"{label}: {sign}" +
            displayedNumber.ToString("0.###", CultureInfo.InvariantCulture) +
            "/h";
    }

    private static string FormatWonder(ArenaManualObservationView observation)
    {
        if (!float.TryParse(
                observation.WonderLaborCompleted,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float completed) ||
            !float.TryParse(
                observation.WonderLaborRequired,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float required) ||
            required <= 0f)
        {
            return "Wonder: —";
        }

        float percent = Mathf.Clamp01(completed / required) * 100f;
        return "Wonder: " +
            percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    private sealed class ArenaManualCitizenView
    {
        public string Status;
        public string Employer;
        public string Wage;
        public string ReservationWage;
        public string Activity;
        public string WorkplaceId;
    }

    private sealed class ArenaManualObservationView
    {
        private enum Section
        {
            None,
            Economy,
            Wonder,
            Citizens
        }

        public readonly Dictionary<string, ArenaManualCitizenView> Citizens =
            new Dictionary<string, ArenaManualCitizenView>(
                StringComparer.Ordinal);

        public bool HasTime;
        public int Day;
        public int Hour;
        public int Minute;
        public string Gold;
        public string GoldIncomePerHour;
        public string PayrollPerHour;
        public string NetGoldPerHour;
        public string Stone;
        public string Wood;
        public string WonderLaborCompleted;
        public string WonderLaborRequired;

        public static ArenaManualObservationView Parse(string observation)
        {
            ArenaManualObservationView result =
                new ArenaManualObservationView();

            if (string.IsNullOrWhiteSpace(observation))
            {
                return result;
            }

            Section section = Section.None;
            string[] lines = observation.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.StartsWith("time: ", StringComparison.Ordinal))
                {
                    ParseTime(line, result);
                    continue;
                }

                if (line == "economy:")
                {
                    section = Section.Economy;
                    continue;
                }

                if (line == "wonder:")
                {
                    section = Section.Wonder;
                    continue;
                }

                if (line == "citizens:")
                {
                    section = Section.Citizens;
                    continue;
                }

                if (line.EndsWith(":", StringComparison.Ordinal))
                {
                    section = Section.None;
                    continue;
                }

                if (section == Section.Economy)
                {
                    ParseEconomyValue(line, result);
                }
                else if (section == Section.Wonder)
                {
                    ParseWonderValue(line, result);
                }
                else if (section == Section.Citizens)
                {
                    ParseCitizen(line, result);
                }
            }

            return result;
        }

        private static void ParseTime(
            string line,
            ArenaManualObservationView result)
        {
            string[] fields = line.Substring(6).Split(' ');
            Dictionary<string, string> values = ParseFields(fields, '=');

            result.HasTime =
                TryParseInt(values, "day", out result.Day) &&
                TryParseInt(values, "hour", out result.Hour) &&
                TryParseInt(values, "minute", out result.Minute);
        }

        private static void ParseEconomyValue(
            string line,
            ArenaManualObservationView result)
        {
            if (!TrySplitPair(line, '=', out string key, out string value))
            {
                return;
            }

            switch (key)
            {
                case "gold":
                    result.Gold = value;
                    break;
                case "goldIncomePerHour":
                    result.GoldIncomePerHour = value;
                    break;
                case "payrollPerHour":
                    result.PayrollPerHour = value;
                    break;
                case "netGoldPerHour":
                    result.NetGoldPerHour = value;
                    break;
                case "stone":
                    result.Stone = value;
                    break;
                case "wood":
                    result.Wood = value;
                    break;
            }
        }

        private static void ParseWonderValue(
            string line,
            ArenaManualObservationView result)
        {
            if (!TrySplitPair(line, '=', out string key, out string value))
            {
                return;
            }

            if (key == "laborHoursCompleted")
            {
                result.WonderLaborCompleted = value;
            }
            else if (key == "laborHoursRequired")
            {
                result.WonderLaborRequired = value;
            }
        }

        private static void ParseCitizen(
            string line,
            ArenaManualObservationView result)
        {
            const string separator = ": status=";
            int separatorIndex = line.IndexOf(
                separator,
                StringComparison.Ordinal);

            if (separatorIndex <= 0)
            {
                return;
            }

            string citizenId = line.Substring(0, separatorIndex);
            string data = line.Substring(separatorIndex + 2);
            string[] fields = data.Split(new[] { ", " },
                StringSplitOptions.None);
            Dictionary<string, string> values = ParseFields(fields, '=');

            values.TryGetValue("status", out string status);
            values.TryGetValue("employer", out string employer);
            values.TryGetValue("wage", out string wage);
            values.TryGetValue("reservation", out string reservation);
            values.TryGetValue("activity", out string activity);
            values.TryGetValue("workplace", out string workplace);

            result.Citizens[citizenId] = new ArenaManualCitizenView
            {
                Status = status,
                Employer = employer,
                Wage = wage,
                ReservationWage = reservation,
                Activity = activity,
                WorkplaceId = workplace
            };
        }

        private static Dictionary<string, string> ParseFields(
            string[] fields,
            char separator)
        {
            Dictionary<string, string> values =
                new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < fields.Length; i++)
            {
                if (TrySplitPair(
                        fields[i],
                        separator,
                        out string key,
                        out string value))
                {
                    values[key] = value;
                }
            }

            return values;
        }

        private static bool TrySplitPair(
            string text,
            char separator,
            out string key,
            out string value)
        {
            int separatorIndex = text.IndexOf(separator);

            if (separatorIndex <= 0 || separatorIndex == text.Length - 1)
            {
                key = null;
                value = null;
                return false;
            }

            key = text.Substring(0, separatorIndex).Trim();
            value = text.Substring(separatorIndex + 1).Trim();
            return true;
        }

        private static bool TryParseInt(
            Dictionary<string, string> values,
            string key,
            out int value)
        {
            value = 0;

            return values.TryGetValue(key, out string text) &&
                int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value);
        }
    }
}
