using System;
using UnityEngine;
using Steamworks;

namespace DungeonSteakhouse.Net.Steam
{
    public sealed class SteamInviteService : MonoBehaviour
    {
        // Channel 99: safe because FacepunchTransport uses SteamNetworkingSockets (relay sockets),
        // which is a completely separate API from SteamNetworking (legacy P2P used here).
        private const int InviteChannel = 99;

        [SerializeField] private SteamLobbyNetcode lobbyNetcode;

        public event Action<SteamId, SteamId> InviteReceived;
        public static SteamInviteService Instance { get; private set; }


        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
        }

        private void OnDisable()
        {
            SteamNetworking.OnP2PSessionRequest -= OnP2PSessionRequest;
        }

        private void Update()
        {
            if (!SteamBootstrap.Ready)
                return;

            // Drain all pending invite packets this frame.
            while (SteamNetworking.IsP2PPacketAvailable(InviteChannel))
            {
                var packet = SteamNetworking.ReadP2PPacket(InviteChannel);
                if (!packet.HasValue)
                    break;

                HandlePacket(packet.Value.SteamId, packet.Value.Data);
            }
        }

        /// <summary>
        /// Sends a lobby invite to a friend via SteamNetworking P2P.
        /// Requires an active hosted lobby on lobbyNetcode.
        /// </summary>
        public void SendInvite(SteamId friendId)
        {
            if (!SteamBootstrap.Ready)
            {
                Debug.LogWarning("[SteamInviteService] Steam not ready, cannot send invite.");
                return;
            }

            if (lobbyNetcode == null || !lobbyNetcode.TryGetSessionId(out string sessionId))
            {
                Debug.LogWarning("[SteamInviteService] No active lobby session to invite into.");
                return;
            }

            if (!ulong.TryParse(sessionId, out ulong lobbyRaw))
            {
                Debug.LogError($"[SteamInviteService] Failed to parse sessionId '{sessionId}' as ulong.");
                return;
            }

            // Payload: 8 bytes = lobby SteamId as little-endian ulong.
            byte[] payload = BitConverter.GetBytes(lobbyRaw);
            bool ok = SteamNetworking.SendP2PPacket(friendId, payload, -1, InviteChannel, P2PSend.Reliable);

            Debug.Log($"[SteamInviteService] Invite sent to {friendId} (lobby={lobbyRaw}, delivered={ok})");
        }

        private void HandlePacket(SteamId from, byte[] data)
        {
            if (data == null || data.Length < 8)
            {
                Debug.LogWarning($"[SteamInviteService] Malformed invite packet from {from} (length={data?.Length}).");
                return;
            }

            SteamId lobbyId = BitConverter.ToUInt64(data, 0);
            Debug.Log($"[SteamInviteService] Invite received from {from} for lobby {lobbyId}");
            InviteReceived?.Invoke(from, lobbyId);
        }

        private void OnP2PSessionRequest(SteamId requester)
        {
            // Auto-accept all incoming P2P sessions so invite packets are not dropped.
            // Payload validation in HandlePacket acts as the filter.
            SteamNetworking.AcceptP2PSessionWithUser(requester);
            Debug.Log($"[SteamInviteService] P2P session accepted from {requester}");
        }
    }
}
