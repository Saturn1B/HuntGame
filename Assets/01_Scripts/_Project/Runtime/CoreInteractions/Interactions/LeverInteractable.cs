using UnityEngine;
using UnityEngine.Events;
using HuntGame.Interactions;

namespace HuntGame.Interactions
{
    [DisallowMultipleComponent]
    public class LeverInteractable : MonoBehaviour, IInteractable
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

        private bool _isOn;
        private bool _isAnimating;
        private float _lastUseTime;

        public bool IsOn => _isOn;

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

        public void SetState(bool on)
        {
            _isOn = on;
            _lastUseTime = Time.time;

            if (debugLogs)
                Debug.Log($"[LeverInteractable] {name} → {(on ? "ON" : "OFF")}");

            if (on) onTurnedOn?.Invoke();
            else onTurnedOff?.Invoke();

            if (leverPivot != null)
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