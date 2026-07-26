using UnityEngine;
using HuntingGame.Inventory;
using HuntGame.Interactions;

namespace HuntingGame.Item
{
    public class Munition : MonoBehaviour, IInteractable
    {
        [Header("Ammo Visual Settings")]
        [SerializeField] protected OrientationSettings orientation;
        [SerializeField] protected bool isPhysic;
        [SerializeField] protected bool isPersistent;

        [Space]

        [Header("Ammo Damage Settings")]
        [SerializeField] protected int damage;

        protected Rigidbody rb;
        protected BoxCollider boxCollider;
        protected ItemBehaviour itemBehaviour;

        protected bool isShot;

        protected virtual void Awake()
        {
            if(isPhysic)
                rb = GetComponent<Rigidbody>();
            boxCollider = GetComponent<BoxCollider>();
            itemBehaviour = GetComponent<ItemBehaviour>();
        }

        private void Start()
		{
            isShot = false;

            boxCollider.enabled = false;

            if (isPhysic)
                rb.isKinematic = true;
        }

        public virtual void Shoot(float shootForce, Vector3 rndOffset)
        {
            isShot = true;

            transform.SetParent(null);

            boxCollider.enabled = true;

            if (isPhysic)
			{
                rb.isKinematic = false;

                Vector3 shootDir = orientation.GetForwardDirection(transform);

                Vector3 finalDir = (shootDir + rndOffset).normalized;

                rb.AddForce(finalDir * shootForce, ForceMode.Impulse);
            }
			else
			{
                //SHOOT WITHOUT PHYSIC
			}
        }

        protected virtual void FixedUpdate()
        {
            if (!isPhysic) return;
            if (!isShot) return;

            if (rb.linearVelocity.magnitude > .1f)
            {
                Quaternion targetRotation = RotationUtils.GetCorrectedLookRotation(rb.linearVelocity.normalized, Vector3.up, orientation);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * 10);
            }
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            isShot = false;

            if (collision.transform.TryGetComponent(out Health health))
                health.ChangeHealth(-damage);

            if(!isPersistent)
                Destroy(gameObject);
			else
			{
				if (isPhysic)
				{
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                boxCollider.isTrigger = true;

                if(collision.transform.lossyScale == Vector3.one)
                    transform.SetParent(collision.transform, true);
            }
        }

		public void Interact(in InteractionContext context)
		{
            if (context.Verb != InteractionVerb.Grab) return;

            if (context.Interactor != null && context.Interactor.TryGetComponent(out PlayerInventory inventory))
                inventory.AddItem(itemBehaviour.itemData);

            Destroy(gameObject);
        }

		public bool CanInteract(in InteractionContext context)
		{
            return !isShot && isPersistent;
		}
	}
}
