using UnityEngine;
using Unity.Netcode;

public readonly struct InteractionContext
{
    public readonly NetInteractable Target;
    public readonly NetworkObject Interactor;
    public readonly ulong InteractorClientId;
    public readonly InteractionVerb Verb;

    public InteractionContext(NetInteractable target, NetworkObject interactor, ulong interactorClientId, InteractionVerb verb)
    {
        Target = target;
        Interactor = interactor;
        InteractorClientId = interactorClientId;
        Verb = verb;
    }
}