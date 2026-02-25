using UnityEngine;

public interface IControlable
{
	void SetMovementInput(Vector2 movementInput);
	void SetLookInput(Vector2 lookInput);
	void SetSprinting(bool isSprinting);
	void SetCrouching(bool isCrouching);
	void Jump();
}
