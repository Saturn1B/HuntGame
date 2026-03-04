using UnityEngine;

[CreateAssetMenu(menuName = "NetGame/Interaction/Actions/Net Pickup")]
public class NetPickupAction : InteractionAction
{
    [Tooltip("If true, the object will be destroyed after despawn.")]
    [SerializeField] private bool destroyWithDespawn = true;

    public override bool CanExecute(in InteractionContext ctx)
    {
        // Basic guard: must have a NetworkObject to despawn.
        return ctx.Target != null && ctx.Target.NetworkObject != null && ctx.Target.NetworkObject.IsSpawned;
    }

    public override void Execute(in InteractionContext ctx)
    {
        // Server-only by design (NetInteractable calls actions on server).
        Debug.Log($"[Server] Pickup '{ctx.Target.name}' by client {ctx.InteractorClientId}.");

        ctx.Target.NetworkObject.Despawn(destroyWithDespawn);
    }
}