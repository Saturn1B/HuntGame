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

    private void Update()
    {
        // Only the owning client should read input and fire the raycast
        if (!IsOwner)
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
            Debug.LogError("PlayerInteractor: viewCamera is not assigned.");
            return;
        }

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableMask, QueryTriggerInteraction.Ignore))
            return;

        NetInteractable target = hit.collider.GetComponentInParent<NetInteractable>();
        if (target == null)
            return;

        RequestUseServerRpc(target.NetworkObject, verb);
    }

    [ServerRpc]
    private void RequestUseServerRpc(NetworkObjectReference targetRef, InteractionVerb verb, ServerRpcParams rpcParams = default)
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
}