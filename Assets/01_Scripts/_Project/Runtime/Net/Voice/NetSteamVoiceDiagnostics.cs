using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using DungeonSteakhouse.Net.Players;

namespace DungeonSteakhouse.Net.Voice
{
    /// <summary>
    /// Periodic console diagnostics for Steam voice:
    /// - TX/RX throughput (bytes/sec)
    /// - packet/chunk counts and drops
    /// - per-talker activity (optional)
    /// - per-speaker buffer fill / underruns (optional)
    ///
    /// This is intentionally console-only (no HUD) and can be toggled on/off in the inspector.
    /// </summary>
    [DefaultExecutionOrder(220)]
    public sealed class NetSteamVoiceDiagnostics : MonoBehaviour
    {
        [Header("Enable")]
        [Tooltip("If disabled, no logs will be produced.")]
        public bool enableLogs = true;

        [Tooltip("Log interval in seconds.")]
        [Range(0.25f, 5f)]
        public float logIntervalSeconds = 1.0f;

        [Header("References")]
        public NetSteamVoiceService voiceService;
        public NetworkManager networkManager;
        public NetPlayerRegistry playerRegistry;

        [Header("Per Talker")]
        [Tooltip("If true, logs per-talker activity and decode stats.")]
        public bool logPerTalker = true;

        [Tooltip("A talker is considered 'active' if we decoded voice within this time window.")]
        [Range(0.05f, 2f)]
        public float talkActivitySeconds = 0.35f;

        [Tooltip("If true, logs talk start/stop events when activity changes.")]
        public bool logTalkStartStop = true;

        [Header("Per Speaker Buffer")]
        [Tooltip("If true, logs buffer fill and underrun metrics for each speaker.")]
        public bool logPerSpeakerBuffer = true;

        private float _timer;

        private NetSteamVoiceService.DiagnosticsSnapshot _prev;
        private bool _hasPrev;

        private readonly Dictionary<ulong, bool> _talking = new();
        private readonly StringBuilder _sb = new(2048);

        private void Awake()
        {
            if (voiceService == null)
                voiceService = FindFirstObjectByType<NetSteamVoiceService>();

            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (playerRegistry == null)
                playerRegistry = FindFirstObjectByType<NetPlayerRegistry>();
        }

        private void Update()
        {
            if (!enableLogs)
                return;

            if (voiceService == null)
                return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < logIntervalSeconds)
                return;

            float dt = Mathf.Max(0.001f, _timer);
            _timer = 0f;

            voiceService.GetDiagnosticsSnapshot(out var now);

            ulong txBytesDelta = 0;
            ulong rxBytesDelta = 0;
            ulong relayBytesDelta = 0;
            ulong dropPacketsDelta = 0;
            ulong dropChunksDelta = 0;
            ulong decodeOkDelta = 0;
            ulong decodeFailDelta = 0;

            if (_hasPrev)
            {
                txBytesDelta = now.TxCompressedBytesTotal - _prev.TxCompressedBytesTotal;
                rxBytesDelta = now.RxCompressedBytesTotal - _prev.RxCompressedBytesTotal;
                relayBytesDelta = now.RelayCompressedBytesTotal - _prev.RelayCompressedBytesTotal;
                dropPacketsDelta = now.RxPacketsDroppedTotal - _prev.RxPacketsDroppedTotal;
                dropChunksDelta = now.RxChunksDroppedTotal - _prev.RxChunksDroppedTotal;
                decodeOkDelta = now.DecodeOkPacketsTotal - _prev.DecodeOkPacketsTotal;
                decodeFailDelta = now.DecodeFailPacketsTotal - _prev.DecodeFailPacketsTotal;
            }

            _prev = now;
            _hasPrev = true;

            double txBps = txBytesDelta / dt;
            double rxBps = rxBytesDelta / dt;
            double relayBps = relayBytesDelta / dt;

            _sb.Clear();

            _sb.AppendLine("[SteamVoice] Diagnostics");
            _sb.Append("  SteamValid: ").Append(now.IsSteamValid)
                .Append(" | Mode: ").Append(FormatMode(now))
                .Append(" | SampleRate: ").Append(now.SteamSampleRate)
                .Append(" | ActiveAssemblies: ").Append(now.ActiveAssemblies)
                .AppendLine();

            _sb.Append("  TX: ").Append(FormatBytesPerSecond(txBps))
                .Append(" | packets: ").Append(now.TxPacketsTotal)
                .Append(" | chunks: ").Append(now.TxChunksTotal)
                .Append(" | bytes: ").Append(now.TxCompressedBytesTotal)
                .AppendLine();

