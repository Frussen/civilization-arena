using UnityEngine;

public class AgentResourceStockpile : MonoBehaviour
{
    [SerializeField] private float stone;
    [SerializeField] private float wood;

    public float Stone => stone;
    public float Wood => wood;

    public bool TryConsume(float stoneAmount, float woodAmount)
    {
        if (stoneAmount < 0f || woodAmount < 0f)
        {
            return false;
        }

        if (stoneAmount > stone || woodAmount > wood)
        {
            return false;
        }

        stone = Mathf.Max(0f, stone - stoneAmount);
        wood = Mathf.Max(0f, wood - woodAmount);
        return true;
    }

    public void Add(ResourceType resourceType, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        switch (resourceType)
        {
            case ResourceType.Stone:
                stone += amount;
                break;
            case ResourceType.Wood:
                wood += amount;
                break;
        }
    }
}
