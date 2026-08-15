using UnityEngine;

public class CitizenEmployment : MonoBehaviour
{
    private const int MinimumAcceptableWage = 5;

    [SerializeField] private WorldClock clock;

    private CitizenWorkAssignment workAssignment;
    [SerializeField] private AgentTreasury currentEmployer;
    [SerializeField] private int currentWage;
    [SerializeField] private int reservationWage = MinimumAcceptableWage;

    public int MinimumWage => MinimumAcceptableWage;
    public AgentTreasury CurrentEmployer => currentEmployer;
    public int CurrentWage => currentWage;
    public int ReservationWage => reservationWage;
    public bool IsEmployed => currentEmployer != null;

    private void Awake()
    {
        workAssignment = GetComponent<CitizenWorkAssignment>();
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

        currentEmployer = employer;
        currentWage = offeredWage;
        reservationWage = Mathf.Max(reservationWage, offeredWage);
        workAssignment.Assign(workplace);
        return true;
    }

    private void ClearEmployment()
    {
        currentEmployer = null;
        currentWage = 0;
        workAssignment.Assign(null);
    }
}
