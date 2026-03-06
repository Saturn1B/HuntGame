using UnityEngine;
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

		[Header("AI Wandering Settings")]
		[SerializeField] private bool debugWanderingRange;
		[SerializeField] private float minWalkingRange;
		[SerializeField] private float maxWalkingRange;
		[SerializeField] private bool rangeAroundPoint;
		[SerializeField, ShowIf("rangeAroundPoint")] private Vector3 rangeCenterPoint;

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

		private State _state;
		private float timer;
		private bool explosionTriggered;

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
			ChangeState(State.IDLE);
			timer = Random.Range(timeMinLimit, timeMaxLimit);
		}

		protected override void Update()
		{
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
						if (Vector3.Distance(transform.position, target) <= stoppingDistance)
						{
							StopAllCoroutines();
							StartCoroutine(FindNewWanderingTarget());
							break;
						}

						base.Update();
					}
					break;
				case State.CHASING:
					if (animator.GetBool("isMoving"))
					{
						if (Vector3.Distance(transform.position, targetTransform.position) >= loosingPlayerRange)
						{
							SetTarget(null);
							ToggleSprint(false);
							ChangeState(State.IDLE);
							break;
						}

						if(Vector3.Distance(transform.position, targetTransform.position) <= explosionTriggerRange)
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
					StopAllCoroutines();
					animator.SetBool("isMoving", false);
					movementController.SetMovementInput(Vector2.zero);
					break;
				case State.WANDERING:
					timer += 2;
					StartCoroutine(FindNewWanderingTarget());
					break;
				case State.CHASING:
					StopAllCoroutines();
					ToggleSprint(true);
					break;
			}
		}

		private IEnumerator FindNewWanderingTarget()
		{
			animator.SetBool("isMoving", false);
			movementController.SetMovementInput(Vector2.zero);

			float waitTime = Random.Range(1, 3);

			yield return new WaitForSeconds(waitTime);

			Vector3 newTarget = Random.insideUnitSphere * maxWalkingRange;
			newTarget = KeepTargetInRange(newTarget);
			newTarget = rangeAroundPoint ? rangeCenterPoint + newTarget : transform.position + newTarget;
			newTarget.y = 0;
			target = newTarget;

			animator.SetBool("isMoving", true);
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
			if (_state == State.CHASING || explosionTriggered) return;

			SetTarget(player);
			ChangeState(State.CHASING);
			StartCoroutine(Detect());
		}

		private IEnumerator Detect()
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
			Vector3 rangeCenter = transform.position;

			if (rangeAroundPoint) rangeCenter = rangeCenterPoint;

			if (debugWanderingRange)
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(rangeCenter, minWalkingRange);

				Gizmos.color = Color.cyan;
				Gizmos.DrawWireSphere(rangeCenter, maxWalkingRange);
			}

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
