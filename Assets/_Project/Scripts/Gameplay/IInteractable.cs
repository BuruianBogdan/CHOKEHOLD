namespace ChokeHold.Gameplay
{
    public interface IInteractable 
    {
        string InteractionText { get; }
        void Interact();    
    }
}