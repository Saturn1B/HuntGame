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

            float dist = Vector3.Distance(interactorPosition, transform.position);
            if (dist > maxInteractDistance)
                return false;

            return ServerOnInteract(interactorClientId);
        }

        /// <summary>
        /// Implement gameplay logic here. This runs on the server only.
        /// Return true if the interaction succeeded.
        /// </summary>
        protected abstract bool ServerOnInteract(ulong interactorClientId);
    }
}