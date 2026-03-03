using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.Interactions
{
    /// <summary>
    /// Simple networked door:
    /// - Server toggles a NetworkVariable<bool> IsOpen
    /// - Clients apply a local rotation (no NetworkTransform required)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetDoorInteractable : NetInteractable
    {
        [Header("Door")]
        [Tooltip("Transform that rotates when the door opens/closes. If null, this transform will be used.")]
        [SerializeField] private Transform doorPivot;

        [Tooltip("Local rotation when door is closed.")]
        [SerializeField] private Vector3 closedLocalEuler = Vector3.zero;

        [Tooltip("Local rotation when door is open.")]
        [SerializeField] private Vector3 openLocalEuler = new Vector3(0f, 90f, 0f);

        [Tooltip("If true, applies the rotation instantly. If false, interpolates in Update.")]
        [SerializeField] private bool instantApply = false;

        [Tooltip("Interpolation speed if instantApply is false.")]
        [Range(1f, 40f)]
        [SerializeField] private float lerpSpeed = 14f;

        [Header("Debug")]
        [SerializeField] private bool logServerToggles = false;

        private readonly NetworkVariable<bool> _isOpen =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Quaternion _targetRot;

        private void Awake()
        {
            if (doorPivot == null)
                doorPivot = transform;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isOpen.OnValueChanged += OnOpenChanged;
            ApplyTargetFromState(_isOpen.Value, force: true);
        }

        public override void OnNetworkDespawn()
        {
            _isOpen.OnValueChanged -= OnOpenChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (doorPivot == null)
                return;

            if (instantApply)
            {
                doorPivot.localRotation = _targetRot;
                return;
            }

            float t = 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime);
            doorPivot.localRotation = Quaternion.Slerp(doorPivot.localRotation, _targetRot, t);
        }

        protected override bool ServerOnInteract(ulong interactorClientId)
        {
            _isOpen.Value = !_isOpen.Value;

            if (logServerToggles)
                Debug.Log($"[NetDoorInteractable] Toggled. IsOpen={_isOpen.Value} by clientId={interactorClientId}");

            return true;
        }

        private void OnOpenChanged(bool previousValue, bool newValue)
        {
            ApplyTargetFromState(newValue, force: instantApply);
        }

        private void ApplyTargetFromState(bool isOpen, bool force)
        {
            if (doorPivot == null)
                return;

            var euler = isOpen ? openLocalEuler : closedLocalEuler;
            _targetRot = Quaternion.Euler(euler);

            if (force)
                doorPivot.localRotation = _targetRot;
        }
    }
}