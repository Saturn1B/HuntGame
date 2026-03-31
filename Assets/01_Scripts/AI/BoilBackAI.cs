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

		private Animator animator;
		private Detector detector;

		private State _state;
		public bool isMoving;
		private float distancePadding = .1f;

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
					break;
				case State.HIDING:
					break;
			}
		}

		private void ChangeState(State newState)
		{
			_state = newState;

			switch (_state)
			{
				case State.WANDERING:
					StartCoroutine(FindNewWanderingTarget());
					break;
				case State.FLEEING:
					break;
				case State.HIDING:
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
			target = newTarget;

			isMoving = true;
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
			//if (_state == State.FLEEING) return;

			//SetTarget(player);
			//ChangeState(State.FLEEING);
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
		}
	}
}
