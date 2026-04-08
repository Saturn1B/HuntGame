using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using DungeonSteakhouse.Net;

namespace DungeonSteakhouse.Net.Session
{
    public enum NetSessionPhase : byte
    {
        Lobby = 0,
        LoadingRun = 1,
        InRun = 2,
        ReturningToLobby = 3
    }

    [DisallowMultipleComponent]
    public sealed class NetGameSession : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] private NetGameConfig config;

        [Header("Spawning")]
        [SerializeField] private NetPlayerSpawnResolver spawnResolver;
        [SerializeField] private NetPlayerTeleporter teleporter;

        [Header("Authority")]
        [Tooltip("If true, only the host/server can start a run or return to lobby.")]
        [SerializeField] private bool onlyHostCanControlSession = true;

        [Header("Scene Management")]
        [Tooltip("If true, use NetworkSceneManager to load/unload scenes (Additive).")]
        [SerializeField] private bool useNetworkSceneManagement = true;

        [Header("Spawning Options")]
        [Tooltip("If true, the server will teleport PlayerObjects to NetSpawnPointGroup locations on connect and on phase changes.")]
        [SerializeField] private bool useSpawnPointGroups = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

        private readonly NetworkVariable<NetSessionPhase> _phase =
            new NetworkVariable<NetSessionPhase>(
                NetSessionPhase.Lobby,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private bool _sceneEventsHooked;
        private bool _sceneOpInProgress;

        public NetSessionPhase Phase => _phase.Value;

        public event Action<NetSessionPhase, NetSessionPhase> PhaseChanged;

        private string LobbySceneName => config != null ? config.TavernSceneName : "Tavern";
        private string RunSceneName   => config != null ? config.DungeonSceneName : "Dungeon";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _phase.OnValueChanged += OnPhaseChangedInternal;
            OnPhaseChangedInternal(_phase.Value, _phase.Value);

            if (IsServer)
            {
                SafeSetPhaseServer(NetSessionPhase.Lobby);

                if (NetworkManager != null)
                    NetworkManager.OnClientConnectedCallback += OnClientConnectedServer;

                HookSceneEventsIfNeeded();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientConnectedCallback -= OnClientConnectedServer;

            UnhookSceneEventsIfNeeded();

            _phase.OnValueChanged -= OnPhaseChangedInternal;

            base.OnNetworkDespawn();
        }

        private void OnPhaseChangedInternal(NetSessionPhase previous, NetSessionPhase current)
        {
            if (verboseLogs)
                Debug.Log($"[NetGameSession] Phase changed: {previous} -> {current}");

            teleporter?.SetCurrentPhase(current);
            PhaseChanged?.Invoke(previous, current);
        }

        public void RequestStartRun(ulong senderClientId)
        {
            // TODO: your server logic
        }

        public void ReturnToLobby(ulong senderClientId)
        {
            // TODO: your server logic
        }

        // -------------------------
        // Backward-compatible API expected by ElevatorButtonInteractable
        // -------------------------

        public bool CanStartRun()
        {
            ulong requester = GetLocalClientIdSafe();
            return CanStartRun(requester);
        }

        public bool CanStartRun(ulong requesterClientId)
        {
            if (NetworkManager == null || !NetworkManager.IsListening) return false;
            if (_sceneOpInProgress) return false;
            if (Phase != NetSessionPhase.Lobby) return false;

            if (onlyHostCanControlSession && requesterClientId != NetworkManager.ServerClientId)
                return false;

            return true;
        }

        public bool CanReturnToLobby()
        {
            ulong requester = GetLocalClientIdSafe();
            return CanReturnToLobby(requester);
        }

        public bool CanReturnToLobby(ulong requesterClientId)
        {
            if (NetworkManager == null || !NetworkManager.IsListening) return false;
            if (_sceneOpInProgress) return false;
            if (Phase != NetSessionPhase.InRun) return false;

            if (onlyHostCanControlSession && requesterClientId != NetworkManager.ServerClientId)
                return false;

            return true;
        }

        public bool ServerTryStartRun()
        {
            if (IsServer)
            {
                if (!CanStartRun(NetworkManager.ServerClientId))
                    return false;

                StartRunServer();
                return true;
            }

            RequestStartRun();
            return false;
        }

        public bool ServerTryReturnToLobby()
        {
            if (IsServer)
            {
                if (!CanReturnToLobby(NetworkManager.ServerClientId))
                    return false;

                ReturnToLobbyServer();
                return true;
            }

            RequestReturnToLobby();
            return false;
        }

        public void RequestStartRun()
        {
            if (!IsClient || NetworkManager == null || !NetworkManager.IsConnectedClient)
            {
                if (verboseLogs) Debug.LogWarning("[NetGameSession] RequestStartRun ignored (not connected as client).");
                return;
            }

            RequestStartRunServerRpc();
        }

        public void RequestReturnToLobby()
        {
            if (!IsClient || NetworkManager == null || !NetworkManager.IsConnectedClient)
            {
                if (verboseLogs) Debug.LogWarning("[NetGameSession] RequestReturnToLobby ignored (not connected as client).");
                return;
            }

            RequestReturnToLobbyServerRpc();
        }

        public void ReturnToLobby()
        {
            RequestReturnToLobby();
        }

#pragma warning disable CS0618
        [ServerRpc(RequireOwnership = false)]
#pragma warning restore CS0618
        private void RequestStartRunServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            if (!CanStartRun(rpcParams.Receive.SenderClientId))
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetGameSession] StartRun denied for sender={rpcParams.Receive.SenderClientId} phase={Phase} inProgress={_sceneOpInProgress}");
                return;
            }

            StartRunServer();
        }

