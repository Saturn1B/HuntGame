using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using Unity.Netcode;
using Steamworks;
using DungeonSteakhouse.Net.Players;

namespace DungeonSteakhouse.Net.Voice
{
    [DefaultExecutionOrder(200)]
    public sealed class NetSteamVoiceService : MonoBehaviour
    {
        private const string MessageName = "NetSteamVoice";

        [Header("Config")]
        [SerializeField] private NetSteamVoiceConfig config;

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetPlayerRegistry playerRegistry;

        [Header("Debug")]
        [SerializeField] private bool logTransportOnce = true;

        private bool _handlerRegistered;
        private bool _loggedHandler;
        private bool _loggedTxInfo;
        private bool _loggedRxInfo;
        private bool _loggedRelayInfo;

        private readonly MemoryStream _captureStream = new(8192);
        private byte[] _sendChunkScratch;
        private float _sendTimer;
        private uint _packetId;

        private readonly Dictionary<AssemblyKey, ChunkAssembly> _assemblies = new();
        private readonly Dictionary<ulong, TalkerCounters> _talkers = new();

        private readonly MemoryStream _decompressInput = new(8192);
        private readonly MemoryStream _decompressOutput = new(16384);

        private int _steamSampleRate;

        // Diagnostics counters
        private ulong _txPacketsTotal;
        private ulong _txChunksTotal;
        private ulong _txCompressedBytesTotal;

        private ulong _rxChunksTotal;
        private ulong _rxCompressedBytesTotal;

        private ulong _relayChunksTotal;
        private ulong _relayCompressedBytesTotal;

        private ulong _rxPacketsAssembledTotal;
        private ulong _rxPacketsDroppedTotal;
        private ulong _rxChunksDroppedTotal;

        private ulong _decodeOkPacketsTotal;
        private ulong _decodeFailPacketsTotal;
        private ulong _decodePcmBytesTotal;

        public readonly struct DiagnosticsSnapshot
        {
            public readonly bool IsSteamValid;
            public readonly bool IsClient;
            public readonly bool IsServer;
            public readonly bool IsHost;

            public readonly ulong LocalClientId;
            public readonly int ConnectedClientsCount;

            public readonly int ActiveAssemblies;

            public readonly ulong TxPacketsTotal;
            public readonly ulong TxChunksTotal;
            public readonly ulong TxCompressedBytesTotal;

            public readonly ulong RxChunksTotal;
            public readonly ulong RxCompressedBytesTotal;

            public readonly ulong RelayChunksTotal;
            public readonly ulong RelayCompressedBytesTotal;

            public readonly ulong RxPacketsAssembledTotal;
            public readonly ulong RxPacketsDroppedTotal;
            public readonly ulong RxChunksDroppedTotal;

            public readonly ulong DecodeOkPacketsTotal;
            public readonly ulong DecodeFailPacketsTotal;
            public readonly ulong DecodePcmBytesTotal;

            public readonly int SteamSampleRate;

            public DiagnosticsSnapshot(
                bool isSteamValid,
                bool isClient,
                bool isServer,
                bool isHost,
                ulong localClientId,
                int connectedClientsCount,
                int activeAssemblies,
                ulong txPacketsTotal,
                ulong txChunksTotal,
                ulong txCompressedBytesTotal,
                ulong rxChunksTotal,
                ulong rxCompressedBytesTotal,
                ulong relayChunksTotal,
                ulong relayCompressedBytesTotal,
                ulong rxPacketsAssembledTotal,
                ulong rxPacketsDroppedTotal,
                ulong rxChunksDroppedTotal,
                ulong decodeOkPacketsTotal,
                ulong decodeFailPacketsTotal,
                ulong decodePcmBytesTotal,
                int steamSampleRate)
            {
                IsSteamValid = isSteamValid;
                IsClient = isClient;
                IsServer = isServer;
                IsHost = isHost;

                LocalClientId = localClientId;
                ConnectedClientsCount = connectedClientsCount;

                ActiveAssemblies = activeAssemblies;

                TxPacketsTotal = txPacketsTotal;
                TxChunksTotal = txChunksTotal;
                TxCompressedBytesTotal = txCompressedBytesTotal;

                RxChunksTotal = rxChunksTotal;
                RxCompressedBytesTotal = rxCompressedBytesTotal;

                RelayChunksTotal = relayChunksTotal;
                RelayCompressedBytesTotal = relayCompressedBytesTotal;

                RxPacketsAssembledTotal = rxPacketsAssembledTotal;
                RxPacketsDroppedTotal = rxPacketsDroppedTotal;
                RxChunksDroppedTotal = rxChunksDroppedTotal;

                DecodeOkPacketsTotal = decodeOkPacketsTotal;
                DecodeFailPacketsTotal = decodeFailPacketsTotal;
                DecodePcmBytesTotal = decodePcmBytesTotal;

                SteamSampleRate = steamSampleRate;
            }
        }

