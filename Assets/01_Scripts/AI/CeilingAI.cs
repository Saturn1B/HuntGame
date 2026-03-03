using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class CeilingAI : AIInputHandler
{
	[Header("Surface Settings")]
	[SerializeField] private string groundAreaName;
	[SerializeField] private string ceilingAreaName;

	[Space]

	[SerializeField] private float switchToGroundDistance;
	[SerializeField] private float surfaceCheckDistance;

	private int groundMask;
	private int ceilingMask;

	private bool isOnCeiling;
	private bool isTransitioning;

	protected override void Awake()
	{
		base.Awake();

		groundMask = 1 << NavMesh.GetAreaFromName(groundAreaName);
		ceilingMask = 1 << NavMesh.GetAreaFromName(ceilingAreaName);

		agent.areaMask = groundMask;
	}

	protected override void Update()
	{
		if (target == null) return;

		if (!isTransitioning)
			base.Update();

		HandleSurfaceLogic();
	}

	private void HandleSurfaceLogic()
	{
		if (isTransitioning)
		{
			if (movementController.IsGroundedLocal())
			{
				isTransitioning = false;
				agent.enabled = true;
			}
			return;
		}

		float distanceToTarget = Vector3.Distance(transform.position, target.position);

		if (!isOnCeiling && distanceToTarget > switchToGroundDistance && HasCeilingAbove())
		{
			StartCoroutine(TransitionToCeiling());
		}

		if(isOnCeiling && distanceToTarget <= switchToGroundDistance)
		{
			StartCoroutine(TransitionToGround());
		}
	}

	private bool HasCeilingAbove()
	{
		return Physics.Raycast(transform.position, Vector3.up, surfaceCheckDistance);
	}

	private IEnumerator TransitionToCeiling()
	{
		isTransitioning = true;
		agent.enabled = false;

		RaycastHit hit;
		if(!Physics.Raycast(transform.position, Vector3.up, out hit, 20f))
		{
			Debug.LogWarning("No ceiling collider found !");
			isTransitioning = false;
			yield break;
		}

		Vector3 ceilingPoint = hit.point + hit.normal * .1f;
		transform.position = ceilingPoint;

		FlipCharacter(true);

		yield return null;

		NavMeshHit navHit;
		if (NavMesh.SamplePosition(transform.position, out navHit, 10f, ceilingMask))
		{
			agent.Warp(navHit.position);
			agent.areaMask = ceilingMask;
			agent.enabled = true;
			isOnCeiling = true;
		}
		else
		{
			Debug.LogWarning("Ceiling collider found but no NavMesh baked there!");
		}

		isTransitioning = false;
	}

	private IEnumerator TransitionToGround()
	{
		isTransitioning = true;
		agent.enabled = false;

		FlipCharacter(false);

		yield return new WaitUntil(() => movementController.IsGroundedLocal());

		NavMeshHit hit;
		if(NavMesh.SamplePosition(transform.position, out hit, 10f, groundMask))
		{
			agent.Warp(hit.position);
			agent.areaMask = groundMask;
			agent.enabled = true;
			isOnCeiling = false;
		}
		else
		{
			Debug.LogWarning("No ground NavMesh found near position!");
		}

		isTransitioning = false;
	}

	private void FlipCharacter(bool toCeiling)
	{
		Vector3 forward = transform.forward;

		if (toCeiling)
			transform.rotation = Quaternion.LookRotation(forward, -Vector3.up);
		else
			transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

		movementController.ResetVerticalVelocity();
	}

	protected override void MoveTowardsTarget()
	{
		if (!movementController.IsGroundedLocal())
		{
			movementController.SetMovementInput(Vector2.zero);
			return;
		}

		if (!agent.enabled || !agent.isOnNavMesh) return;

		base.MoveTowardsTarget();
	}
}