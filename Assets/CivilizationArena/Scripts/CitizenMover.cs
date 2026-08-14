using UnityEngine;
using UnityEngine.AI;

public class CitizenMover : MonoBehaviour
{
    [SerializeField] private Transform destination;

    private NavMeshAgent agent;
    private Animator animator;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        agent.SetDestination(destination.position);
    }

    private void Update()
    {
        bool isWalking = agent.velocity.sqrMagnitude > 0.01f;
        animator.SetBool("IsWalking", isWalking);
    }
}