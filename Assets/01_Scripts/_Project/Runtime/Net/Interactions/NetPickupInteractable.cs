using UnityEngine;
using Unity.Netcode;

namespace DungeonSteakhouse.Net.Interactions
{
    /// <summary>
    /// Pickup interactable that plugs into the existing NetInteractable pipeline.
    /// On interact (server), it attaches the item to the interactor's NetPickupSocket.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetPickupInteractable : NetInteractable
    {
        [Header("Pickup")]
        [Tooltip("If true, only the host/server can pick up items (useful while debugging).")]
        [SerializeField] private bool onlyHostCanPickup = false;

        [Header("Fallback")]
        [Tooltip("If the interactor has no NetPickupSocket, despawn the object as a fallback.")]
        [SerializeField] private bool fallbackToDespawnIfNoSocket = true;

        [Tooltip("If fallback despawn is used, destroy with despawn.")]
        [SerializeField] private bool destroyWithDespawnFallback = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

        private bool _pickedUpServer;

        protected override bool ServerOnInteract(ulong interactorClientId)
        {
            if (!IsServer) return false;
            if (_pickedUpServer) return false;

            if (NetworkObject == null || !NetworkObject.IsSpawned)
                return false;

            if (onlyHostCanPickup && interactorClientId != NetworkManager.ServerClientId)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetPickupInteractable] Pickup denied (host-only). sender={interactorClientId}");
                return false;
            }

            // Resolve the interactor's player object on the server
            if (NetworkManager == null ||
                !NetworkManager.ConnectedClients.TryGetValue(interactorClientId, out var client) ||
                client.PlayerObject == null)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetPickupInteractable] Cannot resolve PlayerObject for client {interactorClientId}.");
                return false;
            }

            NetPickupSocket socket = client.PlayerObject.GetComponentInChildren<NetPickupSocket>(includeInactive: true);
            if (socket == null)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetPickupInteractable] No NetPickupSocket found on PlayerObject for client {interactorClientId}.");

                if (fallbackToDespawnIfNoSocket)
                {
                    _pickedUpServer = true;
                    NetworkObject.Despawn(destroyWithDespawnFallback);
                    return true;
                }

                return false;
            }

            // Try attach
            bool success = socket.ServerTryPickup(NetworkObject);
            if (!success)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetPickupInteractable] Socket pickup failed for client {interactorClientId}.");

                if (fallbackToDespawnIfNoSocket)
                {
                    _pickedUpServer = true;
                    NetworkObject.Despawn(destroyWithDespawnFallback);
                    return true;
                }

                return false;
            }

            _pickedUpServer = true;
            return true;
        }
    }
}