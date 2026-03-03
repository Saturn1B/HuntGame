using UnityEngine;
using Unity.Netcode;

namespace DungeonSteakhouse.Net.Interactions
{
    /// <summary>
    /// Server-authoritative "hand socket" that can hold a single NetworkObject.
    /// - Network-parents the item to the player's NetworkObject (reliable in NGO)
    /// - Continuously snaps the held item to the HoldPoint pose (so it behaves like a child of HoldPoint)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetPickupSocket : NetworkBehaviour
    {
        [Header("Socket")]
        [Tooltip("Where the held object should appear (e.g., a child transform near the hand/camera).")]
        [SerializeField] private Transform holdPoint;

        [Tooltip("If HoldPoint is not assigned, try to find a child named 'HoldPoint' at runtime.")]
        [SerializeField] private bool autoFindHoldPoint = true;

        [Header("Snap While Held")]
        [Tooltip("If true, the held item will be snapped to HoldPoint every frame on the server.")]
        [SerializeField] private bool snapToHoldPointEveryFrame = true;

        [Tooltip("Optional local offset applied after snapping (position, in meters).")]
        [SerializeField] private Vector3 heldPositionOffset = Vector3.zero;

        [Tooltip("Optional local offset applied after snapping (rotation, euler degrees).")]
        [SerializeField] private Vector3 heldRotationOffsetEuler = Vector3.zero;

        [Header("Held Object Settings")]
        [Tooltip("Disable all colliders on the held object while held.")]
        [SerializeField] private bool disableCollidersWhileHeld = true;

        [Tooltip("Set Rigidbody to kinematic while held (if present).")]
        [SerializeField] private bool setRigidbodiesKinematicWhileHeld = true;

        [Tooltip("Disable NetworkTransform components while held (prevents transform fights/jitter).")]
        [SerializeField] private bool disableNetworkTransformWhileHeld = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

        private NetworkObject _heldObject;

        public bool HasItem => _heldObject != null;

        private void Awake()
        {
            if (holdPoint == null && autoFindHoldPoint)
            {
                Transform t = transform.Find("HoldPoint");
                if (t != null) holdPoint = t;
            }
        }

        private void LateUpdate()
        {
            // We only drive held item pose on the server (authoritative).
            if (!IsServer) return;
            if (!snapToHoldPointEveryFrame) return;
            if (_heldObject == null) return;

            SnapHeldToHoldPointServer();
        }

        /// <summary>
        /// Server-only: attach the item to this player's socket.
        /// Returns true if successful.
        /// </summary>
        public bool ServerTryPickup(NetworkObject item)
        {
            if (!IsServer) return false;
            if (item == null || !item.IsSpawned) return false;
            if (_heldObject != null) return false;

            if (holdPoint == null)
            {
                if (verboseLogs) Debug.LogWarning("[NetPickupSocket] Cannot pickup: HoldPoint is not assigned.");
                return false;
            }

            _heldObject = item;

            // Reliable NGO parenting: parent to the player's NetworkObject.
            // (HoldPoint is typically NOT a NetworkObject; NGO can't network-parent to it.)
            bool parentOk = item.TrySetParent(NetworkObject, worldPositionStays: false);
            if (!parentOk)
            {
                if (verboseLogs) Debug.LogWarning("[NetPickupSocket] TrySetParent failed. Aborting pickup.");
                _heldObject = null;
                return false;
            }

            ApplyHeldState(item, isHeld: true);

            // Snap immediately once (then optionally every frame).
            SnapHeldToHoldPointServer();

            if (verboseLogs) Debug.Log($"[NetPickupSocket] Picked up '{item.name}' on '{name}'.");

            return true;
        }

        /// <summary>
        /// Server-only: drop the held item in front of the player.
        /// Returns true if something was dropped.
        /// </summary>
        public bool ServerTryDrop(float forwardDistance = 0.6f)
        {
            if (!IsServer) return false;
            if (_heldObject == null) return false;

            NetworkObject item = _heldObject;
            _heldObject = null;

            // Unparent (network-wise)
            item.TryRemoveParent();

            // Place it slightly in front
            Vector3 dropPos = transform.position + transform.forward * forwardDistance;
            item.transform.position = dropPos;

            ApplyHeldState(item, isHeld: false);

            if (verboseLogs) Debug.Log($"[NetPickupSocket] Dropped '{item.name}' from '{name}'.");

            return true;
        }

        private void SnapHeldToHoldPointServer()
        {
            if (_heldObject == null) return;
            if (holdPoint == null) return;

            // Item is parented to player root => compute holdPoint pose in player's local space,
            // so it works even if HoldPoint is deep under a rig hierarchy.
            Vector3 localPos = transform.InverseTransformPoint(holdPoint.position);
            Quaternion localRot = Quaternion.Inverse(transform.rotation) * holdPoint.rotation;

            Quaternion rotOffset = Quaternion.Euler(heldRotationOffsetEuler);

            _heldObject.transform.localPosition = localPos + heldPositionOffset;
            _heldObject.transform.localRotation = localRot * rotOffset;
        }

        private void ApplyHeldState(NetworkObject item, bool isHeld)
        {
            if (item == null) return;

            if (disableCollidersWhileHeld)
            {
                Collider[] colliders = item.GetComponentsInChildren<Collider>(includeInactive: true);
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].enabled = !isHeld;
            }

            if (setRigidbodiesKinematicWhileHeld)
            {
                Rigidbody[] rbs = item.GetComponentsInChildren<Rigidbody>(includeInactive: true);
                for (int i = 0; i < rbs.Length; i++)
                {
                    rbs[i].isKinematic = isHeld;
                    rbs[i].useGravity = !isHeld;
                    if (isHeld) rbs[i].linearVelocity = Vector3.zero;
                }
            }

            if (disableNetworkTransformWhileHeld)
            {
                // Disable NGO NetworkTransform if present
                var nts = item.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(includeInactive: true);
                for (int i = 0; i < nts.Length; i++)
                    nts[i].enabled = !isHeld;

                // Some projects have a "ClientNetworkTransform" (not always present in NGO assemblies).
                // We disable it by name to avoid compile-time dependency.
                Component[] comps = item.GetComponentsInChildren<Component>(includeInactive: true);
                for (int i = 0; i < comps.Length; i++)
                {
                    Component c = comps[i];
                    if (c == null) continue;

                    string typeName = c.GetType().Name;
                    if (typeName == "ClientNetworkTransform" || typeName == "OwnerNetworkTransform")
                    {
                        if (c is Behaviour b)
                            b.enabled = !isHeld;
                    }
                }
            }
        }
    }
}