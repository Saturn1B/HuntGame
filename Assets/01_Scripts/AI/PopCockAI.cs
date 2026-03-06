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
			PATROLLING,
			CHASING
		}

		[Space]

		[Header("AI Timer Settings")]
		[SerializeField] private float timeMinLimit;
		[SerializeField] private float timeMaxLimit;

		[Space]

		[Header("AI Wandering Settings")]
		[SerializeField] private float minWalkingRange;
		[SerializeField] private float maxWalkingRange;
		[SerializeField] private bool rangeAroundPoint;
		[SerializeField, ShowIf("rangeAroundPoint")] private Vector3 rangeCenterPoint;

		[Space]

		[Header("AI Animation Settings")]
		[SerializeField] private Animator animator;

		private State _state;
		private float timer;
		private bool isChasing;

		protected override void Awake()
		{
			base.Awake();

			//Set animator if not already set
			if (animator == null)
				animator = GetComponentInChildren<Animator>();

			//Set starting state to IDLE and start timer
			ChangeState(State.IDLE);
			timer = Random.Range(timeMinLimit, timeMaxLimit);
		}

		protected override void Update()
		{
			if (timer <= 0)
			{
				timer = Random.Range(timeMinLimit, timeMaxLimit);
				if (!isChasing)
					ChangeState(_state == State.IDLE ? State.PATROLLING : State.IDLE);
			}
			else if (!isChasing)
				timer -= Time.deltaTime;

			switch (_state)
			{
				case State.IDLE:
					if (animator.GetBool("isMoving"))
						animator.SetBool("isMoving", false);
					break;
				case State.PATROLLING:
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
					break;
			}
		}

		private void ChangeState(State newState)
		{
			isChasing = false;

			_state = newState;

			switch (_state)
			{
				case State.IDLE:
					StopAllCoroutines();
					animator.SetBool("isMoving", false);
					movementController.SetMovementInput(Vector2.zero);
					break;
				case State.PATROLLING:
					timer += 2;
					StartCoroutine(FindNewWanderingTarget());
					break;
				case State.CHASING:
					animator.SetBool("isMoving", true);
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

		//EDITOR

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.red;
			Vector3 rangeCenter = transform.position;
			if (rangeAroundPoint) rangeCenter = rangeCenterPoint;
			Gizmos.DrawWireSphere(rangeCenter, minWalkingRange);
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(rangeCenter, maxWalkingRange);
		}
	}
}
