using UnityEngine;

public abstract class InteractionActionCore : ScriptableObject
{
    public virtual bool CanExecute(in InteractionContextCore ctx) => true;

    // Can run locally (solo) or on server (multiplayer).
    public abstract void Execute(in InteractionContextCore ctx);
}