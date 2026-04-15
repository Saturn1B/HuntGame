using Unity.Netcode;
using UnityEngine;

namespace HuntGame.Player
{
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform bodyTransform;
        [SerializeField] private FirstPersonCamera firstPersonCamera;
        [SerializeField] private PlayerHeadIK headIK;

        [Header("Smoothing")]
        [SerializeField] private float velocitySmoothing = 10f;

        // Animator parameter hashes
        private static readonly int HashVelocityX = Animator.StringToHash("VelocityX");
        private static readonly int HashVelocityZ = Animator.StringToHash("VelocityZ");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");

        // NetworkVariables
        private readonly NetworkVariable<float> _netVelocityX = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> _netVelocityZ = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> _netIsGrounded = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> _netBodyYaw = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> _netHeadYaw = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> _netHeadPitch = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private Vector2 _smoothedVelocity;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (characterController == null)
                characterController = GetComponentInParent<CharacterController>();

            if (firstPersonCamera == null)
                firstPersonCamera = GetComponentInParent<FirstPersonCamera>();

            if (headIK == null)
                headIK = GetComponent<PlayerHeadIK>();

            if (bodyTransform == null)
                bodyTransform = transform;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                if (headIK != null) headIK.SetOwner();
            }
            else
            {
                if (headIK != null) headIK.SetRemote();

                _netVelocityX.OnValueChanged += OnVelocityChanged;
                _netVelocityZ.OnValueChanged += OnVelocityChanged;
                _netIsGrounded.OnValueChanged += OnGroundedChanged;
                _netBodyYaw.OnValueChanged += OnBodyYawChanged;
                _netHeadYaw.OnValueChanged += OnHeadChanged;
                _netHeadPitch.OnValueChanged += OnHeadChanged;

                ApplyRemoteAnimatorState();
                ApplyRemoteBodyYaw(_netBodyYaw.Value);
                ApplyRemoteHead();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                _netVelocityX.OnValueChanged -= OnVelocityChanged;
                _netVelocityZ.OnValueChanged -= OnVelocityChanged;
                _netIsGrounded.OnValueChanged -= OnGroundedChanged;
                _netBodyYaw.OnValueChanged -= OnBodyYawChanged;
                _netHeadYaw.OnValueChanged -= OnHeadChanged;
                _netHeadPitch.OnValueChanged -= OnHeadChanged;
            }

            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            if (!IsOwner || !IsSpawned) return;

            // Vélocité locale
            Vector3 worldVel = characterController != null
                ? new Vector3(characterController.velocity.x, 0f, characterController.velocity.z)
                : Vector3.zero;

            Vector3 localVel = characterController.transform.InverseTransformDirection(worldVel);

            _smoothedVelocity = Vector2.Lerp(
                _smoothedVelocity,
                new Vector2(localVel.x, localVel.z),
                velocitySmoothing * Time.deltaTime);

            bool grounded = characterController != null && characterController.isGrounded;

            float bodyYaw = firstPersonCamera != null
                ? firstPersonCamera.BodyYaw
                : bodyTransform.eulerAngles.y;

            // Write NetworkVariables
            _netVelocityX.Value = _smoothedVelocity.x;
            _netVelocityZ.Value = _smoothedVelocity.y;
            _netIsGrounded.Value = grounded;
            _netBodyYaw.Value = bodyYaw;

            if (headIK != null)
            {
                _netHeadYaw.Value = headIK.CurrentHeadYaw;
                _netHeadPitch.Value = headIK.CurrentHeadPitch;
            }

            DriveAnimator(_smoothedVelocity.x, _smoothedVelocity.y, grounded);
        }

        // Callbacks non-owner
        private void OnVelocityChanged(float _old, float _new) => ApplyRemoteAnimatorState();
        private void OnGroundedChanged(bool _old, bool _new) => ApplyRemoteAnimatorState();
        private void OnBodyYawChanged(float _old, float _new) => ApplyRemoteBodyYaw(_new);
        private void OnHeadChanged(float _old, float _new) => ApplyRemoteHead();

        private void ApplyRemoteAnimatorState()
        {
            DriveAnimator(_netVelocityX.Value, _netVelocityZ.Value, _netIsGrounded.Value);
        }

        private void ApplyRemoteBodyYaw(float yaw)
        {
            if (bodyTransform != null)
                bodyTransform.eulerAngles = new Vector3(0f, yaw, 0f);
        }

        private void ApplyRemoteHead()
        {
            //Debug.Log($"ApplyRemoteHead — IsOwner:{IsOwner} yaw:{_netHeadYaw.Value} pitch:{_netHeadPitch.Value} headIK:{headIK != null}");
            if (headIK != null)
                headIK.SetRemoteTarget(_netHeadYaw.Value, _netHeadPitch.Value);
        }

        private void DriveAnimator(float velX, float velZ, bool grounded)
        {
            if (animator == null) return;
            animator.SetFloat(HashVelocityX, velX);
            animator.SetFloat(HashVelocityZ, velZ);
            animator.SetBool(HashIsGrounded, grounded);
        }
    }
}