using UnityEngine;

public class WonderConstruction : MonoBehaviour
{
    [SerializeField] private AgentTreasury owner;
    [SerializeField] private float stoneRequired = 2000f;
    [SerializeField] private float woodRequired = 3000f;
    [SerializeField] private float laborHoursRequired = 100f;
    [SerializeField] private GameObject completedVisual;

    [SerializeField] private float laborHoursCompleted;
    [SerializeField] private bool completed;

    public AgentTreasury Owner => owner;
    public float StoneRequired => stoneRequired;
    public float WoodRequired => woodRequired;
    public float LaborHoursRequired => laborHoursRequired;
    public float LaborHoursCompleted => laborHoursCompleted;
    public bool Completed => completed;

    private void Awake()
    {
        if (completedVisual != null)
        {
            completedVisual.SetActive(completed);
        }
    }

    public void ContributeLabor(
        AgentTreasury employer,
        int simulatedMinutes)
    {
        if (completed ||
            owner == null ||
            employer != owner ||
            simulatedMinutes <= 0 ||
            stoneRequired < 0f ||
            woodRequired < 0f ||
            laborHoursRequired <= 0f)
        {
            return;
        }

        AgentResourceStockpile stockpile =
            owner.GetComponent<AgentResourceStockpile>();

        if (stockpile == null)
        {
            return;
        }

        laborHoursCompleted = Mathf.Clamp(
            laborHoursCompleted,
            0f,
            laborHoursRequired);

        float remainingLabor = laborHoursRequired - laborHoursCompleted;
        float potentialLabor = Mathf.Min(
            simulatedMinutes / 60f,
            remainingLabor);

        float stonePerLaborHour = stoneRequired / laborHoursRequired;
        float woodPerLaborHour = woodRequired / laborHoursRequired;

        float supportedLabor = potentialLabor;

        if (stonePerLaborHour > 0f)
        {
            supportedLabor = Mathf.Min(
                supportedLabor,
                stockpile.Stone / stonePerLaborHour);
        }

        if (woodPerLaborHour > 0f)
        {
            supportedLabor = Mathf.Min(
                supportedLabor,
                stockpile.Wood / woodPerLaborHour);
        }

        if (supportedLabor <= 0f)
        {
            return;
        }

        float stoneToConsume = stonePerLaborHour * supportedLabor;
        float woodToConsume = woodPerLaborHour * supportedLabor;

        if (!stockpile.TryConsume(stoneToConsume, woodToConsume))
        {
            return;
        }

        laborHoursCompleted = Mathf.Min(
            laborHoursRequired,
            laborHoursCompleted + supportedLabor);

        if (laborHoursCompleted >= laborHoursRequired)
        {
            completed = true;

            if (completedVisual != null)
            {
                completedVisual.SetActive(true);
            }

            Debug.Log($"{name} Wonder completed.", this);
        }
    }
}
