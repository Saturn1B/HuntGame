using UnityEngine;

namespace HuntGame.Player
{
    public class PlayerHeadIK : MonoBehaviour
    {
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float headYawLimit = 60f;
        [SerializeField] private float headPitchLimit = 60f;
        [SerializeField] private float smoothSpeed = 15f;

        private float _currentHeadYaw;
        private float _currentHeadPitch;
        private float _targetHeadYaw;
        private float _targetHeadPitch;
        private bool _isOwner;

        public float CurrentHeadYaw => _currentHeadYaw;
        public float CurrentHeadPitch => _currentHeadPitch;

        public void SetOwner() => _isOwner = true;
        public void SetRemote() => _isOwner = false;

        private void LateUpdate()
        {
            if (headBone == null) return;

            if (_isOwner)
            {
                // Calcule localement depuis la caméra
                if (cameraTransform == null) return;

                float rawPitch = cameraTransform.eulerAngles.x;
                if (rawPitch > 180f) rawPitch -= 360f;
                _targetPitch = Mathf.Clamp(rawPitch, -headPitchLimit, headPitchLimit);

                float camHolderYaw = cameraTransform.parent.eulerAngles.y;
                float bodyYaw = transform.eulerAngles.y;
                float deltaYaw = Mathf.DeltaAngle(bodyYaw, camHolderYaw);
                _targetYaw = Mathf.Clamp(deltaYaw, -headYawLimit, headYawLimit);
            }
            // Non-owner : _targetYaw et _targetPitch sont mis à jour via SetRemoteTarget

            // Smooth vers la cible dans tous les cas
            _currentHeadYaw = Mathf.LerpAngle(
                _currentHeadYaw, _targetYaw, smoothSpeed * Time.deltaTime);
            _currentHeadPitch = Mathf.LerpAngle(
                _currentHeadPitch, _targetPitch, smoothSpeed * Time.deltaTime);

            headBone.localEulerAngles = new Vector3(_currentHeadPitch, _currentHeadYaw, 0f);
        }

        /// <summary>Appelé par PlayerAnimationController sur les non-owners.</summary>
        public void SetRemoteTarget(float yaw, float pitch)
        {
            _targetYaw = yaw;
            _targetPitch = pitch;
        }

        private float _targetYaw;
        private float _targetPitch;
    }
}