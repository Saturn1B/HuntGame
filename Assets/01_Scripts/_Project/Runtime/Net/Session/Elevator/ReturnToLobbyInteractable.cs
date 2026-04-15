using UnityEngine;
using HuntGame.Interactions;

namespace DungeonSteakhouse.Net.Session
{
    /// <summary>
    /// Placed on a button/lever in the Dungeon scene to trigger a return to lobby.
    /// Requires a NetcodeInteractableObject on the same GameObject to route the
    /// client interaction through the Netcode RPC pipeline before this is called.
    /// The 10s countdown is handled server-side in NetSessionManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReturnToLobbyInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool logDebug = true;

        public bool CanInteract(in InteractionContext context)
        {
            var session = NetSessionManager.Instance;
            return session != null && session.State == NetSessionState.InRun;
        }

        public void Interact(in InteractionContext context)
        {
            if (context.Verb != InteractionVerb.Use)
                return;

            NetSessionManager.Instance?.RequestReturnToLobbyServerRpc();

            if (logDebug)
                Debug.Log("[ReturnToLobbyInteractable] Return to lobby requested.");
        }
    }
}
