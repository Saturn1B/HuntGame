using UnityEngine;
using System.Collections.Generic;

public class RagdollController : MonoBehaviour, ISimpleInteractable
{
    [Header("Ragdoll Bones")]
    public List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    [Header("Ragdoll Grabbing Control")]
    [SerializeField] private Rigidbody rootBone;
    [SerializeField] private float ragdollWeight;


    private Animator animator;
    private ConfigurableJoint joint;
    private Transform grabber;

    private bool isRagdolled;
    private bool isGrabbed;

    private void Awake()
    {
        //Set animator
        animator = GetComponentInChildren<Animator>();
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

        foreach (Rigidbody rb in ragdollBodies)
        {
            if (rb == null) continue;
            rb.isKinematic = !enable;
        }

        rootBone.transform.parent = null;

        isRagdolled = enable;
    }

    public void ApplyForce(Vector3 position, Vector3 force, float radius = 0f)
    {
        if (ragdollBodies == null || ragdollBodies.Count == 0) return;

        if(radius <= 0)
        {
            Rigidbody closest = null;
            float bestDist = float.MaxValue;

            foreach (Rigidbody rb in ragdollBodies)
            {
                if (rb == null) continue;
                float d = Vector3.SqrMagnitude(rb.worldCenterOfMass - position);
                if(d < bestDist)
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
        foreach (Rigidbody rb in ragdollBodies)
        {
            Collider col = rb.transform.GetComponent<Collider>();
            if (col == null) continue;
            col.isTrigger = !value;
        }
    }

	public void StartInteract(Transform owner)
	{
        if (isGrabbed) return;

        isGrabbed = true;
        grabber = owner;

        if(grabber.TryGetComponent(out CharacterMovement characterMovement))
            characterMovement.SetSlowingWeight(true, ragdollWeight);

        joint = grabber.GetComponentInChildren<ConfigurableJoint>();

        joint.connectedBody = rootBone;
        //Vector3 worldHandAnchor = joint.transform.TransformPoint(joint.anchor);
        joint.connectedAnchor = rootBone.transform.InverseTransformPoint(rootBone.transform.position);

        ToggleRagdollCollision(false);
        transform.SetParent(joint.transform, true);
	}

	public void EndInteract()
	{
        if (!isGrabbed) return;

        isGrabbed = false;

        if (grabber.TryGetComponent(out CharacterMovement characterMovement))
            characterMovement.SetSlowingWeight(false, 0);

        grabber = null;

        if (joint != null)
		{
            joint.connectedBody = null;
            joint.connectedAnchor = Vector3.zero;
            joint = null;
		}

        ToggleRagdollCollision(true);
        transform.parent = null;
	}

	public bool CanInteract()
	{
        return isRagdolled;
	}
}
