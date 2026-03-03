using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.Interactions
{
    /// <summary>
    /// Simple on/off toggle interactable.
    /// - Server toggles a NetworkVariable<bool>.
    /// - Clients apply either:
    ///   A) Animator bool parameter
    ///   B) Pivot rotation
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetToggleInteractable : NetInteractable
    {
        [Header("State")]
        [SerializeField] private bool defaultValue = false;

        [Header("Animator (optional)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string animatorBoolName = "IsOn";

        [Header("Pivot Rotation (optional)")]
        [SerializeField] private Transform pivot;
        [SerializeField] private Vector3 offLocalEuler = Vector3.zero;
        [SerializeField] private Vector3 onLocalEuler = new Vector3(0f, 0f, -35f);

        [SerializeField] private bool instantApply = true;
        [Range(1f, 40f)]
        [SerializeField] private float lerpSpeed = 14f;

        [Header("Debug")]
        [SerializeField] private bool logServer = false;

        private readonly NetworkVariable<bool> _isOn =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Quaternion _targetRot;

        private void Awake()
        {
            if (pivot == null)
                pivot = transform;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _isOn.OnValueChanged += OnToggleChanged;

            if (IsServer)
                _isOn.Value = defaultValue;

            ApplyLocal(_isOn.Value, force: true);
        }

        public override void OnNetworkDespawn()
        {
            _isOn.OnValueChanged -= OnToggleChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (pivot == null)
                return;

            if (instantApply)
            {
                pivot.localRotation = _targetRot;
                return;
            }

            float t = 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime);
            pivot.localRotation = Quaternion.Slerp(pivot.localRotation, _targetRot, t);
        }

        protected override bool ServerOnInteract(ulong interactorClientId)
        {
            _isOn.Value = !_isOn.Value;

            if (logServer)
                Debug.Log($"[NetToggleInteractable] Toggled. IsOn={_isOn.Value} by clientId={interactorClientId}");

            return true;
        }

        private void OnToggleChanged(bool prev, bool next)
        {
            ApplyLocal(next, force: instantApply);
        }

        private void ApplyLocal(bool isOn, bool force)
        {
            if (animator != null && !string.IsNullOrEmpty(animatorBoolName))
                animator.SetBool(animatorBoolName, isOn);

            if (pivot != null)
            {
                _targetRot = Quaternion.Euler(isOn ? onLocalEuler : offLocalEuler);
                if (force)
                    pivot.localRotation = _targetRot;
            }
        }
    }
}