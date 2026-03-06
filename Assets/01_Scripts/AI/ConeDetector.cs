using UnityEngine;
using System;
using System.Collections;

namespace HuntingGame.AI
{
    public class ConeDetector : Detector
    {
        [Header("Cone Shape Settings")]
        [SerializeField] private float viewRadius = 10f;
        [SerializeField] private float viewAngle = 90f;

        protected override void Detect()
		{
            Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

            Transform bestCandidate = null;

			foreach (var hit in hits)
			{
                Transform target = hit.transform;

                Vector3 dirToTarget = (target.position - transform.position).normalized;

                float angleToTarget = Vector3.Angle(transform.forward, dirToTarget);
                if (angleToTarget > viewAngle * .5f) continue;

                float distToTarget = Vector3.Distance(transform.position, target.position);
                if (Physics.Raycast(transform.position, dirToTarget, out RaycastHit hitInfo, distToTarget, obstacleMask)) continue;

                bestCandidate = target;
                break;
			}

            //Find Target
            if(currentTarget == null && bestCandidate != null)
			{
                currentTarget = bestCandidate;
                _onPlayerSpotted?.Invoke(currentTarget);
			}
            //Loose Target
            else if(currentTarget != null && bestCandidate == null)
			{
                Transform lostTarget = currentTarget;
                currentTarget = null;
                _onPlayerLost?.Invoke(lostTarget);
			}
            //Switch target
            else if(currentTarget != null && bestCandidate != currentTarget)
			{
                Transform lostTarget = currentTarget;
                currentTarget = bestCandidate;
                _onPlayerLost?.Invoke(lostTarget);
                _onPlayerSpotted?.Invoke(currentTarget);
            }
        }

		//EDITOR

		private void OnDrawGizmosSelected()
		{
            Vector3 origin = transform.position;

            //Gizmos.color = Color.yellow;
            //Gizmos.DrawWireSphere(origin, viewRadius);

            int stepCount = 16;
            float stepAngleSize = viewAngle / stepCount;

            Gizmos.color = new Color(1, .5f, 0, 1);
			for (int i = 0; i <= stepCount; i++)
			{
                float angle = -viewAngle * .5f + stepAngleSize * i;
                Vector3 dir = DirFromAngle(angle, false);
                Gizmos.DrawLine(origin, origin + dir * viewRadius);
			}

            Gizmos.color = Color.red;
            Vector3 leftBoundaryDir = DirFromAngle(-viewAngle * .5f, false);
            Vector3 rightBoundaryDir = DirFromAngle(viewAngle * .5f, false);

            Gizmos.DrawLine(origin, origin + leftBoundaryDir * viewRadius);
            Gizmos.DrawLine(origin, origin + rightBoundaryDir * viewRadius);
        }

        private Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
		{
            if (!angleIsGlobal)
                angleInDegrees += transform.eulerAngles.y;

            float rad = angleInDegrees * Mathf.Deg2Rad;

            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
		}
	}
}
