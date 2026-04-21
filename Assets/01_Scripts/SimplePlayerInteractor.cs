using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerInteractor : MonoBehaviour
{
	[Header("Raycast")]
	[SerializeField] private Camera viewCamera;
	[SerializeField] private float rayDistance = 3f;

	private ISimpleInteractable interactable;

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
				if (interactable.CanInteract())
				{
					Debug.Log("Interact success");
					interactable.StartInteract(transform);
				}
			}
		}
		else
		{
			if (interactable != null && interactable.CanInteract())
			{
				Debug.Log("End interact success");
				interactable.EndInteract();
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
