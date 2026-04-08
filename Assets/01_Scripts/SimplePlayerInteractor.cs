using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerInteractor : MonoBehaviour
{
	[Header("Raycast")]
	[SerializeField] private Camera viewCamera;
	[SerializeField] private float rayDistance = 3f;

	private ISimpleInteractable interactable;

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
}
