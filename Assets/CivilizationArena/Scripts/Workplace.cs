using UnityEngine;
using UnityEngine.Serialization;

public class Workplace : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType = ResourceType.Stone;
    [FormerlySerializedAs("stonePerHour")]
    [SerializeField] private float productionPerHour = 12f;
    [FormerlySerializedAs("storedStone")]
    [SerializeField] private float storedAmount;
    [SerializeField] private float workRadius = 2.5f;

    public ResourceType ResourceType => resourceType;
    public float ProductionPerHour => productionPerHour;
    public float StoredAmount => storedAmount;
    public float WorkRadius => workRadius;

    public bool IsWithinWorkArea(Vector3 worldPosition)
    {
        return Vector3.Distance(transform.position, worldPosition) <= workRadius;
    }

    public void Work(int simulatedMinutes)
    {
        if (simulatedMinutes <= 0)
        {
            return;
        }

        float producedAmount =
            productionPerHour * simulatedMinutes / 60f;

        storedAmount += producedAmount;
    }
}
