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
        private NetReadyPlatformGate elevatorGate;
        private NetPlayerTeleporter teleporter;

        [SerializeField] private float returnToLobbyCountdown = 10f;
        [SerializeField] private bool verboseLogs = true;

        private readonly NetworkVariable<NetSessionState> _state = new(
            NetSessionState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _returnCountdownRemaining = new(
            -1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetSessionState State => _state.Value;
        public float ReturnCountdownRemaining => _returnCountdownRemaining.Value;

        public event Action<NetSessionState, NetSessionState> StateChanged;

        // WaitTavernUnload  → start-run:    taverne se décharge, puis LoadDungeon
        // WaitDungeonLoad   → start-run:    donjon se charge,    puis InRun + téléport
        // WaitDungeonUnload → return-lobby: donjon se décharge,  puis LoadTaverne
        // WaitTavernLoad    → return-lobby: taverne se charge,   puis Lobby + téléport
        private enum FlowStep { None, WaitTavernUnload, WaitDungeonLoad, WaitDungeonUnload, WaitTavernLoad }
        private FlowStep _flowStep;
        private Coroutine _countdownRoutine;

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
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateValueChanged;

            if (IsServer)
            {
                if (NetworkManager != null)
                    NetworkManager.OnClientConnectedCallback -= OnClientConnected;

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

            if (verboseLogs)
                Debug.Log($"[NetSessionManager] References resolved — gate={elevatorGate != null} teleporter={teleporter != null}");
        }

        private void OnStateValueChanged(NetSessionState previous, NetSessionState current)
        {
            Debug.Log($"[NetSessionManager] {previous} -> {current} (IsServer={IsServer})");
            StateChanged?.Invoke(previous, current);
        }

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

            StartRunServer();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestReturnToLobbyServerRpc(ServerRpcParams rpcParams = default)
        {
            if (State != NetSessionState.InRun)
            {
                if (verboseLogs) Debug.LogWarning($"[NetSessionManager] ReturnToLobby denied: state={State}");
                return;
            }

            if (_countdownRoutine != null) return;

            _countdownRoutine = StartCoroutine(ReturnCountdownRoutine());
        }

        private void StartRunServer()
        {
            SetState(NetSessionState.StartingRun);
            elevatorGate?.ServerClearConfirmedReady();

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

        private IEnumerator ReturnCountdownRoutine()
        {
            float remaining = returnToLobbyCountdown;
            while (remaining > 0f)
            {
                _returnCountdownRemaining.Value = remaining;
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }

            _returnCountdownRemaining.Value = -1f;
            _countdownRoutine = null;
            SetState(NetSessionState.ReturningToLobby);
            StartReturnSequence();
        }

        private void StartReturnSequence()
        {
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
                    teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);
                }
            }
            else
            {
                LoadTaverne(sceneManager);
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
                teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);
            }
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (!IsServer) return;

            // Résolution des références quand Elevator ou Taverne se charge (serveur uniquement)
            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                sceneEvent.ClientId == NetworkManager.ServerClientId &&
                (sceneEvent.SceneName == config.ElevatorSceneName || sceneEvent.SceneName == config.TavernSceneName))
            {
                ResolveReferences();
            }

            switch (_flowStep)
            {
                case FlowStep.WaitTavernUnload:
                    // UnloadEventCompleted n'a pas de ClientId pertinent, on filtre sur le serveur
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
                        sceneEvent.SceneName == config.DungeonSceneName &&
                        sceneEvent.ClientsThatCompleted?.Count == NetworkManager.ConnectedClients.Count)
                    {
                        _flowStep = FlowStep.None;
                        SpawnSeedBroadcaster();
                        SetState(NetSessionState.InRun);
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

            // TODO: Handle players outside elevator (death screen, loot loss, respawn, etc.)
            foreach (ulong clientId in playersOutside)
                teleporter?.TeleportClient(clientId, NetSpawnContext.Lobby);
        }

        private void OnClientConnected(ulong clientId)
        {
            teleporter?.DeferredTeleportClient(clientId);
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