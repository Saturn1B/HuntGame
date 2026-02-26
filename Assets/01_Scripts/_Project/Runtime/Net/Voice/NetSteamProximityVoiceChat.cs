using System;
using System.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Steamworks;

namespace DungeonSteakhouse.Net.Voice
{
    [DisallowMultipleComponent]
    public sealed class NetSteamProximityVoiceChat : MonoBehaviour
    {
        private const string MsgClientToServer = "SteamVoice_C2S";
        private const string MsgServerToClients = "SteamVoice_S2C";

        public static NetSteamProximityVoiceChat Instance { get; private set; }

        [Header("Behavior")]
        [Tooltip("If enabled, Steam voice recording is always ON while connected as a client.")]
        [SerializeField] private bool alwaysTransmit = true;

        [Header("Networking")]
        [Tooltip("Delivery mode used for voice packets. UnreliableSequenced is recommended for real-time voice.")]
        [SerializeField] private NetworkDelivery delivery = NetworkDelivery.UnreliableSequenced;

        [Header("Performance")]
        [Tooltip("If the listener is farther than this distance, we skip decoding/playback locally (packets are still received).")]
        [SerializeField] private float decodeDistanceMax = 25f;

        [Header("Debug")]
        [SerializeField] private bool logOnce = true;
        [SerializeField] private bool logErrors = false;

        private bool _registered;
        private bool _loggedRegister;
        private bool _loggedSend;
        private bool _loggedRecv;
        private bool _loggedRelay;

        private readonly MemoryStream _voiceCompressed = new MemoryStream(8192);
        private readonly MemoryStream _voiceIn = new MemoryStream(8192);
        private readonly MemoryStream _voiceOut = new MemoryStream(16384);

        private byte[] _txBuffer = new byte[8192];
        private byte[] _rxBuffer = new byte[8192];
        private byte[] _skipBuffer = new byte[8192];
        private float[] _floatBuffer = new float[8192];

        private int _sampleRate = 24000;

        private void Awake()
        {
            // Singleton to avoid multiple handler registrations / duplicated capture
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            TryRegisterHandlers();
        }

        private void OnDisable()
        {
            StopRecordingSafe();
        }

        private void Update()
        {
            TryRegisterHandlers();

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
                return;

            // Capture + send on any client (including Host)
            if (!nm.IsClient || !nm.IsConnectedClient)
                return;

            if (!SteamClient.IsValid)
                return;

            if (_sampleRate <= 0)
                _sampleRate = (int)SteamUser.OptimalSampleRate;

            // Open-mic
            bool transmit = alwaysTransmit;
            SteamUser.VoiceRecord = transmit;

            if (!transmit)
                return;

            if (!SteamUser.HasVoiceData)
                return;

            _voiceCompressed.Position = 0;
            _voiceCompressed.SetLength(0);

            int bytesWritten = SteamUser.ReadVoiceData(_voiceCompressed);
            if (bytesWritten <= 0)
                return;

            EnsureByteBuffer(ref _txBuffer, bytesWritten);
            Buffer.BlockCopy(_voiceCompressed.GetBuffer(), 0, _txBuffer, 0, bytesWritten);

            if (logOnce && !_loggedSend)
            {
                _loggedSend = true;
                Debug.Log($"[SteamVoice] TX active. LocalClientId={nm.LocalClientId} IsHost={nm.IsHost} Delivery={delivery}");
            }

            // Send to server
            using (var writer = new FastBufferWriter(sizeof(int) + bytesWritten, Allocator.Temp))
            {
                writer.WriteValueSafe(bytesWritten);
                writer.WriteBytesSafe(_txBuffer, bytesWritten, 0);

                nm.CustomMessagingManager.SendNamedMessage(
                    MsgClientToServer,
                    NetworkManager.ServerClientId,
                    writer,
                    delivery
                );
            }
        }

        private void TryRegisterHandlers()
        {
            if (_registered)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null)
                return;

            var cmm = nm.CustomMessagingManager;
            if (cmm == null)
                return;

            // Server relay handler (host has it too)
            cmm.RegisterNamedMessageHandler(MsgClientToServer, OnVoiceFromClient);

            // Client receive handler (host has it too)
            cmm.RegisterNamedMessageHandler(MsgServerToClients, OnVoiceFromServer);

            _registered = true;

            if (logOnce && !_loggedRegister)
            {
                _loggedRegister = true;
                Debug.Log($"[SteamVoice] Handlers registered on NetworkManager '{nm.gameObject.name}'.");
            }
        }

        // -------------------------
        // SERVER: relay voice to everyone (except the speaker)
        // -------------------------
        private void OnVoiceFromClient(ulong senderClientId, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return;

            reader.ReadValueSafe(out int length);
            if (length <= 0 || length > 64 * 1024)
                return;

            EnsureByteBuffer(ref _rxBuffer, length);
            reader.ReadBytesSafe(ref _rxBuffer, length, 0);

            if (logOnce && !_loggedRelay)
            {
                _loggedRelay = true;
                Debug.Log($"[SteamVoice] RELAY active. ConnectedClients={nm.ConnectedClientsIds.Count}");
            }

            using (var writer = new FastBufferWriter(sizeof(ulong) + sizeof(int) + length, Allocator.Temp))
            {
                writer.WriteValueSafe(senderClientId);
                writer.WriteValueSafe(length);
                writer.WriteBytesSafe(_rxBuffer, length, 0);

                foreach (var clientId in nm.ConnectedClientsIds)
                {
                    if (clientId == senderClientId)
                        continue;

                    nm.CustomMessagingManager.SendNamedMessage(
                        MsgServerToClients,
                        clientId,
                        writer,
                        delivery
                    );
                }
            }
        }

