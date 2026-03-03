using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CharacterMovement))]
public class AIInputHandler : MonoBehaviour
{
    [Header("AI Settings")]
    [SerializeField] protected Transform target;
    [SerializeField] protected float stoppingDistance = 2;

    protected NavMeshAgent agent;
    protected CharacterMovement movementController;

	protected virtual void Awake()
	{
		if (movementController == null)
			movementController = GetComponent<CharacterMovement>();

		if (agent == null)
			agent = GetComponent<NavMeshAgent>();

		agent.updatePosition = false;
		agent.updateRotation = false;
		agent.updateUpAxis = false;
	}

	protected virtual void Update()
	{
		if (target == null) return;

		MoveTowardsTarget();
		FaceMovementDirection();
	}

	protected virtual void MoveTowardsTarget()
	{
		agent.SetDestination(target.position);

		if(agent.remainingDistance <= stoppingDistance)
		{
			movementController.SetMovementInput(Vector2.zero);
			return;
		}

		Vector3 nextPoint = agent.steeringTarget;
		Vector3 direction = (nextPoint - transform.position).normalized;

		Vector3 localDir = transform.InverseTransformDirection(direction);

		movementController.SetMovementInput(new Vector2(localDir.x, localDir.z));

		agent.nextPosition = transform.position;
	}

	protected virtual void FaceMovementDirection()
	{

		if (!agent.enabled || !agent.isOnNavMesh) return;

		Vector3 direction = (agent.steeringTarget - transform.position);

		if (direction.sqrMagnitude < .01f) return;

		direction = Vector3.ProjectOnPlane(direction, transform.up);

		Quaternion targetRotation = Quaternion.LookRotation(direction, transform.up);

		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
	}

	public void SetTarget(Transform newTarget) => target = newTarget;
}
