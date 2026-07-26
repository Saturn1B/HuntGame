using Unity.Netcode;
using UnityEngine;

namespace HuntGame
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCubePusher : NetworkBehaviour
    {
        [SerializeField] private float pushForce = 5f;
        [SerializeField, Tooltip("Server-side authoritative range check: the target must be within this distance of the pushing player.")]
        private float maxPushDistance = 3f;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsOwner) return;
            if (hit.moveDirection.y < -0.3f) return;

            var rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;

            // On v�rifie que c'est bien un NetworkObject avant d'envoyer le RPC
            var netObj = hit.collider.GetComponent<NetworkObject>();
            if (netObj == null) return;

            PushServerRpc(netObj.NetworkObjectId, hit.moveDirection);
        }

        [ServerRpc]
        private void PushServerRpc(ulong targetId, Vector3 direction)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var netObj)) return;

            // Server-authoritative distance check: never trust a client-supplied position/force pair.
            float sqrDist = (netObj.transform.position - transform.position).sqrMagnitude;
            if (sqrDist > maxPushDistance * maxPushDistance) return;

            var rb = netObj.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) return;

            // Direction only comes from the client; magnitude is always server-defined to prevent force injection.
            Vector3 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            rb.AddForce(safeDirection * pushForce, ForceMode.Impulse);
        }
    }
}