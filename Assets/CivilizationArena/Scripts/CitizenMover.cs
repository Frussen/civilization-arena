using UnityEngine;
using UnityEngine.AI;

public class CitizenMover : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

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

    public void MoveTo(Transform destination)
    {
        agent.SetDestination(destination.position);
    }
}