using System.Collections;
using UnityEngine;

namespace HuntGame.Player
{
[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour, IMovable, ILookable, ISlowable
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
    [SerializeField] private LayerMask groundLayer;

    private CharacterController characterController;
    private Vector3 velocity = Vector3.zero;

    private Vector2 currentMovementInput;
    private bool isCrouching;
    private bool isSprinting;
    private bool canMove = true;

    private bool isDragging;
    private float dragWeight;

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

    public void ResetVerticalVelocity()
    {
        velocity.y = 0f;
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
        if (IsGroundedLocal())
		{
            float jumpForce = Mathf.Sqrt(jumpHeight * 2f * gravity);

            float jumpDirection = transform.up.y > 0 ? 1f : -1f;
            velocity.y = jumpForce * jumpDirection;
        }
    }

    public void SetSlowingWeight(bool dragging, float weight)
	{
        isDragging = dragging;
        dragWeight = weight;
	}

    private void HandleMovement()
    {
        //Check speed
        float currentSpeed = isDragging ? GetCurrentSpeedWithDrag() : GetCurrentSpeed();

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
        float gravityMultiplier = transform.up.y > 0 ? -1f : 1f;

        if (!IsGroundedLocal())
            velocity.y += gravityMultiplier * gravity * Time.deltaTime;
        else if (velocity.y < 0)
            velocity.y = gravityMultiplier * 2f;
	}
    public bool IsGroundedLocal()
    {
        float rayLength = (characterController.height / 2f) + 0.2f;
        return Physics.Raycast(transform.position, -transform.up, rayLength, groundLayer);
    }

    private float GetCurrentSpeed()
	{
        if (isCrouching) return crouchSpeed;
        if (isSprinting) return sprintSpeed;
        return moveSpeed;
	}

    private float GetCurrentSpeedWithDrag()
	{
        float slowFactor = 1 / (1 + dragWeight / 40f);
        float targetSpeed = GetCurrentSpeed() * slowFactor;

        return targetSpeed;
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
}
