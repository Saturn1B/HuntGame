using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.Interactions
{
    /// <summary>
    /// Base class for networked interactables.
    /// - Server authoritative: interaction requests are validated and applied on the server.
    /// - Clients should never change gameplay state directly here.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class NetInteractable : NetworkBehaviour
    {
        [Header("Interaction")]
        [Tooltip("Max allowed distance between the interactor and this object (server validation).")]
        [SerializeField] private float maxInteractDistance = 2.5f;

        [Tooltip("If true, this interactable can be triggered by any player. If false, only the host can use it.")]
        [SerializeField] private bool allowNonHost = true;

        public float MaxInteractDistance => maxInteractDistance;

        /// <summary>
        /// Server-side validation entry point.
        /// </summary>
        public bool ServerTryInteract(ulong interactorClientId, Vector3 interactorPosition)
        {
            if (!IsServer)
                return false;

            if (!allowNonHost && interactorClientId != NetworkManager.ServerClientId)
                return false;

            float dist = ComputeMinDistanceToInteractor(interactorPosition);
            if (dist > maxInteractDistance)
                return false;

            return ServerOnInteract(interactorClientId);
        }

        private float ComputeMinDistanceToInteractor(Vector3 interactorPosition)
        {
            // Prefer collider-based distance (more robust than transform.position when pivots are offset)
            Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: false);
            if (colliders != null && colliders.Length > 0)
            {
                float min = float.MaxValue;

                for (int i = 0; i < colliders.Length; i++)
                {
                    var c = colliders[i];
                    if (c == null || !c.enabled)
                        continue;

                    Vector3 closest = c.ClosestPoint(interactorPosition);
                    float d = Vector3.Distance(interactorPosition, closest);
                    if (d < min) min = d;
                }

                if (min < float.MaxValue)
                    return min;
            }

            return Vector3.Distance(interactorPosition, transform.position);
        }

        /// <summary>
        /// Implement gameplay logic here. This runs on the server only.
        /// Return true if the interaction succeeded.
        /// </summary>
        protected abstract bool ServerOnInteract(ulong interactorClientId);
    }
}