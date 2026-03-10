using UnityEngine;

[CreateAssetMenu(menuName = "NetGame/Interaction/Actions/Door Rotate Toggle")]
public class DoorRotateToggleAction : InteractionAction
{
    [SerializeField] private float openAngleDegrees = 90f;
    [SerializeField] private Vector3 localAxis = Vector3.up;

    public override void Execute(in InteractionContext ctx)
    {
        // Toggle replicated state.
        bool isOpen = !ctx.Target.BoolState.Value;
        ctx.Target.BoolState.Value = isOpen;

        Quaternion closed = ctx.Target.GetClosedLocalRotation();
        Quaternion open = closed * Quaternion.AngleAxis(openAngleDegrees, localAxis.normalized);

        // Server moves the transform; NetworkTransform replicates it.
        ctx.Target.transform.localRotation = isOpen ? open : closed;

        Debug.Log($"[Server] Door '{ctx.Target.name}' is now {(isOpen ? "OPEN" : "CLOSED")}.");
    }
}