        public readonly struct TalkerSnapshot
        {
            public readonly ulong TalkerClientId;
            public readonly ulong RxCompressedBytesTotal;
            public readonly ulong DecodedPacketsTotal;
            public readonly ulong DecodedPcmBytesTotal;
            public readonly float LastDecodedTime;

            public TalkerSnapshot(ulong talkerClientId, ulong rxCompressedBytesTotal, ulong decodedPacketsTotal, ulong decodedPcmBytesTotal, float lastDecodedTime)
            {
                TalkerClientId = talkerClientId;
                RxCompressedBytesTotal = rxCompressedBytesTotal;
                DecodedPacketsTotal = decodedPacketsTotal;
                DecodedPcmBytesTotal = decodedPcmBytesTotal;
                LastDecodedTime = lastDecodedTime;
            }
        }

        private struct TalkerCounters
        {
            public ulong RxCompressedBytesTotal;
            public ulong DecodedPacketsTotal;
            public ulong DecodedPcmBytesTotal;
            public float LastDecodedTime;
        }

        public void GetDiagnosticsSnapshot(out DiagnosticsSnapshot snapshot)
        {
            bool isSteamValid = SteamClient.IsValid;
            bool isClient = networkManager != null && networkManager.IsClient;
            bool isServer = networkManager != null && networkManager.IsServer;
            bool isHost = networkManager != null && networkManager.IsHost;

            ulong localId = networkManager != null ? networkManager.LocalClientId : 0;
            int connected = (networkManager != null && networkManager.ConnectedClientsIds != null) ? networkManager.ConnectedClientsIds.Count : 0;

            snapshot = new DiagnosticsSnapshot(
                isSteamValid,
                isClient,
                isServer,
                isHost,
                localId,
                connected,
                _assemblies.Count,
                _txPacketsTotal,
                _txChunksTotal,
                _txCompressedBytesTotal,
                _rxChunksTotal,
                _rxCompressedBytesTotal,
                _relayChunksTotal,
                _relayCompressedBytesTotal,
                _rxPacketsAssembledTotal,
                _rxPacketsDroppedTotal,
                _rxChunksDroppedTotal,
                _decodeOkPacketsTotal,
                _decodeFailPacketsTotal,
                _decodePcmBytesTotal,
                _steamSampleRate
            );
        }

        public bool TryGetTalkerSnapshot(ulong talkerClientId, out TalkerSnapshot snapshot)
        {
            if (_talkers.TryGetValue(talkerClientId, out var counters))
            {
                snapshot = new TalkerSnapshot(
                    talkerClientId,
                    counters.RxCompressedBytesTotal,
                    counters.DecodedPacketsTotal,
                    counters.DecodedPcmBytesTotal,
                    counters.LastDecodedTime
                );
                return true;
            }

            snapshot = default;
            return false;
        }

        private void Awake()
        {
            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (playerRegistry == null)
                playerRegistry = FindFirstObjectByType<NetPlayerRegistry>();

            if (config == null)
                Debug.LogWarning("[NetSteamVoiceService] NetSteamVoiceConfig is not assigned.");

            _steamSampleRate = 0;
        }

        private void OnEnable()
        {
            TryRegisterHandler();
        }

        private void OnDisable()
        {
            TryUnregisterHandler();
            SafeSetVoiceRecord(false);
        }

