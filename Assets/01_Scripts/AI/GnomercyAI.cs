using UnityEngine;
using HuntingGame.Inventory;
using HuntingGame.Item;
using System.Collections;
using UnityEngine.AI;

namespace HuntingGame.AI
{
    public class GnomercyAI : CeilingAI
    {
        private enum State
        {
            WANDERING,
            STEALING,
            FLEEING
        }

		[Header("AI Stealing Settings")]
		[SerializeField] private bool debugStealingRange;
		[SerializeField] private float droppingPlayerRange;
		[SerializeField] private float stealingPlayerRange;
		[SerializeField] private Transform stealingObjectTransform;

		[Header("AI Fleeing Settings")]
		[SerializeField] private bool debugFleeingRange;
		[SerializeField] private float fleeingRange;

		private Animator animator;
        private Detector detector;
        private WanderingBehaviour wanderingBehaviour;

		private bool _isMoving;
		[HideInInspector]
		public bool isMoving
		{
			get => _isMoving;
			private set
			{
				_isMoving = value;
				if (animator != null) animator.SetBool("isMoving", _isMoving);
			}
		}

		private bool _stealingDone;
		[HideInInspector]
		public bool stealingDone
		{
			get => _stealingDone;
			private set
			{
				_stealingDone = value;
				if (animator != null) animator.SetBool("hasLoot", _stealingDone);
			}
		}

		private State _state;
		private float distancePadding = .75f;
		private PlayerInventory targetPlayerInventory;
		private Transform currentTrackingPlayer;
		private ItemScriptable stoledItem;

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
		}

		private void Start()
		{
			stealingDone = false;
			SetSurfaceInstant(true);
			//Set starting state to IDLE and start timer
			ChangeState(State.WANDERING);
		}

		protected override void Update()
		{
			if (isRagdoll) return;

			if (isTransitioning)
			{
				if (movementController.IsGroundedLocal())
					isTransitioning = false;
				return;
			}


			switch (_state)
			{
				case State.WANDERING:
					if (!isMoving) break;
					if (Vector3.Distance(transform.position, target) < stoppingDistance + distancePadding)
					{
						wanderingBehaviour.StopWandering();
						wanderingBehaviour.StartWandering(agent.areaMask);
						break;
					}
					target = wanderingBehaviour.currentTarget;
					base.Update();
					break;
				case State.STEALING:

					if (!IsItemStillAvailable())
					{
						ChangeState(State.WANDERING);
						break;
					}

					float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);
					if (distanceToTarget < droppingPlayerRange && isOnCeiling)
					{
						if (!IsItemStillAvailable())
						{
							ChangeState(State.WANDERING);
							break;
						}

						if (isOnCeiling && !isTransitioning)
							StartCoroutine(TransitionToGround());
						break;
					}
					if (distanceToTarget < stealingPlayerRange && !stealingDone)
					{
						if (!IsItemStillAvailable())
						{
							ChangeState(State.WANDERING);
							break;
						}
						stealingDone = true;
						targetPlayerInventory.RemoveItemOfType(stoledItem);
						GameObject obj = Instantiate(stoledItem.itemVisualPrefab != null ? stoledItem.itemVisualPrefab : stoledItem.itemPrefab, stealingObjectTransform);
						obj.transform.localRotation = stoledItem.offset.rotation;
						ChangeState(State.FLEEING);
					}

					base.Update();
					break;
				case State.FLEEING:
					KeepDistance();
					break;
			}
		}

		private void ChangeState(State newState)
		{
			_state = newState;

			switch (_state)
			{
				case State.WANDERING:
					SetTarget(null);
					ToggleSprint(false);
					if (!isOnCeiling && !stealingDone) SetSurfaceInstant(true);
					else if (stealingDone && isOnCeiling) SetSurfaceInstant(false);
					wanderingBehaviour.StopWandering();
					wanderingBehaviour.StartWandering(agent.areaMask);
					break;
				case State.STEALING:
					ToggleSprint(false);
					wanderingBehaviour.StopWandering();
					isMoving = true;
					break;
				case State.FLEEING:
					ToggleSprint(true);
					wanderingBehaviour.StopWandering();
					break;
			}
		}

		private void SpotPlayer(Transform player)
		{
			if (_state != State.WANDERING) return;

			if (stealingDone)
			{
				currentTrackingPlayer = player;
				SetTarget(player);

				ChangeState(State.FLEEING);
				return;
			}

			if (player.TryGetComponent(out targetPlayerInventory))
			{
				stoledItem = targetPlayerInventory.GetRandomItem();
				if (stoledItem == null)
					return;
			}
			else
				return;

			currentTrackingPlayer = player;
			SetTarget(currentTrackingPlayer);
			ChangeState(State.STEALING);
		}

		private void KeepDistance()
		{
			if (targetTransform == null) return;

			float distanceToPlayer = Vector3.Distance(transform.position, targetTransform.position);

			if (distanceToPlayer < fleeingRange)
			{
				Vector3 fleeDirection = (transform.position - targetTransform.position).normalized;
				Vector3 fleePoint = transform.position + fleeDirection * 5;

				NavMeshHit hit;
				if (NavMesh.SamplePosition(fleePoint, out hit, 5, NavMesh.AllAreas))
				{
					isMoving = true;
					target = hit.position;
					ToggleSprint(true);
					MoveTowardsSetTarget(target);
					FaceMovementDirection();
				}
			}
			else
			{
				isMoving = false;
				ToggleSprint(false);
				movementController.SetMovementInput(Vector2.zero);
				currentTrackingPlayer = null;
				ChangeState(State.WANDERING);
			}
		}

		private bool IsItemStillAvailable()
		{
			if (targetPlayerInventory.GetNumberItemOfType(stoledItem) == 0)
			{
				stoledItem = targetPlayerInventory.GetRandomItem();
				if (stoledItem == null)
				{
					return false;
				}
			}

			return true;
		}

		private void OnWanderingMovingChanged(bool isMoving)
		{
			this.isMoving = isMoving;

			if (!isMoving)
				movementController.SetMovementInput(Vector2.zero);
		}

		private void OnWanderingTargetChanged(Vector3 currentTarget) => target = currentTarget;

		//EDITOR

		private void OnDrawGizmosSelected()
		{
			if (debugStealingRange)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(transform.position, droppingPlayerRange);

				Gizmos.color = new Color(1, .5f, 0, 1);
				Gizmos.DrawWireSphere(transform.position, stealingPlayerRange);
			}

			if (debugFleeingRange)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(transform.position, fleeingRange);
			}
		}
	}
}
