using UnityEngine;

namespace DungeonSteakhouse.Net.Voice
{
    [DisallowMultipleComponent]
    public sealed class SteamVoiceSpeaker : MonoBehaviour
    {
        [Header("Optional - if null, the script will create one")]
        [SerializeField] private AudioSource audioSource;

        [Header("3D Proximity Settings")]
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 15f;

        [Header("Streaming Buffer")]
        [SerializeField] private float bufferSeconds = 0.35f;

        [Header("Talking Indicator (optional)")]
        [SerializeField] private VoiceTalkIndicator talkIndicator;

        [Header("Mouth Blendshape (optional)")]
        [SerializeField] private MouthBlendshapeTalkAnimator mouthBlendshapeAnimator;

        private AudioClip _clip;

        private float[] _ring;
        private int _ringWrite;
        private int _ringRead;
        private int _ringCount;

        private readonly object _lock = new object();

        public float MaxDistance => maxDistance;

        public void EnsureInitialized(int sampleRate)
        {
            if (sampleRate <= 0)
                sampleRate = 24000;

            if (audioSource == null)
            {
                var go = new GameObject("VoiceSpeakerAudio");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                audioSource = go.AddComponent<AudioSource>();
            }

            if (talkIndicator == null)
                talkIndicator = GetComponentInChildren<VoiceTalkIndicator>(true);

            if (mouthBlendshapeAnimator == null)
                mouthBlendshapeAnimator = GetComponentInChildren<MouthBlendshapeTalkAnimator>(true);

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;
            audioSource.dopplerLevel = 0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;

            int ringSize = Mathf.Max(1024, Mathf.CeilToInt(sampleRate * Mathf.Max(0.1f, bufferSeconds)));
            if (_ring == null || _ring.Length != ringSize)
            {
                lock (_lock)
                {
                    _ring = new float[ringSize];
                    _ringWrite = 0;
                    _ringRead = 0;
                    _ringCount = 0;
                }
            }

            if (_clip == null || _clip.frequency != sampleRate)
            {
                _clip = AudioClip.Create(
                    name: "SteamVoiceStream",
                    lengthSamples: sampleRate,
                    channels: 1,
                    frequency: sampleRate,
                    stream: true,
                    pcmreadercallback: OnAudioRead
                );

                audioSource.clip = _clip;
            }

            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        public void PushSamples(float[] samples, int sampleCount)
        {
            if (samples == null || sampleCount <= 0)
                return;

            if (_ring == null)
                return;

            // Receiving voice data implies "talking" (simple indicators)
            if (talkIndicator != null)
                talkIndicator.PingTalking();

            if (mouthBlendshapeAnimator != null)
                mouthBlendshapeAnimator.PingTalking();

            lock (_lock)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    if (_ringCount == _ring.Length)
                    {
                        // Drop oldest samples if buffer is full (prevents latency growth)
                        _ringRead = (_ringRead + 1) % _ring.Length;
                        _ringCount--;
                    }

                    _ring[_ringWrite] = samples[i];
                    _ringWrite = (_ringWrite + 1) % _ring.Length;
                    _ringCount++;
                }
            }
        }

        private void OnAudioRead(float[] data)
        {
            lock (_lock)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (_ringCount > 0)
                    {
                        data[i] = _ring[_ringRead];
                        _ringRead = (_ringRead + 1) % _ring.Length;
                        _ringCount--;
                    }
                    else
                    {
                        data[i] = 0f;
                    }
                }
            }
        }
    }
}