using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using DungeonSteakhouse.Net;

namespace DungeonSteakhouse.Net.Session
{
    [DisallowMultipleComponent]
    public sealed class NetSessionManager : NetworkBehaviour
    {
        public static NetSessionManager Instance { get; private set; }

        [SerializeField] private NetGameConfig config;
        [SerializeField] private NetworkObject seedBroadcasterPrefab;
        [SerializeField] private ElevatorSequenceController elevatorSequence;
        private NetReadyPlatformGate elevatorGate;
        private NetPlayerTeleporter teleporter;

        [SerializeField] private bool verboseLogs = true;

        private readonly NetworkVariable<NetSessionState> _state = new(
            NetSessionState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetSessionState State => _state.Value;

        public event Action<NetSessionState, NetSessionState> StateChanged;

        private enum FlowStep { None, WaitTavernUnload, WaitDungeonLoad, WaitDungeonUnload, WaitTavernLoad }
        private FlowStep _flowStep;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _state.OnValueChanged += OnStateValueChanged;

            if (NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
                NetworkManager.SceneManager.VerifySceneBeforeLoading = VerifyScene;
            }

            if (!IsServer) return;

            ConfigureSceneManager();
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateValueChanged;

            if (IsServer)
            {
                if (NetworkManager != null)
                {
                    NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                    NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                }

                if (NetworkManager?.SceneManager != null)
                    NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }

            base.OnNetworkDespawn();
        }

        private void ResolveReferences()
        {
            if (elevatorGate == null)
                elevatorGate = FindObjectOfType<NetReadyPlatformGate>();

            if (teleporter == null)
                teleporter = FindObjectOfType<NetPlayerTeleporter>();

            if (elevatorSequence == null)
                elevatorSequence = FindObjectOfType<ElevatorSequenceController>();

            if (verboseLogs)
                Debug.Log($"[NetSessionManager] References resolved — gate={elevatorGate != null} teleporter={teleporter != null} sequence={elevatorSequence != null}");
        }

        private void OnStateValueChanged(NetSessionState previous, NetSessionState current)
        {
            Debug.Log($"[NetSessionManager] {previous} -> {current} (IsServer={IsServer})");
            StateChanged?.Invoke(previous, current);
        }

        // ── Start Run ────────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestStartRunServerRpc(ServerRpcParams rpcParams = default)
        {
            if (State != NetSessionState.Lobby)
            {
                if (verboseLogs) Debug.LogWarning($"[NetSessionManager] StartRun denied: state={State}");
                return;
            }
            if (elevatorGate != null && !elevatorGate.AllReadyConfirmedServer)
            {
                if (verboseLogs) Debug.LogWarning("[NetSessionManager] StartRun denied: elevator gate not confirmed ready.");
                return;
            }

            SetState(NetSessionState.StartingRun);
            elevatorGate?.ServerClearConfirmedReady();

            if (elevatorSequence != null)
                elevatorSequence.StartSequence();
            else
                StartRunAfterSequence();
        }