        private void Update()
        {
            // Make the handler registration robust (works even if NetworkManager initializes later).
            TryRegisterHandler();

            if (config == null || !config.enableVoice)
                return;

            if (networkManager == null || (!networkManager.IsClient && !networkManager.IsHost))
                return;

            if (!SteamClient.IsValid)
                return;

            EnsureSteamSampleRate();
            CleanupOldAssemblies();

            _sendTimer += Time.unscaledDeltaTime;
            if (_sendTimer < config.sendIntervalSeconds)
                return;

            _sendTimer = 0f;

            // Open-mic: record all the time
            if (!SafeSetVoiceRecord(true))
                return;

            if (!SafeHasVoiceData())
                return;

            _captureStream.SetLength(0);
            _captureStream.Position = 0;

            int bytes = SafeReadVoiceData(_captureStream);
            if (bytes <= 0)
                return;

            var buf = _captureStream.GetBuffer();
            SendCompressedVoice(buf, bytes);
        }

        private void TryRegisterHandler()
        {
            if (_handlerRegistered)
                return;

            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (networkManager == null)
                return;

            var cmm = networkManager.CustomMessagingManager;
            if (cmm == null)
                return;

            cmm.RegisterNamedMessageHandler(MessageName, OnVoiceMessage);
            _handlerRegistered = true;

            if (logTransportOnce && !_loggedHandler)
            {
                _loggedHandler = true;
                Debug.Log($"[NetSteamVoiceService] Registered handler '{MessageName}' on NetworkManager '{networkManager.gameObject.name}'. LocalClientId={networkManager.LocalClientId}");
            }
        }

        private void TryUnregisterHandler()
        {
            if (!_handlerRegistered)
                return;

            if (networkManager == null || networkManager.CustomMessagingManager == null)
                return;

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            _handlerRegistered = false;

            if (logTransportOnce)
                Debug.Log($"[NetSteamVoiceService] Unregistered handler '{MessageName}'.");
        }

        private void EnsureSendScratchSize(int requiredSize)
        {
            if (requiredSize <= 0)
                return;

            if (_sendChunkScratch == null || _sendChunkScratch.Length < requiredSize)
                _sendChunkScratch = new byte[Mathf.NextPowerOfTwo(requiredSize)];
        }

        private void EnsureSteamSampleRate()
        {
            if (_steamSampleRate > 0)
                return;

            try
            {
                _steamSampleRate = (int)SteamUser.OptimalSampleRate;
                if (_steamSampleRate <= 0)
                    _steamSampleRate = AudioSettings.outputSampleRate;
            }
            catch (Exception)
            {
                _steamSampleRate = AudioSettings.outputSampleRate;
            }
        }

