namespace HuntGame.Interactions
{
    /// <summary>
    /// Implemented by IInteractable components that mirror their state in a NetworkVariable
    /// (DoorInteractable, ChestInteractable, LeverInteractable, PickupInteractable).
    /// Lets NetcodeInteractableObject force a resync on the one client whose locally-predicted
    /// interaction the server ended up rejecting (e.g. a race between two players on the same
    /// object), instead of leaving that client's visual state permanently out of sync.
    /// </summary>
    public interface INetworkResyncable
    {
        void ResyncFromNetworkState();
    }
}
