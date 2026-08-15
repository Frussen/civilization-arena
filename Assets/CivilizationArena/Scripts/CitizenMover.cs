using UnityEngine;
using UnityEngine.AI;

public class CitizenMover : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private Transform currentDestination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        bool isWalking = agent.velocity.sqrMagnitude > 0.01f;
        animator.SetBool("IsWalking", isWalking);
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
