using UnityEngine;

public class AgentDecisionScheduler : MonoBehaviour
{
    [SerializeField] private WorldClock worldClock;
    [SerializeField] private AgentTextInterface textInterface;
    [SerializeField] private MatchController matchController;

    [SerializeField] private int decisionIntervalMinutes = 360;
    [SerializeField] private bool requestDecisionOnStart = true;

    [SerializeField] private bool awaitingAction;
    [SerializeField] private int simulatedMinutesUntilNextDecision;
    [SerializeField] private int decisionsRequested;

    private bool firstUpdatePending;
    private float timeScaleBeforePause = 1f;

    public bool AwaitingAction => awaitingAction;
    public int SimulatedMinutesUntilNextDecision =>
        simulatedMinutesUntilNextDecision;
    public int DecisionsRequested => decisionsRequested;

    private void Awake()
    {
        awaitingAction = false;
        decisionsRequested = 0;
        simulatedMinutesUntilNextDecision = SafeDecisionInterval;
        firstUpdatePending = true;
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

            if (requestDecisionOnStart)
            {
                RequestDecision();
            }

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
        if (awaitingAction ||
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

    private int SafeDecisionInterval => Mathf.Max(1, decisionIntervalMinutes);
}
