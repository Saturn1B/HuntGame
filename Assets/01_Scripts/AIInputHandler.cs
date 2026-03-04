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
		//Set character movement if not already set
		if (movementController == null)
			movementController = GetComponent<CharacterMovement>();

		//Set agent if not already set
		if (agent == null)
			agent = GetComponent<NavMeshAgent>();

		//Turn off the agent handling of position and rotation, so the movement can all be handled by the character movement
		agent.updatePosition = false;
		agent.updateRotation = false;
		agent.updateUpAxis = false;
	}

	protected virtual void Update()
	{
		//Return if no target found
		if (target == null) return;

		//Move toward target and face toward movement direction
		MoveTowardsTarget();
		FaceMovementDirection();
	}

	protected virtual void MoveTowardsTarget()
	{
		//Set the destination to the target
		agent.SetDestination(target.position);

		//Check if the remaining distance between AI and target is less than stopping distance, if yes set the movement to 0 and return
		if(agent.remainingDistance <= stoppingDistance)
		{
			movementController.SetMovementInput(Vector2.zero);
			return;
		}

		//Get next point to move and it's direction
		Vector3 nextPoint = agent.steeringTarget;
		Vector3 direction = (nextPoint - transform.position).normalized;

		//Get the direction from global to local
		Vector3 localDir = transform.InverseTransformDirection(direction);

		//Set the movement to the desired direction
		movementController.SetMovementInput(new Vector2(localDir.x, localDir.z));

		//Set next pos to current pos
		agent.nextPosition = transform.position;
	}

	protected virtual void FaceMovementDirection()
	{
		//Check if agent is enabled and on mesh, if not, return
		if (!agent.enabled || !agent.isOnNavMesh) return;

		//Calculate the movement direction toward next point
		Vector3 direction = (agent.steeringTarget - transform.position);

		//Check if direction is close to 0, it means we're already on point, if so, return
		if (direction.sqrMagnitude < .01f) return;

		//Keep rotation aligned with the current surface
		direction = Vector3.ProjectOnPlane(direction, transform.up);

		//Set target rotation to the movement direction with the AI current local up
		Quaternion targetRotation = Quaternion.LookRotation(direction, transform.up);

		//Smoothly rotate toward the target rotation
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
	}

	//Set a new target
	public void SetTarget(Transform newTarget) => target = newTarget;
}
