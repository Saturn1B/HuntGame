using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Sirenix.OdinInspector;
using System;

namespace HuntingGame.AI
{
    public class WanderingBehaviour : MonoBehaviour
    {
		[Header("AI Wandering Settings")]
		[SerializeField] private bool debugWanderingRange;
		[SerializeField] private float minWalkingRange;
		[SerializeField] private float maxWalkingRange;
		[SerializeField] private bool rangeAroundPoint;
		[SerializeField, ShowIf("rangeAroundPoint")] private Vector3 rangeCenterPoint;

		public event Action<bool> _onMovingChanged;
		public event Action<Vector3> _onTargetChanged;

		private bool _isMoving;
		public bool isMoving
		{
			get => _isMoving;
			private set
			{
				_isMoving = value;
				_onMovingChanged?.Invoke(_isMoving);
			}
		}
		private Vector3 _currentTarget;
		public Vector3 currentTarget
		{
			get => _currentTarget;
			private set
			{
				_currentTarget = value;
				_onTargetChanged?.Invoke(_currentTarget);
			}
		}

		public void StartWandering(int mask = NavMesh.AllAreas) => StartCoroutine(FindNewTarget(true, mask));
		public void StopWandering(bool keepMoving = false)
		{
			Debug.Log($"is moving state before stop wandering: {isMoving}");
			StopAllCoroutines();
			if(!keepMoving)
				isMoving = false;
		}

		private IEnumerator FindNewTarget(bool wait = true, int mask = NavMesh.AllAreas)
		{
			isMoving = false;

			if (wait)
			{
				float waitTime = UnityEngine.Random.Range(1, 3);
				yield return new WaitForSeconds(waitTime);
			}

			Vector3 newTarget = UnityEngine.Random.insideUnitSphere * maxWalkingRange;
			newTarget = KeepTargetInRange(newTarget);
			newTarget = rangeAroundPoint ? rangeCenterPoint + newTarget : transform.position + newTarget;
			newTarget.y = transform.position.y;

			NavMeshHit hit;

			if (NavMesh.SamplePosition(newTarget, out hit, maxWalkingRange, mask))
			{
				currentTarget = hit.position;

				isMoving = true;
			}
			else
				StartCoroutine(FindNewTarget(false));
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
