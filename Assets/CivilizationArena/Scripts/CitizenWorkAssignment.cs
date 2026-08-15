using UnityEngine;

public class CitizenWorkAssignment : MonoBehaviour
{
    [SerializeField] private Workplace currentWorkplace;

    public Workplace CurrentWorkplace => currentWorkplace;

    public void Assign(Workplace workplace)
    {
        currentWorkplace = workplace;
    }
}
