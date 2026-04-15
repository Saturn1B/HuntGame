using UnityEngine;
using Sirenix.OdinInspector;

namespace HuntingGame
{
    public class CenterAim : MonoBehaviour
    {
		[Header("Settings")]
        [SerializeField] float maxAimDistance = 100f;
        [SerializeField] float aimSpeed = 20f;

		[Space]

		[Header("Axis Alignment")]
		[SerializeField] private OrientationSettings orientation;

		private bool isFirstFrame = true;

		private void Update()
		{
			Vector3 targetPoint;

			Ray ray = Camera.main.ViewportPointToRay(new Vector3(.5f, .5f, 0));
			RaycastHit hit;

			if (Physics.Raycast(ray, out hit, maxAimDistance))
				targetPoint = hit.point;
			else
				targetPoint = ray.GetPoint(maxAimDistance);

			Vector3 aimDirection = (targetPoint - transform.position).normalized;

			Quaternion targetRotation = RotationUtils.GetCorrectedLookRotation(aimDirection, Camera.main.transform.up, orientation);

			if (isFirstFrame)
			{
				transform.rotation = targetRotation;
				isFirstFrame = false;
			}
			else
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * aimSpeed);
		}
	}
}
