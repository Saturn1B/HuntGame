using System;
using System.Reflection;
using UnityEngine;
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
        [SerializeField] private MonoBehaviour steamBootstrap;
        [SerializeField] private MonoBehaviour steamLobbyNetcode;

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
                Destroy(gameObject);
                return;
            }

            Instance = this;

            ValidateReferences();

            if (config != null && config.dontDestroyOnLoad)
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

            if (steamLobbyNetcode == null)
            {
                Debug.LogError("[NetGameRoot] Missing SteamLobbyNetcode reference.");
                SetState(NetGameState.Offline);
                return;
            }

            _hostRequestInFlight = true;
            SetState(NetGameState.Hosting);

            if (!TryInvokeInstanceMethod(steamLobbyNetcode, "Host"))
            {
                Debug.LogError("[NetGameRoot] Failed to invoke SteamLobbyNetcode.Host().");
                _hostRequestInFlight = false;
                SetState(NetGameState.Offline);
            }
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

        private bool TryInvokeInstanceMethod(MonoBehaviour target, string methodName)
        {
            if (target == null)
                return false;

            try
            {
                var type = target.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var method = type.GetMethod(methodName, flags);
                if (method == null)
                {
                    Debug.LogError($"[NetGameRoot] Method '{methodName}' not found on {type.Name}.");
                    return false;
                }

                method.Invoke(target, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
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
                Debug.LogWarning("[NetGameRoot] NetPlayerRegistry reference is missing (Step 2 requires it).");

            if (identityProviderBehaviour != null && identityProviderBehaviour is not INetIdentityProvider)
                Debug.LogError("[NetGameRoot] IdentityProviderBehaviour does not implement INetIdentityProvider.");

            if (steamBootstrap == null)
                Debug.LogWarning("[NetGameRoot] SteamBootstrap reference is missing (not fatal if Steam is initialized elsewhere).");

            if (steamLobbyNetcode == null)
                Debug.LogError("[NetGameRoot] SteamLobbyNetcode reference is missing (Host will not work).");
        }
    }
}