        private bool SafeSetVoiceRecord(bool enabled)
        {
            try
            {
                SteamUser.VoiceRecord = enabled;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool SafeHasVoiceData()
        {
            try
            {
                return SteamUser.HasVoiceData;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private int SafeReadVoiceData(Stream output)
        {
            try
            {
                return SteamUser.ReadVoiceData(output);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void SendCompressedVoice(byte[] data, int length)
        {
            if (data == null || length <= 0)
                return;

            if (networkManager == null || networkManager.CustomMessagingManager == null)
                return;

            var senderId = networkManager.LocalClientId;

            int maxChunk = Mathf.Max(256, config.maxChunkBytes);
            int chunkCount = Mathf.CeilToInt(length / (float)maxChunk);
            uint packetId = ++_packetId;

            _txPacketsTotal++;
            _txChunksTotal += (ulong)chunkCount;
            _txCompressedBytesTotal += (ulong)length;

            if (logTransportOnce && !_loggedTxInfo)
            {
                _loggedTxInfo = true;
                int connected = networkManager.ConnectedClientsIds != null ? networkManager.ConnectedClientsIds.Count : 0;
                Debug.Log($"[NetSteamVoiceService] TX started. IsServer={networkManager.IsServer} IsClient={networkManager.IsClient} LocalClientId={senderId} ConnectedClients={connected}");
            }

            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                int offset = chunkIndex * maxChunk;
                int chunkLen = Mathf.Min(maxChunk, length - offset);
                if (chunkLen <= 0)
                    continue;

                using var writer = new FastBufferWriter(32 + chunkLen, Allocator.Temp);
                writer.WriteValueSafe(senderId);
                writer.WriteValueSafe(packetId);
                writer.WriteValueSafe((ushort)chunkIndex);
                writer.WriteValueSafe((ushort)chunkCount);
                writer.WriteValueSafe((ushort)chunkLen);

                EnsureSendScratchSize(chunkLen);
                Buffer.BlockCopy(data, offset, _sendChunkScratch, 0, chunkLen);
                writer.WriteBytesSafe(_sendChunkScratch, 0, chunkLen);

                if (networkManager.IsServer)
                {
                    foreach (var client in networkManager.ConnectedClientsIds)
                    {
                        if (client == senderId)
                            continue;

                        networkManager.CustomMessagingManager.SendNamedMessage(
                            MessageName, client, writer, NetworkDelivery.Unreliable);
                    }
                }
                else
                {
                    networkManager.CustomMessagingManager.SendNamedMessage(
                        MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
                }
            }
        }

        private void OnVoiceMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong talkerClientId);
            reader.ReadValueSafe(out uint packetId);
            reader.ReadValueSafe(out ushort chunkIndex);
            reader.ReadValueSafe(out ushort chunkCount);
            reader.ReadValueSafe(out ushort chunkLen);

            if (chunkLen == 0 || chunkCount == 0)
                return;

            _rxChunksTotal++;
            _rxCompressedBytesTotal += chunkLen;

            if (logTransportOnce && !_loggedRxInfo)
            {
                _loggedRxInfo = true;
                Debug.Log($"[NetSteamVoiceService] RX started. sender={senderClientId} talker={talkerClientId} chunk={chunkIndex + 1}/{chunkCount} len={chunkLen}");
            }

            var chunk = new byte[chunkLen];
            ReadBytesCompat(reader, chunk, chunkLen);

            if (networkManager == null)
                return;

            bool isFromServer = senderClientId == NetworkManager.ServerClientId;

            if (networkManager.IsServer && !isFromServer)
            {
                if (talkerClientId != senderClientId)
                    return;

                if (logTransportOnce && !_loggedRelayInfo)
                {
                    _loggedRelayInfo = true;
                    Debug.Log($"[NetSteamVoiceService] RELAY started. talker={talkerClientId} ConnectedClients={networkManager.ConnectedClientsIds.Count}");
                }

                using var writer = new FastBufferWriter(32 + chunkLen, Allocator.Temp);
                writer.WriteValueSafe(talkerClientId);
                writer.WriteValueSafe(packetId);
                writer.WriteValueSafe(chunkIndex);
                writer.WriteValueSafe(chunkCount);
                writer.WriteValueSafe(chunkLen);
                writer.WriteBytesSafe(chunk, 0, chunkLen);

                foreach (var client in networkManager.ConnectedClientsIds)
                {
                    if (client == talkerClientId)
                        continue;

                    networkManager.CustomMessagingManager.SendNamedMessage(
                        MessageName, client, writer, NetworkDelivery.Unreliable);

                    _relayChunksTotal++;
                    _relayCompressedBytesTotal += chunkLen;
                }

                return;
            }

            var key = new AssemblyKey(talkerClientId, packetId);
            if (!_assemblies.TryGetValue(key, out var assembly))
            {
                assembly = new ChunkAssembly(chunkCount, Time.unscaledTime);
                _assemblies[key] = assembly;
            }

            assembly.LastTime = Time.unscaledTime;
            assembly.SetChunk(chunkIndex, chunk);

            if (!assembly.IsComplete)
                return;

            _assemblies.Remove(key);
            _rxPacketsAssembledTotal++;

            _decompressInput.SetLength(0);
            _decompressInput.Position = 0;

            int compressedLength = assembly.WriteTo(_decompressInput);
            if (compressedLength <= 0)
                return;

            _decompressInput.Position = 0;
            DecodeAndPlayFromStream(talkerClientId, _decompressInput, compressedLength);
        }

        private static void ReadBytesCompat(FastBufferReader reader, byte[] dst, int count)
        {
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out byte b);
                dst[i] = b;
            }
        }

        private void DecodeAndPlayFromStream(ulong talkerClientId, Stream compressedStream, int compressedLength)
        {
            if (networkManager == null)
                return;

            if (talkerClientId == networkManager.LocalClientId)
                return;

            if (!TryGetSpeaker(talkerClientId, out var speaker))
                return;

            if (!SteamClient.IsValid)
                return;

            EnsureSteamSampleRate();

            _decompressOutput.SetLength(0);
            _decompressOutput.Position = 0;

            int written = 0;
            try
            {
                written = SteamUser.DecompressVoice(compressedStream, compressedLength, _decompressOutput);
            }
            catch (Exception)
            {
                written = 0;
            }

            if (written <= 0)
            {
                _decodeFailPacketsTotal++;
                return;
            }

            _decodeOkPacketsTotal++;
            _decodePcmBytesTotal += (ulong)written;

            var pcm = _decompressOutput.GetBuffer();
            speaker.EnqueuePcm16Resampled(pcm, written, _steamSampleRate);

            if (!_talkers.TryGetValue(talkerClientId, out var counters))
                counters = default;

            counters.RxCompressedBytesTotal += (ulong)compressedLength;
            counters.DecodedPacketsTotal++;
            counters.DecodedPcmBytesTotal += (ulong)written;
            counters.LastDecodedTime = Time.unscaledTime;

            _talkers[talkerClientId] = counters;
        }

        private bool TryGetSpeaker(ulong talkerClientId, out NetSteamVoiceSpeaker speaker)
        {
            speaker = null;

            if (playerRegistry == null)
                return false;

            var players = playerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null)
                    continue;

                if (p.OwnerClientId != talkerClientId)
                    continue;

                speaker = p.GetComponent<NetSteamVoiceSpeaker>();
                return speaker != null;
            }

            return false;
        }

