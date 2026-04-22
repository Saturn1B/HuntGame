using UnityEngine;
using System;
using System.Collections;
using Sirenix.OdinInspector;

namespace HuntingGame.AI
{
    public class ConeDetector : Detector
    {
        private enum Shape
		{
            SPHERE,
            CAPSULE
		}

        [Header("Cone Shape Settings")]
        [SerializeField] private float viewRadius = 10f;
        [SerializeField] private float viewAngle = 90f;
        [SerializeField] private Shape _shape = Shape.SPHERE;
        [SerializeField, ShowIf("@_shape == Shape.CAPSULE")] private float capsuleHeight = 5f;

        protected override void Detect()
		{
            Collider[] hits;
			switch (_shape)
			{
				case Shape.SPHERE:
                    hits = Physics.OverlapSphere(transform.position, viewRadius, targetMask);
                    break;
				case Shape.CAPSULE:
                    Vector3 point1 = transform.position;
                    Vector3 point2 = transform.position - (transform.up);
                    hits = Physics.OverlapSphere(transform.position, viewRadius, targetMask);
                    break;
				default:
                    hits = Physics.OverlapSphere(transform.position, viewRadius, targetMask);
                    break;
			}


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
                InvokePlayerSpotted(currentTarget);
			}
            //Loose Target
            else if(currentTarget != null && bestCandidate == null)
			{
                Transform lostTarget = currentTarget;
                currentTarget = null;
                InvokePlayerLost(lostTarget);
			}
            //Switch target
            else if(currentTarget != null && bestCandidate != currentTarget)
			{
                Transform lostTarget = currentTarget;
                currentTarget = bestCandidate;
                InvokePlayerLost(lostTarget);
                InvokePlayerSpotted(currentTarget);
            }
        }

		//EDITOR

		private void OnDrawGizmosSelected()
		{
			switch (_shape)
			{
				case Shape.SPHERE:
                    DisplayAngleFlat(transform.position.y);
                    break;
				case Shape.CAPSULE:
                    int height = Mathf.RoundToInt(capsuleHeight);
					for (int i = 0; i < capsuleHeight; i++)
					{
                        DisplayAngleFlat(transform.position.y + i * transform.up.y);
					}
					break;
				default:
                    DisplayAngleFlat(transform.position.y);
                    break;
			}
        }

        private void DisplayAngleFlat(float yPos)
		{
            Vector3 origin = transform.position;
            origin.y = yPos;

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