            _sb.Append("  RX: ").Append(FormatBytesPerSecond(rxBps))
                .Append(" | chunks: ").Append(now.RxChunksTotal)
                .Append(" | bytes: ").Append(now.RxCompressedBytesTotal)
                .AppendLine();

            if (now.IsServer)
            {
                _sb.Append("  RELAY: ").Append(FormatBytesPerSecond(relayBps))
                    .Append(" | chunks: ").Append(now.RelayChunksTotal)
                    .Append(" | bytes: ").Append(now.RelayCompressedBytesTotal)
                    .AppendLine();
            }

            _sb.Append("  ASSEMBLED: ").Append(now.RxPacketsAssembledTotal)
                .Append(" | DROPPED: ").Append(now.RxPacketsDroppedTotal)
                .Append(" (Δ ").Append(dropPacketsDelta).Append(")")
                .Append(" | droppedChunks: ").Append(now.RxChunksDroppedTotal)
                .Append(" (Δ ").Append(dropChunksDelta).Append(")")
                .AppendLine();

            _sb.Append("  DECODE: ok ").Append(now.DecodeOkPacketsTotal)
                .Append(" (Δ ").Append(decodeOkDelta).Append(")")
                .Append(" | fail ").Append(now.DecodeFailPacketsTotal)
                .Append(" (Δ ").Append(decodeFailDelta).Append(")")
                .Append(" | pcmBytes ").Append(now.DecodePcmBytesTotal)
                .AppendLine();

            if (logPerTalker || logPerSpeakerBuffer)
            {
                AppendPerPlayerSection(now);
            }

            Debug.Log(_sb.ToString());
        }

        private void AppendPerPlayerSection(NetSteamVoiceService.DiagnosticsSnapshot now)
        {
            if (playerRegistry == null || playerRegistry.Players == null)
                return;

            float tNow = Time.unscaledTime;

            var players = playerRegistry.Players;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null)
                    continue;

                ulong id = p.OwnerClientId;

                if (logPerTalker)
                {
                    bool hasTalker = voiceService.TryGetTalkerSnapshot(id, out var ts);
                    bool isActive = hasTalker && (tNow - ts.LastDecodedTime) <= talkActivitySeconds;

                    if (logTalkStartStop)
                    {
                        _talking.TryGetValue(id, out bool wasActive);
                        if (wasActive != isActive)
                        {
                            _talking[id] = isActive;
                            Debug.Log($"[SteamVoice] Talker {(isActive ? "START" : "STOP")} clientId={id} name='{p.DisplayName}'");
                        }
                    }
                    else
                    {
                        _talking[id] = isActive;
                    }

                    if (hasTalker)
                    {
                        _sb.Append("  TALKER ").Append(id)
                            .Append(" '").Append(p.DisplayName).Append('\'')
                            .Append(" | active ").Append(isActive)
                            .Append(" | decoded ").Append(ts.DecodedPacketsTotal)
                            .Append(" | rxBytes ").Append(ts.RxCompressedBytesTotal)
                            .Append(" | pcmBytes ").Append(ts.DecodedPcmBytesTotal)
                            .Append(" | last ").Append((tNow - ts.LastDecodedTime).ToString("0.00")).Append("s ago")
                            .AppendLine();
                    }
                }

                if (logPerSpeakerBuffer)
                {
                    var speaker = p.GetComponent<NetSteamVoiceSpeaker>();
                    if (speaker == null)
                        continue;

                    speaker.GetDebugSnapshot(out var ss);

                    _sb.Append("  SPEAKER ").Append(id)
                        .Append(" | buffer ").Append(ss.FillSamples).Append('/').Append(ss.CapacitySamples)
                        .Append(" (").Append(ss.BufferedSeconds.ToString("0.00")).Append("s)")
                        .Append(" | underrunCallbacks ").Append(ss.UnderrunCallbacks)
                        .Append(" | underrunMissingSamples ").Append(ss.UnderrunMissingSamples)
                        .Append(" | overrunDroppedSamples ").Append(ss.OverrunDroppedSamples)
                        .AppendLine();
                }
            }
        }

        private static string FormatMode(NetSteamVoiceService.DiagnosticsSnapshot s)
        {
            if (s.IsHost) return "Host";
            if (s.IsServer) return "Server";
            if (s.IsClient) return "Client";
            return "None";
        }

        private static string FormatBytesPerSecond(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024.0)
                return $"{bytesPerSecond:0} B/s";
            if (bytesPerSecond < 1024.0 * 1024.0)
                return $"{(bytesPerSecond / 1024.0):0.0} KB/s";
            return $"{(bytesPerSecond / (1024.0 * 1024.0)):0.00} MB/s";
        }
    }
}