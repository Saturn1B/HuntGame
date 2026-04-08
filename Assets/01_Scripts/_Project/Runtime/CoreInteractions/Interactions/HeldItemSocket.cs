using UnityEngine;

namespace HuntGame.Interactions
{
    [DisallowMultipleComponent]
    public class HeldItemSocket : MonoBehaviour
    {
        [Header("Hold Settings")]
        [SerializeField] private Vector3 holdPositionOffset = new Vector3(0.3f, -0.2f, 0.5f);
        [SerializeField] private Vector3 holdRotationOffset = Vector3.zero;
        [SerializeField] private float attachSpeed = 12f;

        private PickupInteractable _heldItem;

        public bool HasItem => _heldItem != null;
        public PickupInteractable HeldItem => _heldItem;

        public void Attach(PickupInteractable item)
        {
            if (_heldItem != null) Drop();
            _heldItem = item;
        }

        public void Drop()
        {
            if (_heldItem == null) return;
            _heldItem = null;
        }

        private void Update()
        {
            if (_heldItem == null) return;

            // Suit le socket sans re-parenter — compatible NetworkObject
            Vector3 targetPos = transform.TransformPoint(holdPositionOffset);
            Quaternion targetRot = transform.rotation * Quaternion.Euler(holdRotationOffset);

            _heldItem.transform.position = Vector3.Lerp(
                _heldItem.transform.position, targetPos,
                Time.deltaTime * attachSpeed);

            _heldItem.transform.rotation = Quaternion.Lerp(
                _heldItem.transform.rotation, targetRot,
                Time.deltaTime * attachSpeed);
        }
    }
}