#pragma warning disable CS0618
        [ServerRpc(RequireOwnership = false)]
#pragma warning restore CS0618
        private void RequestReturnToLobbyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            if (!CanReturnToLobby(rpcParams.Receive.SenderClientId))
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetGameSession] ReturnToLobby denied for sender={rpcParams.Receive.SenderClientId} phase={Phase} inProgress={_sceneOpInProgress}");
                return;
            }

            ReturnToLobbyServer();
        }

        // -------------------------
        // Server flow
        // -------------------------

        private void StartRunServer()
        {
            SafeSetPhaseServer(NetSessionPhase.LoadingRun);

            if (!useNetworkSceneManagement)
            {
                SafeSetPhaseServer(NetSessionPhase.InRun);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Run);
                return;
            }

            if (!TryGetSceneManager(out var sceneManager))
            {
                SafeSetPhaseServer(NetSessionPhase.InRun);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Run);
                return;
            }

            var runScene = SceneManager.GetSceneByName(RunSceneName);
            if (runScene.IsValid() && runScene.isLoaded)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetGameSession] Run scene '{RunSceneName}' is already loaded. Forcing InRun + teleport.");

                SafeSetPhaseServer(NetSessionPhase.InRun);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Run);
                return;
            }

            _sceneOpInProgress = true;

            if (verboseLogs)
                Debug.Log($"[NetGameSession] Loading run scene (Additive): '{RunSceneName}'");

            var status = sceneManager.LoadScene(RunSceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                _sceneOpInProgress = false;
                Debug.LogWarning($"[NetGameSession] LoadScene '{RunSceneName}' failed with status={status}.");
                SafeSetPhaseServer(NetSessionPhase.InRun);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Run);
            }
        }

        private void ReturnToLobbyServer()
        {
            SafeSetPhaseServer(NetSessionPhase.ReturningToLobby);

            if (!useNetworkSceneManagement)
            {
                SafeSetPhaseServer(NetSessionPhase.Lobby);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);
                return;
            }

            if (!TryGetSceneManager(out var sceneManager))
            {
                SafeSetPhaseServer(NetSessionPhase.Lobby);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);
                return;
            }

            var runScene = SceneManager.GetSceneByName(RunSceneName);
            if (!runScene.IsValid() || !runScene.isLoaded)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[NetGameSession] Run scene '{RunSceneName}' is not loaded. Forcing Lobby + teleport.");

                SafeSetPhaseServer(NetSessionPhase.Lobby);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);
                return;
            }

            _sceneOpInProgress = true;

            if (verboseLogs)
                Debug.Log($"[NetGameSession] Unloading run scene: '{RunSceneName}'");

            var status = sceneManager.UnloadScene(runScene);
            if (status != SceneEventProgressStatus.Started)
            {
                _sceneOpInProgress = false;
                Debug.LogWarning($"[NetGameSession] UnloadScene '{RunSceneName}' failed with status={status}.");
                SafeSetPhaseServer(NetSessionPhase.Lobby);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);
            }
        }

        // -------------------------
        // Scene events
        // -------------------------

        private void HookSceneEventsIfNeeded()
        {
            if (_sceneEventsHooked) return;
            if (!useNetworkSceneManagement) return;
            if (NetworkManager == null || NetworkManager.SceneManager == null) return;

            NetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

            if (config != null)
                NetworkManager.SceneManager.ActiveSceneSynchronizationEnabled = config.SyncActiveScene;

            NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            _sceneEventsHooked = true;

            if (verboseLogs)
                Debug.Log("[NetGameSession] Hooked NetworkSceneManager.OnSceneEvent.");
        }

        private void UnhookSceneEventsIfNeeded()
        {
            if (!_sceneEventsHooked) return;

            if (NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;

            _sceneEventsHooked = false;

            if (verboseLogs)
                Debug.Log("[NetGameSession] Unhooked NetworkSceneManager.OnSceneEvent.");
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (!IsServer) return;
            if (NetworkManager == null || sceneEvent.ClientId != NetworkManager.ServerClientId) return;
            if (sceneEvent.SceneName != RunSceneName) return;

            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                _sceneOpInProgress = false;
                SafeSetPhaseServer(NetSessionPhase.InRun);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Run);

                if (verboseLogs)
                    Debug.Log($"[NetGameSession] LoadEventCompleted for '{RunSceneName}' -> InRun + teleport Run.");
            }

            if (sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted)
            {
                _sceneOpInProgress = false;
                SafeSetPhaseServer(NetSessionPhase.Lobby);
                if (useSpawnPointGroups) teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);

                if (verboseLogs)
                    Debug.Log($"[NetGameSession] UnloadEventCompleted for '{RunSceneName}' -> Lobby + teleport Lobby.");
            }
        }

        // -------------------------
        // Spawn handling — deferred to teleporter
        // -------------------------

        private void OnClientConnectedServer(ulong clientId)
        {
            if (!IsServer || !useSpawnPointGroups) return;
            teleporter?.DeferredTeleportClient(clientId);
        }

        // -------------------------
        // Helpers
        // -------------------------

        private bool TryGetSceneManager(out NetworkSceneManager sceneManager)
        {
            sceneManager = null;

            if (NetworkManager == null)
            {
                Debug.LogError("[NetGameSession] NetworkManager is null.");
                return false;
            }

            sceneManager = NetworkManager.SceneManager;
            if (sceneManager == null)
            {
                Debug.LogError("[NetGameSession] NetworkSceneManager is null. Is Scene Management enabled on NetworkManager?");
                return false;
            }

            return true;
        }

        private void SafeSetPhaseServer(NetSessionPhase newPhase)
        {
            if (!IsServer) return;
            if (_phase.Value == newPhase) return;
            _phase.Value = newPhase;
        }

        private ulong GetLocalClientIdSafe()
        {
            if (NetworkManager == null) return 0;
            if (!NetworkManager.IsConnectedClient) return 0;
            return NetworkManager.LocalClientId;
        }
    }
}