        private void CleanupOldAssemblies()
        {
            if (config == null)
                return;

            float now = Time.unscaledTime;
            float timeout = Mathf.Max(0.2f, config.chunkAssemblyTimeoutSeconds);

            var toRemove = ListPool<AssemblyKey>.Get();
            foreach (var kvp in _assemblies)
            {
                if (now - kvp.Value.LastTime > timeout)
                {
                    toRemove.Add(kvp.Key);
                    _rxPacketsDroppedTotal++;
                    _rxChunksDroppedTotal += (ulong)Mathf.Max(0, kvp.Value.ExpectedChunks - kvp.Value.ReceivedChunks);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
                _assemblies.Remove(toRemove[i]);

            ListPool<AssemblyKey>.Release(toRemove);
        }

        private readonly struct AssemblyKey : IEquatable<AssemblyKey>
        {
            public readonly ulong Talker;
            public readonly uint PacketId;

            public AssemblyKey(ulong talker, uint packetId)
            {
                Talker = talker;
                PacketId = packetId;
            }

            public bool Equals(AssemblyKey other) => Talker == other.Talker && PacketId == other.PacketId;
            public override bool Equals(object obj) => obj is AssemblyKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Talker, PacketId);
        }

        private sealed class ChunkAssembly
        {
            private readonly byte[][] _chunks;
            private int _received;

            public float LastTime { get; set; }
            public int ExpectedChunks => _chunks.Length;
            public int ReceivedChunks => _received;
            public bool IsComplete => _received == _chunks.Length;

            public ChunkAssembly(int chunkCount, float now)
            {
                _chunks = new byte[Mathf.Max(1, chunkCount)][];
                LastTime = now;
            }

            public void SetChunk(int index, byte[] data)
            {
                if (index < 0 || index >= _chunks.Length)
                    return;

                if (_chunks[index] != null)
                    return;

                _chunks[index] = data;
                _received++;
            }

            public int WriteTo(Stream output)
            {
                if (output == null)
                    return 0;

                int total = 0;
                for (int i = 0; i < _chunks.Length; i++)
                {
                    var chunk = _chunks[i];
                    if (chunk == null)
                        return 0;

                    output.Write(chunk, 0, chunk.Length);
                    total += chunk.Length;
                }

                return total;
            }
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                if (Pool.Count > 0)
                {
                    var list = Pool.Pop();
                    list.Clear();
                    return list;
                }

                return new List<T>(8);
            }

            public static void Release(List<T> list)
            {
                if (list == null)
                    return;

                list.Clear();
                Pool.Push(list);
            }
        }
    }
}