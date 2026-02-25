using System;
using UnityEngine;
using DungeonSteakhouse.Net.Flow;

namespace DungeonSteakhouse.Net.Voice
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class NetSteamVoiceSpeaker : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ulong ownerClientId;

        [Header("Config")]
        [SerializeField] private NetSteamVoiceConfig config;

        private AudioSource _audioSource;
        private NetSceneFlow _sceneFlow;

        private float[] _ring;
        private int _read;
        private int _write;
        private int _count;
        private readonly object _lock = new();

        public ulong OwnerClientId => ownerClientId;

        public void SetOwnerClientId(ulong id)
        {
            ownerClientId = id;
        }

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();

            // Make sure we actually get OnAudioFilterRead callbacks
            _audioSource.playOnAwake = true;
            _audioSource.loop = true;
            _audioSource.spatialize = false;
            _audioSource.volume = 1f;

            // Silent clip just to keep AudioSource "alive"
            var sr = AudioSettings.outputSampleRate;
            var clip = AudioClip.Create("NetSteamVoice_Silence", sr, 1, sr, false);
            _audioSource.clip = clip;
            _audioSource.Play();

            AllocateBuffer();

            _sceneFlow = FindFirstObjectByType<NetSceneFlow>();
        }

        private void OnEnable()
        {
            if (_sceneFlow != null)
                _sceneFlow.PhaseChanged += OnPhaseChanged;

            ApplySpatialSettings(_sceneFlow != null ? _sceneFlow.Phase : NetRunPhase.Hub);
        }

        private void OnDisable()
        {
            if (_sceneFlow != null)
                _sceneFlow.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(NetRunPhase phase)
        {
            ApplySpatialSettings(phase);
        }

        private void ApplySpatialSettings(NetRunPhase phase)
        {
            if (config == null || _audioSource == null)
                return;

            if (phase == NetRunPhase.InRun)
            {
                _audioSource.spatialBlend = config.runSpatialBlend;
                _audioSource.minDistance = config.runMinDistance;
                _audioSource.maxDistance = config.runMaxDistance;
                _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
            else
            {
                _audioSource.spatialBlend = config.hubSpatialBlend;
            }
        }

        private void AllocateBuffer()
        {
            var sr = AudioSettings.outputSampleRate;
            var size = Mathf.CeilToInt(sr * Mathf.Max(0.2f, config != null ? config.bufferSeconds : 1f));
            _ring = new float[size];
            _read = 0;
            _write = 0;
            _count = 0;
        }

        /// <summary>
        /// Enqueue PCM16 mono bytes (little-endian) at sourceRate, resampled into Unity output rate.
        /// </summary>
        public void EnqueuePcm16Resampled(byte[] pcm16, int byteCount, int sourceRate)
        {
            if (pcm16 == null || byteCount <= 0)
                return;

            var dstRate = AudioSettings.outputSampleRate;
            var srcSamples = byteCount / 2;
            if (srcSamples <= 0 || sourceRate <= 0 || dstRate <= 0)
                return;

            // Resample into Unity output sample-rate directly into ring buffer
            var dstSamples = Mathf.Max(1, Mathf.RoundToInt(srcSamples * (dstRate / (float)sourceRate)));

            lock (_lock)
            {
                for (int j = 0; j < dstSamples; j++)
                {
                    var srcPos = j * (sourceRate / (float)dstRate);
                    var i0 = Mathf.Clamp((int)srcPos, 0, srcSamples - 1);
                    var i1 = Mathf.Clamp(i0 + 1, 0, srcSamples - 1);
                    var t = srcPos - i0;

                    short s0 = ReadInt16LE(pcm16, i0 * 2);
                    short s1 = ReadInt16LE(pcm16, i1 * 2);

                    var sample = Mathf.Lerp(s0, s1, t) / 32768f;

                    WriteSample(sample);
                }
            }
        }

        private static short ReadInt16LE(byte[] data, int offset)
        {
            unchecked
            {
                return (short)(data[offset] | (data[offset + 1] << 8));
            }
        }

        private void WriteSample(float s)
        {
            if (_ring == null || _ring.Length == 0)
                return;

            // If buffer full, drop oldest (read++)
            if (_count == _ring.Length)
            {
                _read = (_read + 1) % _ring.Length;
                _count--;
            }

            _ring[_write] = s;
            _write = (_write + 1) % _ring.Length;
            _count++;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0)
                return;

            lock (_lock)
            {
                for (int i = 0; i < data.Length; i += channels)
                {
                    float s = 0f;

                    if (_count > 0)
                    {
                        s = _ring[_read];
                        _read = (_read + 1) % _ring.Length;
                        _count--;
                    }

                    // Mono -> fill all channels
                    for (int c = 0; c < channels; c++)
                        data[i + c] = s;
                }
            }
        }
    }
}