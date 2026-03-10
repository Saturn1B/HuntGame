using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerInteractor : NetworkBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private float rayDistance = 3f;
    [SerializeField] private LayerMask interactableMask = ~0;

    [Header("Input System")]
    [SerializeField] private Key useKey = Key.E;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void Update()
    {
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // In network mode, only the owning client should read input.
        if (isNetworked && !IsOwner)
            return;

        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb[useKey].wasPressedThisFrame)
        {
            TryRequestUse(InteractionVerb.Use);
        }
    }

    private void TryRequestUse(InteractionVerb verb)
    {
        if (viewCamera == null)
        {
            Debug.LogError("[PlayerInteractor] viewCamera is not assigned.");
            return;
        }

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            if (debugLogs) Debug.Log("[PlayerInteractor] Raycast hit nothing.");
            return;
        }

        // 1) New migrated pipeline (preferred)
        NetInteractableAdapter newAdapter = hit.collider.GetComponentInParent<NetInteractableAdapter>();
        if (newAdapter != null)
        {
            if (debugLogs) Debug.Log($"[PlayerInteractor] Hit NetInteractableAdapter: {newAdapter.name}");
            RequestUseNewAdapterServerRpc(newAdapter.NetworkObject, verb);
            return;
        }

        // 2) Legacy pipeline (still supported)
        NetInteractable legacy = hit.collider.GetComponentInParent<NetInteractable>();
        if (legacy != null)
        {
            if (debugLogs) Debug.Log($"[PlayerInteractor] Hit legacy NetInteractable: {legacy.name}");
            RequestUseLegacyServerRpc(legacy.NetworkObject, verb);
            return;
        }

        // 3) Standalone pipeline (only when not networked)
        InteractableCore core = hit.collider.GetComponentInParent<InteractableCore>();
        if (core != null)
        {
            bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isNetworked)
            {
                // Avoid desync: if you're in multiplayer, the object should be networked via NetInteractableAdapter.
                Debug.LogWarning($"[PlayerInteractor] Hit InteractableCore '{core.name}' but it is not networked. Add NetworkObject + NetInteractableAdapter to use it in multiplayer.", core);
                return;
            }

            if (debugLogs) Debug.Log($"[PlayerInteractor] Hit standalone InteractableCore: {core.name}");
            core.TryUseLocal(transform, verb);
        }
    }

    [ServerRpc]
    private void RequestUseLegacyServerRpc(NetworkObjectReference targetRef, InteractionVerb verb, ServerRpcParams rpcParams = default)
    {
        if (!targetRef.TryGet(out NetworkObject targetObj))
            return;

        NetInteractable interactable = targetObj.GetComponent<NetInteractable>();
        if (interactable == null)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != OwnerClientId)
            return;

        interactable.TryUseServer(NetworkObject, sender, verb);
    }

    [ServerRpc]
    private void RequestUseNewAdapterServerRpc(NetworkObjectReference targetRef, InteractionVerb verb, ServerRpcParams rpcParams = default)
    {
        if (!targetRef.TryGet(out NetworkObject targetObj))
            return;

        NetInteractableAdapter adapter = targetObj.GetComponent<NetInteractableAdapter>();
        if (adapter == null)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender != OwnerClientId)
            return;

        adapter.TryUseServer(NetworkObject, sender, verb);
    }
}