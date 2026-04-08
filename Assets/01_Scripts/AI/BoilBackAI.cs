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
		private WanderingBehaviour wanderingBehaviour;

		private State _state;
		private bool isMoving;
		private float distancePadding = .5f;

		private Transform currentTrackingPlayer;

		protected override void OnEnable()
		{
			base.OnEnable();

			if (detector != null)
				detector._onPlayerSpotted += SpotPlayer;

			if (wanderingBehaviour != null)
			{
				wanderingBehaviour._onMovingChanged += OnWanderingMovingChanged;
				wanderingBehaviour._onTargetChanged += OnWanderingTargetChanged;
			}
		}

		protected override void OnDisable()
		{
			base.OnDisable();

			if (detector != null)
				detector._onPlayerSpotted -= SpotPlayer;

			if (wanderingBehaviour != null)
			{
				wanderingBehaviour._onMovingChanged -= OnWanderingMovingChanged;
				wanderingBehaviour._onTargetChanged -= OnWanderingTargetChanged;
			}
		}

		protected override void Awake()
		{
			base.Awake();

			//Set animator
			animator = GetComponentInChildren<Animator>();
			//Set detector
			detector = GetComponent<Detector>();
			//Set wandering behaviour
			wanderingBehaviour = GetComponent<WanderingBehaviour>();

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
						wanderingBehaviour.StopWandering();
						wanderingBehaviour.StartWandering();
						break;
					}
					target = wanderingBehaviour.currentTarget;
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
					wanderingBehaviour.StopWandering();
					wanderingBehaviour.StartWandering();
					currentTrackingPlayer = null;
					SetTarget(currentTrackingPlayer);
					break;
				case State.FLEEING:
					wanderingBehaviour.StopWandering();
					break;
				case State.HIDING:
					wanderingBehaviour.StopWandering();
					break;
			}
		}

		private void SpotPlayer(Transform player)
		{
			if (_state == State.FLEEING) return;

			currentTrackingPlayer = player;
			SetTarget(currentTrackingPlayer);
			ChangeState(State.FLEEING);
		}

		private void OnWanderingMovingChanged(bool isMoving)
		{
			this.isMoving = isMoving;

			if (!isMoving)
				movementController.SetMovementInput(Vector2.zero);
		}

		private void OnWanderingTargetChanged(Vector3 currentTarget) => target = currentTarget;

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
			if (debugFleeingRange)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(transform.position, fleeingRange);

				Gizmos.color = Color.cyan;
				Gizmos.DrawWireSphere(transform.position, relaxingRange);
			}

			if (debugHidingRange)
			{
				Gizmos.color = Color.blue;
				Gizmos.DrawWireSphere(transform.position, hidingRange);
			}
		}
	}
}
