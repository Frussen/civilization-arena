using UnityEngine;
using UnityEngine.AI;

public class CitizenMover : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitSeconds = 2f;

    private NavMeshAgent agent;
    private Animator animator;

    private int currentWaypointIndex;
    private bool isWaiting;
    private float waitTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (waypoints.Length == 0)
        {
            return;
        }

        MoveToCurrentWaypoint();
    }

    private void Update()
    {
        if (waypoints.Length == 0)
        {
            return;
        }

        bool isWalking = agent.velocity.sqrMagnitude > 0.01f;
        animator.SetBool("IsWalking", isWalking);

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                isWaiting = false;

                currentWaypointIndex =
                    (currentWaypointIndex + 1) % waypoints.Length;

                MoveToCurrentWaypoint();
            }

            return;
        }

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance)
        {
            isWaiting = true;
            waitTimer = waitSeconds;
        }
    }

    private void MoveToCurrentWaypoint()
    {
        agent.SetDestination(waypoints[currentWaypointIndex].position);
    }
}