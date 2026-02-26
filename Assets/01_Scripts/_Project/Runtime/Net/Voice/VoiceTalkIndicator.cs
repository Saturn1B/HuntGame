using UnityEngine;

namespace DungeonSteakhouse.Net.Voice
{
    /// <summary>
    /// Very simple "talking mouth" indicator driven by voice activity pings.
    /// - Call PingTalking() whenever you receive voice audio for this player.
    /// - While "talking", it can either:
    ///   A) Keep mouth open
    ///   B) Flap (swap open/closed) at a fixed rate
    /// </summary>
    public sealed class VoiceTalkIndicator : MonoBehaviour
    {
        [Header("Renderer")]
        [Tooltip("SpriteRenderer used for mouth swapping. If null, will search in children.")]
        [SerializeField] private SpriteRenderer mouthRenderer;

        [Header("Sprites")]
        [Tooltip("Sprite used when not talking.")]
        [SerializeField] private Sprite mouthClosed;

        [Tooltip("Sprite used when talking.")]
        [SerializeField] private Sprite mouthOpen;

        [Header("Timing")]
        [Tooltip("How long we keep the 'talking' state alive after a ping (seconds).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float holdSeconds = 0.20f;

        [Tooltip("If true, mouth will alternate open/closed while talking (cartoon style).")]
        [SerializeField] private bool flapWhileTalking = true;

        [Tooltip("Flap speed (frames per second). Used only if flapWhileTalking is enabled.")]
        [Range(2f, 30f)]
        [SerializeField] private float flapFps = 12f;

        private float _lastPingTime;
        private bool _isTalking;
        private float _flapTimer;
        private bool _flapOpen;

        private void Awake()
        {
            if (mouthRenderer == null)
                mouthRenderer = GetComponentInChildren<SpriteRenderer>(true);

            ApplyClosed();
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            bool shouldTalk = (now - _lastPingTime) <= holdSeconds;

            if (shouldTalk != _isTalking)
            {
                _isTalking = shouldTalk;
                _flapTimer = 0f;
                _flapOpen = true;

                if (_isTalking)
                    ApplyOpen();
                else
                    ApplyClosed();
            }

            if (!_isTalking)
                return;

            if (!flapWhileTalking)
            {
                ApplyOpen();
                return;
            }

            _flapTimer += Time.unscaledDeltaTime;
            float interval = 1f / Mathf.Max(1f, flapFps);

            if (_flapTimer >= interval)
            {
                _flapTimer -= interval;
                _flapOpen = !_flapOpen;

                if (_flapOpen) ApplyOpen();
                else ApplyClosed();
            }
        }

        /// <summary>
        /// Call this whenever you receive voice data for this player.
        /// </summary>
        public void PingTalking()
        {
            _lastPingTime = Time.unscaledTime;

            // If we were idle, immediately switch to talking state
            if (!_isTalking)
            {
                _isTalking = true;
                _flapTimer = 0f;
                _flapOpen = true;
                ApplyOpen();
            }
        }

        private void ApplyOpen()
        {
            if (mouthRenderer == null || mouthOpen == null)
                return;

            mouthRenderer.sprite = mouthOpen;
        }

        private void ApplyClosed()
        {
            if (mouthRenderer == null || mouthClosed == null)
                return;

            mouthRenderer.sprite = mouthClosed;
        }
    }
}