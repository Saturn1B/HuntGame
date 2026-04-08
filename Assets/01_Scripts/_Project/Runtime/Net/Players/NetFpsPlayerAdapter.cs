using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using HuntGame.Player;

namespace DungeonSteakhouse.Net.Players
{
    /// <summary>
    /// Makes a single-player FPS controller work in multiplayer WITHOUT modifying the original FPS scripts.
    /// - Enables input + camera + movement only for the owning client.
    /// - Disables local-only components on non-owners (no cursor lock, no extra AudioListener, etc.).
    /// - Camera ownership across scenes is handled exclusively by NetLocalCameraEnforcer.
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
        }

        private static void SetEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }
}
