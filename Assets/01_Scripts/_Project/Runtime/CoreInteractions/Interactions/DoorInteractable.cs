using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HuntGame.Interactions
{
    [DisallowMultipleComponent]
    public sealed class DoorInteractable : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Transform _doorPivot;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _openCloseTime = 0.5f;
        [SerializeField] private float _maxUseDistance = 3f;
        [SerializeField] private float _useCooldownSeconds = 0.1f;
        [SerializeField] private bool _debugLogs;

        // Server-written, everyone-read: lets a late-joining/reconnecting client resync the
        // door's current state from the spawn snapshot instead of only from one-shot ClientRpcs.
        private readonly NetworkVariable<bool> _isOpenNetworked = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool _isOpen;
        private bool _isAnimating;
        private float _lastUseTime;
        private Quaternion _closedRotation;
        private Quaternion _openRotation;

        private void Awake()
        {
            if (_doorPivot == null)
            {
                _doorPivot = transform;
                if (_debugLogs) Debug.LogWarning("[DoorInteractable] _doorPivot not assigned, using own transform.");
            }

            _closedRotation = _doorPivot.localRotation;
            _openRotation = _closedRotation * Quaternion.Euler(0f, _openAngle, 0f);

            if (_debugLogs) Debug.Log($"[DoorInteractable] Initialized. ClosedRot={_closedRotation.eulerAngles} OpenRot={_openRotation.eulerAngles}");
        }

        public override void OnNetworkSpawn()
        {
            _isOpenNetworked.OnValueChanged += OnNetworkedStateChanged;

            // Resync path: applies immediately for a client whose spawn snapshot already carries
            // a non-default value (e.g. a late joiner, or a client reconnecting mid-run).
            if (_isOpenNetworked.Value != _isOpen)
                SetState(_isOpenNetworked.Value, instant: true);
        }

        public override void OnNetworkDespawn()
        {
            _isOpenNetworked.OnValueChanged -= OnNetworkedStateChanged;
        }

        private void OnNetworkedStateChanged(bool previous, bool current)
        {
            SetState(current);
        }

        public bool CanInteract(in InteractionContext context)
        {
            // Server context skips all local state checks (animating, cooldown, distance).
            // These are already validated client-side before the ServerRpc is sent.
            // Checking them server-side would block the broadcast when the host interacts,
            // because local prediction already mutated _isAnimating and _lastUseTime.
            if (context.IsServer)
            {
                if (_debugLogs) Debug.Log("[DoorInteractable] CanInteract = true (server context).");
                return true;
            }

            if (_isAnimating)
            {
                if (_debugLogs) Debug.Log("[DoorInteractable] CanInteract = false (animating).");
                return false;
            }

            float timeSinceLastUse = Time.time - _lastUseTime;
            if (timeSinceLastUse < _useCooldownSeconds)
            {
                if (_debugLogs) Debug.Log($"[DoorInteractable] CanInteract = false (cooldown {timeSinceLastUse:F2}s / {_useCooldownSeconds}s).");
                return false;
            }

            if (context.Interactor == null)
            {
                if (_debugLogs) Debug.Log("[DoorInteractable] CanInteract = true (no interactor).");
                return true;
            }

            float sqrDist = (_doorPivot.position - context.Interactor.position).sqrMagnitude;
            float sqrMax = _maxUseDistance * _maxUseDistance;

            if (sqrDist > sqrMax)
            {
                if (_debugLogs) Debug.Log($"[DoorInteractable] CanInteract = false (distance {Mathf.Sqrt(sqrDist):F2}m > max {_maxUseDistance}m).");
                return false;
            }

            if (_debugLogs) Debug.Log($"[DoorInteractable] CanInteract = true (distance {Mathf.Sqrt(sqrDist):F2}m).");
            return true;
        }

        public void Interact(in InteractionContext context)
        {
            _lastUseTime = Time.time;

            bool targetState = context.Verb switch
            {
                InteractionVerb.Open => true,
                InteractionVerb.Close => false,
                _ => !_isOpen
            };

            if (_debugLogs) Debug.Log($"[DoorInteractable] Interact called. Verb={context.Verb} IsServer={context.IsServer} → targetState={targetState}");

            SetState(targetState);
        }

        public void SetState(bool open) => SetState(open, instant: false);

        private void SetState(bool open, bool instant)
        {
            if (IsServer)
                _isOpenNetworked.Value = open;

            if (_isOpen == open)
            {
                if (_debugLogs) Debug.Log($"[DoorInteractable] SetState({open}) ignored, already in that state.");
                return;
            }

            if (_debugLogs) Debug.Log($"[DoorInteractable] SetState({open}) → starting animation.");

            _isOpen = open;
            StopAllCoroutines();

            if (instant)
            {
                // Resync path (late join / reconnect): snap directly, no need to replay the animation.
                _doorPivot.localRotation = _isOpen ? _openRotation : _closedRotation;
                return;
            }

            StartCoroutine(AnimateDoor(_isOpen ? _openRotation : _closedRotation));
        }

        private IEnumerator AnimateDoor(Quaternion targetRotation)
        {
            _isAnimating = true;
            Quaternion startRotation = _doorPivot.localRotation;
            float elapsed = 0f;

            if (_debugLogs) Debug.Log($"[DoorInteractable] Animation started. From={startRotation.eulerAngles} To={targetRotation.eulerAngles}");

            while (elapsed < _openCloseTime)
            {
                elapsed += Time.deltaTime;
                _doorPivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.Clamp01(elapsed / _openCloseTime));
                yield return null;
            }

            _doorPivot.localRotation = targetRotation;
            _isAnimating = false;

            if (_debugLogs) Debug.Log($"[DoorInteractable] Animation complete. IsOpen={_isOpen}");
        }
    }
}