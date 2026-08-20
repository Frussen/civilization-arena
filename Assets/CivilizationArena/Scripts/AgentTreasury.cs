using UnityEngine;

public class AgentTreasury : MonoBehaviour
{
    [SerializeField] private WorldClock clock;
    [SerializeField] private float initialGold = 100f;
    [SerializeField] private float goldIncomePerHour = 40f;
    [SerializeField] private float payrollCoverageHours = 10f;

    [SerializeField] private float currentGold;
    [SerializeField] private float currentPayrollPerHour;

    public float CurrentGold => currentGold;
    public float GoldIncomePerHour => goldIncomePerHour;
    public float CurrentPayrollPerHour => currentPayrollPerHour;
    public float PayrollCoverageHours => Mathf.Max(0f, payrollCoverageHours);

    private void Awake()
    {
        currentGold = Mathf.Max(0f, initialGold);
        currentPayrollPerHour = 0f;
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

    public bool HasPayrollCoverage(float projectedPayrollPerHour)
    {
        if (projectedPayrollPerHour < 0f)
        {
            return false;
        }

        float requiredGold = projectedPayrollPerHour * PayrollCoverageHours;
        return currentGold >= requiredGold;
    }

    internal void AddPayrollCommitment(int wagePerHour)
    {
        if (wagePerHour > 0)
        {
            currentPayrollPerHour += wagePerHour;
        }
    }

    internal void RemovePayrollCommitment(int wagePerHour)
    {
        if (wagePerHour > 0)
        {
            currentPayrollPerHour = Mathf.Max(
                0f,
                currentPayrollPerHour - wagePerHour);
        }
    }
}
