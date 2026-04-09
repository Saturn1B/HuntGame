using UnityEngine;

namespace HuntGame.Player
{
    public class FirstPersonCamera : MonoBehaviour, ILookable
    {
        [Header("Look Settings")]
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float minPitch = -90f;
        [SerializeField] private float maxPitch = 90f;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Body Lag")]
        [SerializeField] private Transform bodyTransform;
        [SerializeField] private float bodyLagSpeed = 8f;
        [SerializeField] private float maxBodyAngle = 60f;

        private float yaw;
        private float pitch;
        private float _bodyYaw;
        private Vector2 currentLookInput;

        public float CameraYaw => yaw;
        public float BodyYaw => _bodyYaw;

        private void Start()
        {
            if (cameraTransform == null)
                cameraTransform = GetComponentInChildren<Camera>().transform;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            yaw = transform.eulerAngles.y;
            pitch = cameraTransform.localEulerAngles.x;
            _bodyYaw = yaw;
        }

        private void LateUpdate()
        {
            HandleCameraRotation();

            if (bodyTransform != null)
                UpdateBodyRotation();
        }

        public void SetLookInput(Vector2 lookInput) => currentLookInput = lookInput;

        private void HandleCameraRotation()
        {
            yaw += currentLookInput.x * lookSensitivity;
            pitch -= currentLookInput.y * lookSensitivity;
            pitch = ClampAngle(pitch, minPitch, maxPitch);

            // CamHolder tourne avec le yaw
            transform.eulerAngles = new Vector3(0f, yaw, 0f);
            // Caméra enfant gère seulement le pitch
            cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        private void UpdateBodyRotation()
        {
            float delta = Mathf.DeltaAngle(_bodyYaw, yaw);
            if (Mathf.Abs(delta) > maxBodyAngle)
                _bodyYaw = yaw - Mathf.Sign(delta) * maxBodyAngle;

            _bodyYaw = Mathf.LerpAngle(_bodyYaw, yaw, bodyLagSpeed * Time.deltaTime);
            bodyTransform.eulerAngles = new Vector3(0f, _bodyYaw, 0f);
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }
    }
}