using UnityEngine;
using System.Collections.Generic;

namespace HuntingGame.AI
{
    public class RagdollController : MonoBehaviour, ISimpleInteractable
    {
        [Header("Ragdoll Bones")]
        public List<Rigidbody> ragdollBodies = new List<Rigidbody>();
        private List<CapsuleCollider> ragdollColliders = new List<CapsuleCollider>();

        [Space]

        [Header("Ragdoll Grabbing Control")]
        [SerializeField] private Rigidbody rootBone;

        [Space]

        [Header("Lift Settings")]
        [SerializeField] private float liftSensitivity = 12f;
        [SerializeField] private float liftDecaySpeed = 1.8f;
        [SerializeField] private float maxLiftForce = 600f;

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

		private void Update()
		{
			if (isGrabbed)
			{
                if (grabber.TryGetComponent(out FirstPersonCamera firstPersonCamera))
                    UpdateGrabLift(firstPersonCamera.GetCurrentLookInput().y);
            }
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

                if(ragdollColliders[i].gameObject.layer == LayerMask.NameToLayer("Ragdoll"))
                    ragdollColliders[i].enabled = true;
                else
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

        private float liftStrain;

        public void UpdateGrabLift(float mouseDeltaY)
		{
            if (!isGrabbed) return;

            float resistanceFactor = Mathf.Max(.1f, ragdollWeight);

            if(mouseDeltaY > 0f)
			{
                float gain = (mouseDeltaY * liftSensitivity) / resistanceFactor;
                liftStrain = Mathf.Clamp01(liftStrain + gain * Time.deltaTime);
			}
			else
			{
                float decay = liftDecaySpeed * (resistanceFactor / 10f);
                liftStrain = Mathf.MoveTowards(liftStrain, 0f, decay * Time.deltaTime);
			}

            float gravityCancel = rootBone.mass * Mathf.Abs(Physics.gravity.y);
            float appliedForce = Mathf.Min(liftStrain * gravityCancel, maxLiftForce);

            rootBone.AddForce(Vector3.up * appliedForce, ForceMode.Force);
		}

        public void StartInteract(Transform owner)
        {
            if (isGrabbed) return;

            isGrabbed = true;
            grabber = owner;
            liftStrain = 0;

            if (grabber.TryGetComponent(out CharacterMovement characterMovement))
                characterMovement.SetSlowingWeight(true, ragdollWeight);
            if (grabber.TryGetComponent(out FirstPersonCamera firstPersonCamera))
                firstPersonCamera.SetSlowingWeight(true, ragdollWeight);

            joint = grabber.GetComponentInChildren<ConfigurableJoint>();

            joint.connectedBody = rootBone;

            joint.linearLimit = new SoftJointLimit
            {
                limit = 1,
                bounciness = 0,
                contactDistance = 0
            };
            joint.yMotion = ConfigurableJointMotion.Free;

            joint.yDrive = new JointDrive
            {
                positionSpring = 0,
                positionDamper = 0,
                maximumForce = Mathf.Infinity
            };

            Vector3 worldHandAnchor = joint.transform.TransformPoint(joint.anchor);
            joint.connectedAnchor = rootBone.transform.InverseTransformPoint(worldHandAnchor);

            transform.SetParent(joint.transform, true);
        }

        public void EndInteract()
        {
            if (!isGrabbed) return;

            isGrabbed = false;
            liftStrain = 0;

            if (grabber.TryGetComponent(out CharacterMovement characterMovement))
                characterMovement.SetSlowingWeight(false, 0);
            if (grabber.TryGetComponent(out FirstPersonCamera firstPersonCamera))
                firstPersonCamera.SetSlowingWeight(false, 0);

            grabber = null;

            if (joint != null)
            {
                joint.connectedBody = null;
                joint.connectedAnchor = Vector3.zero;
                joint = null;
            }

            transform.parent = null;
        }

        public bool CanInteract()
        {
            return isRagdolled;
        }
    }
}
