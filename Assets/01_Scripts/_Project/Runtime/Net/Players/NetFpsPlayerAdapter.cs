using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonSteakhouse.Net.Players
{
    /// <summary>
    /// Makes a single-player FPS controller work in multiplayer WITHOUT modifying the original FPS scripts.
    /// - Enables input + camera + movement only for the owning client.
    /// - Disables local-only components on non-owners (no cursor lock, no extra AudioListener, etc.).
    /// </summary>
    public sealed class NetFpsPlayerAdapter : NetworkBehaviour
    {
        [Header("Local-only Components (enabled only for Owner)")]
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private PlayerInputHandler inputHandler;
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private FirstPersonCamera firstPersonCamera;
        [SerializeField] private HeadBobbing headBobbing;

        [Header("Local Camera (enabled only for Owner)")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener playerAudioListener;

        [Header("Optional Scene Camera Handling")]
        [Tooltip("If enabled, disables any other enabled camera in the scene when the local player spawns.")]
        [SerializeField] private bool disableOtherSceneCamerasOnOwnerSpawn = true;

        [Tooltip("If enabled, disables any other AudioListener in the scene when the local player spawns.")]
        [SerializeField] private bool disableOtherAudioListenersOnOwnerSpawn = true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            ResolveReferencesIfNeeded();
            ApplyLocalState(IsOwner);
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            ApplyLocalState(true);
        }

        public override void OnLostOwnership()
        {
            base.OnLostOwnership();
            ApplyLocalState(false);
        }

        public override void OnNetworkDespawn()
        {
            ApplyLocalState(false);
            base.OnNetworkDespawn();
        }

        private void ResolveReferencesIfNeeded()
        {
            if (!playerInput) playerInput = GetComponent<PlayerInput>();
            if (!inputHandler) inputHandler = GetComponent<PlayerInputHandler>();
            if (!characterMovement) characterMovement = GetComponent<CharacterMovement>();
            if (!firstPersonCamera) firstPersonCamera = GetComponent<FirstPersonCamera>();
            if (!headBobbing) headBobbing = GetComponent<HeadBobbing>();

            if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);

            if (!playerAudioListener)
            {
                if (playerCamera != null)
                    playerAudioListener = playerCamera.GetComponent<AudioListener>();

                if (!playerAudioListener)
                    playerAudioListener = GetComponentInChildren<AudioListener>(true);
            }
        }

        private void ApplyLocalState(bool isOwner)
        {
            SetEnabled(playerInput, isOwner);
            SetEnabled(inputHandler, isOwner);
            SetEnabled(characterMovement, isOwner);
            SetEnabled(firstPersonCamera, isOwner);
            SetEnabled(headBobbing, isOwner);

            if (playerCamera) playerCamera.enabled = isOwner;
            if (playerAudioListener) playerAudioListener.enabled = isOwner;

            if (!isOwner)
                return;

            if (disableOtherSceneCamerasOnOwnerSpawn)
                DisableOtherEnabledCameras();

            if (disableOtherAudioListenersOnOwnerSpawn)
                DisableOtherAudioListeners();
        }

        private void DisableOtherEnabledCameras()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                var cam = cams[i];
                if (cam == null) continue;
                if (playerCamera != null && cam == playerCamera) continue;

                if (cam.enabled)
                    cam.enabled = false;
            }
        }

        private void DisableOtherAudioListeners()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                var l = listeners[i];
                if (l == null) continue;
                if (playerAudioListener != null && l == playerAudioListener) continue;

                if (l.enabled)
                    l.enabled = false;
            }
        }

        private static void SetEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }
}