using UnityEngine;
using Sirenix.OdinInspector;
using DungeonSteakhouse.Net.Players;

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
			// Camera.main is unreliable in additive multiplayer scenes right after a scene load
			// (stale/null until NetLocalCameraEnforcer re-tags the owner's camera); prefer the
			// enforcer's known-good local camera when one is available (falls back to Camera.main
			// for non-networked/singleplayer scenes).
			Camera cam = NetLocalCameraEnforcer.LocalCamera != null ? NetLocalCameraEnforcer.LocalCamera : Camera.main;
			if (cam == null) return;

			Vector3 targetPoint;

			Ray ray = cam.ViewportPointToRay(new Vector3(.5f, .5f, 0));
			RaycastHit hit;

			if (Physics.Raycast(ray, out hit, maxAimDistance))
				targetPoint = hit.point;
			else
				targetPoint = ray.GetPoint(maxAimDistance);

			Vector3 aimDirection = (targetPoint - transform.position).normalized;

			Quaternion targetRotation = RotationUtils.GetCorrectedLookRotation(aimDirection, cam.transform.up, orientation);

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
