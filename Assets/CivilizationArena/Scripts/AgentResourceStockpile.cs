using UnityEngine;

public class AgentResourceStockpile : MonoBehaviour
{
    [SerializeField] private float stone;
    [SerializeField] private float wood;

    public float Stone => stone;
    public float Wood => wood;

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
