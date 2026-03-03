using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonSteakhouse.Net.Players
{
    /// <summary>
    /// Ensures the local player's camera is the active main camera across additive scene loads.
    /// - Enables the owner's camera + AudioListener
    /// - Disables any other enabled cameras/listeners found in loaded scenes
    /// - Re-applies for a short window after scene load to handle cameras enabled late by scripts
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetLocalCameraEnforcer : NetworkBehaviour
    {
        [Header("Owner Camera (auto-resolved if null)")]
        [SerializeField] private Camera ownerCamera;
        [SerializeField] private AudioListener ownerAudioListener;

        [Header("Behavior")]
        [Tooltip("If true, sets the owner's camera tag to MainCamera and removes MainCamera tag from other cameras.")]
        [SerializeField] private bool forceMainCameraTag = true;

        [Tooltip("If true, disables all other enabled cameras (outside this player hierarchy).")]
        [SerializeField] private bool disableOtherCameras = true;

        [Tooltip("If true, disables all other enabled AudioListeners (outside this player hierarchy).")]
        [SerializeField] private bool disableOtherAudioListeners = true;

        [Header("Robustness")]
        [Tooltip("How long to keep enforcing after a scene load (seconds). Helps when cameras are enabled late by scripts.")]
        [Range(0f, 5f)]
        [SerializeField] private float enforceForSecondsAfterSceneLoad = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private float _enforceUntilTime;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            ResolveOwnerRig();

            if (!IsOwner)
            {
                DisableOwnerRig();
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            EnforceNow("OnNetworkSpawn");
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            }

            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            if (!IsOwner)
                return;

            if (Time.unscaledTime <= _enforceUntilTime)
                EnforceNow("EnforcementWindow");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsOwner)
                return;

            _enforceUntilTime = Time.unscaledTime + Mathf.Max(0f, enforceForSecondsAfterSceneLoad);
            EnforceNow($"SceneLoaded:{scene.name}");
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (!IsOwner)
                return;

            _enforceUntilTime = Time.unscaledTime + Mathf.Max(0f, enforceForSecondsAfterSceneLoad);
            EnforceNow($"ActiveSceneChanged:{newScene.name}");
        }

        private void ResolveOwnerRig()
        {
            if (ownerCamera == null)
                ownerCamera = GetComponentInChildren<Camera>(true);

            if (ownerAudioListener == null && ownerCamera != null)
                ownerAudioListener = ownerCamera.GetComponent<AudioListener>();

            if (ownerAudioListener == null)
                ownerAudioListener = GetComponentInChildren<AudioListener>(true);
        }

        private void DisableOwnerRig()
        {
            if (ownerCamera != null)
                ownerCamera.enabled = false;

            if (ownerAudioListener != null)
                ownerAudioListener.enabled = false;
        }

        private void EnforceNow(string reason)
        {
            ResolveOwnerRig();

            if (ownerCamera == null)
            {
                if (logDebug)
                    Debug.LogWarning($"[NetLocalCameraEnforcer] No owner camera found. Reason={reason}");
                return;
            }

            // Ensure owner's rig is enabled
            ownerCamera.enabled = true;

            if (ownerAudioListener != null)
                ownerAudioListener.enabled = true;

            if (forceMainCameraTag)
                ownerCamera.tag = "MainCamera";

            if (disableOtherCameras)
                DisableNonOwnerCameras();

            if (disableOtherAudioListeners)
                DisableNonOwnerAudioListeners();

            if (logDebug)
                Debug.Log($"[NetLocalCameraEnforcer] Enforced owner camera. Reason={reason} OwnerCamera='{ownerCamera.name}'");
        }

        private void DisableNonOwnerCameras()
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null)
                    continue;

                if (cam == ownerCamera)
                    continue;

                // Do not touch cameras inside the local player hierarchy (safe for attached effects)
                if (cam.transform.IsChildOf(transform))
                    continue;

                if (forceMainCameraTag && cam.CompareTag("MainCamera"))
                    cam.tag = "Untagged";

                if (cam.enabled)
                    cam.enabled = false;
            }
        }

        private void DisableNonOwnerAudioListeners()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                var l = listeners[i];
                if (l == null)
                    continue;

                if (ownerAudioListener != null && l == ownerAudioListener)
                    continue;

                if (l.transform.IsChildOf(transform))
                    continue;

                if (l.enabled)
                    l.enabled = false;
            }
        }
    }
}