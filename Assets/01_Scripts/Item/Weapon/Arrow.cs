using UnityEngine;

namespace HuntingGame.Item
{
    public class Arrow : MonoBehaviour
    {
		[SerializeField] private OrientationSettings orientation;

		private Rigidbody rb;
		private BoxCollider boxCollider;

		private bool isShot;

		private void Awake()
		{
			rb = GetComponent<Rigidbody>();
			boxCollider = GetComponent<BoxCollider>();
		}

		private void Start()
		{
			boxCollider.enabled = false;
			rb.isKinematic = true;
			isShot = false;
		}

		public void Shoot(float shootForce)
		{
			transform.SetParent(null);

			boxCollider.enabled = true;

			rb.isKinematic = false;
			rb.AddForce(transform.up * shootForce, ForceMode.Impulse);

			isShot = true;
		}

		private void FixedUpdate()
		{
			if (!isShot) return;

			if (rb.linearVelocity.magnitude > .1f)
			{
				Quaternion targetRotation = RotationUtils.GetCorrectedLookRotation(rb.linearVelocity.normalized, Vector3.up, orientation);

				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 10);
			}
		}

		private void OnCollisionEnter(Collision collision)
		{
			isShot = false;

			rb.isKinematic = true;
			rb.linearVelocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;

			boxCollider.isTrigger = true;
		}
	}
}
