using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Sirenix.OdinInspector;

namespace HuntingGame.AI
{
	public class PopCockAI : AIInputHandler
	{
		private enum State
		{
			IDLE,
			WANDERING,
			CHASING
		}

		[Space]

		[Header("AI Timer Settings")]
		[SerializeField] private float timeMinLimit;
		[SerializeField] private float timeMaxLimit;

		[Space]

		[Header("AI Chasing Settings")]
		[SerializeField] private bool debugChasingRange;
		[SerializeField] private float loosingPlayerRange;
		[SerializeField] private float explosionTriggerRange;

		[Header("AI Effect Settings")]
		[SerializeField] private Vector3 explosionSpawn;
		[SerializeField] private GameObject explosionVfxPrefab;
		[SerializeField] private ParticleSystem smokeVfx;
		[SerializeField] private ParticleSystem alertVfx;


		private Animator animator;
		private Detector detector;
		private WanderingBehaviour wanderingBehaviour;

		private State _state;
		private float timer;
		private float distancePadding = .5f;
		private bool explosionTriggered;

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
			ChangeState(State.IDLE);
			timer = Random.Range(timeMinLimit, timeMaxLimit);
		}

		protected override void Update()
		{
			if (isRagdoll) return;

			if (timer <= 0)
			{
				timer = Random.Range(timeMinLimit, timeMaxLimit);
				if (_state != State.CHASING)
					ChangeState(_state == State.IDLE ? State.WANDERING : State.IDLE);
			}
			else if (_state != State.CHASING)
				timer -= Time.deltaTime;

			switch (_state)
			{
				case State.IDLE:
					if (animator.GetBool("isMoving"))
						animator.SetBool("isMoving", false);
					break;
				case State.WANDERING:
					if (animator.GetBool("isMoving"))
					{
						if (Vector3.Distance(transform.position, target) <= stoppingDistance + distancePadding)
						{
							wanderingBehaviour.StopWandering();
							wanderingBehaviour.StartWandering();
							break;
						}
						target = wanderingBehaviour.currentTarget;
						base.Update();
					}
					break;
				case State.CHASING:
					if (animator.GetBool("isMoving"))
					{
						float distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);
						if (distanceToTarget >= loosingPlayerRange)
						{
							SetTarget(null);
							ToggleSprint(false);
							movementController.SetMovementInput(Vector2.zero);
							ChangeState(State.IDLE);
							break;
						}

						if(distanceToTarget <= explosionTriggerRange)
						{
							animator.SetBool("isMoving", false);
							movementController.SetMovementInput(Vector2.zero);
							StartCoroutine(Explose());
						}
						else
							base.Update();
					}
					else
						movementController.SetMovementInput(Vector2.zero);

					break;
			}
		}

		private void ChangeState(State newState)
		{
			_state = newState;

			switch (_state)
			{
				case State.IDLE:
					Debug.Log("Change to iddle");
					ToggleSprint(false);
					wanderingBehaviour.StopWandering();
					break;
				case State.WANDERING:
					timer += 2;
					wanderingBehaviour.StopWandering();
					wanderingBehaviour.StartWandering();
					break;
				case State.CHASING:
					wanderingBehaviour.StopWandering(true);
					ToggleSprint(true);
					break;
			}
		}

		private void SpotPlayer(Transform player)
		{
			if (_state == State.CHASING || explosionTriggered) return;

			SetTarget(player);
			ChangeState(State.CHASING);
			StartCoroutine(DetectPlayer());
		}

		private void OnWanderingMovingChanged(bool isMoving)
		{
			Debug.Log("Change is moving");

			animator.SetBool("isMoving", isMoving);

			if (!isMoving)
				movementController.SetMovementInput(Vector2.zero);
		}

		private void OnWanderingTargetChanged(Vector3 currentTarget) => target = currentTarget;

		private IEnumerator DetectPlayer()
		{
			animator.SetBool("isMoving", false);

			alertVfx.Play();

			yield return new WaitForSeconds(.8f);

			animator.SetBool("isMoving", true);
		}

		private IEnumerator Explose()
		{
			explosionTriggered = true;

			alertVfx.Stop();

			//REPLACE BY EXPLOSION ANIMATION
			yield return new WaitForSeconds(1f);

			ParticleSystem explosionVfx = Instantiate(explosionVfxPrefab, transform.position + explosionSpawn, Quaternion.identity).GetComponent<ParticleSystem>();
			explosionVfx.Play();

			Destroy(gameObject);
		}

		//EDITOR

		private void OnDrawGizmosSelected()
		{
			if (debugChasingRange)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(transform.position, loosingPlayerRange);

				Gizmos.color = new Color(1, .5f, 0, 1);
				Gizmos.DrawWireSphere(transform.position, explosionTriggerRange);
			}
		}
	}
}
