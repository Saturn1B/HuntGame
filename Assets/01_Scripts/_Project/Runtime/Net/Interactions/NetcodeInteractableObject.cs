using System.Collections.Generic;
using Unity.Collections;
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
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] RequestInteractFromClient called. verb={verb}");

            if (_interactable == null)
            {
                Debug.LogError("[NetcodeInteractableObject] _interactable is null, cannot interact.");
                return;
            }

            if (playerNetworkObject == null)
            {
                Debug.LogError("[NetcodeInteractableObject] playerNetworkObject is null. Is NetworkObject on the player?");
                return;
            }

            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] playerNetworkObject: {playerNetworkObject.name} (clientId={playerNetworkObject.OwnerClientId})");

            var localCtx = InteractionContext.Local(playerNetworkObject.transform, verb);

            bool canInteract = _interactable.CanInteract(in localCtx);
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] CanInteract = {canInteract}");

            if (!canInteract) return;

            // Local prediction: play immediately on the interacting client.
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] Playing local prediction on {gameObject.name}.");
            _interactable.Interact(in localCtx);

            ulong playerNetObjId = playerNetworkObject.NetworkObjectId;
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] Sending ServerRpc. playerNetObjId={playerNetObjId}");
            RequestInteractServerRpc(verb, playerNetObjId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractServerRpc(InteractionVerb verb, ulong playerNetworkObjectId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] ServerRpc received from clientId={senderClientId}");

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
            {
                Debug.LogWarning($"[NetcodeInteractableObject] Player NetworkObject {playerNetworkObjectId} not found.");
                return;
            }

            if (playerNetObj.OwnerClientId != senderClientId)
            {
                Debug.LogWarning($"[NetcodeInteractableObject] Security rejected: client {senderClientId} does not own NetworkObject {playerNetworkObjectId}.");
                return;
            }

            var ctx = InteractionContext.Server(playerNetObj.transform, verb, senderClientId);

            bool canInteract = _interactable.CanInteract(in ctx);
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] Server CanInteract = {canInteract}");

            if (!canInteract)
            {
                // The sender already played this interaction locally (optimistic prediction in
                // RequestInteractFromClient). Since the server refused it -- e.g. a race between two
                // players interacting with the same object -- tell only that client to resync,
                // instead of leaving its visual state permanently diverged from everyone else's.
                var rejectParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } }
                };
                NotifyInteractRejectedClientRpc(rejectParams);
                return;
            }

            // Apply the authoritative state change on the server's own copy. This is what actually
            // writes any NetworkVariable-backed state (see DoorInteractable/ChestInteractable/
            // LeverInteractable/PickupInteractable) so late-joiners/reconnects can resync -- without
            // this, a dedicated (non-host) server would never run Interact() on its own instance.
            _interactable.Interact(in ctx);

            // Build target list excluding the sender (already played locally).
            var otherClients = new List<ulong>();
            foreach (var id in NetworkManager.ConnectedClientsIds)
            {
                if (id != senderClientId)
                    otherClients.Add(id);
            }

            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] Broadcasting to {otherClients.Count} other client(s).");

            if (otherClients.Count == 0) return;

            // NativeArray with Persistent allocator � safe for NGO to read asynchronously.
            var targetArray = new NativeArray<ulong>(otherClients.ToArray(), Allocator.Persistent);

            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIdsNativeArray = targetArray
                }
            };

            BroadcastInteractClientRpc(verb, playerNetworkObjectId, clientRpcParams);

            targetArray.Dispose();
        }

        [ClientRpc]
        private void NotifyInteractRejectedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] Server rejected interaction on {gameObject.name}; resyncing local prediction.");

            if (_interactable is INetworkResyncable resyncable)
                resyncable.ResyncFromNetworkState();
        }

        [ClientRpc]
        private void BroadcastInteractClientRpc(InteractionVerb verb, ulong playerNetworkObjectId, ClientRpcParams clientRpcParams = default)
        {
            if (_debugLogs) Debug.Log($"[NetcodeInteractableObject] ClientRpc received. Playing on {gameObject.name}.");

            if (_interactable == null) return;

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
            {
                Debug.LogWarning($"[NetcodeInteractableObject] ClientRpc: player NetworkObject {playerNetworkObjectId} not found.");
                return;
            }

            var ctx = InteractionContext.Local(playerNetObj.transform, verb);
            _interactable.Interact(in ctx);
        }
    }
}