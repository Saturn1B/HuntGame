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
        [SerializeField] private bool logDebug = true;

        public bool CanInteract(in InteractionContext context)
        {
            var session = NetSessionManager.Instance;
            return session != null && session.State == NetSessionState.Lobby;
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
