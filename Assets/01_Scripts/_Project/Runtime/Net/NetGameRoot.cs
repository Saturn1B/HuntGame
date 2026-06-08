using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Netcode.Transports.Facepunch;
using DungeonSteakhouse.Net.Core;
using DungeonSteakhouse.Net.Players;

namespace DungeonSteakhouse.Net
{
    public enum NetGameState
    {
        Offline = 0,
        Hosting = 10,
        Connecting = 20,
        InSession = 30
    }

    public sealed class NetGameRoot : MonoBehaviour
    {
        public static NetGameRoot Instance { get; private set; }

        public event Action<NetGameState> StateChanged;

        [Header("Config")]
        [SerializeField] private NetGameConfig config;

        [Header("Scene References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FacepunchTransport transport;

        [Header("Services")]
        [SerializeField] private MonoBehaviour identityProviderBehaviour; // Must implement INetIdentityProvider
        [SerializeField] private NetPlayerRegistry playerRegistry;

        [Header("Existing Implementation (do not delete)")]
        [SerializeField] private MonoBehaviour steamBootstrapBehaviour;   // Must implement INetBootstrapper
        [SerializeField] private MonoBehaviour steamLobbyNetcodeBehaviour; // Must implement INetLobbyController

        private INetBootstrapper _bootstrapper;
        private INetLobbyController _lobbyController;

        private NetGameState _state = NetGameState.Offline;
        private bool _hostRequestInFlight;

        public NetGameState State => _state;
        public NetGameConfig Config => config;
        public NetPlayerRegistry PlayerRegistry => playerRegistry;

        public INetIdentityProvider IdentityProvider => identityProviderBehaviour as INetIdentityProvider;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[NetGameRoot] Duplicate singleton detected. Destroying new instance.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _bootstrapper    = steamBootstrapBehaviour    as INetBootstrapper;
            _lobbyController = steamLobbyNetcodeBehaviour as INetLobbyController;

            ValidateReferences();

            if (config != null && config.DontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            HookNetcodeCallbacks();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnhookNetcodeCallbacks();
        }

        public void Host()
        {
            if (_hostRequestInFlight)
            {
                Debug.LogWarning("[NetGameRoot] Host request already in flight.");
                return;
            }

            if (networkManager != null && (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient))
            {
                Debug.LogWarning("[NetGameRoot] Already running a network session. Ignoring Host().");
                return;
            }

            if (_lobbyController == null)
            {
                Debug.LogError("[NetGameRoot] Missing SteamLobbyNetcode reference or it does not implement INetLobbyController.");
                SetState(NetGameState.Offline);
                return;
            }

            _hostRequestInFlight = true;
            SetState(NetGameState.Hosting);
            _lobbyController.Host();
        }

        public void Shutdown()
        {
            _hostRequestInFlight = false;

            if (networkManager != null)
            {
                if (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient)
                    networkManager.Shutdown();
            }

            SetState(NetGameState.Offline);
        }

        public string GetDebugStatus()
        {
            if (networkManager == null)
                return "NetworkManager: <missing>";

            return $"State={_state} | " +
                   $"IsHost={networkManager.IsHost} IsServer={networkManager.IsServer} IsClient={networkManager.IsClient} | " +
                   $"Transport={(transport != null ? transport.GetType().Name : "<missing>")}";
        }

        private void HookNetcodeCallbacks()
        {
            if (networkManager == null)
                return;

            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void UnhookNetcodeCallbacks()
        {
            if (networkManager == null)
                return;

            networkManager.OnServerStarted -= OnServerStarted;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnServerStarted()
        {
            _hostRequestInFlight = false;
            SetState(NetGameState.InSession);
            if (config == null || networkManager.SceneManager == null) return;

            networkManager.SceneManager.OnSceneEvent += OnSceneEvent_WaitForElevator;
            networkManager.SceneManager.LoadScene(config.ElevatorSceneName, LoadSceneMode.Additive);
        }

        private void OnSceneEvent_WaitForElevator(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;
            if (sceneEvent.SceneName != config.ElevatorSceneName) return;

            // Elevator chargée → on se désabonne et on charge Taverne
            networkManager.SceneManager.OnSceneEvent -= OnSceneEvent_WaitForElevator;
            networkManager.SceneManager.LoadScene(config.TavernSceneName, LoadSceneMode.Additive);
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[NetGameRoot] Client connected: {clientId}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[NetGameRoot] Client disconnected: {clientId}");

            if (networkManager != null && !networkManager.IsClient && !networkManager.IsServer)
                SetState(NetGameState.Offline);
        }

        private void SetState(NetGameState newState)
        {
            if (_state == newState)
                return;

            _state = newState;
            StateChanged?.Invoke(_state);
        }

        private void ValidateReferences()
        {
            if (config == null)
                Debug.LogWarning("[NetGameRoot] NetGameConfig is not assigned.");

            if (networkManager == null)
                Debug.LogError("[NetGameRoot] NetworkManager reference is missing.");

            if (transport == null)
                Debug.LogError("[NetGameRoot] FacepunchTransport reference is missing.");

            if (playerRegistry == null)
                Debug.LogWarning("[NetGameRoot] NetPlayerRegistry reference is missing.");

            if (identityProviderBehaviour != null && identityProviderBehaviour is not INetIdentityProvider)
                Debug.LogError("[NetGameRoot] IdentityProviderBehaviour does not implement INetIdentityProvider.");

            if (_bootstrapper == null)
                Debug.LogWarning("[NetGameRoot] steamBootstrapBehaviour is missing or does not implement INetBootstrapper.");

            if (_lobbyController == null)
                Debug.LogError("[NetGameRoot] steamLobbyNetcodeBehaviour is missing or does not implement INetLobbyController (Host will not work).");
        }
    }
}
