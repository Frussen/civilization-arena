using System;
using UnityEngine;

public class AgentDecisionScheduler : MonoBehaviour
{
    [SerializeField] private WorldClock worldClock;
    [SerializeField] private AgentTextInterface textInterface;
    [SerializeField] private MatchController matchController;

    [SerializeField] private AgentControlMode controlMode =
        AgentControlMode.Manual;
    [SerializeField] private int decisionIntervalMinutes = 360;
    [SerializeField] private bool requestDecisionOnStart = true;

    [SerializeField] private bool awaitingAction;
    [SerializeField] private int simulatedMinutesUntilNextDecision;
    [SerializeField] private int decisionsRequested;

    private bool firstUpdatePending;
    private float timeScaleBeforePause = 1f;
    private AgentControlMode activeControlMode;

    public AgentControlMode ControlMode => controlMode;
    public bool AwaitingAction => awaitingAction;
    public int SimulatedMinutesUntilNextDecision =>
        simulatedMinutesUntilNextDecision;
    public int DecisionsRequested => decisionsRequested;
    public event Action<int, string> DecisionRequested;

    private void Awake()
    {
        awaitingAction = false;
        decisionsRequested = 0;
        simulatedMinutesUntilNextDecision = SafeDecisionInterval;
        firstUpdatePending = true;
        activeControlMode = controlMode;
    }

    private void OnEnable()
    {
        if (textInterface != null)
        {
            textInterface.ActionApplied += HandleActionApplied;
        }
    }

    private void OnDisable()
    {
        if (textInterface != null)
        {
            textInterface.ActionApplied -= HandleActionApplied;
        }
    }

    private void Update()
    {
        if (matchController == null || matchController.IsEnded)
        {
            awaitingAction = false;
            return;
        }

        if (firstUpdatePending)
        {
            firstUpdatePending = false;
            activeControlMode = controlMode;

            if (controlMode == AgentControlMode.Api &&
                requestDecisionOnStart)
            {
                RequestDecision();
            }

            return;
        }

        if (controlMode != activeControlMode)
        {
            HandleControlModeChanged();
        }

        if (controlMode == AgentControlMode.Manual)
        {
            return;
        }

        if (awaitingAction || worldClock == null)
        {
            return;
        }

        int simulatedMinutes = worldClock.MinutesAdvancedThisFrame;
        if (simulatedMinutes <= 0)
        {
            return;
        }

        simulatedMinutesUntilNextDecision = Mathf.Max(
            0,
            simulatedMinutesUntilNextDecision - simulatedMinutes);

        if (simulatedMinutesUntilNextDecision == 0)
        {
            RequestDecision();
        }
    }

    private void RequestDecision()
    {
        if (controlMode != AgentControlMode.Api ||
            awaitingAction ||
            matchController == null ||
            matchController.IsEnded ||
            textInterface == null)
        {
            return;
        }

        if (!textInterface.GenerateObservation())
        {
            simulatedMinutesUntilNextDecision = SafeDecisionInterval;
            return;
        }

        if (Time.timeScale > 0f)
        {
            timeScaleBeforePause = Time.timeScale;
        }

        decisionsRequested++;
        awaitingAction = true;
        simulatedMinutesUntilNextDecision = 0;
        Time.timeScale = 0f;
        DecisionRequested?.Invoke(
            decisionsRequested,
            textInterface.LatestObservation);
    }

    private void HandleActionApplied()
    {
        if (!awaitingAction)
        {
            return;
        }

        if (matchController == null || matchController.IsEnded)
        {
            awaitingAction = false;
            return;
        }

        awaitingAction = false;
        simulatedMinutesUntilNextDecision = SafeDecisionInterval;
        Time.timeScale = timeScaleBeforePause > 0f
            ? timeScaleBeforePause
            : 1f;
    }

    private void HandleControlModeChanged()
    {
        activeControlMode = controlMode;

        if (controlMode == AgentControlMode.Manual)
        {
            bool schedulerOwnedPause = awaitingAction;
            awaitingAction = false;
            simulatedMinutesUntilNextDecision = SafeDecisionInterval;

            if (schedulerOwnedPause &&
                matchController != null &&
                !matchController.IsEnded &&
                Time.timeScale == 0f)
            {
                Time.timeScale = timeScaleBeforePause > 0f
                    ? timeScaleBeforePause
                    : 1f;
            }

            return;
        }

        awaitingAction = false;
        simulatedMinutesUntilNextDecision = SafeDecisionInterval;
        RequestDecision();
    }

    private int SafeDecisionInterval => Mathf.Max(1, decisionIntervalMinutes);
}
