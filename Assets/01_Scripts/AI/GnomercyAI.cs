using UnityEngine;
using HuntingGame.Inventory;
using HuntingGame.Item;

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

		private Animator animator;
        private Detector detector;
        private WanderingBehaviour wanderingBehaviour;

		private State _state;
		private bool isMoving;
		private float distancePadding = .5f;
		private PlayerInventory targetPlayerInventory;
		private ItemScriptable stoledItem;
		private bool stealingDone;

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
						//TO DO fix wandering for when on ceiling
						//wanderingBehaviour.StartWandering();
						break;
					}
					target = wanderingBehaviour.currentTarget;
					base.Update();
					break;
				case State.STEALING:
					//TO DO go to player -> drop to floor -> steal planned object -> go into fleeing
					if (!isMoving) break;

					float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);
					if (distanceToTarget < droppingPlayerRange)
					{
						if (isOnCeiling)
							StartCoroutine(TransitionToGround());
						break;
					}

					if (distanceToTarget < stealingPlayerRange && !isOnCeiling && !stealingDone)
					{
						targetPlayerInventory.RemoveItemOfType(stoledItem);
						ChangeState(State.FLEEING);
					}

					break;
				case State.FLEEING:

					break;
			}
		}

		private void ChangeState(State newState)
		{
			_state = newState;

			switch (_state)
			{
				case State.WANDERING:
					ToggleSprint(false);
					if (!isOnCeiling) StartCoroutine(TransitionToCeiling());
					wanderingBehaviour.StopWandering();
					//wanderingBehaviour.StartWandering();
					break;
				case State.STEALING:
					ToggleSprint(false);
					wanderingBehaviour.StopWandering();
					break;
				case State.FLEEING:
					ToggleSprint(true);
					wanderingBehaviour.StopWandering();
					break;
			}
		}

		private void SpotPlayer(Transform player)
		{
			if (_state == State.WANDERING) return;

			if (player.TryGetComponent(out targetPlayerInventory))
			{
				stoledItem = targetPlayerInventory.GetRandomItem();
				if (stoledItem == null)
					return;
			}
			else
				return;

			SetTarget(player);
			ChangeState(State.STEALING);
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

		}
	}
}
