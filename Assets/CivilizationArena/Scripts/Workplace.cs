using UnityEngine;

public class Workplace : MonoBehaviour
{
    [SerializeField] private float stonePerHour = 12f;
    [SerializeField] private float storedStone;
    [SerializeField] private float workRadius = 2.5f;

    public float StoredStone => storedStone;
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

        float producedStone =
            stonePerHour * simulatedMinutes / 60f;

        storedStone += producedStone;
    }
}
