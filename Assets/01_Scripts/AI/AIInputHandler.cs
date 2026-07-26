using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using HuntGame.Player;

namespace HuntingGame.AI
{
	[RequireComponent(typeof(NavMeshAgent))]
	[RequireComponent(typeof(CharacterMovement))]
	public class AIInputHandler : MonoBehaviour
	{
		[Header("AI General Settings")]
		[SerializeField] protected Vector3 target;
		[SerializeField] protected Transform targetTransform;
		[SerializeField] protected float stoppingDistance = 2;
		[SerializeField] protected float turningSpeed = 4;

		protected Vector3 targetPosition => targetTransform != null ? targetTransform.position : target;

		protected NavMeshAgent agent;
		protected CharacterMovement movementController;
		protected CharacterController characterController;
		protected RagdollController ragdollController;
		protected Health health;
		[SerializeField] protected Collider ragdollCollider;

		protected Action _arrivedAtTarget;

		protected bool isRagdoll;

		protected virtual void Awake()
		{
			//Set character movement
			movementController = GetComponent<CharacterMovement>();

			//Set agent
			agent = GetComponent<NavMeshAgent>();

			//Set character controller
			characterController = GetComponent<CharacterController>();

			//Set ragdoll controller
			ragdollController = GetComponent<RagdollController>();

			//Set health
			health = GetComponent<Health>();

			//Toggle ragdoll collider off at start
			ragdollCollider.enabled = false;

			//Turn off the agent handling of position and rotation, so the movement can all be handled by the character movement
			agent.updatePosition = false;
			agent.updateRotation = false;
			agent.updateUpAxis = false;
		}

		protected virtual void OnEnable()
		{
			if (health != null)
				health._onDeath += Death;
		}

		protected virtual void OnDisable()
		{
			if (health != null)
				health._onDeath -= Death;
		}

		protected virtual void Update()
		{
			//Return if no target found
			if (target == Vector3.zero && targetTransform == null) return;

			//Move toward target and face toward movement direction
			MoveTowardsTarget();
			FaceMovementDirection();
		}

		protected bool stoppedMoving;

		protected virtual void MoveTowardsSetTarget(Vector3 destination)
		{
			//Set the destination to the target
			agent.SetDestination(destination);

			//Check if the remaining distance between AI and target is less than stopping distance, if yes set the movement to 0 and return
			if (agent.remainingDistance <= stoppingDistance)
			{
				//Check if already stopped moving, if not invoke the arrived at target. Security so we only invoke once the action
				if (!stoppedMoving)
					_arrivedAtTarget?.Invoke();

				movementController.SetMovementInput(Vector2.zero);
				stoppedMoving = true;

				return;
			}
			//Turn back to is moving
			stoppedMoving = false;

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
		protected virtual void MoveTowardsTarget()
		{
			MoveTowardsSetTarget(targetPosition);
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
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turningSpeed);
		}

		protected virtual void FaceTarget()
		{
			//Check if agent is enabled and on mesh, if not, return
			if (!agent.enabled || !agent.isOnNavMesh) return;

			//Calculate the movement direction toward target
			Vector3 direction = (targetPosition - transform.position);

			//Check if direction is close to 0, it means we're already on point, if so, return
			if (direction.sqrMagnitude < .01f) return;

			//Keep rotation aligned with the current surface
			direction = Vector3.ProjectOnPlane(direction, transform.up);

			//Set target rotation to the movement direction with the AI current local up
			Quaternion targetRotation = Quaternion.LookRotation(direction, transform.up);

			//Smoothly rotate toward the target rotation
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turningSpeed);
		}

		//Set a new target
		public void SetTarget(Transform newTarget) => targetTransform = newTarget;

		protected virtual void ToggleSprint(bool value) => movementController?.SetSprinting(value);

		[ContextMenu("Toggle Ragdoll")]
		private void ToggleRagdoll() => StartCoroutine(Ragdoll());
		protected virtual IEnumerator Ragdoll()
		{
			agent.enabled = false;
			characterController.enabled = false;
			movementController.enabled = false;
			health.enabled = false;

			isRagdoll = true;

			yield return new WaitForSeconds(.1f);

			ragdollCollider.enabled = true;
			ragdollController.Ragdoll();
			enabled = false;
		}

		protected virtual void Death()
		{
			StartCoroutine(Ragdoll());
		}
	}
}
