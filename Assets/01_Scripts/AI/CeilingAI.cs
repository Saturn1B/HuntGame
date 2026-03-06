using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace HuntingGame.AI
{
	public class CeilingAI : AIInputHandler
	{
		[Header("AI Surface Settings")]
		[SerializeField] private string groundAreaName;
		[SerializeField] private string ceilingAreaName;

		[Space]

		[Header("AI Distance Settings")]
		[SerializeField] private float switchToGroundDistance;
		[SerializeField] private float surfaceCheckDistance;

		private int groundMask;
		private int ceilingMask;

		private bool isOnCeiling;
		private bool isTransitioning;

		protected override void Awake()
		{
			base.Awake();

			//Find ground and ceiling mask
			groundMask = 1 << NavMesh.GetAreaFromName(groundAreaName);
			ceilingMask = 1 << NavMesh.GetAreaFromName(ceilingAreaName);

			//Set starting agent area mask to ground
			agent.areaMask = groundMask;
		}

		protected override void Update()
		{
			//If no target, return
			if (target == null) return;

			//If is not transitionning, do base update
			if (!isTransitioning)
				base.Update();

			//Handle the surface switching logic
			HandleSurfaceLogic();
		}

		private void HandleSurfaceLogic()
		{
			//Check if is transitionning
			if (isTransitioning)
			{
				//Check if is grounded, if yes, stop transitionning
				if (movementController.IsGroundedLocal())
					isTransitioning = false;
				//In all cases, return
				return;
			}

			//Calculate distance between AI and target
			float distanceToTarget = Vector3.Distance(transform.position, targetTransform == null ? target : targetTransform.position);

			//If is not on the ceiling, and is far from target and has a ceiling above, then transition to ceiling walk
			if (!isOnCeiling && distanceToTarget > switchToGroundDistance && HasCeilingAbove())
				StartCoroutine(TransitionToCeiling());

			//If is on ceiling and is close to target, then transition to ground walk
			if (isOnCeiling && distanceToTarget <= switchToGroundDistance)
				StartCoroutine(TransitionToGround());
		}

		private bool HasCeilingAbove()
		{
			//Raycast up to check for ceiling presence
			return Physics.Raycast(transform.position, Vector3.up, surfaceCheckDistance);
		}

		private IEnumerator TransitionToCeiling()
		{
			//Start transitionning
			isTransitioning = true;

			//Check for ceiling, if none found return
			RaycastHit hit;
			if (!Physics.Raycast(transform.position, Vector3.up, out hit, 20f))
			{
				Debug.LogWarning("No ceiling collider found !");
				isTransitioning = false;
				yield break;
			}

			//Flip character then wait for it to be grounded
			FlipCharacter(true);
			yield return new WaitUntil(() => movementController.IsGroundedLocal());

			//Check for ceiling navmesh presence, if found change area mask to ceiling mask and set on ceiling
			NavMeshHit navHit;
			if (NavMesh.SamplePosition(transform.position, out navHit, 10f, ceilingMask))
			{
				agent.areaMask = ceilingMask;
				isOnCeiling = true;
			}
			else
				Debug.LogWarning("Ceiling collider found but no NavMesh baked there!");

			//Stop transitionning
			isTransitioning = false;
		}

		private IEnumerator TransitionToGround()
		{
			//Start transitionning
			isTransitioning = true;

			//Flip character then wait for it to be grounded
			FlipCharacter(false);
			yield return new WaitUntil(() => movementController.IsGroundedLocal());

			//Check for ground navmesh presence, if found change area mask to ground mask and set off ceiling
			NavMeshHit hit;
			if (NavMesh.SamplePosition(transform.position, out hit, 10f, groundMask))
			{
				agent.areaMask = groundMask;
				isOnCeiling = false;
			}
			else
				Debug.LogWarning("No ground NavMesh found near position!");

			//Stop transitionning
			isTransitioning = false;
		}

		private void FlipCharacter(bool toCeiling)
		{
			//Find forward vector
			Vector3 forward = transform.forward;

			//Rotate toward the forward direction with the current up depending on wether is on ground or on ceiling
			if (toCeiling)
				transform.rotation = Quaternion.LookRotation(forward, -Vector3.up);
			else
				transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

			//Reset Y velocity to 0
			movementController.ResetVerticalVelocity();
		}

		protected override void MoveTowardsTarget()
		{
			//If not grounded, set movement to 0
			if (!movementController.IsGroundedLocal())
			{
				movementController.SetMovementInput(Vector2.zero);
				return;
			}

			//Check if agent is enabled and on mesh, if not, return
			if (!agent.enabled || !agent.isOnNavMesh) return;

			base.MoveTowardsTarget();
		}
	}
}