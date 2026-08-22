using UnityEngine;
using UnityEngine.AI;

public class CitizenMover : MonoBehaviour
{
    private static readonly int IsWalkingId =
        Animator.StringToHash("IsWalking");
    private static readonly int IsWorkingId =
        Animator.StringToHash("IsWorking");

    private NavMeshAgent agent;
    private Animator animator;
    private CitizenWorker worker;
    private Transform currentDestination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        worker = GetComponent<CitizenWorker>();
    }

    private void Update()
    {
        bool isWalking = agent.velocity.sqrMagnitude > 0.01f;
        bool isWorking = !isWalking &&
            worker != null &&
            worker.IsActivelyWorking;
        animator.SetBool(IsWalkingId, isWalking);
        animator.SetBool(IsWorkingId, isWorking);
    }

    public void MoveTo(Transform destination, float stoppingDistance)
    {
        currentDestination = destination;
        agent.stoppingDistance = stoppingDistance;
        agent.SetDestination(destination.position);
    }

    public bool HasArrivedAt(Transform destination)
    {
        if (currentDestination != destination)
        {
            return false;
        }

        if (agent.pathPending)
        {
            return false;
        }

        float distance =
            Vector3.Distance(transform.position, destination.position);

        return distance <= agent.stoppingDistance + 0.2f;
    }
}