        /// <summary>Called by ElevatorSequenceController after door close animation.</summary>
        public void StartRunAfterSequence()
        {
            if (!IsServer) return;
            if (!TryGetSceneManager(out var sceneManager)) return;

            var tavernScene = SceneManager.GetSceneByName(config.TavernSceneName);
            if (tavernScene.IsValid() && tavernScene.isLoaded)
            {
                _flowStep = FlowStep.WaitTavernUnload;
                var status = sceneManager.UnloadScene(tavernScene);
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"[NetSessionManager] UnloadScene '{config.TavernSceneName}' failed: {status}");
                    _flowStep = FlowStep.None;
                    SetState(NetSessionState.Lobby);
                }
            }
            else
            {
                LoadDungeon(sceneManager);
            }
        }

        // ── Return to Lobby ──────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void RequestReturnToLobbyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (State != NetSessionState.InRun)
            {
                if (verboseLogs) Debug.LogWarning($"[NetSessionManager] ReturnToLobby denied: state={State}");
                return;
            }

            SetState(NetSessionState.ReturningToLobby);

            if (elevatorSequence != null)
                elevatorSequence.StartReturnSequence();
            else
                StartReturnAfterSequence();
        }

        /// <summary>Called by ElevatorSequenceController after countdown + door close.</summary>
        public void StartReturnAfterSequence()
        {
            if (!IsServer) return;
            if (!TryGetSceneManager(out var sceneManager)) return;

            var dungeonScene = SceneManager.GetSceneByName(config.DungeonSceneName);
            if (dungeonScene.IsValid() && dungeonScene.isLoaded)
            {
                _flowStep = FlowStep.WaitDungeonUnload;
                var status = sceneManager.UnloadScene(dungeonScene);
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"[NetSessionManager] UnloadScene '{config.DungeonSceneName}' failed: {status}");
                    _flowStep = FlowStep.None;
                    SetState(NetSessionState.Lobby);
                }
            }
            else
            {
                LoadTaverne(sceneManager);
            }
        }

        // ── Scene loading ────────────────────────────────────────────────────

        private void LoadDungeon(NetworkSceneManager sceneManager)
        {
            _flowStep = FlowStep.WaitDungeonLoad;
            var status = sceneManager.LoadScene(config.DungeonSceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[NetSessionManager] LoadScene '{config.DungeonSceneName}' failed: {status}");
                _flowStep = FlowStep.None;
                SetState(NetSessionState.Lobby);
            }
        }

        private void LoadTaverne(NetworkSceneManager sceneManager)
        {
            _flowStep = FlowStep.WaitTavernLoad;
            var status = sceneManager.LoadScene(config.TavernSceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[NetSessionManager] LoadScene '{config.TavernSceneName}' failed: {status}");
                _flowStep = FlowStep.None;
                SetState(NetSessionState.Lobby);
            }
        }

        private void SpawnSeedBroadcaster()
        {
            if (seedBroadcasterPrefab == null)
            {
                Debug.LogError("[NetSessionManager] seedBroadcasterPrefab not assigned.");
                return;
            }

            var dungeonScene = SceneManager.GetSceneByName(config.DungeonSceneName);
            var instance = Instantiate(seedBroadcasterPrefab);
            if (dungeonScene.IsValid() && dungeonScene.isLoaded)
                SceneManager.MoveGameObjectToScene(instance.gameObject, dungeonScene);
            instance.Spawn(destroyWithScene: true);
        }

        // ── Scene events ─────────────────────────────────────────────────────

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (!IsServer) return;

            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                sceneEvent.ClientId == NetworkManager.ServerClientId &&
                (sceneEvent.SceneName == config.ElevatorSceneName || sceneEvent.SceneName == config.TavernSceneName))
            {
                ResolveReferences();
            }

            switch (_flowStep)
            {
                case FlowStep.WaitTavernUnload:
                    if (sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted &&
                        sceneEvent.ClientId == NetworkManager.ServerClientId &&
                        sceneEvent.SceneName == config.TavernSceneName)
                    {
                        if (!TryGetSceneManager(out var sm)) { _flowStep = FlowStep.None; return; }
                        LoadDungeon(sm);
                    }
                    break;

                case FlowStep.WaitDungeonLoad:
                    if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                        sceneEvent.ClientId == NetworkManager.ServerClientId &&
                        sceneEvent.SceneName == config.DungeonSceneName &&
                        sceneEvent.ClientsThatCompleted?.Count == NetworkManager.ConnectedClients.Count)
                    {
                        _flowStep = FlowStep.None;
                        SpawnSeedBroadcaster();
                        SetState(NetSessionState.InRun);
                        elevatorSequence?.OpenDoors();
                        teleporter?.TeleportAllPlayers(NetSpawnContext.Run);
                    }
                    break;

                case FlowStep.WaitDungeonUnload:
                    if (sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted &&
                        sceneEvent.ClientId == NetworkManager.ServerClientId &&
                        sceneEvent.SceneName == config.DungeonSceneName)
                    {
                        if (!TryGetSceneManager(out var sm)) { _flowStep = FlowStep.None; return; }
                        LoadTaverne(sm);
                    }
                    break;

                case FlowStep.WaitTavernLoad:
                    if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                        sceneEvent.ClientId == NetworkManager.ServerClientId &&
                        sceneEvent.SceneName == config.TavernSceneName)
                    {
                        _flowStep = FlowStep.None;
                        SetState(NetSessionState.Lobby);
                        elevatorSequence?.OpenDoors();
                        HandleReturnTeleports();
                    }
                    break;
            }
        }

        private void HandleReturnTeleports()
        {
            var onPlatform = elevatorGate?.GetPlayersOnPlatform();
            var onPlatformSet = onPlatform != null ? new HashSet<ulong>(onPlatform) : null;

            var playersInElevator = new List<ulong>();
            var playersOutside = new List<ulong>();

            foreach (var kvp in NetworkManager.ConnectedClients)
            {
                ulong clientId = kvp.Key;
                if (onPlatformSet != null && onPlatformSet.Contains(clientId))
                    playersInElevator.Add(clientId);
                else
                    playersOutside.Add(clientId);
            }

            if (verboseLogs)
            {
                Debug.Log($"[NetSessionManager] Return — in elevator: [{string.Join(", ", playersInElevator)}]");
                Debug.Log($"[NetSessionManager] Return — outside:     [{string.Join(", ", playersOutside)}]");
            }

            foreach (ulong clientId in playersInElevator)
                teleporter?.TeleportClient(clientId, NetSpawnContext.Lobby);

            foreach (ulong clientId in playersOutside)
                teleporter?.TeleportClient(clientId, NetSpawnContext.Lobby);
        }

        private void OnClientConnected(ulong clientId)
        {
            teleporter?.DeferredTeleportClient(clientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (verboseLogs)
                Debug.LogWarning($"[NetSessionManager] Client disconnected: clientId={clientId} (State={State}, FlowStep={_flowStep}).");

            // Clean up any per-client tracking that would otherwise leak (OnTriggerExit never
            // fires for a destroyed player object) or wrongly keep counting a disconnected client.
            elevatorGate?.ServerRemoveClient(clientId);
        }

        private void ConfigureSceneManager()
        {
            var sceneManager = NetworkManager?.SceneManager;
            if (sceneManager == null) return;

            sceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            sceneManager.ActiveSceneSynchronizationEnabled = config != null && config.SyncActiveScene;
            sceneManager.OnSceneEvent += OnSceneEvent;

            if (verboseLogs)
                Debug.Log("[NetSessionManager] Configured.");
        }

        private bool VerifyScene(int sceneIndex, string sceneName, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) return false;
            if (config == null) return false;

            return sceneName == config.TavernSceneName
                || sceneName == config.DungeonSceneName
                || sceneName == config.ElevatorSceneName;
        }

        private void SetState(NetSessionState newState)
        {
            if (_state.Value == newState) return;
            _state.Value = newState;
        }

        private bool TryGetSceneManager(out NetworkSceneManager sceneManager)
        {
            sceneManager = NetworkManager?.SceneManager;
            if (sceneManager != null) return true;
            Debug.LogError("[NetSessionManager] NetworkSceneManager not available.");
            return false;
        }
    }
}