using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace HuntGame.Interactions
{
    /// <summary>
    /// Owner-side component that raycasts and triggers interactions.
    /// Enabled/disabled externally by NetFpsPlayerAdapter based on ownership.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera _viewCamera;
        [SerializeField] private float _rayDistance = 3f;
        [SerializeField] private LayerMask _interactableMask = ~0;
        [SerializeField] private Key _useKey = Key.E;
        [SerializeField] private bool _debugLogs;

        private void Update()
        {
            if (Keyboard.current == null)
            {
                if (_debugLogs) Debug.LogWarning("[PlayerInteractor] Keyboard.current is null.");
                return;
            }

            if (!Keyboard.current[_useKey].wasPressedThisFrame) return;

            if (_debugLogs) Debug.Log("[PlayerInteractor] Key pressed.");

            TryInteract(InteractionVerb.Use);
        }

        private void TryInteract(InteractionVerb verb)
        {
            if (_viewCamera == null)
            {
                Debug.LogError("[PlayerInteractor] _viewCamera is not assigned.");
                return;
            }

            Ray ray = new Ray(_viewCamera.transform.position, _viewCamera.transform.forward);

            if (_debugLogs)
                Debug.DrawRay(ray.origin, ray.direction * _rayDistance, Color.yellow, 0.5f);

            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, _rayDistance, _interactableMask);

            if (!hit)
            {
                if (_debugLogs) Debug.Log("[PlayerInteractor] Raycast hit nothing.");
                return;
            }

            if (_debugLogs) Debug.Log($"[PlayerInteractor] Raycast hit: {hitInfo.collider.name} (layer: {LayerMask.LayerToName(hitInfo.collider.gameObject.layer)})");

            // Networked path
            var netInteractable = hitInfo.collider.GetComponentInParent<NetcodeInteractableObject>();
            if (netInteractable != null)
            {
                if (_debugLogs) Debug.Log($"[PlayerInteractor] Found NetcodeInteractableObject on {netInteractable.name}. Requesting interact...");

                var playerNetObj = GetComponent<NetworkObject>();
                if (playerNetObj == null)
                {
                    Debug.LogError("[PlayerInteractor] No NetworkObject found on this GameObject. Required for networked interaction.");
                    return;
                }

                netInteractable.RequestInteractFromClient(verb, playerNetObj);
                return;
            }

            // Local path
            var interactable = hitInfo.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                if (_debugLogs) Debug.Log($"[PlayerInteractor] Hit {hitInfo.collider.name} but no IInteractable or NetcodeInteractableObject found.");
                return;
            }

            if (_debugLogs) Debug.Log($"[PlayerInteractor] Found local IInteractable on {((MonoBehaviour)interactable).name}.");

            var context = InteractionContext.Local(transform, verb);

            bool canInteract = interactable.CanInteract(in context);
            if (_debugLogs) Debug.Log($"[PlayerInteractor] CanInteract = {canInteract}.");

            if (canInteract)
                interactable.Interact(in context);
        }
    }
}