using UnityEngine;

namespace DungeonSteakhouse.Net.Voice
{
    [CreateAssetMenu(menuName = "DungeonSteakhouse/Net/Net Steam Voice Config", fileName = "NetSteamVoiceConfig")]
    public sealed class NetSteamVoiceConfig : ScriptableObject
    {
        [Header("General")]
        public bool enableVoice = true;

        [Tooltip("How often we capture/send voice (seconds). 0.05 = 20 packets/sec.")]
        [Range(0.02f, 0.2f)]
        public float sendIntervalSeconds = 0.05f;

        [Header("Networking")]
        [Tooltip("Max payload per chunk (bytes). Bigger allows fewer chunks but more bandwidth per packet.")]
        [Range(256, 4096)]
        public int maxChunkBytes = 1024;

        [Tooltip("Drop incomplete chunk assemblies after this timeout (seconds).")]
        [Range(0.2f, 2f)]
        public float chunkAssemblyTimeoutSeconds = 0.75f;

        [Header("Audio")]
        [Tooltip("Buffered audio seconds per speaker (helps smooth network jitter).")]
        [Range(0.2f, 3f)]
        public float bufferSeconds = 1.0f;

        [Header("Spatialization")]
        [Tooltip("In Hub: voice is 2D (everyone equally).")]
        [Range(0f, 1f)]
        public float hubSpatialBlend = 0f;

        [Tooltip("In Run: voice is 3D positional.")]
        [Range(0f, 1f)]
        public float runSpatialBlend = 1f;

        [Min(1f)]
        public float runMaxDistance = 25f;

        [Min(0f)]
        public float runMinDistance = 1f;
    }
}