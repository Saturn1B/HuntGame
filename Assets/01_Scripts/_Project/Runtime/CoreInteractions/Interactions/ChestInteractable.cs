using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using HuntGame.Interactions;
using UnityEditor.PackageManager;

namespace HuntGame.Interactions
{
    [DisallowMultipleComponent]
    public class ChestInteractable : MonoBehaviour, IInteractable
    {
        [Header("Lid")]
        [SerializeField] private Transform lid;
        [SerializeField] private float closedAngle = 0f;
        [SerializeField] private float openAngle = -110f;
        [SerializeField] private float openSpeed = 3f;

        [Header("Interaction")]
        [SerializeField] private float maxUseDistance = 2.5f;
        [SerializeField] private float useCooldown = 0.4f;

        [Header("Events")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        private bool _isOpen;
        private bool _isAnimating;
        private float _lastUseTime;
        private Quaternion _closedRot;
        private Quaternion _openRot;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (lid == null) return;
            _closedRot = Quaternion.Euler(closedAngle, 0f, 0f);
            _openRot = Quaternion.Euler(openAngle, 0f, 0f);
        }

        // ── IInteractable ────────────────────────────────────────────────────

        public bool CanInteract(in InteractionContext context)
        {
            if (_isAnimating) return false;
            if (Time.time - _lastUseTime < useCooldown) return false;
            if (context.Verb != InteractionVerb.Use &&
                context.Verb != InteractionVerb.Open &&
                context.Verb != InteractionVerb.Close) return false;

            if (context.Interactor != null)
            {
                float dist = Vector3.Distance(
                    context.Interactor.position, transform.position);
                if (dist > maxUseDistance) return false;
            }

            // Open ne marche que si fermé, Close que si ouvert
            if (context.Verb == InteractionVerb.Open && _isOpen) return false;
            if (context.Verb == InteractionVerb.Close && !_isOpen) return false;

            return true;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return;
            SetState(!_isOpen);
        }

        public void SetState(bool open)
        {
            _isOpen = open;
            _lastUseTime = Time.time;

            if (debugLogs)
                Debug.Log($"[ChestInteractable] {name} → {(open ? "ouvert" : "fermé")}");

            if (open) onOpened?.Invoke();
            else onClosed?.Invoke();

            if (lid != null)
                StartCoroutine(AnimateLid(open ? _openRot : _closedRot));
        }

        // ── Animation ────────────────────────────────────────────────────────

        private IEnumerator AnimateLid(Quaternion target)
        {
            _isAnimating = true;

            while (Quaternion.Angle(lid.localRotation, target) > 0.5f)
            {
                lid.localRotation = Quaternion.Lerp(
                    lid.localRotation, target,
                    Time.deltaTime * openSpeed);
                yield return null;
            }

            lid.localRotation = target;
            _isAnimating = false;
        }
    }
}