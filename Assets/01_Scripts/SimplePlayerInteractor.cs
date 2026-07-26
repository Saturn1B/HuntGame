using UnityEngine;
using UnityEngine.InputSystem;
using HuntGame.Interactions;

public class SimplePlayerInteractor : MonoBehaviour
{
	[Header("Raycast")]
	[SerializeField] private Camera viewCamera;
	[SerializeField] private float rayDistance = 3f;

	private IInteractable interactable;

	private ISimpleUseable useable;

	public void TryInteract(bool isPressed)
	{
		if (viewCamera == null) return;

		if (isPressed)
		{
			Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

			if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance)) return;

			if (hit.transform.TryGetComponent(out interactable))
			{
				Debug.Log("Try interact");
				var context = InteractionContext.Local(transform, InteractionVerb.Grab);
				if (interactable.CanInteract(in context))
				{
					Debug.Log("Interact success");
					interactable.Interact(in context);
				}
			}
		}
		else
		{
			var context = InteractionContext.Local(transform, InteractionVerb.Release);
			if (interactable != null && interactable.CanInteract(in context))
			{
				Debug.Log("End interact success");
				interactable.Interact(in context);
			}
		}
	}

	public void TryUse(bool isPressed)
	{
		if(useable != null)

		if (isPressed)
		{
				Debug.Log("Try use");
				if (useable.CanUse())
				{
					Debug.Log("Interact success");
					useable.StartUse();
				}
			}
		else
		{
			if (useable != null && useable.CanUse())
			{
				Debug.Log("End interact success");
					useable.EndUse();
			}
		}
	}

	public void SetUseable(ISimpleUseable use)
	{
		useable = use;
	}
}
