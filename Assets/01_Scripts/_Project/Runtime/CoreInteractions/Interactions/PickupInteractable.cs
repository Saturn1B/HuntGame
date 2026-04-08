using Unity.Netcode;
using UnityEngine;
using HuntGame.Interactions;

namespace HuntGame.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class PickupInteractable : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private float maxPickupDistance = 3f;
        [SerializeField] private bool canBeDropped = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        private Rigidbody _rb;
        private bool _isHeld;

        public bool IsHeld => _isHeld;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        // ── IInteractable ────────────────────────────────────────────────────

        public bool CanInteract(in InteractionContext context)
        {
            if (context.Verb != InteractionVerb.Use) return false;

            // Si déjà tenu, permet le drop via Use
            if (_isHeld && canBeDropped) return true;
            if (_isHeld) return false;

            // Vérifie la distance
            if (context.Interactor != null)
            {
                float dist = Vector3.Distance(
                    context.Interactor.position, transform.position);
                if (dist > maxPickupDistance) return false;
            }

            return true;
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context)) return;

            if (_isHeld)
            {
                // Drop — le socket appellera ServerDrop via NetcodeInteractableObject
                ServerDrop();
                return;
            }

            // Pickup — trouve le socket sur l'interacteur
            var socket = context.Interactor
                .GetComponentInChildren<HeldItemSocket>();

            if (socket == null)
            {
                // Cherche sur le NetworkObject parent du joueur
                socket = context.Interactor
                    .GetComponentInParent<HeldItemSocket>();
            }

            if (socket == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[PickupInteractable] Aucun HeldItemSocket trouvé sur l'interacteur.");
                return;
            }

            ServerPickup(socket);
        }

        // ── Server-side logic ────────────────────────────────────────────────

        // Appelé côté serveur par NetcodeInteractableObject via BroadcastInteractClientRpc
        public void ServerPickup(HeldItemSocket socket)
        {
            if (_isHeld) return;

            _isHeld = true;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;

            socket.Attach(this); // maintenant sans SetParent, juste stocke la référence

            if (debugLogs)
                Debug.Log($"[PickupInteractable] {name} ramassé.");
        }

        public void ServerDrop()
        {
            if (!_isHeld) return;

            _isHeld = false;
            _rb.isKinematic = false;
            _rb.detectCollisions = true;
            // Plus de SetParent ici

            _rb.AddForce(transform.forward * 2f, ForceMode.Impulse);

            if (debugLogs)
                Debug.Log($"[PickupInteractable] {name} posé.");
        }
    }
}