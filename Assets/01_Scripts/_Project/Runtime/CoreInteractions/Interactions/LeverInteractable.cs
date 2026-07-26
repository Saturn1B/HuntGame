using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using HuntGame.Interactions;

namespace HuntGame.Interactions
{
    [DisallowMultipleComponent]
    public class LeverInteractable : NetworkBehaviour, IInteractable
    {
        [Header("Pivot")]
        [SerializeField] private Transform leverPivot;
        [SerializeField] private float angleOff = 0f;
        [SerializeField] private float angleOn = 45f;
        [SerializeField] private float animSpeed = 8f;

        [Header("Events")]
        public UnityEvent onTurnedOn;
        public UnityEvent onTurnedOff;

        [Header("Cooldown")]
        [SerializeField] private float useCooldown = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        // Server-written, everyone-read: lets a late-joining/reconnecting client resync the
        // lever's current state from the spawn snapshot instead of only from one-shot ClientRpcs.
        private readonly NetworkVariable<bool> _isOnNetworked = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _isOn;
        private bool _isAnimating;
        private float _lastUseTime;

        public bool IsOn => _isOn;

        public override void OnNetworkSpawn()
        {
            _isOnNetworked.OnValueChanged += OnNetworkedStateChanged;

            if (_isOnNetworked.Value != _isOn)
                SetState(_isOnNetworked.Value, instant: true);
        }

        public override void OnNetworkDespawn()
        {
            _isOnNetworked.OnValueChanged -= OnNetworkedStateChanged;
        }

        private void OnNetworkedStateChanged(bool previous, bool current)
        {
            SetState(current);
        }

        // ── IInteractable ────────────────────────────────────────────────────

        public bool CanInteract(in InteractionContext context)
        {
            if (_isAnimating) return false;
            if (Time.time - _lastUseTime < useCooldown) return false;
            return context.Verb == InteractionVerb.Use;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return;
            SetState(!_isOn);
        }

        // ── Public API (appelé par le serveur via NetcodeInteractableObject) ─

        public void SetState(bool on) => SetState(on, instant: false);

        private void SetState(bool on, bool instant)
        {
            if (IsServer)
                _isOnNetworked.Value = on;

            if (_isOn == on) return;

            _isOn = on;
            _lastUseTime = Time.time;

            if (debugLogs)
                Debug.Log($"[LeverInteractable] {name} → {(on ? "ON" : "OFF")}");

            if (on) onTurnedOn?.Invoke();
            else onTurnedOff?.Invoke();

            if (leverPivot == null) return;

            if (instant)
            {
                // Resync path (late join / reconnect): snap directly, no need to replay the animation.
                StopAllCoroutines();
                leverPivot.localRotation = Quaternion.Euler(on ? angleOn : angleOff, 0f, 0f);
                return;
            }

            StartCoroutine(AnimateLever(on ? angleOn : angleOff));
        }

        // ── Animation ────────────────────────────────────────────────────────

        private System.Collections.IEnumerator AnimateLever(float targetAngle)
        {
            _isAnimating = true;
            var targetRot = Quaternion.Euler(targetAngle, 0f, 0f);

            while (Quaternion.Angle(leverPivot.localRotation, targetRot) > 0.5f)
            {
                leverPivot.localRotation = Quaternion.Lerp(
                    leverPivot.localRotation, targetRot,
                    Time.deltaTime * animSpeed);
                yield return null;
            }

            leverPivot.localRotation = targetRot;
            _isAnimating = false;
        }
    }
}