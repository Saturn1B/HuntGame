using UnityEngine;

[CreateAssetMenu(menuName = "NetGame/Interaction/Actions/Debug Increment Counter")]
public class DebugIncrementCounterAction : InteractionAction
{
    public override void Execute(in InteractionContext ctx)
    {
        // Server-side replicated counter.
        ctx.Target.IntState.Value++;

        Debug.Log($"[Server] '{ctx.Target.name}' used by client {ctx.InteractorClientId}. Count={ctx.Target.IntState.Value}");
    }
}