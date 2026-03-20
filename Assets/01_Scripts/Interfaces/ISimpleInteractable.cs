using UnityEngine;

public interface ISimpleInteractable
{
	public void StartInteract(Transform owner);
	public void EndInteract();
	public bool CanInteract();
}
