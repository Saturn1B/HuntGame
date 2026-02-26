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

        private long _totalWrittenSamples;
        private long _totalReadSamples;

        private int _overrunDroppedSamples;
        private int _underrunCallbacks;
        private int _underrunMissingSamples;

        // Main-thread timestamps for audio-thread logic (approximate is fine)
        private volatile float _mainThreadTimeUnscaled;
        private volatile float _lastEnqueueTimeUnscaled;
        private volatile bool _hasEverReceivedAudio;

        // Playback state
        private bool _isPlayingVoice;
        private float _gain;

        // Cached tuning values
        private int _prefillSamples;
        private float _fadeInStep;
        private float _fadeOutStep;
        private int _cachedOutputSampleRate;
        private float _cachedPrefillSeconds;
        private float _cachedFadeInSeconds;
        private float _cachedFadeOutSeconds;

        public ulong OwnerClientId => ownerClientId;

        public readonly struct DebugSnapshot
        {
            public readonly ulong OwnerClientId;
            public readonly int CapacitySamples;
            public readonly int FillSamples;
            public readonly float BufferedSeconds;
            public readonly long TotalWrittenSamples;
            public readonly long TotalReadSamples;
            public readonly int OverrunDroppedSamples;
            public readonly int UnderrunCallbacks;
            public readonly int UnderrunMissingSamples;

            public readonly bool IsPlayingVoice;
            public readonly bool HasRecentAudio;
            public readonly float SecondsSinceLastEnqueue;
            public readonly int PrefillSamples;

            public DebugSnapshot(
                ulong ownerClientId,
                int capacitySamples,
                int fillSamples,
                float bufferedSeconds,
                long totalWrittenSamples,
                long totalReadSamples,
                int overrunDroppedSamples,
                int underrunCallbacks,
                int underrunMissingSamples,
                bool isPlayingVoice,
                bool hasRecentAudio,
                float secondsSinceLastEnqueue,
                int prefillSamples)
            {
                OwnerClientId = ownerClientId;
                CapacitySamples = capacitySamples;
                FillSamples = fillSamples;
                BufferedSeconds = bufferedSeconds;
                TotalWrittenSamples = totalWrittenSamples;
                TotalReadSamples = totalReadSamples;
                OverrunDroppedSamples = overrunDroppedSamples;
                UnderrunCallbacks = underrunCallbacks;
                UnderrunMissingSamples = underrunMissingSamples;

                IsPlayingVoice = isPlayingVoice;
                HasRecentAudio = hasRecentAudio;
                SecondsSinceLastEnqueue = secondsSinceLastEnqueue;
                PrefillSamples = prefillSamples;
            }
        }

        public void GetDebugSnapshot(out DebugSnapshot snapshot)
        {
            lock (_lock)
            {
                int capacity = _ring != null ? _ring.Length : 0;
                int fill = _count;
                int sr = AudioSettings.outputSampleRate;
                float bufferedSeconds = (sr > 0 && fill > 0) ? (fill / (float)sr) : 0f;

                float now = Time.unscaledTime;
                float sinceLast = _hasEverReceivedAudio ? Mathf.Max(0f, now - _lastEnqueueTimeUnscaled) : float.PositiveInfinity;
                bool recent = _hasEverReceivedAudio && sinceLast <= GetTalkerActiveSeconds();

                snapshot = new DebugSnapshot(
                    ownerClientId,
                    capacity,
                    fill,
                    bufferedSeconds,
                    _totalWrittenSamples,
                    _totalReadSamples,
                    _overrunDroppedSamples,
                    _underrunCallbacks,
                    _underrunMissingSamples,
                    _isPlayingVoice,
                    recent,
                    sinceLast,
                    _prefillSamples
                );
            }
        }

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
            int sr = AudioSettings.outputSampleRate;
            var clip = AudioClip.Create("NetSteamVoice_Silence", sr, 1, sr, false);
            _audioSource.clip = clip;
            _audioSource.Play();

            AllocateBuffer();
            RefreshCachedTuning(force: true);

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

        private void Update()
        {
            _mainThreadTimeUnscaled = Time.unscaledTime;
            RefreshCachedTuning(force: false);
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
            int sr = AudioSettings.outputSampleRate;
            float seconds = config != null ? Mathf.Max(0.2f, config.bufferSeconds) : 1f;
            int size = Mathf.CeilToInt(sr * seconds);

            lock (_lock)
            {
                _ring = new float[size];
                _read = 0;
                _write = 0;
                _count = 0;

                _totalWrittenSamples = 0;
                _totalReadSamples = 0;

                _overrunDroppedSamples = 0;
                _underrunCallbacks = 0;
                _underrunMissingSamples = 0;

                _isPlayingVoice = false;
                _gain = 0f;
            }
        }

        private void RefreshCachedTuning(bool force)
        {
            int sr = AudioSettings.outputSampleRate;

            float prefillSec = config != null ? config.prefillSeconds : 0.08f;
            float fadeInSec = config != null ? config.fadeInSeconds : 0.02f;
            float fadeOutSec = config != null ? config.fadeOutSeconds : 0.05f;

            if (!force &&
                sr == _cachedOutputSampleRate &&
                Mathf.Approximately(prefillSec, _cachedPrefillSeconds) &&
                Mathf.Approximately(fadeInSec, _cachedFadeInSeconds) &&
                Mathf.Approximately(fadeOutSec, _cachedFadeOutSeconds))
            {
                return;
            }

            _cachedOutputSampleRate = sr;
            _cachedPrefillSeconds = prefillSec;
            _cachedFadeInSeconds = fadeInSec;
            _cachedFadeOutSeconds = fadeOutSec;

            int capacity = _ring != null ? _ring.Length : 0;
            int prefillSamples = Mathf.RoundToInt(sr * Mathf.Max(0f, prefillSec));
            _prefillSamples = Mathf.Clamp(prefillSamples, 0, capacity);

            _fadeInStep = (fadeInSec <= 0f || sr <= 0) ? 1f : (1f / (fadeInSec * sr));
            _fadeOutStep = (fadeOutSec <= 0f || sr <= 0) ? 1f : (1f / (fadeOutSec * sr));
        }

        private float GetTalkerActiveSeconds()
        {
            return config != null ? Mathf.Max(0.05f, config.talkerActiveSeconds) : 0.35f;
        }

        private bool GetRebufferOnUnderrun()
        {
            return config == null || config.rebufferOnUnderrun;
        }

        /// <summary>
        /// Enqueue PCM16 mono bytes (little-endian) at sourceRate, resampled into Unity output rate.
        /// </summary>
        public void EnqueuePcm16Resampled(byte[] pcm16, int byteCount, int sourceRate)
        {
            if (pcm16 == null || byteCount <= 0)
                return;

            int dstRate = AudioSettings.outputSampleRate;
            int srcSamples = byteCount / 2;
            if (srcSamples <= 0 || sourceRate <= 0 || dstRate <= 0)
                return;

            _hasEverReceivedAudio = true;
            _lastEnqueueTimeUnscaled = _mainThreadTimeUnscaled;

            // Resample into Unity output sample-rate directly into ring buffer
            int dstSamples = Mathf.Max(1, Mathf.RoundToInt(srcSamples * (dstRate / (float)sourceRate)));

            lock (_lock)
            {
                for (int j = 0; j < dstSamples; j++)
                {
                    float srcPos = j * (sourceRate / (float)dstRate);
                    int i0 = Mathf.Clamp((int)srcPos, 0, srcSamples - 1);
                    int i1 = Mathf.Clamp(i0 + 1, 0, srcSamples - 1);
                    float t = srcPos - i0;

                    short s0 = ReadInt16LE(pcm16, i0 * 2);
                    short s1 = ReadInt16LE(pcm16, i1 * 2);

                    float sample = Mathf.Lerp(s0, s1, t) / 32768f;
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

            // If buffer full, drop oldest sample (reduces latency growth)
            if (_count == _ring.Length)
            {
                _read = (_read + 1) % _ring.Length;
                _count--;
                _overrunDroppedSamples++;
            }

            _ring[_write] = s;
            _write = (_write + 1) % _ring.Length;
            _count++;
            _totalWrittenSamples++;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0)
                return;

            float now = _mainThreadTimeUnscaled;
            float activeWindow = GetTalkerActiveSeconds();
            bool hasRecentAudio = _hasEverReceivedAudio && (now - _lastEnqueueTimeUnscaled) <= activeWindow;

            int missingSamples = 0;

            lock (_lock)
            {
                // Start playback only once we have enough buffered audio (prefill)
                if (!_isPlayingVoice && hasRecentAudio && _count >= _prefillSamples)
                    _isPlayingVoice = true;

                // If talker is inactive and buffer drained, stop playback
                if (_isPlayingVoice && !hasRecentAudio && _count == 0)
                    _isPlayingVoice = false;

                bool shouldCountUnderruns = hasRecentAudio || _isPlayingVoice;
                bool rebufferOnUnderrun = GetRebufferOnUnderrun();

                for (int i = 0; i < data.Length; i += channels)
                {
                    // Target gain depends on current playback state
                    float targetGain = _isPlayingVoice ? 1f : 0f;

                    // Apply per-sample fade for pop-free transitions
                    if (_gain < targetGain)
                        _gain = Mathf.Min(targetGain, _gain + _fadeInStep);
                    else if (_gain > targetGain)
                        _gain = Mathf.Max(targetGain, _gain - _fadeOutStep);

                    float s = 0f;

                    if (_isPlayingVoice)
                    {
                        if (_count > 0)
                        {
                            s = _ring[_read];
                            _read = (_read + 1) % _ring.Length;
                            _count--;
                            _totalReadSamples++;
                        }
                        else
                        {
                            missingSamples++;

                            // If we're supposed to be playing voice but the buffer is empty,
                            // drop to silence and optionally require prefill again for stability.
                            if (rebufferOnUnderrun)
                                _isPlayingVoice = false;
                        }
                    }

                    float outSample = s * _gain;

                    // Mono -> fill all channels
                    for (int c = 0; c < channels; c++)
                        data[i + c] = outSample;
                }

                if (shouldCountUnderruns && missingSamples > 0)
                {
                    _underrunCallbacks++;
                    _underrunMissingSamples += missingSamples;
                }
            }
        }
    }
}