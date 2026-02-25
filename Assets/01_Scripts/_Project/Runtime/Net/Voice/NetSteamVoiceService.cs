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
    // Run later than most systems to reduce chances of calling SteamUser before Steam is ready.
    [DefaultExecutionOrder(200)]
    public sealed class NetSteamVoiceService : MonoBehaviour
    {
        private const string MessageName = "NetSteamVoice";

        [Header("Config")]
        [SerializeField] private NetSteamVoiceConfig config;

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetPlayerRegistry playerRegistry;

        private readonly MemoryStream _captureStream = new(8192);
        private float _sendTimer;

        private uint _packetId;

        private readonly Dictionary<AssemblyKey, ChunkAssembly> _assemblies = new();

        private readonly MemoryStream _decompressInput = new(8192);
        private readonly MemoryStream _decompressOutput = new(16384);

        // Lazy initialized when Steam is valid
        private int _steamSampleRate;

        private void Awake()
        {
            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (playerRegistry == null)
                playerRegistry = FindFirstObjectByType<NetPlayerRegistry>();

            if (config == null)
                Debug.LogWarning("[NetSteamVoiceService] NetSteamVoiceConfig is not assigned.");

            // DO NOT touch SteamUser here (Steam might not be initialized yet).
            _steamSampleRate = 0;
        }

        private void OnEnable()
        {
            if (networkManager != null && networkManager.CustomMessagingManager != null)
                networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnVoiceMessage);
        }

        private void OnDisable()
        {
            if (networkManager != null && networkManager.CustomMessagingManager != null)
                networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);

            // Stop recording safely (Steam might not be ready / might already be shut down).
            SafeSetVoiceRecord(false);
        }

        private void Update()
        {
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

            // Open-mic: record all the time (safe wrapper)
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

        private void EnsureSteamSampleRate()
        {
            if (_steamSampleRate > 0)
                return;

            try
            {
                // This can throw if Steam isn't ready, even if IsValid just became true this frame.
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

            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                int offset = chunkIndex * maxChunk;
                int chunkLen = Mathf.Min(maxChunk, length - offset);
                if (chunkLen <= 0)
                    continue;

                var chunkBuf = new byte[chunkLen];
                Buffer.BlockCopy(data, offset, chunkBuf, 0, chunkLen);

                using var writer = new FastBufferWriter(32 + chunkLen, Allocator.Temp);
                writer.WriteValueSafe(senderId);
                writer.WriteValueSafe(packetId);
                writer.WriteValueSafe((ushort)chunkIndex);
                writer.WriteValueSafe((ushort)chunkCount);
                writer.WriteValueSafe((ushort)chunkLen);
                writer.WriteBytesSafe(chunkBuf, 0, chunkLen);

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

            var chunk = new byte[chunkLen];
            ReadBytesCompat(reader, chunk, chunkLen);

            // Server relays client voice to other clients
            if (networkManager != null && networkManager.IsServer)
            {
                if (talkerClientId != senderClientId)
                    return;

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
                }

                return;
            }

            // Client: assemble chunks then decode & play
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

            var compressed = assembly.Combine();
            if (compressed == null || compressed.Length == 0)
                return;

            DecodeAndPlay(talkerClientId, compressed);
        }

        private static void ReadBytesCompat(FastBufferReader reader, byte[] dst, int count)
        {
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out byte b);
                dst[i] = b;
            }
        }

        private void DecodeAndPlay(ulong talkerClientId, byte[] compressed)
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

            _decompressInput.SetLength(0);
            _decompressInput.Position = 0;
            _decompressInput.Write(compressed, 0, compressed.Length);
            _decompressInput.Position = 0;

            _decompressOutput.SetLength(0);
            _decompressOutput.Position = 0;

            int written = 0;
            try
            {
                written = SteamUser.DecompressVoice(_decompressInput, compressed.Length, _decompressOutput);
            }
            catch (Exception)
            {
                written = 0;
            }

            if (written <= 0)
                return;

            var pcm = _decompressOutput.GetBuffer();
            speaker.EnqueuePcm16Resampled(pcm, written, _steamSampleRate);
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
                    toRemove.Add(kvp.Key);
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

            public byte[] Combine()
            {
                int total = 0;
                for (int i = 0; i < _chunks.Length; i++)
                {
                    if (_chunks[i] == null)
                        return null;

                    total += _chunks[i].Length;
                }

                var result = new byte[total];
                int offset = 0;

                for (int i = 0; i < _chunks.Length; i++)
                {
                    Buffer.BlockCopy(_chunks[i], 0, result, offset, _chunks[i].Length);
                    offset += _chunks[i].Length;
                }

                return result;
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