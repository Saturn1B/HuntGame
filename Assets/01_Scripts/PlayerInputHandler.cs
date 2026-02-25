using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
	[SerializeField] private CharacterMovement movementController;
	[SerializeField] private FirstPersonCamera cameraController;

	private void Awake()
	{
		if (movementController == null)
			movementController = GetComponent<CharacterMovement>();

		if (cameraController == null)
			cameraController = GetComponent<FirstPersonCamera>();
	}

	public void OnMove(InputValue value)
	{
		Vector2 movementInput = value.Get<Vector2>();
		movementController?.SetMovementInput(movementInput);
	}

	public void OnCamera(InputValue value)
	{
		Vector2 lookInput = value.Get<Vector2>();
		cameraController?.SetLookInput(lookInput);
	}

	public void OnSprint(InputValue value) { movementController?.SetSprinting(value.isPressed); }

	public void OnCrouch(InputValue value)
	{
		if (value.isPressed)
			movementController?.SetCrouching(true);
		else
			movementController?.SetCrouching(false);
	}

	public void OnJump(InputValue value) { movementController?.Jump(); }
}
