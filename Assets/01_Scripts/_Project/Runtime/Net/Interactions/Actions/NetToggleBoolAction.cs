using UnityEngine;

[CreateAssetMenu(menuName = "NetGame/Interaction/Actions/Net Toggle Bool")]
public class NetToggleBoolAction : InteractionAction
{
    public override void Execute(in InteractionContext ctx)
    {
        bool newValue = !ctx.Target.BoolState.Value;
        ctx.Target.BoolState.Value = newValue;

        Debug.Log($"[Server] Toggle '{ctx.Target.name}' -> BoolState={newValue} (client {ctx.InteractorClientId}).");

        // Later: you can react to BoolState changes client-side to play animation/sfx/visuals.
    }
}