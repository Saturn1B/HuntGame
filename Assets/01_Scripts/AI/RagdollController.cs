using UnityEngine;
using System.Collections.Generic;

public class RagdollController : MonoBehaviour
{
    [Header("Ragdoll Bones")]
    public List<Rigidbody> ragdollBodies = new List<Rigidbody>();

    private Animator animator;

    private void Awake()
    {
        //Set animator
        animator = GetComponentInChildren<Animator>();
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
}
