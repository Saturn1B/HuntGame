using Unity.Netcode;
using UnityEngine;
using HuntGame.Interactions;

namespace HuntGame.Interactions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class PickupInteractable : NetworkBehaviour, IInteractable, INetworkResyncable
    {
        [Header("Settings")]
        [SerializeField] private float maxPickupDistance = 3f;
        [SerializeField] private bool canBeDropped = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogs;

        // Server-written, everyone-read: lets a late-joining/reconnecting client resync who (if
        // anyone) is holding this item instead of only from one-shot ClientRpcs.
        private readonly NetworkVariable<bool> _isHeldNetworked = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> _holderNetworkObjectId = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody _rb;
        private bool _isHeld;

        public bool IsHeld => _isHeld;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            _isHeldNetworked.OnValueChanged += OnHeldStateChanged;
            ResyncFromNetworkState();
        }

        public override void OnNetworkDespawn()
        {
            _isHeldNetworked.OnValueChanged -= OnHeldStateChanged;
        }

        private void OnHeldStateChanged(bool previous, bool current) => ResyncFromNetworkState();

        /// <summary>
        /// Applies the replicated held/holder state locally without touching the NetworkVariables
        /// again -- used for late-join / reconnect resync, as well as correcting a client whose
        /// locally-predicted pickup/drop the server ended up rejecting.
        /// </summary>
        public void ResyncFromNetworkState()
        {
            if (_isHeldNetworked.Value == _isHeld) return;

            if (!_isHeldNetworked.Value)
            {
                ApplyDrop(withImpulse: false);
                return;
            }

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(_holderNetworkObjectId.Value, out var holderObj))
                return;

            var socket = holderObj.GetComponentInChildren<HeldItemSocket>()
                         ?? holderObj.GetComponentInParent<HeldItemSocket>();

            if (socket != null)
                ApplyPickup(socket);
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

            ApplyPickup(socket);

            if (IsServer)
            {
                var holderNetObj = socket.GetComponentInParent<NetworkObject>();
                _holderNetworkObjectId.Value = holderNetObj != null ? holderNetObj.NetworkObjectId : 0;
                _isHeldNetworked.Value = true;
            }

            if (debugLogs)
                Debug.Log($"[PickupInteractable] {name} ramassé.");
        }

        public void ServerDrop()
        {
            if (!_isHeld) return;

            ApplyDrop(withImpulse: true);

            if (IsServer)
            {
                _isHeldNetworked.Value = false;
                _holderNetworkObjectId.Value = 0;
            }

            if (debugLogs)
                Debug.Log($"[PickupInteractable] {name} posé.");
        }

        private void ApplyPickup(HeldItemSocket socket)
        {
            _isHeld = true;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;

            socket.Attach(this); // maintenant sans SetParent, juste stocke la référence
        }

        private void ApplyDrop(bool withImpulse)
        {
            _isHeld = false;
            _rb.isKinematic = false;
            _rb.detectCollisions = true;
            // Plus de SetParent ici

            // Only the actual drop action gets the little "toss" impulse -- a resync (late join /
            // reconnect) must not retroactively shove an item that has been sitting still for a while.
            if (withImpulse)
                _rb.AddForce(transform.forward * 2f, ForceMode.Impulse);
        }
    }
}