using UnityEngine;

public class Workplace : MonoBehaviour
{
    [SerializeField] private float stonePerHour = 12f;
    [SerializeField] private float storedStone;

    public float StoredStone => storedStone;

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
