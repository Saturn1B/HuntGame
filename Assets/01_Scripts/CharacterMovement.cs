using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour, IControlable
{
    [Header("Movement Speed")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float sprintSpeed = 20f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float jumpHeight = 3f;

    [Header("Crouch Settings")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchingHeight = .3f;
    [SerializeField] private float crouchTransitionSpeed = .1f;

    [Header("Physics")]
    [SerializeField] private float gravity = 9.81f;

    private CharacterController characterController;
    private Vector3 velocity = Vector3.zero;

    private Vector2 currentMovementInput;
    private bool isCrouching;
    private bool isSprinting;
    private bool canMove = true;

	private void Awake()
	{
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!canMove)
        {
            ApplyGravity();
            characterController.Move(velocity * Time.deltaTime);
            return;
        }

        HandleMovement();
    }

    public void SetMovementInput(Vector2 movementInput) { currentMovementInput = movementInput; }
    public void SetLookInput(Vector2 lookInput) { }
    public void SetSprinting(bool sprinting) { isSprinting = sprinting; }
    public void SetCrouching(bool crouching)
	{
        if(isCrouching != crouching)
		{
            StopAllCoroutines();
            StartCoroutine(CrouchStandTransition());
		}
	}
    public void Jump()
	{
        if (characterController.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * 2f * gravity);
	}

    private void HandleMovement()
    {
        //Check speed
        float currentSpeed = GetCurrentSpeed();

        //Calculate movement
        float horizontal = currentMovementInput.x * currentSpeed;
        float vertical = currentMovementInput.y * currentSpeed;

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical);
        moveDirection = transform.rotation * moveDirection;

        velocity.x = moveDirection.x;
        velocity.z = moveDirection.z;

        ApplyGravity();

        //Apply movement
        characterController.Move(velocity * Time.deltaTime);
    }

    private void ApplyGravity()
	{
        if (!characterController.isGrounded)
            velocity.y -= gravity * Time.deltaTime;
        else if (velocity.y < 0)
            velocity.y = -2f;
	}

    private float GetCurrentSpeed()
	{
        if (isCrouching) return crouchSpeed;
        if (isSprinting) return sprintSpeed;
        return moveSpeed;
	}

    private IEnumerator CrouchStandTransition()
    {
        float targetHeight = isCrouching ? standingHeight : crouchingHeight;
        float initialHeight = characterController.height;
        float timeElapsed = 0f;

        while (timeElapsed < crouchTransitionSpeed)
        {
            characterController.height = Mathf.Lerp(initialHeight, targetHeight, timeElapsed / crouchTransitionSpeed);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        characterController.height = targetHeight;
        isCrouching = !isCrouching;
    }

    public void SetCanMove(bool canMove) => this.canMove = canMove;
}
