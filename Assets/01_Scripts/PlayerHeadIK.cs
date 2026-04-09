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
        private bool _isRemote;

        public float CurrentHeadYaw => _currentHeadYaw;
        public float CurrentHeadPitch => _currentHeadPitch;

        private void LateUpdate()
        {
            if (_isRemote) return;
            if (headBone == null || cameraTransform == null) return;

            // Pitch
            float rawPitch = cameraTransform.eulerAngles.x;
            if (rawPitch > 180f) rawPitch -= 360f;
            float targetPitch = Mathf.Clamp(rawPitch, -headPitchLimit, headPitchLimit);
            _currentHeadPitch = Mathf.LerpAngle(
                _currentHeadPitch, targetPitch, smoothSpeed * Time.deltaTime);

            // Yaw
            float camHolderYaw = cameraTransform.parent.eulerAngles.y;
            float bodyYaw = transform.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(bodyYaw, camHolderYaw);
            float targetYaw = Mathf.Clamp(deltaYaw, -headYawLimit, headYawLimit);
            _currentHeadYaw = Mathf.LerpAngle(
                _currentHeadYaw, targetYaw, smoothSpeed * Time.deltaTime);

            ApplyToBone(_currentHeadPitch, _currentHeadYaw);
        }

        public void ApplyRemote(float yaw, float pitch)
        {
            _isRemote = true;
            _currentHeadYaw = yaw;
            _currentHeadPitch = pitch;
            ApplyToBone(pitch, yaw);
        }

        public void SetOwner()
        {
            _isRemote = false;
        }

        private void ApplyToBone(float pitch, float yaw)
        {
            if (headBone == null) return;
            headBone.localEulerAngles = new Vector3(pitch, yaw, 0f);
        }
    }
}