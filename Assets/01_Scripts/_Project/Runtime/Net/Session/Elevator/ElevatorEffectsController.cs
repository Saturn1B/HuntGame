using System.Collections;
using DungeonSteakhouse.Net.Players;
using UnityEngine;

namespace DungeonSteakhouse.Net.Session
{
    [DisallowMultipleComponent]
    public sealed class ElevatorEffectsController : MonoBehaviour
    {
        [SerializeField] private NetSessionManager sessionManager;
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private float cameraShakeDuration = 3f;
        [SerializeField] private float cameraShakeIntensity = 0.05f;
        [SerializeField] private bool logDebug = false;

        private void OnEnable()
        {
            if (sessionManager == null)
                sessionManager = NetSessionManager.Instance;

            if (sessionManager != null)
                sessionManager.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
                sessionManager.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(NetSessionState previous, NetSessionState current)
        {
            if (logDebug)
                Debug.Log($"[ElevatorEffectsController] State: {previous} -> {current}");

            switch (current)
            {
                case NetSessionState.StartingRun:
                    TriggerDoorClose();
                    StartCoroutine(CameraShakeRoutine());
                    break;

                case NetSessionState.ReturningToLobby:
                    StartCoroutine(CameraShakeRoutine());
                    break;

                case NetSessionState.Lobby:
                    TriggerDoorOpen();
                    break;
            }
        }

        private void TriggerDoorClose()
        {
            doorAnimator?.SetTrigger("Close");
        }

        private void TriggerDoorOpen()
        {
            doorAnimator?.SetTrigger("Open");
        }

        private IEnumerator CameraShakeRoutine()
        {
            // Camera.main is unreliable here: right after an additive scene load, it can return
            // null or a stale camera until NetLocalCameraEnforcer has (re)tagged the owner's camera.
            var cam = NetLocalCameraEnforcer.LocalCamera != null ? NetLocalCameraEnforcer.LocalCamera : Camera.main;
            if (cam == null) yield break;

            var originalLocalPos = cam.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < cameraShakeDuration)
            {
                cam.transform.localPosition = originalLocalPos + (Vector3)(Random.insideUnitSphere * cameraShakeIntensity);
                elapsed += Time.deltaTime;
                yield return null;
            }

            cam.transform.localPosition = originalLocalPos;
        }
    }
}
