using UnityEngine;

namespace DungeonSteakhouse.Net.Voice
{
    [CreateAssetMenu(menuName = "DungeonSteakhouse/Net/Net Steam Voice Config", fileName = "NetSteamVoiceConfig")]
    public sealed class NetSteamVoiceConfig : ScriptableObject
    {
        [Header("General")]
        [SerializeField] private bool enableVoice = true;
        public bool EnableVoice => enableVoice;

        [Tooltip("How often we capture/send voice (seconds). 0.05 = 20 packets/sec.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] private float sendIntervalSeconds = 0.05f;
        public float SendIntervalSeconds => sendIntervalSeconds;

        [Header("Networking")]
        [Tooltip("Max payload per chunk (bytes). Bigger allows fewer chunks but more bandwidth per packet.")]
        [Range(256, 4096)]
        [SerializeField] private int maxChunkBytes = 1024;
        public int MaxChunkBytes => maxChunkBytes;

        [Tooltip("Drop incomplete chunk assemblies after this timeout (seconds).")]
        [Range(0.2f, 2f)]
        [SerializeField] private float chunkAssemblyTimeoutSeconds = 0.75f;
        public float ChunkAssemblyTimeoutSeconds => chunkAssemblyTimeoutSeconds;

        [Header("Audio")]
        [Tooltip("Buffered audio seconds per speaker (helps smooth network jitter).")]
        [Range(0.2f, 3f)]
        [SerializeField] private float bufferSeconds = 1.0f;
        public float BufferSeconds => bufferSeconds;

        [Header("Audio Robustness")]
        [Tooltip("Minimum buffered seconds required before starting playback (reduces crackles on jitter).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float prefillSeconds = 0.08f;
        public float PrefillSeconds => prefillSeconds;

        [Tooltip("If we haven't received audio for this long, we consider the talker inactive (avoid counting idle underruns).")]
        [Range(0.05f, 2f)]
        [SerializeField] private float talkerActiveSeconds = 0.35f;
        public float TalkerActiveSeconds => talkerActiveSeconds;

        [Tooltip("Fade-in time (seconds) when voice playback starts (reduces pops).")]
        [Range(0f, 0.1f)]
        [SerializeField] private float fadeInSeconds = 0.02f;
        public float FadeInSeconds => fadeInSeconds;

        [Tooltip("Fade-out time (seconds) when voice playback stops or underruns (reduces pops).")]
        [Range(0f, 0.2f)]
        [SerializeField] private float fadeOutSeconds = 0.05f;
        public float FadeOutSeconds => fadeOutSeconds;

        [Tooltip("If true, an underrun will stop playback and require prefill again (more stable, slightly more drop).")]
        [SerializeField] private bool rebufferOnUnderrun = true;
        public bool RebufferOnUnderrun => rebufferOnUnderrun;

        [Header("Spatialization")]
        [Tooltip("In Hub: voice is 2D (everyone equally).")]
        [Range(0f, 1f)]
        [SerializeField] private float hubSpatialBlend = 0f;
        public float HubSpatialBlend => hubSpatialBlend;

        [Tooltip("In Run: voice is 3D positional.")]
        [Range(0f, 1f)]
        [SerializeField] private float runSpatialBlend = 1f;
        public float RunSpatialBlend => runSpatialBlend;

        [Min(1f)]
        [SerializeField] private float runMaxDistance = 25f;
        public float RunMaxDistance => runMaxDistance;

        [Min(0f)]
        [SerializeField] private float runMinDistance = 1f;
        public float RunMinDistance => runMinDistance;

        [Header("Audio Buffers")]
        [Tooltip("Initial capacity (bytes) for the compressed voice stream buffer.")]
        [SerializeField] private int compressedBufferBytes = 8192;
        public int CompressedBufferBytes => compressedBufferBytes;

        [Tooltip("Initial capacity (bytes) for the raw (decompressed) voice stream buffer.")]
        [SerializeField] private int rawBufferBytes = 16384;
        public int RawBufferBytes => rawBufferBytes;

        [Tooltip("Voice sample rate. 0 = use Steam's OptimalSampleRate.")]
        [SerializeField] private int sampleRate = 24000;
        public int SampleRate => sampleRate;
    }
}
