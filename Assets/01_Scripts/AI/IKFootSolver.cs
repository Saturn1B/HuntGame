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

        private Vector3 newPosition;
        private Vector3 currentPosition;
        private Vector3 oldPosition;

        private float lerp;

		private void Start()
		{
            currentPosition = newPosition = oldPosition = transform.position;
            lerp = 1;
		}

		private void Update()
		{
            transform.position = currentPosition;

            Ray ray = new Ray(home.transform.position + Vector3.up, Vector3.down);

            if(Physics.Raycast(ray, out RaycastHit hit, 4))
			{
                float dist = Vector3.Distance(newPosition, hit.point);

                if (dist > stepDistance && lerp >= 1 && !opposedLegA.IsMoving() && !opposedLegB.IsMoving() && !opposedLegC.IsMoving())
				{
                    lerp = 0;

                    Vector3 direction = (hit.point - newPosition).normalized;
                    float overshootDistance = stepDistance * .5f;

                    newPosition = hit.point + direction * stepDistance;
				}
			}

            if(lerp < 1)
			{
                Vector3 footPosition = Vector3.Lerp(oldPosition, newPosition, lerp);
                footPosition.y += Mathf.Sin(lerp * Mathf.PI) * stepHeight;

                currentPosition = footPosition;

                lerp += Time.deltaTime * speed;
			}
			else
			{
                oldPosition = newPosition;
			}
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
