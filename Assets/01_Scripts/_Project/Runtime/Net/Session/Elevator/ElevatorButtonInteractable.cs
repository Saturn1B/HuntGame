using UnityEngine;
using HuntGame.Interactions;

namespace DungeonSteakhouse.Net.Session
{
    /// <summary>
    /// Placed on the elevator button in the Elevator scene.
    /// Requires a NetcodeInteractableObject on the same GameObject to route the
    /// client interaction through the Netcode RPC pipeline before this is called.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElevatorButtonInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private NetReadyPlatformGate readyGate;
        [SerializeField] private bool logDebug = true;

        private void Start()
        {
            if (readyGate == null)
                readyGate = FindObjectOfType<NetReadyPlatformGate>();
        }

        public bool CanInteract(in InteractionContext context)
        {
            var session = NetSessionManager.Instance;
            if (session == null || session.State != NetSessionState.Lobby)
                return false;

            // Gate null → no platform requirement, fall through to server-side guard.
            return readyGate == null || readyGate.AllReadyConfirmedServer;
        }

        public void Interact(in InteractionContext context)
        {
            if (context.Verb != InteractionVerb.Use)
                return;

            NetSessionManager.Instance?.RequestStartRunServerRpc();

            if (logDebug)
                Debug.Log("[ElevatorButtonInteractable] Start run requested.");
        }
    }
}
