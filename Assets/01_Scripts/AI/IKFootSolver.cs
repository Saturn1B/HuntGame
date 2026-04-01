using UnityEngine;

namespace HuntingGame.AI
{
    public class IKFootSolver : MonoBehaviour
    {
        [Header("Inverse Kinematic Settings")]
        [SerializeField] private Transform mainBody;
        [SerializeField] private Transform home;
        [SerializeField] private float stepDistance;
        [SerializeField] private float stepHeight;
        [SerializeField] private float speed;
        [SerializeField] private IKFootSolver opposedLegA, opposedLegB, opposedLegC;

        [SerializeField] private float panicDistanceMultiplier = 2f;

        private Vector3 newPosition;
        private Vector3 currentPosition;
        private Vector3 oldPosition;

        private Vector3 lastBodyPosition;

        private float lerp;

		private void Start()
		{
            currentPosition = newPosition = oldPosition = transform.position;
            lerp = 1;
		}

		private void Update()
		{
            transform.position = currentPosition;

            float bodyVelocity = (mainBody.position - lastBodyPosition).magnitude / Time.deltaTime;

            Ray ray = new Ray(home.transform.position + Vector3.up, Vector3.down);

            if(Physics.Raycast(ray, out RaycastHit hit, 4))
			{
                float dist = Vector3.Distance(newPosition, hit.point);

                bool isTooFar = dist > stepDistance * panicDistanceMultiplier;
                bool canMoveGait = !opposedLegA.IsMoving() && !opposedLegB.IsMoving() && !opposedLegC.IsMoving();

                if (dist > stepDistance && lerp >= 1 && (canMoveGait || isTooFar))
				{
                    lerp = 0;

                    Vector3 direction = (hit.point - newPosition).normalized;
                    float dynamicOvershoot = bodyVelocity * .1f;
                    dynamicOvershoot = Mathf.Clamp(dynamicOvershoot, 0, stepDistance);

                    newPosition = hit.point + direction * dynamicOvershoot;
				}
			}
            else if(Vector3.Distance(newPosition, home.position) > stepDistance * panicDistanceMultiplier)
                newPosition = home.position;

            if(lerp < 1)
			{
                Vector3 footPosition = Vector3.Lerp(oldPosition, newPosition, lerp);
                footPosition.y += Mathf.Sin(lerp * Mathf.PI) * stepHeight;

                currentPosition = footPosition;

                float currentStepSpeed = speed + (bodyVelocity * .5f);
                lerp += Time.deltaTime * currentStepSpeed;
			}
			else
                oldPosition = newPosition;

            lastBodyPosition = mainBody.position;
        }

        public bool IsMoving()
		{
            return lerp < 1;
		}

		private void OnDrawGizmos()
		{
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(newPosition, .05f);
        }
	}
}
