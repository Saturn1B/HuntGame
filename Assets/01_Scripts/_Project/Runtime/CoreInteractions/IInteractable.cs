namespace HuntGame.Interactions
{
    public interface IInteractable
    {
        bool CanInteract(in InteractionContext context);
        void Interact(in InteractionContext context);
    }
}
