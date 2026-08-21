using UnityEngine;

public class CitizenEmployment : MonoBehaviour
{
    private const int MinimumAcceptableWage = 5;

    private CitizenWorkAssignment workAssignment;
    [SerializeField] private AgentTreasury currentEmployer;
    [SerializeField] private int currentWage;
    [SerializeField] private int reservationWage = MinimumAcceptableWage;
    private bool payrollRegistered;

    public int MinimumWage => MinimumAcceptableWage;
    public AgentTreasury CurrentEmployer => currentEmployer;
    public int CurrentWage => currentWage;
    public int ReservationWage => reservationWage;
    public bool IsEmployed => currentEmployer != null;

    private void Awake()
    {
        workAssignment = GetComponent<CitizenWorkAssignment>();
    }

    private void Start()
    {
        if (!payrollRegistered && currentEmployer != null && currentWage > 0)
        {
            currentEmployer.RegisterEmployee(this, currentWage);
            payrollRegistered = true;
        }
    }

    private void OnDestroy()
    {
        UnregisterCurrentPayroll();
    }

    internal void ProcessWage(
        AgentTreasury expectedEmployer,
        int simulatedMinutes)
    {
        if (expectedEmployer == null ||
            currentEmployer != expectedEmployer ||
            simulatedMinutes <= 0)
        {
            return;
        }

        float wagePayment = currentWage * (simulatedMinutes / 60f);
        if (!currentEmployer.TrySpend(wagePayment))
        {
            ClearEmployment();
        }
    }

    public bool TryAcceptOffer(
        AgentTreasury employer,
        Workplace workplace,
        int offeredWage)
    {
        if (employer == null || workplace == null || offeredWage <= 0)
        {
            return false;
        }

        if (IsEmployed)
        {
            if (offeredWage <= currentWage)
            {
                return false;
            }
        }
        else if (offeredWage < reservationWage)
        {
            return false;
        }

        float existingCommitment = payrollRegistered && currentEmployer == employer
            ? currentWage
            : 0f;
        float projectedPayrollPerHour =
            employer.CurrentPayrollPerHour - existingCommitment + offeredWage;

        if (!employer.HasPayrollCoverage(projectedPayrollPerHour))
        {
            return false;
        }

        if (payrollRegistered)
        {
            UnregisterCurrentPayroll();
        }

        employer.RegisterEmployee(this, offeredWage);
        payrollRegistered = true;

        currentEmployer = employer;
        currentWage = offeredWage;
        reservationWage = Mathf.Max(reservationWage, offeredWage);
        workAssignment.Assign(workplace);
        return true;
    }

    private void ClearEmployment()
    {
        UnregisterCurrentPayroll();
        currentEmployer = null;
        currentWage = 0;
        workAssignment.Assign(null);
    }

    private void UnregisterCurrentPayroll()
    {
        if (!payrollRegistered)
        {
            return;
        }

        AgentTreasury employer = currentEmployer;
        payrollRegistered = false;

        if (employer != null)
        {
            employer.UnregisterEmployee(this, currentWage);
        }
    }
}
