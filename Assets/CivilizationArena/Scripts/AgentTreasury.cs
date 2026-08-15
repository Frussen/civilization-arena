using UnityEngine;

public class AgentTreasury : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private float initialGold = 100f;
    [SerializeField] private float goldIncomePerHour = 40f;

    [SerializeField] private float currentGold;

    public float CurrentGold => currentGold;
    public float GoldIncomePerHour => goldIncomePerHour;

    private void Awake()
    {
        currentGold = Mathf.Max(0f, initialGold);
    }

    private void Update()
    {
        int simulatedMinutes = clock.MinutesAdvancedThisFrame;
        if (simulatedMinutes <= 0)
        {
            return;
        }

        float income = goldIncomePerHour * simulatedMinutes / 60f;
        currentGold = Mathf.Max(0f, currentGold + income);
    }

    public bool TrySpend(float amount)
    {
        if (amount < 0f || amount > currentGold)
        {
            return false;
        }

        currentGold -= amount;
        return true;
    }
}
