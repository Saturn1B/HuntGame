using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Sirenix.OdinInspector;

namespace HuntingGame.AI
{
	public class BoilBackAI : AIInputHandler
	{
		private enum State
		{
			WANDERING,
			FLEEING,
			HIDING
		}

		[Space]

		[Header("AI Wandering Settings")]
		[SerializeField] private bool debugWanderingRange;
		[SerializeField] private float minWalkingRange;
		[SerializeField] private float maxWalkingRange;
		[SerializeField] private bool rangeAroundPoint;
		[SerializeField, ShowIf("rangeAroundPoint")] private Vector3 rangeCenterPoint;

		[Space]

		[Header("AI Fleeing Settings")]
		[SerializeField] private bool debugFleeingRange;
		[SerializeField] private float fleeingRange;
		[SerializeField] private float relaxingRange;

		[Space]

		[Header("AI `Hiding Settings")]
		[SerializeField] private bool debugHidingRange;
		[SerializeField] private float hidingRange;


		private Animator animator;
		private Detector detector;

		private State _state;
		private bool isMoving;
		private float distancePadding = .1f;

		private Transform currentTrackingPlayer;

		private void OnEnable()
		{
			if (detector != null)
				detector._onPlayerSpotted += SpotPlayer;
		}

		private void OnDisable()
		{
			if (detector != null)
				detector._onPlayerSpotted -= SpotPlayer;
		}

		protected override void Awake()
		{
			base.Awake();

			//Set animator
			animator = GetComponentInChildren<Animator>();
			//Set detector
			detector = GetComponent<Detector>();

			//Set starting state to IDLE and start timer
			ChangeState(State.WANDERING);
		}

		protected override void Update()
		{
			if (isRagdoll) return;

			switch (_state)
			{
				case State.WANDERING:
					if (!isMoving) break;
					if (Vector3.Distance(transform.position, target) < stoppingDistance + distancePadding)
					{
						StopAllCoroutines();
						StartCoroutine(FindNewWanderingTarget());
						Debug.Log("Find new target");
						break;
					}
					base.Update();
					break;
				case State.FLEEING:
					float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);
					if (distanceToTarget >= relaxingRange)
					{
						ChangeState(State.WANDERING);
						break;
					}
					if(distanceToTarget < hidingRange)
					{
						ChangeState(State.HIDING);
						break;
					}
					FaceTarget();
					KeepDistance();
					break;
				case State.HIDING:
					if (Vector3.Distance(transform.position, targetTransform.position) >= hidingRange)
					{
						SetTarget(currentTrackingPlayer);
						ChangeState(State.FLEEING);
						break;
					}
					break;
			}
		}

		private void ChangeState(State newState)
		{
			_state = newState;

			switch (_state)
			{
				case State.WANDERING:
					StopAllCoroutines();
					StartCoroutine(FindNewWanderingTarget());
					currentTrackingPlayer = null;
					SetTarget(currentTrackingPlayer);
					break;
				case State.FLEEING:
					StopAllCoroutines();
					isMoving = false;
					movementController.SetMovementInput(Vector2.zero);
					break;
				case State.HIDING:
					StopAllCoroutines();
					isMoving = false;
					movementController.SetMovementInput(Vector2.zero);
					break;
			}
		}

		private IEnumerator FindNewWanderingTarget()
		{
			isMoving = false;

			movementController.SetMovementInput(Vector2.zero);

			float waitTime = Random.Range(1, 3);

			yield return new WaitForSeconds(waitTime);

			Vector3 newTarget = Random.insideUnitSphere * maxWalkingRange;
			newTarget = KeepTargetInRange(newTarget);
			newTarget = rangeAroundPoint ? rangeCenterPoint + newTarget : transform.position + newTarget;
			newTarget.y = 0;

			NavMeshHit hit;

			if (NavMesh.SamplePosition(newTarget, out hit, maxWalkingRange, NavMesh.AllAreas))
			{
				target = hit.position;
				isMoving = true;
			}
			else
				StartCoroutine(FindNewWanderingTarget());
		}

		private Vector3 KeepTargetInRange(Vector3 newTarget)
		{
			if (Mathf.Abs(newTarget.x) < minWalkingRange)
			{
				if (newTarget.x < 0) newTarget.x = -minWalkingRange;
				else if (newTarget.x >= 0) newTarget.x = minWalkingRange;
			}

			if (Mathf.Abs(newTarget.z) < minWalkingRange)
			{
				if (newTarget.z < 0) newTarget.z = -minWalkingRange;
				else if (newTarget.z >= 0) newTarget.z = minWalkingRange;
			}

			return newTarget;
		}

		private void SpotPlayer(Transform player)
		{
			if (_state == State.FLEEING) return;

			currentTrackingPlayer = player;
			SetTarget(currentTrackingPlayer);
			ChangeState(State.FLEEING);
		}

		private void KeepDistance()
		{
			if (targetTransform == null) return;

			float distanceToPlayer = Vector3.Distance(transform.position, targetTransform.position);

			if(distanceToPlayer < fleeingRange)
			{
				Vector3 fleeDirection = (transform.position - targetTransform.position).normalized;
				Vector3 fleePoint = transform.position + fleeDirection * 5;

				NavMeshHit hit;
				if(NavMesh.SamplePosition(fleePoint, out hit, 5f, NavMesh.AllAreas))
				{
					isMoving = true;
					target = hit.position;
					ToggleSprint(true);
					MoveTowardsSetTarget(target);
				}
			}
			else
			{
				isMoving = false;
				ToggleSprint(false);
				movementController.SetMovementInput(Vector2.zero);
			}
		}

		//EDITOR

		private void OnDrawGizmosSelected()
		{
			Vector3 rangeCenter = transform.position;

			if (rangeAroundPoint) rangeCenter = rangeCenterPoint;

			if (debugWanderingRange)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(rangeCenter, minWalkingRange);

				Gizmos.color = Color.cyan;
				Gizmos.DrawWireSphere(rangeCenter, maxWalkingRange);
			}

			if (debugFleeingRange)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(rangeCenter, fleeingRange);

				Gizmos.color = Color.cyan;
				Gizmos.DrawWireSphere(rangeCenter, relaxingRange);
			}

			if (debugHidingRange)
			{
				Gizmos.color = Color.blue;
				Gizmos.DrawWireSphere(rangeCenter, hidingRange);
			}
		}
	}
}
