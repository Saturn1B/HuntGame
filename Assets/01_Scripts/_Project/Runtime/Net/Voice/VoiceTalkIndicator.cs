using UnityEngine;

namespace DungeonSteakhouse.Net.Voice
{
    /// <summary>
    /// Simple mouth state indicator:
    /// - Mouth is OPEN while voice pings are received frequently.
    /// - Mouth becomes CLOSED after holdSeconds without a ping.
    /// </summary>
    public sealed class VoiceTalkIndicator : MonoBehaviour
    {
        [Header("Renderer")]
        [Tooltip("SpriteRenderer used for mouth swapping. If null, it will search in children.")]
        [SerializeField] private SpriteRenderer mouthRenderer;

        [Header("Sprites")]
        [Tooltip("Sprite used when not talking.")]
        [SerializeField] private Sprite mouthClosed;

        [Tooltip("Sprite used when talking.")]
        [SerializeField] private Sprite mouthOpen;

        [Header("Timing")]
        [Tooltip("How long we keep the mouth open after the last voice ping (seconds).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float holdSeconds = 0.20f;

        private float _lastPingTime;
        private bool _isOpen;

        private void Awake()
        {
            if (mouthRenderer == null)
                mouthRenderer = GetComponentInChildren<SpriteRenderer>(true);

            SetClosed();
        }

        private void Update()
        {
            bool shouldBeOpen = (Time.unscaledTime - _lastPingTime) <= holdSeconds;

            if (shouldBeOpen == _isOpen)
                return;

            _isOpen = shouldBeOpen;

            if (_isOpen) SetOpen();
            else SetClosed();
        }

        /// <summary>
        /// Call this whenever voice data is received for this player.
        /// </summary>
        public void PingTalking()
        {
            _lastPingTime = Time.unscaledTime;

            if (!_isOpen)
            {
                _isOpen = true;
                SetOpen();
            }
        }

        private void SetOpen()
        {
            if (mouthRenderer == null || mouthOpen == null)
                return;

            mouthRenderer.sprite = mouthOpen;
        }

        private void SetClosed()
        {
            if (mouthRenderer == null || mouthClosed == null)
                return;

            mouthRenderer.sprite = mouthClosed;
        }
    }
}