using Unity.Netcode;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DungeonSteakhouse.Net.Interactions
{
    /// <summary>
    /// Player-side interaction driver (owner only).
    /// - Raycasts from the local camera
    /// - Sends an interaction request to the server for the hit NetworkObject
    ///
    /// Input System ONLY (no legacy UnityEngine.Input calls).
    /// Includes verbose debug logs to quickly diagnose setup issues.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetInteractor : NetworkBehaviour
    {
        [Header("Raycast")]
        [Tooltip("Camera used for interaction raycasts. If null, Camera.main will be used.")]
        [SerializeField] private Camera raycastCamera;

        [Tooltip("Physics layers used for interaction raycasts.")]
        [SerializeField] private LayerMask interactMask = ~0;

        [Tooltip("Max raycast distance (client-side). Server still validates distance separately.")]
        [SerializeField] private float raycastDistance = 3.0f;

        [Tooltip("If true, raycasts can hit trigger colliders too.")]
        [SerializeField] private bool includeTriggerColliders = false;

        [Header("Input (Input System)")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("Optional InputActionReference. If assigned, it will be used. Otherwise a default action is created (E key).")]
        [SerializeField] private InputActionReference interactActionReference;
        private InputAction _defaultInteractAction;
#endif

        [Header("Debug")]
        [Tooltip("If true, prints verbose logs to diagnose why interactions are not happening.")]
        [SerializeField] private bool logDebug = true;

        [Tooltip("If true, draws the ray each frame (Scene view / Game view gizmos depending on settings).")]
        [SerializeField] private bool drawDebugRay = true;

        [Tooltip("Minimum time between interaction attempts (seconds).")]
        [Range(0f, 1f)]
        [SerializeField] private float interactCooldownSeconds = 0.15f;

        private float _nextInteractTime;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                if (logDebug)
                    Debug.Log($"[NetInteractor] Disabled (not owner). OwnerClientId={OwnerClientId} LocalClientId={NetworkManager.Singleton?.LocalClientId}");
                enabled = false;
                return;
            }

            ResolveCamera();
            SetupInput();

            if (logDebug)
                Debug.Log($"[NetInteractor] Ready. LocalClientId={NetworkManager.Singleton?.LocalClientId} Camera='{(raycastCamera != null ? raycastCamera.name : "null")}' Mask={interactMask.value} Dist={raycastDistance:0.00}");
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            _defaultInteractAction?.Enable();
            if (interactActionReference != null && interactActionReference.action != null)
                interactActionReference.action.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            _defaultInteractAction?.Disable();
            if (interactActionReference != null && interactActionReference.action != null)
                interactActionReference.action.Disable();
#endif
        }

        private void LateUpdate()
        {
            if (!IsOwner)
                return;

            ResolveCamera();

            if (drawDebugRay && raycastCamera != null)
            {
                Debug.DrawRay(raycastCamera.transform.position, raycastCamera.transform.forward * raycastDistance, Color.white);
            }

            if (Time.unscaledTime < _nextInteractTime)
                return;

            if (WasInteractPressed())
            {
                _nextInteractTime = Time.unscaledTime + interactCooldownSeconds;

                if (logDebug)
                    Debug.Log("[NetInteractor] Interact pressed.");

                TryInteract();
            }
        }

        private void ResolveCamera()
        {
            if (raycastCamera == null)
                raycastCamera = Camera.main;
        }

        private void SetupInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (interactActionReference == null || interactActionReference.action == null)
            {
                _defaultInteractAction = new InputAction(
                    name: "Interact",
                    type: InputActionType.Button,
                    binding: "<Keyboard>/e"
                );

                _defaultInteractAction.AddBinding("<Gamepad>/buttonSouth");
                _defaultInteractAction.Enable();

                if (logDebug)
                    Debug.Log("[NetInteractor] Using default InputAction binding: <Keyboard>/e (and <Gamepad>/buttonSouth).");
            }
            else
            {
                interactActionReference.action.Enable();

                if (logDebug)
                    Debug.Log($"[NetInteractor] Using InputActionReference: '{interactActionReference.action.name}'.");
            }
#else
            Debug.LogError("[NetInteractor] ENABLE_INPUT_SYSTEM is not defined. This project requires the Input System package.");
#endif
        }

        private bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var action = (interactActionReference != null && interactActionReference.action != null)
                ? interactActionReference.action
                : _defaultInteractAction;

            return action != null && action.WasPressedThisFrame();
#else
            return false;
#endif
        }

        private void TryInteract()
        {
            if (raycastCamera == null)
            {
                if (logDebug)
                    Debug.LogWarning("[NetInteractor] No camera available (Camera.main is null).");
                return;
            }

            var ray = new Ray(raycastCamera.transform.position, raycastCamera.transform.forward);
            var triggerMode = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

            if (!Physics.Raycast(ray, out var hit, raycastDistance, interactMask, triggerMode))
            {
                if (logDebug)
                    Debug.Log("[NetInteractor] Raycast: no hit.");
                return;
            }

            if (logDebug)
                Debug.Log($"[NetInteractor] Raycast hit: '{hit.collider.name}' at {hit.distance:0.00}m.");

            var netObj = hit.collider != null ? hit.collider.GetComponentInParent<NetworkObject>() : null;
            if (netObj == null)
            {
                if (logDebug)
                    Debug.LogWarning("[NetInteractor] Hit object has NO NetworkObject in parents. Add NetworkObject to the interactable root.");
                return;
            }

            if (!netObj.IsSpawned)
            {
                if (logDebug)
                    Debug.LogWarning($"[NetInteractor] Hit NetworkObject is not spawned. name='{netObj.name}' netId={netObj.NetworkObjectId}");
                return;
            }

            if (logDebug)
                Debug.Log($"[NetInteractor] Target NetworkObject: name='{netObj.name}' netId={netObj.NetworkObjectId}.");

            RequestInteractServerRpc(netObj.NetworkObjectId, transform.position);

            if (logDebug)
                Debug.Log("[NetInteractor] RequestInteractServerRpc sent.");
        }

        [ServerRpc]
        private void RequestInteractServerRpc(ulong targetNetworkObjectId, Vector3 interactorPosition, ServerRpcParams rpcParams = default)
        {
            if (!IsServer)
                return;

            ulong sender = rpcParams.Receive.SenderClientId;

            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null)
            {
                Debug.LogWarning("[NetInteractor] Server: missing NetworkManager/SpawnManager.");
                return;
            }

            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out var targetObj) || targetObj == null)
            {
                Debug.LogWarning($"[NetInteractor] Server: target not found in SpawnedObjects. netId={targetNetworkObjectId}");
                return;
            }

            var interactable = targetObj.GetComponent<NetInteractable>();
            if (interactable == null)
                interactable = targetObj.GetComponentInChildren<NetInteractable>(true);

            if (interactable == null)
            {
                Debug.LogWarning($"[NetInteractor] Server: target has no NetInteractable. netId={targetNetworkObjectId} name='{targetObj.name}'");
                return;
            }

            bool ok = interactable.ServerTryInteract(sender, interactorPosition);
            Debug.Log($"[NetInteractor] Server: interaction processed. sender={sender} targetNetId={targetNetworkObjectId} ok={ok}");
        }
    }
}