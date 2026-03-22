using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    private float yaw;
    private float pitch;
    private Vector2 currentLookInput;

    private bool isDragging;
    private float dragWeight;

    private void Start()
	{
        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>().transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = transform.eulerAngles.y;
        pitch = cameraTransform.localEulerAngles.x;
	}

	private void LateUpdate()
	{
        HandleCameraRotation();
	}

    public void SetLookInput(Vector2 lookInput) => currentLookInput = lookInput;

    private void HandleCameraRotation()
	{
        float sensitivity = isDragging ? GetSensitivityWithDrag() : lookSensitivity;

        yaw += currentLookInput.x * sensitivity;
        pitch -= currentLookInput.y * sensitivity;

        pitch = ClampAngle(pitch, minPitch, maxPitch);

        transform.eulerAngles = new Vector3(0f, yaw, 0f);
        cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
	}

    private float ClampAngle(float angle, float min, float max)
	{
        if (angle < -360f) angle += 360;
        if (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
	}

    public void SetSlowingWeight(bool dragging, float weight)
    {
        isDragging = dragging;
        dragWeight = weight;
    }

    private float GetSensitivityWithDrag()
    {
        float slowFactor = 1 / (1 + dragWeight / 10f);
        float targetSpeed = lookSensitivity * slowFactor;

        return targetSpeed;
    }
}
