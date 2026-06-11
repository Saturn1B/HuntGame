using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.Session
{
    /// <summary>
    /// Orchestrates elevator door/chain animations for both run start and return-to-lobby sequences.
    /// Server drives all coroutines; ClientRpcs push animator triggers to all clients.
    /// Scene transitions are handled by NetSessionManager.
    /// 
    /// SETUP:
    /// - Attach to a GameObject with NetworkObject in Elevator scene
    /// - Assign doorAnimator and chesneAnimator in inspector
    /// - Door Animator: trigger "Close" (DoorsOpen->DoorsClose, no exit time)
    ///                  trigger "Open"  (DoorsClose->DoorsOpen, no exit time)
    /// - Chesne Animator: trigger "Rise" (Idle->Rise, no exit time)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevatorSequenceController : NetworkBehaviour
    {
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private Animator chesneAnimator;

        [Header("Start Run")]
        [Tooltip("Must match the Door Close animation clip length")]
        [SerializeField] private float doorCloseSeconds = 2f;
        [Tooltip("Chain rise duration — runs while dungeon loads")]
        [SerializeField] private float chainRiseSeconds = 3f;

        [Header("Return to Lobby")]
        [Tooltip("Countdown before doors close on return")]
        [SerializeField] private float returnCountdownSeconds = 20f;
        [Tooltip("Must match the Door Close animation clip length")]
        [SerializeField] private float returnDoorCloseSeconds = 2f;
        [Tooltip("Delay after tavern loaded before doors reopen")]
        [SerializeField] private float chainStopSeconds = 3f;

        private bool _sequenceRunning;

        // ── Start Run ────────────────────────────────────────────────────────

        /// <summary>Called by NetSessionManager when run starts.</summary>
        public void StartSequence()
        {
            if (!IsServer || _sequenceRunning) return;
            StartCoroutine(StartRunRoutine());
        }

        private IEnumerator StartRunRoutine()
        {
            _sequenceRunning = true;

            PlayDoorCloseClientRpc();
            yield return new WaitForSeconds(doorCloseSeconds);

            // Chain rises while dungeon loads in parallel
            PlayChainRiseClientRpc();
            NetSessionManager.Instance?.StartRunAfterSequence();

            yield return new WaitForSeconds(chainRiseSeconds);
            _sequenceRunning = false;
        }

        // ── Return to Lobby ──────────────────────────────────────────────────

        /// <summary>Called by NetSessionManager when return countdown starts.</summary>
        public void StartReturnSequence()
        {
            if (!IsServer || _sequenceRunning) return;
            StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            _sequenceRunning = true;

            // Broadcast countdown to all clients
            StartReturnCountdownClientRpc(returnCountdownSeconds);
            yield return new WaitForSeconds(returnCountdownSeconds);

            PlayDoorCloseClientRpc();
            yield return new WaitForSeconds(returnDoorCloseSeconds);

            // Doors closed — trigger scene transition
            NetSessionManager.Instance?.StartReturnAfterSequence();

            _sequenceRunning = false;
        }

        // ── Reopen doors (called after scene loaded) ─────────────────────────

        /// <summary>Called by NetSessionManager once tavern/dungeon is fully loaded.</summary>
        public void OpenDoors()
        {
            if (!IsServer) return;
            StartCoroutine(OpenDoorsRoutine());
        }

        private IEnumerator OpenDoorsRoutine()
        {
            yield return new WaitForSeconds(chainStopSeconds);
            PlayDoorOpenClientRpc();
        }

        // ── ClientRpcs ───────────────────────────────────────────────────────

        [ClientRpc]
        private void PlayDoorCloseClientRpc()
        {
            doorAnimator?.SetTrigger("Close");
        }

        [ClientRpc]
        private void PlayDoorOpenClientRpc()
        {
            doorAnimator?.SetTrigger("Open");
        }

        [ClientRpc]
        private void PlayChainRiseClientRpc()
        {
            chesneAnimator?.SetTrigger("Rise");
        }

        [ClientRpc]
        private void StartReturnCountdownClientRpc(float duration)
        {
            // UI can listen to NetSessionManager.ReturnCountdownRemaining instead,
            // but this ensures all clients know the countdown started.
            Debug.Log($"[ElevatorSequence] Return countdown started: {duration}s");
        }
    }
}