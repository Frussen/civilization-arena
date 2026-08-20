using UnityEngine;

public class CitizenEmployment : MonoBehaviour
{
    private const int MinimumAcceptableWage = 5;

    [SerializeField] private WorldClock clock;

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
            currentEmployer.AddPayrollCommitment(currentWage);
            payrollRegistered = true;
        }
    }

    private void Update()
    {
        if (!IsEmployed)
        {
            return;
        }

        int simulatedMinutes = clock.MinutesAdvancedThisFrame;
        if (simulatedMinutes <= 0)
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

        if (payrollRegistered && currentEmployer != null)
        {
            currentEmployer.RemovePayrollCommitment(currentWage);
        }

        employer.AddPayrollCommitment(offeredWage);
        payrollRegistered = true;

        currentEmployer = employer;
        currentWage = offeredWage;
        reservationWage = Mathf.Max(reservationWage, offeredWage);
        workAssignment.Assign(workplace);
        return true;
    }

    private void ClearEmployment()
    {
        if (payrollRegistered && currentEmployer != null)
        {
            currentEmployer.RemovePayrollCommitment(currentWage);
        }

        payrollRegistered = false;
        currentEmployer = null;
        currentWage = 0;
        workAssignment.Assign(null);
    }
}
