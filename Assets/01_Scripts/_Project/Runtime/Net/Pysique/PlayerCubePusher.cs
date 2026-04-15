using Unity.Netcode;
using UnityEngine;

namespace HuntGame
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCubePusher : NetworkBehaviour
    {
        [SerializeField] private float pushForce = 5f;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsOwner) return;
            if (hit.moveDirection.y < -0.3f) return;

            var rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;

            // On vérifie que c'est bien un NetworkObject avant d'envoyer le RPC
            var netObj = hit.collider.GetComponent<NetworkObject>();
            if (netObj == null) return;

            var force = hit.moveDirection * pushForce;
            PushServerRpc(netObj.NetworkObjectId, force);
        }

        [ServerRpc]
        private void PushServerRpc(ulong targetId, Vector3 force)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var netObj)) return;

            var rb = netObj.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) return;

            rb.AddForce(force, ForceMode.Impulse);
        }
    }
}