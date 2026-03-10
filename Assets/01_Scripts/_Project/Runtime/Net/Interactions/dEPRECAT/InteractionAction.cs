using UnityEngine;

public abstract class InteractionAction : ScriptableObject
{
    public virtual bool CanExecute(in InteractionContext ctx) => true;

    // This is called on the server only.
    public abstract void Execute(in InteractionContext ctx);
}