using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.InteractionsV2
{
    /// <summary>
    /// Example usable: toggles a door open/closed.
    /// - Client predicts instantly (visual)
    /// - Server toggles a NetworkVariable<bool>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorUsable : NetUsable
    {
        [Header("Door Visual")]
        [SerializeField] private Transform doorVisual;
        [SerializeField] private Vector3 closedLocalEuler = Vector3.zero;
        [SerializeField] private Vector3 openLocalEuler = new Vector3(0f, 90f, 0f);

        private readonly NetworkVariable<bool> _isOpen =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Local predicted state (only used for immediate feedback)
        private bool _predictedOpen;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isOpen.OnValueChanged += OnOpenChanged;
            ApplyVisual(_isOpen.Value);

            _predictedOpen = _isOpen.Value;
        }

        public override void OnNetworkDespawn()
        {
            _isOpen.OnValueChanged -= OnOpenChanged;
            base.OnNetworkDespawn();
        }

        public override void ClientPredictUse(in UseContext ctx)
        {
            // Instant feedback: flip predicted state and apply visuals immediately.
            _predictedOpen = !_predictedOpen;
            ApplyVisual(_predictedOpen);
        }

        public override void ClientReconcileRejected(in UseContext ctx)
        {
            // Rollback to authoritative replicated state.
            _predictedOpen = _isOpen.Value;
            ApplyVisual(_predictedOpen);
        }

        public override bool ServerUse(in UseContext ctx)
        {
            if (!IsServer) return false;

            _isOpen.Value = !_isOpen.Value;
            return true;
        }

        private void OnOpenChanged(bool previous, bool current)
        {
            // Authoritative replication
            _predictedOpen = current;
            ApplyVisual(current);
        }

        private void ApplyVisual(bool open)
        {
            if (doorVisual == null) doorVisual = transform;

            doorVisual.localRotation = Quaternion.Euler(open ? openLocalEuler : closedLocalEuler);
        }
    }
}