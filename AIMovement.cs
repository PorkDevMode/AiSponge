using UnityEngine;
using UnityEngine.AI;

public class AIMovement : MonoBehaviour
{
    public float movementInterval = 30f;  // Interval between movements
    public float movementRadius = 1f;    // Radius within which the character will move
    private NavMeshAgent agent;
    private float timer;
    private bool canMove = true;
    private bool isCameraFollowing = false;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        timer = movementInterval;
    }

    private void Update()
    {
        if (!isCameraFollowing)
        {
            timer -= Time.deltaTime;

            if (canMove && timer <= 0f)
            {
                MoveRandomly();
                timer = movementInterval;
            }
        }
    }

    private void MoveRandomly()
    {
        Vector3 randomDirection = Random.insideUnitSphere * movementRadius;
        randomDirection += transform.position;
        NavMeshHit hit;

        // Find a valid random point on the NavMesh to move to
        if (NavMesh.SamplePosition(randomDirection, out hit, movementRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    public void SetCameraFollowing(bool following)
    {
        isCameraFollowing = following;

        if (isCameraFollowing)
        {
            canMove = false;
            agent.isStopped = true;
            agent.ResetPath();
            Invoke("ResumeMovement", 5f);
        }
    }

    private void ResumeMovement()
    {
        canMove = true;
        agent.isStopped = false;
    }
}