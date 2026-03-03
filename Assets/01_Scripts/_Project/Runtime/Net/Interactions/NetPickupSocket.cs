using UnityEngine;
using Unity.Netcode;

namespace DungeonSteakhouse.Net.Interactions
{
    [DisallowMultipleComponent]
    public sealed class NetPickupSocket : NetworkBehaviour
    {
        [Header("Socket")]
        [SerializeField] private Transform holdPoint;
        [SerializeField] private bool autoFindHoldPoint = true;

        [Header("Snap While Held")]
        [SerializeField] private bool snapToHoldPointEveryFrame = true;
        [SerializeField] private Vector3 heldPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 heldRotationOffsetEuler = Vector3.zero;

        [Header("Held Object Settings")]
        [SerializeField] private bool disableCollidersWhileHeld = true;
        [SerializeField] private bool setRigidbodiesKinematicWhileHeld = true;
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
            // Server authoritative snap (good enough for now)
            if (!IsServer) return;
            if (!snapToHoldPointEveryFrame) return;
            if (_heldObject == null) return;

            SnapHeldToHoldPointServer();
        }

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

            bool parentOk = item.TrySetParent(NetworkObject, worldPositionStays: false);
            if (!parentOk)
            {
                if (verboseLogs) Debug.LogWarning("[NetPickupSocket] TrySetParent failed. Aborting pickup.");
                _heldObject = null;
                return false;
            }

            ApplyHeldState(item, isHeld: true);
            SnapHeldToHoldPointServer();

            if (verboseLogs) Debug.Log($"[NetPickupSocket] Picked up '{item.name}' on '{name}'.");

            return true;
        }

        public bool ServerTryDrop(float forwardDistance = 0.6f)
        {
            if (!IsServer) return false;
            if (_heldObject == null) return false;

            NetworkObject item = _heldObject;
            _heldObject = null;

            item.TryRemoveParent();

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
                var nts = item.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(includeInactive: true);
                for (int i = 0; i < nts.Length; i++)
                    nts[i].enabled = !isHeld;

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