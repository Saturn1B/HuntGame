using Unity.Netcode;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DungeonSteakhouse.Net.InteractionsV2
{
    [DisallowMultipleComponent]
    public sealed class NetInteractorV2 : NetworkBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private float raycastDistance = 3.0f;
        [SerializeField] private QueryTriggerInteraction triggerMode = QueryTriggerInteraction.Ignore;

        [Header("Input")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("If null, a default action is created: E key + gamepad south button.")]
        [SerializeField] private InputActionReference useActionReference;
        private InputAction _defaultUseAction;
#endif

        [Header("Debug Ray")]
        [Tooltip("If true, draws a debug ray every frame (Scene view).")]
        [SerializeField] private bool debugDrawRay = true;

        [Tooltip("If true, uses a LineRenderer so you can see the ray in Game view too.")]
        [SerializeField] private bool debugUseLineRenderer = true;

        [Tooltip("Optional LineRenderer (if null and debugUseLineRenderer is true, it will be created).")]
        [SerializeField] private LineRenderer debugLine;

        [Tooltip("Ray width when using LineRenderer.")]
        [Range(0.001f, 0.05f)]
        [SerializeField] private float debugLineWidth = 0.01f;

        [Tooltip("If true, prints debug logs (spammy).")]
        [SerializeField] private bool logDebug = false;

        private uint _predictionCounter;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            ResolveCamera();
            SetupInput();

            if (debugUseLineRenderer)
                EnsureDebugLineRenderer();
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            _defaultUseAction?.Enable();
            if (useActionReference != null && useActionReference.action != null)
                useActionReference.action.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            _defaultUseAction?.Disable();
            if (useActionReference != null && useActionReference.action != null)
                useActionReference.action.Disable();
#endif
        }

        private void Update()
        {
            if (!IsOwner) return;

            ResolveCamera();

            // Always draw ray (debug)
            DrawDebugRay();

            if (WasUsePressed())
            {
                TryUse(UseAction.Primary);
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
            if (useActionReference == null || useActionReference.action == null)
            {
                _defaultUseAction = new InputAction(
                    name: "Use",
                    type: InputActionType.Button,
                    binding: "<Keyboard>/e"
                );

                _defaultUseAction.AddBinding("<Gamepad>/buttonSouth");
                _defaultUseAction.Enable();
            }
            else
            {
                useActionReference.action.Enable();
            }
#else
            Debug.LogError("[NetInteractorV2] This project requires the Input System (ENABLE_INPUT_SYSTEM).");
#endif
        }

        private bool WasUsePressed()
        {
#if ENABLE_INPUT_SYSTEM
            InputAction a = (useActionReference != null && useActionReference.action != null)
                ? useActionReference.action
                : _defaultUseAction;

            return a != null && a.WasPressedThisFrame();
#else
            return false;
#endif
        }

        private void TryUse(UseAction action)
        {
            if (raycastCamera == null) return;

            Ray ray = new Ray(raycastCamera.transform.position, raycastCamera.transform.forward);

            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, interactMask, triggerMode))
            {
                if (logDebug) Debug.Log("[NetInteractorV2] No hit.");
                return;
            }

            if (!TryFindUsableAndNetworkObject(hit.collider, out NetUsable usable, out NetworkObject netObj))
            {
                if (logDebug) Debug.Log("[NetInteractorV2] Hit has no NetUsable/NetworkObject in parents.");
                return;
            }

            if (netObj == null || !netObj.IsSpawned)
            {
                if (logDebug) Debug.LogWarning("[NetInteractorV2] Target NetworkObject not spawned.");
                return;
            }

            uint predictionId = ++_predictionCounter;

            var ctx = new UseContext(
                interactorClientId: NetworkManager.LocalClientId,
                hitPoint: hit.point,
                predictionId: predictionId,
                action: action
            );

            // Client prediction (instant)
            usable.ClientPredictUse(in ctx);

            // Server request
            RequestUseServerRpc(netObj.NetworkObjectId, ctx);
        }

        private void DrawDebugRay()
        {
            if (!debugDrawRay && !debugUseLineRenderer)
                return;

            if (raycastCamera == null)
                return;

            Vector3 origin = raycastCamera.transform.position;
            Vector3 dir = raycastCamera.transform.forward;

            Ray ray = new Ray(origin, dir);

            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, raycastDistance, interactMask, triggerMode);

            Color c;
            Vector3 endPoint;

            if (!hitSomething)
            {
                c = Color.red; // no hit
                endPoint = origin + dir * raycastDistance;
            }
            else
            {
                endPoint = hit.point;

                // If we hit a usable => green, else yellow
                bool hitUsable = TryFindUsableAndNetworkObject(hit.collider, out _, out _);
                c = hitUsable ? Color.green : Color.yellow;
            }

            if (debugDrawRay)
            {
                Debug.DrawLine(origin, endPoint, c);
                Debug.DrawRay(origin, dir * 0.15f, c); // tiny indicator at origin
            }

            if (debugUseLineRenderer)
            {
                EnsureDebugLineRenderer();
                if (debugLine != null)
                {
                    debugLine.enabled = true;
                    debugLine.positionCount = 2;
                    debugLine.SetPosition(0, origin);
                    debugLine.SetPosition(1, endPoint);

                    // We intentionally don't set colors to keep it simple; Unity will use the material's color.
                    // If you want color feedback, we can duplicate materials or use gradient.
                }
            }
            else
            {
                if (debugLine != null)
                    debugLine.enabled = false;
            }
        }

        private void EnsureDebugLineRenderer()
        {
            if (debugLine != null)
                return;

            GameObject go = new GameObject("DebugRay_LineRenderer");
            go.transform.SetParent(transform, worldPositionStays: false);

            debugLine = go.AddComponent<LineRenderer>();
            debugLine.useWorldSpace = true;
            debugLine.positionCount = 2;

            debugLine.startWidth = debugLineWidth;
            debugLine.endWidth = debugLineWidth;

            // Use Unity built-in default sprite shader (works in most pipelines).
            // If your project uses URP/HDRP, we may want a pipeline-specific material later.
            debugLine.material = new Material(Shader.Find("Sprites/Default"));
        }

        private static bool TryFindUsableAndNetworkObject(Collider hitCollider, out NetUsable usable, out NetworkObject netObj)
        {
            usable = null;
            netObj = null;

            if (hitCollider == null) return false;

            Transform t = hitCollider.transform;
            while (t != null)
            {
                if (usable == null && t.TryGetComponent(out NetUsable u))
                    usable = u;

                if (netObj == null && t.TryGetComponent(out NetworkObject n))
                    netObj = n;

                if (usable != null && netObj != null)
                    return true;

                t = t.parent;
            }

            return false;
        }

        [ServerRpc]
        private void RequestUseServerRpc(ulong targetNetworkObjectId, UseContext ctx, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong sender = rpcParams.Receive.SenderClientId;

            // Authoritative interactor id
            ctx.InteractorClientId = sender;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null)
                return;

            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObj) || targetObj == null)
                return;

            if (!TryFindUsableOnNetworkObject(targetObj, out NetUsable usable))
                return;

            if (!TryGetAuthoritativeInteractorPosition(nm, sender, out Vector3 serverInteractorPos))
            {
                RejectUseClientRpc(targetNetworkObjectId, ctx, MakeTargetParams(sender));
                return;
            }

            if (!usable.ServerCanUse(in ctx, serverInteractorPos))
            {
                RejectUseClientRpc(targetNetworkObjectId, ctx, MakeTargetParams(sender));
                return;
            }

            bool applied = usable.ServerUse(in ctx);
            if (!applied)
            {
                RejectUseClientRpc(targetNetworkObjectId, ctx, MakeTargetParams(sender));
            }
        }

        [ClientRpc]
        private void RejectUseClientRpc(ulong targetNetworkObjectId, UseContext ctx, ClientRpcParams rpcParams = default)
        {
            // Only the requesting client receives this
            if (!IsOwner) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null)
                return;

            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObj) || targetObj == null)
                return;

            if (!TryFindUsableOnNetworkObject(targetObj, out NetUsable usable))
                return;

            usable.ClientReconcileRejected(in ctx);
        }

        private static ClientRpcParams MakeTargetParams(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }

        private static bool TryGetAuthoritativeInteractorPosition(NetworkManager nm, ulong senderClientId, out Vector3 position)
        {
            position = default;

            if (nm == null) return false;

            if (!nm.ConnectedClients.TryGetValue(senderClientId, out var client) || client == null || client.PlayerObject == null)
                return false;

            position = client.PlayerObject.transform.position;
            return true;
        }

        private static bool TryFindUsableOnNetworkObject(NetworkObject targetObj, out NetUsable usable)
        {
            usable = null;
            if (targetObj == null) return false;

            if (targetObj.TryGetComponent(out NetUsable u))
            {
                usable = u;
                return true;
            }

            Transform root = targetObj.transform;
            var stack = new System.Collections.Generic.Stack<Transform>(root.childCount + 1);
            stack.Push(root);

            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                if (t != null && t.TryGetComponent(out NetUsable childUsable))
                {
                    usable = childUsable;
                    return true;
                }

                if (t == null) continue;

                for (int i = 0; i < t.childCount; i++)
                    stack.Push(t.GetChild(i));
            }

            return false;
        }
    }
}