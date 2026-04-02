using Unity.Netcode;
using UnityEngine;

namespace HuntGame.Interactions
{
    /// <summary>
    /// Networked wrapper for any IInteractable on the same GameObject.
    /// - Local prediction: the interacting client plays the interaction immediately.
    /// - Server validation + broadcast to all other clients via ClientRpc.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetcodeInteractableObject : NetworkBehaviour
    {
        [SerializeField] private bool _debugLogs;

        private IInteractable _interactable;

        private void Awake()
        {
            _interactable = GetComponent<IInteractable>();
            if (_interactable == null)
                Debug.LogError($"[NetcodeInteractableObject] No IInteractable found on {gameObject.name}.", this);
        }

        /// <summary>
        /// Called by PlayerInteractor on the owning client after raycast.
        /// </summary>
        public void RequestInteractFromClient(InteractionVerb verb, NetworkObject playerNetworkObject)
        {
            if (_interactable == null) return;

            var localCtx = InteractionContext.Local(playerNetworkObject.transform, verb);

            if (!_interactable.CanInteract(in localCtx)) return;

            // Local prediction: play immediately on the interacting client.
            _interactable.Interact(in localCtx);

            if (_debugLogs)
                Debug.Log($"[NetcodeInteractableObject] Local prediction on {gameObject.name}.");

            // Send to server for validation and broadcast to others.
            ulong playerNetObjId = playerNetworkObject != null ? playerNetworkObject.NetworkObjectId : ulong.MaxValue;
            RequestInteractServerRpc(verb, playerNetObjId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractServerRpc(InteractionVerb verb, ulong playerNetworkObjectId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
            {
                if (_debugLogs)
                    Debug.LogWarning($"[NetcodeInteractableObject] Player NetworkObject {playerNetworkObjectId} not found.");
                return;
            }

            // Security: sender must own the player NetworkObject.
            if (playerNetObj.OwnerClientId != senderClientId)
            {
                if (_debugLogs)
                    Debug.LogWarning($"[NetcodeInteractableObject] Client {senderClientId} does not own NetworkObject {playerNetworkObjectId}.");
                return;
            }

            var ctx = InteractionContext.Server(playerNetObj.transform, verb, senderClientId);

            if (_interactable == null || !_interactable.CanInteract(in ctx)) return;

            if (_debugLogs)
                Debug.Log($"[NetcodeInteractableObject] Server validated interaction on {gameObject.name}.");

            // Broadcast to all clients except the sender (already played locally).
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIdsNativeArray = GetOtherClientIds(senderClientId)
                }
            };

            BroadcastInteractClientRpc(verb, playerNetworkObjectId, clientRpcParams);
        }

        [ClientRpc]
        private void BroadcastInteractClientRpc(InteractionVerb verb, ulong playerNetworkObjectId, ClientRpcParams clientRpcParams = default)
        {
            if (_interactable == null) return;

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
                return;

            var ctx = InteractionContext.Local(playerNetObj.transform, verb);
            _interactable.Interact(in ctx);

            if (_debugLogs)
                Debug.Log($"[NetcodeInteractableObject] Received broadcast interaction on {gameObject.name}.");
        }

        private Unity.Collections.NativeArray<ulong> GetOtherClientIds(ulong excludeClientId)
        {
            var allClients = NetworkManager.ConnectedClientsIds;
            var others = new System.Collections.Generic.List<ulong>();

            foreach (var id in allClients)
            {
                if (id != excludeClientId)
                    others.Add(id);
            }

            return new Unity.Collections.NativeArray<ulong>(others.ToArray(), Unity.Collections.Allocator.Temp);
        }
    }
}