        // -------------------------
        // CLIENT: receive voice and play it from the speaker object
        // -------------------------
        private void OnVoiceFromServer(ulong serverClientId, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsClient)
                return;

            reader.ReadValueSafe(out ulong speakerClientId);
            reader.ReadValueSafe(out int length);

            if (length <= 0 || length > 64 * 1024)
                return;

            // Do not play our own voice (avoid echo)
            if (speakerClientId == nm.LocalClientId)
            {
                SkipBytes(reader, length);
                return;
            }

            if (!TryFindPlayerObject(nm, speakerClientId, out var playerObj))
            {
                SkipBytes(reader, length);
                return;
            }

            var speaker = playerObj.GetComponentInChildren<SteamVoiceSpeaker>(true);
            if (speaker == null)
            {
                SkipBytes(reader, length);
                return;
            }

            // Optional distance cull (decode skip)
            var listener = ResolveListenerTransform();
            if (listener != null)
            {
                float max = Mathf.Max(decodeDistanceMax, speaker.MaxDistance);
                float sqr = (playerObj.transform.position - listener.position).sqrMagnitude;
                if (sqr > max * max)
                {
                    SkipBytes(reader, length);
                    return;
                }
            }

            EnsureByteBuffer(ref _rxBuffer, length);
            reader.ReadBytesSafe(ref _rxBuffer, length, 0);

            if (!SteamClient.IsValid)
                return;

            if (_sampleRate <= 0)
                _sampleRate = (int)SteamUser.OptimalSampleRate;

            speaker.EnsureInitialized(_sampleRate);

            // Decompress to PCM 16-bit mono
            _voiceIn.Position = 0;
            _voiceIn.SetLength(0);
            _voiceIn.Write(_rxBuffer, 0, length);
            _voiceIn.Position = 0;

            _voiceOut.Position = 0;
            _voiceOut.SetLength(0);

            int outBytes;
            try
            {
                outBytes = SteamUser.DecompressVoice(_voiceIn, length, _voiceOut);
            }
            catch (Exception e)
            {
                if (logErrors)
                    Debug.LogWarning($"[SteamVoice] Decompress failed: {e.Message}");
                return;
            }

            if (outBytes <= 0)
                return;

            if (logOnce && !_loggedRecv)
            {
                _loggedRecv = true;
                Debug.Log($"[SteamVoice] RX active. LocalClientId={nm.LocalClientId} SampleRate={_sampleRate}");
            }

            byte[] pcm = _voiceOut.GetBuffer();
            int sampleCount = outBytes / 2;

            EnsureFloatBuffer(sampleCount);

            int bi = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(pcm[bi] | (pcm[bi + 1] << 8));
                _floatBuffer[i] = s / 32768f;
                bi += 2;
            }

            speaker.PushSamples(_floatBuffer, sampleCount);
        }

        private static bool TryFindPlayerObject(NetworkManager nm, ulong clientId, out NetworkObject playerObj)
        {
            playerObj = null;

            if (nm == null || nm.SpawnManager == null)
                return false;

            var spawned = nm.SpawnManager.SpawnedObjectsList;
            if (spawned == null)
                return false;

            foreach (var no in spawned)
            {
                if (no == null)
                    continue;

                if (no.IsPlayerObject && no.OwnerClientId == clientId)
                {
                    playerObj = no;
                    return true;
                }
            }

            return false;
        }

        private void SkipBytes(FastBufferReader reader, int count)
        {
            if (count <= 0)
                return;

            EnsureByteBuffer(ref _skipBuffer, count);
            reader.ReadBytesSafe(ref _skipBuffer, count, 0);
        }

        private static void EnsureByteBuffer(ref byte[] buffer, int needed)
        {
            if (buffer != null && buffer.Length >= needed)
                return;

            int newSize = Mathf.NextPowerOfTwo(Mathf.Max(needed, 1024));
            buffer = new byte[newSize];
        }

        private void EnsureFloatBuffer(int neededSamples)
        {
            if (_floatBuffer != null && _floatBuffer.Length >= neededSamples)
                return;

            int newSize = Mathf.NextPowerOfTwo(Mathf.Max(neededSamples, 1024));
            _floatBuffer = new float[newSize];
        }

        private void StopRecordingSafe()
        {
            if (SteamClient.IsValid)
                SteamUser.VoiceRecord = false;
        }

        private Transform ResolveListenerTransform()
        {
            if (Camera.main != null)
                return Camera.main.transform;

            var listener = FindFirstObjectByType<AudioListener>();
            return listener != null ? listener.transform : null;
        }
    }
}