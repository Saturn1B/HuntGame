using UnityEngine;

public readonly struct InteractionContextCore
{
    public readonly InteractableCore Target;
    public readonly Transform Interactor;
    public readonly ulong InteractorClientId;
    public readonly InteractionVerb Verb;
    public readonly IInteractableState State;
    public readonly bool IsServerAuthoritative;

    public InteractionContextCore(
        InteractableCore target,
        Transform interactor,
        ulong interactorClientId,
        InteractionVerb verb,
        IInteractableState state,
        bool isServerAuthoritative)
    {
        Target = target;
        Interactor = interactor;
        InteractorClientId = interactorClientId;
        Verb = verb;
        State = state;
        IsServerAuthoritative = isServerAuthoritative;
    }
}