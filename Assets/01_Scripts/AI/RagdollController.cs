using UnityEngine;
using System.Collections.Generic;
using HuntGame.Interactions;
using HuntGame.Player;

namespace HuntingGame.AI
{
    public class RagdollController : MonoBehaviour, IInteractable
    {
        public enum RagdollSize
        {
            SMALL,
            BIG
        }

        [Header("Ragdoll Bones")]
        public List<Rigidbody> ragdollBodies = new List<Rigidbody>();
        private List<CapsuleCollider> ragdollColliders = new List<CapsuleCollider>();

        [Space]

        [Header("Ragdoll Grabbing Control")]
        [SerializeField] private RagdollSize _ragdollSize;
        [SerializeField] private Rigidbody rootBone;

        private float ragdollWeight;

        private Animator animator;
        private ConfigurableJoint joint;
        private Transform grabber;

        private bool isRagdolled;
        private bool isGrabbed;

        private void Awake()
        {
            //Set animator
            animator = GetComponentInChildren<Animator>();
            ragdollWeight = rootBone.mass * 1.5f;

			foreach (Rigidbody rb in ragdollBodies)
			{
                if (rb == null) continue;
                ragdollColliders.Add(rb.GetComponent<CapsuleCollider>());
			}

            ToggleRagdoll(false);
        }

        private void LateUpdate()
        {
            if (!isRagdolled) return;

            transform.position = rootBone.position;
        }

        [ContextMenu("Toggle Ragdoll")]
        public void Ragdoll() => ToggleRagdoll(true);

        public void ToggleRagdoll(bool enable)
        {
            if (animator != null)
                animator.enabled = !enable;

			for (int i = 0; i < ragdollBodies.Count; i++)
			{
                if (ragdollBodies[i] == null) continue;

                ragdollColliders[i].enabled = enable;
                ragdollBodies[i].isKinematic = !enable;
                ragdollBodies[i].interpolation = enable ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }

            if (enable)
                rootBone.transform.parent = null;

            isRagdolled = enable;
        }

        public void ApplyForce(Vector3 position, Vector3 force, float radius = 0f)
        {
            if (ragdollBodies == null || ragdollBodies.Count == 0) return;

            if (radius <= 0)
            {
                Rigidbody closest = null;
                float bestDist = float.MaxValue;

                foreach (Rigidbody rb in ragdollBodies)
                {
                    if (rb == null) continue;
                    float d = Vector3.SqrMagnitude(rb.worldCenterOfMass - position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        closest = rb;
                    }
                }

                if (closest != null)
                    closest.AddForce(force, ForceMode.Impulse);
            }
            else
            {
                foreach (Rigidbody rb in ragdollBodies)
                {
                    if (rb == null) continue;
                    rb.AddExplosionForce(force.magnitude, position, radius, 0f, ForceMode.Impulse);
                }
            }
        }

        private void ToggleRagdollCollision(bool value)
        {
            foreach (CapsuleCollider col in ragdollColliders)
            {
                if (col == null) continue;
                col.isTrigger = !value;
            }
        }

        public void Interact(in InteractionContext context)
        {
            if (context.Verb == InteractionVerb.Release)
            {
                EndInteract();
                return;
            }

            if (context.Verb != InteractionVerb.Grab) return;

            StartInteract(context.Interactor);
        }

        private void StartInteract(Transform owner)
        {
            if (isGrabbed) return;

            isGrabbed = true;
            grabber = owner;

            foreach (ISlowable slowable in grabber.GetComponents<ISlowable>())
                slowable.SetSlowingWeight(true, ragdollWeight);

            joint = grabber.GetComponentInChildren<ConfigurableJoint>();

            joint.connectedBody = rootBone;

            switch (_ragdollSize)
            {
                case RagdollSize.SMALL:
                    SoftJointLimit limit1 = new SoftJointLimit
                    {
                        limit = .3f,
                        bounciness = 0,
                        contactDistance = 0
                    };
                    joint.linearLimit = limit1;

                    joint.yMotion = ConfigurableJointMotion.Limited;

                    JointDrive drive1 = new JointDrive
                    {
                        positionSpring = 400,
                        positionDamper = 400,
                        maximumForce = Mathf.Infinity
                    };
                    joint.yDrive = drive1;


                    joint.connectedAnchor = rootBone.transform.InverseTransformPoint(rootBone.transform.position);
                    ToggleRagdollCollision(false);
                    break;
                case RagdollSize.BIG:
                    SoftJointLimit limit2 = new SoftJointLimit
                    {
                        limit = 1,
                        bounciness = 0,
                        contactDistance = 0
                    };
                    joint.linearLimit = limit2;

                    joint.yMotion = ConfigurableJointMotion.Free;

                    JointDrive drive2 = new JointDrive
                    {
                        positionSpring = 0,
                        positionDamper = 0,
                        maximumForce = Mathf.Infinity
                    };
                    joint.yDrive = drive2;

                    Vector3 worldHandAnchor = joint.transform.TransformPoint(joint.anchor);
                    joint.connectedAnchor = rootBone.transform.InverseTransformPoint(worldHandAnchor);
                    break;
            }

            transform.SetParent(joint.transform, true);
        }

        private void EndInteract()
        {
            if (!isGrabbed) return;

            isGrabbed = false;

            foreach (ISlowable slowable in grabber.GetComponents<ISlowable>())
                slowable.SetSlowingWeight(false, 0);

            grabber = null;

            if (joint != null)
            {
                joint.connectedBody = null;
                joint.connectedAnchor = Vector3.zero;
                joint = null;
            }

            if (_ragdollSize == RagdollSize.SMALL)
                ToggleRagdollCollision(true);

            transform.parent = null;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return isRagdolled;
        }
    }
}
