using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;
using DungeonSteakhouse.Net;
using DungeonSteakhouse.Net.Connection;

namespace DungeonSteakhouse.Net.Steam
{
    public sealed class SteamLobbyNetcode : MonoBehaviour
    {
        [Header("Config (single source of truth)")]
        [SerializeField] private NetGameConfig config;

        [Header("Fallback values (used only if Config is missing)")]
        [SerializeField] private int fallbackMaxMembers = 4;
        [SerializeField] private string fallbackBuildVersion = "0.1.0-dev";

        [Header("Netcode")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FacepunchTransport transport;

        private Lobby? _currentLobby;
        private bool _isCreatingLobby;

        // Shared session identifier (stable across all players in the Steam lobby)
        private string _sessionId;

        public bool TryGetSessionId(out string sessionId)
        {
            sessionId = _sessionId;
            return !string.IsNullOrWhiteSpace(sessionId);
        }

        private void Awake()
        {
            ResolveConfigIfMissing();
            ValidateReferences();
        }

        private void OnEnable()
        {
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        }

        private void OnDisable()
        {
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        }

        public void Host()
        {
            _ = HostAsync();
        }

        private async Task HostAsync()
        {
            if (!SteamBootstrap.Ready)
            {
                Debug.LogError("[SteamLobbyNetcode] Steam is not ready.");
                return;
            }

            if (networkManager == null || transport == null)
            {
                Debug.LogError("[SteamLobbyNetcode] Missing Netcode references.");
                return;
            }

            if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
            {
                Debug.LogWarning("[SteamLobbyNetcode] Network session already running.");
                return;
            }

            _isCreatingLobby = true;

            Lobby? lobby = null;
            try
            {
                lobby = await SteamMatchmaking.CreateLobbyAsync(GetMaxPlayers());
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (lobby == null)
            {
                Debug.LogError("[SteamLobbyNetcode] CreateLobbyAsync failed.");
                _isCreatingLobby = false;
                return;
            }

            _currentLobby = lobby.Value;
            _sessionId = _currentLobby.Value.Id.ToString();

            _currentLobby.Value.SetJoinable(true);
            _currentLobby.Value.SetFriendsOnly();
            _currentLobby.Value.SetData("name", $"{SteamClient.Name}'s Lobby");
            _currentLobby.Value.SetData("buildVersion", GetBuildVersion());

            ApplyConnectionPayload();

            var ok = networkManager.StartHost();
            if (!ok)
            {
                Debug.LogError("[SteamLobbyNetcode] NetworkManager.StartHost() failed.");
                _currentLobby.Value.Leave();
                _currentLobby = null;
                _sessionId = null;
            }

            _isCreatingLobby = false;
        }

        private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friend)
        {
            _ = JoinLobbyAsync(lobby.Id);
        }

        private async Task JoinLobbyAsync(SteamId lobbyId)
        {
            if (!SteamBootstrap.Ready)
            {
                Debug.LogError("[SteamLobbyNetcode] Steam is not ready.");
                return;
            }

            if (networkManager == null || transport == null)
            {
                Debug.LogError("[SteamLobbyNetcode] Missing Netcode references.");
                return;
            }

            if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
            {
                Debug.LogWarning("[SteamLobbyNetcode] Already running a network session. Shutdown before joining a lobby.");
                return;
            }

            try
            {
                await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            _currentLobby = lobby;
            _sessionId = lobby.Id.ToString();

            // Host also enters its own lobby
            if (_isCreatingLobby || (networkManager != null && networkManager.IsHost))
                return;

            var lobbyVersion = lobby.GetData("buildVersion");
            var localVersion = GetBuildVersion();

            if (!string.IsNullOrEmpty(lobbyVersion) && lobbyVersion != localVersion)
            {
                Debug.LogError($"[SteamLobbyNetcode] Build version mismatch. Lobby={lobbyVersion} Local={localVersion}");
                lobby.Leave();
                _currentLobby = null;
                _sessionId = null;
                return;
            }

            var hostSteamId = lobby.Owner.Id;
            transport.targetSteamId = hostSteamId;

            ApplyConnectionPayload();

            var ok = networkManager.StartClient();
            if (!ok)
            {
                Debug.LogError("[SteamLobbyNetcode] NetworkManager.StartClient() failed.");
                lobby.Leave();
                _currentLobby = null;
                _sessionId = null;
            }
        }

        public void LeaveLobby()
        {
            if (_currentLobby != null)
            {
                _currentLobby.Value.Leave();
                _currentLobby = null;
            }

            _sessionId = null;
        }

        private void ApplyConnectionPayload()
        {
            if (networkManager == null || networkManager.NetworkConfig == null)
                return;

            var payload = new NetConnectionPayload
            {
                buildVersion = GetBuildVersion(),
                platformUserId = (ulong)SteamClient.SteamId,
                displayName = SteamClient.Name
            };

            networkManager.NetworkConfig.ConnectionData = NetConnectionPayloadCodec.Encode(payload);
        }

        private int GetMaxPlayers()
        {
            if (config != null && config.maxPlayers > 0)
                return config.maxPlayers;

            return Mathf.Max(1, fallbackMaxMembers);
        }

        private string GetBuildVersion()
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.buildVersion))
                return config.buildVersion;

            return fallbackBuildVersion;
        }

        private void ResolveConfigIfMissing()
        {
            if (config != null)
                return;

            var root = NetGameRoot.Instance != null ? NetGameRoot.Instance : FindFirstObjectByType<NetGameRoot>();
            if (root != null && root.Config != null)
                config = root.Config;
        }

        private void ValidateReferences()
        {
            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (networkManager != null && transport == null)
                transport = networkManager.GetComponent<FacepunchTransport>();

            if (networkManager == null)
                Debug.LogError("[SteamLobbyNetcode] NetworkManager is missing.");

            if (transport == null)
                Debug.LogError("[SteamLobbyNetcode] FacepunchTransport is missing.");

            if (config == null)
                Debug.LogWarning("[SteamLobbyNetcode] NetGameConfig is not assigned (fallback values will be used).");
        }
    }
}