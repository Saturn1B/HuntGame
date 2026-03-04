using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;

public class PopCockAI : AIInputHandler
{
    private enum State
	{
		IDLE,
		PATROLING,
		CHASING
	}

	[Space]

	[SerializeField] private float timeMinLimit;
	[SerializeField] private float timeMaxLimit;

	[Space]

	[SerializeField] private float minWalkingRange;
	[SerializeField] private float maxWalkingRange;
	[SerializeField] private bool rangeAroundPoint;
	[SerializeField, ShowIf("rangeAroundPoint")] private Vector3 rangeCenterPoint;

	[Space]

	[SerializeField] private Animator animator;

	private State _state;
	private float timer;
	private bool isChasing;

	protected override void Awake()
	{
		base.Awake();

		ChangeState(State.IDLE);
		timer = Random.Range(timeMinLimit, timeMaxLimit);
	}

	protected override void Update()
	{
		if (timer <= 0)
		{
			timer = Random.Range(timeMinLimit, timeMaxLimit);
			if (!isChasing)
				ChangeState(_state == State.IDLE ? State.PATROLING : State.IDLE);
		}
		else if (!isChasing)
			timer -= Time.deltaTime;

		switch (_state)
		{
			case State.IDLE:
				if (animator.GetBool("isMoving"))
					animator?.SetBool("isMoving", false);
				break;
			case State.PATROLING:
				if (animator.GetBool("isMoving"))
				{
					if (Vector3.Distance(transform.position, target) <= stoppingDistance)
					{
						StopAllCoroutines();
						StartCoroutine(FindNewWanderingTarget());
						break;
					}

					MoveTowardsTarget();
					FaceMovementDirection();
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
				animator?.SetBool("isMoving", false);
				movementController.SetMovementInput(Vector2.zero);
				break;
			case State.PATROLING:
				timer += 2;
				StartCoroutine(FindNewWanderingTarget());
				break;
			case State.CHASING:
				animator?.SetBool("isMoving", true);
				break;
		}
	}

	private IEnumerator FindNewWanderingTarget()
	{
		animator?.SetBool("isMoving", false);
		movementController.SetMovementInput(Vector2.zero);

		float waitTime = Random.Range(1, 3);
		if (timer < waitTime + 2) yield break;
		yield return new WaitForSeconds(waitTime);

		Vector3 newTarget = Random.insideUnitSphere * maxWalkingRange;
		KeepTargetInRange(newTarget);
		newTarget = transform.position + newTarget;
		newTarget.y = 0;
		target = newTarget;

		animator?.SetBool("isMoving", true);
	}

	private void KeepTargetInRange(Vector3 newTarget)
	{
		if(Mathf.Abs(newTarget.x) < minWalkingRange)
		{
			if (newTarget.x < 0) newTarget.x = -minWalkingRange;
			else if (newTarget.x >= 0) newTarget.x = minWalkingRange;
		}

		if (Mathf.Abs(newTarget.z) < minWalkingRange)
		{
			if (newTarget.z < 0) newTarget.z = -minWalkingRange;
			else if (newTarget.z >= 0) newTarget.z = minWalkingRange;
		}
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
