using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.InteractionsV2
{
    /// <summary>
    /// Base class for all interactables (Usables).
    /// - Interactor finds this component using TryGetComponent (fast).
    /// - Client calls ClientPredictUse immediately (super reactive).
    /// - Server later executes ServerUse (authoritative state replication).
    /// - If server rejects, client can reconcile (optional).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class NetUsable : NetworkBehaviour
    {
        [Header("Server Validation (lightweight)")]
        [Tooltip("Max distance allowed on server between interactor player and this usable.")]
        [SerializeField] private float maxUseDistance = 2.5f;

        [Tooltip("If true, server will validate distance. Recommended to avoid weird desyncs.")]
        [SerializeField] private bool validateDistanceOnServer = true;

        [Tooltip("Optional: only host can use (debug / admin).")]
        [SerializeField] private bool hostOnly = false;

        public float MaxUseDistance => maxUseDistance;
        public bool ValidateDistanceOnServer => validateDistanceOnServer;
        public bool HostOnly => hostOnly;

        /// <summary>
        /// Called immediately on the local client BEFORE server confirmation.
        /// Keep it visual/feel-good: animations, sounds, UI, local pose, etc.
        /// Should be safe to call even if later rejected.
        /// </summary>
        public virtual void ClientPredictUse(in UseContext ctx) { }

        /// <summary>
        /// Called on the local client if the server rejects the use request.
        /// Use it to rollback predicted visuals if needed (e.g., show item again).
        /// </summary>
        public virtual void ClientReconcileRejected(in UseContext ctx) { }

        /// <summary>
        /// Server-side "can use" check (called by the interactor system).
        /// </summary>
        public virtual bool ServerCanUse(in UseContext ctx, Vector3 serverInteractorPosition)
        {
            if (!IsServer) return false;

            if (hostOnly && ctx.InteractorClientId != NetworkManager.ServerClientId)
                return false;

            if (!validateDistanceOnServer)
                return true;

            float dist = ComputeMinDistanceToInteractor(serverInteractorPosition);
            return dist <= maxUseDistance;
        }

        /// <summary>
        /// Server-side use execution. Return true if applied.
        /// This should update replicated state (NetworkVariables, parenting, despawn, etc.).
        /// </summary>
        public abstract bool ServerUse(in UseContext ctx);

        private float ComputeMinDistanceToInteractor(Vector3 interactorPosition)
        {
            // Collider-based distance is more robust than transform pivot distance.
            Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: false);
            if (colliders != null && colliders.Length > 0)
            {
                float min = float.MaxValue;

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider c = colliders[i];
                    if (c == null || !c.enabled) continue;

                    Vector3 closest = c.ClosestPoint(interactorPosition);
                    float d = Vector3.Distance(interactorPosition, closest);
                    if (d < min) min = d;
                }

                if (min < float.MaxValue)
                    return min;
            }

            return Vector3.Distance(interactorPosition, transform.position);
        }
    }
}