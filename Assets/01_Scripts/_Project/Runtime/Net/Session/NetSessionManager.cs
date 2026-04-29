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
        [SerializeField] private NetReadyPlatformGate elevatorGate;  // assign in inspector
        [SerializeField] private NetPlayerTeleporter teleporter;      // assign in inspector
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

        private enum FlowStep { None, WaitDungeonLoad, WaitTavernUnload, WaitTavernLoad, WaitDungeonUnload }
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
                NetworkManager.SceneManager.SetClientSynchronizationMode(
                    LoadSceneMode.Additive);

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

        private void OnStateValueChanged(NetSessionState previous, NetSessionState current)
        {
            if (verboseLogs)
                Debug.Log($"[NetSessionManager] State: {previous} -> {current}");
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

            if (_returnCountdownRemaining.Value > 0f) return;

            _countdownRoutine = StartCoroutine(ReturnCountdownRoutine());
        }

        private void StartRunServer()
        {
            SetState(NetSessionState.StartingRun);
            elevatorGate?.ServerClearConfirmedReady();

            if (!TryGetSceneManager(out var sceneManager)) return;

            _flowStep = FlowStep.WaitDungeonLoad;
            var status = sceneManager.LoadScene(config.DungeonSceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[NetSessionManager] LoadScene '{config.DungeonSceneName}' failed: {status}");
                _flowStep = FlowStep.None;
                SetState(NetSessionState.Lobby);
            }
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
            SetState(NetSessionState.ReturningToLobby);
            StartReturnSequence();
        }

        private void StartReturnSequence()
        {
            if (!TryGetSceneManager(out var sceneManager)) return;

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

        // One-frame delay lets NGO register the Dungeon scene handle before Tavern is unloaded.
        private IEnumerator UnloadTavernNextFrame(Scene tavernScene)
        {
            yield return null;
            NetworkManager.SceneManager.UnloadScene(tavernScene);
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (!IsServer) return;
            if (sceneEvent.ClientId != NetworkManager.ServerClientId) return;

            switch (_flowStep)
            {
                case FlowStep.WaitDungeonLoad:
                    if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                        sceneEvent.SceneName == config.DungeonSceneName)
                    {
                        var tavernScene = SceneManager.GetSceneByName(config.TavernSceneName);
                        if (tavernScene.IsValid() && tavernScene.isLoaded)
                        {
                            _flowStep = FlowStep.WaitTavernUnload;
                            StartCoroutine(UnloadTavernNextFrame(tavernScene));
                        }
                        else
                        {
                            _flowStep = FlowStep.None;
                            SetState(NetSessionState.InRun);
                            teleporter?.TeleportAllPlayers(NetSpawnContext.Run);
                        }
                    }
                    break;

                case FlowStep.WaitTavernUnload:
                    if (sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted &&
                        sceneEvent.SceneName == config.TavernSceneName)
                    {
                        _flowStep = FlowStep.None;
                        SetState(NetSessionState.InRun);
                        teleporter?.TeleportAllPlayers(NetSpawnContext.Run);
                    }
                    break;

                case FlowStep.WaitTavernLoad:
                    if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                        sceneEvent.SceneName == config.TavernSceneName)
                    {
                        var dungeonScene = SceneManager.GetSceneByName(config.DungeonSceneName);
                        if (dungeonScene.IsValid() && dungeonScene.isLoaded)
                        {
                            _flowStep = FlowStep.WaitDungeonUnload;
                            NetworkManager.SceneManager.UnloadScene(dungeonScene);
                        }
                        else
                        {
                            _flowStep = FlowStep.None;
                            SetState(NetSessionState.Lobby);
                            teleporter?.TeleportAllPlayers(NetSpawnContext.Lobby);
                        }
                    }
                    break;

                case FlowStep.WaitDungeonUnload:
                    if (sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted &&
                        sceneEvent.SceneName == config.DungeonSceneName)
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
            // Convert to HashSet for O(1) lookup — avoids CS7036 ambiguous Contains() overload.
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
            // For now: teleport them to Lobby spawn as fallback.
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
            sceneManager.VerifySceneBeforeLoading = VerifyScene;
            sceneManager.OnSceneEvent += OnSceneEvent;

            if (verboseLogs)
                Debug.Log($"[NetSessionManager] Configured. ActiveSceneSync={sceneManager.ActiveSceneSynchronizationEnabled}");
        }

        private bool VerifyScene(int sceneIndex, string sceneName, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) return false;
            if (config == null)
            {
                Debug.LogError("[NetSessionManager] VerifyScene: config is NULL — scene blocked!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                return false;
            }
            bool allowed = sceneName == config.TavernSceneName || sceneName == config.DungeonSceneName;
            Debug.Log($"[NetSessionManager] VerifyScene: '{sceneName}' -> {allowed}");
            return allowed;
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