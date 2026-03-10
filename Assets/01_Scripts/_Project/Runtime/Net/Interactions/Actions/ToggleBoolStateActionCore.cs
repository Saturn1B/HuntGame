using UnityEngine;

[CreateAssetMenu(menuName = "DungeonSteakhouse/Interaction/Actions Core/Toggle Bool State")]
public sealed class ToggleBoolStateActionCore : InteractionActionCore
{
    [SerializeField] private bool setValueInsteadOfToggle = false;
    [SerializeField] private bool value = false;

    public override void Execute(in InteractionContextCore ctx)
    {
        if (ctx.State == null)
            return;

        if (setValueInsteadOfToggle)
            ctx.State.BoolState = value;
        else
            ctx.State.BoolState = !ctx.State.BoolState;
    }
}