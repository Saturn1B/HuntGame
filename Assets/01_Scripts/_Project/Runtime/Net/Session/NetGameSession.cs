using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// Backward-compatible session phase enum used by existing scripts (ElevatorButtonInteractable, etc.).
/// </summary>
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
    public static NetGameSession Instance { get; private set; }

    [Header("Scene Flow (Optional)")]
    [Tooltip("If true, the session will use Netcode scene management to move between Lobby and Run scenes.")]
    [SerializeField] private bool useNetworkSceneManagement = true;

    [Tooltip("Lobby scene name (must be in Build Settings).")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Tooltip("Run scene name (must be in Build Settings).")]
    [SerializeField] private string runSceneName = "Run";

    [Header("Authority")]
    [Tooltip("If true, only the host/server can start a run or return to lobby.")]
    [SerializeField] private bool onlyHostCanControlSession = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private readonly NetworkVariable<NetSessionPhase> _phase =
        new NetworkVariable<NetSessionPhase>(
            NetSessionPhase.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private bool _subscribedToSceneEvents;
    private bool _isSceneOperationInProgress;

    public NetSessionPhase Phase => _phase.Value;

    /// <summary>
    /// Optional event for UI/tools.
    /// </summary>
    public event Action<NetSessionPhase, NetSessionPhase> PhaseChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _phase.OnValueChanged += OnPhaseChangedInternal;

        // Force an initial local "sync" callback so UI updates immediately on join.
        OnPhaseChangedInternal(_phase.Value, _phase.Value);

        if (IsServer)
        {
            SafeSetPhaseServer(NetSessionPhase.Lobby);
            SubscribeToSceneEventsIfNeeded();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        _phase.OnValueChanged -= OnPhaseChangedInternal;

        if (IsServer)
        {
            UnsubscribeFromSceneEventsIfNeeded();
        }
    }

    private void OnPhaseChangedInternal(NetSessionPhase previous, NetSessionPhase current)
    {
        if (verboseLogs)
        {
            Debug.Log($"[NetGameSession] Phase changed: {previous} -> {current}");
        }

        PhaseChanged?.Invoke(previous, current);
    }

    // ---------------------------------------------------------------------
    // Backward-compatible API expected by your existing scripts
    // ---------------------------------------------------------------------

    /// <summary>
    /// Used by existing UI/scripts (e.g. NetLobbyRosterTMP).
    /// </summary>
    public bool CanStartRun()
    {
        ulong requester = GetLocalClientIdSafe();
        return CanStartRun(requester);
    }

    /// <summary>
    /// Overload in case a script passes a clientId.
    /// </summary>
    public bool CanStartRun(ulong requesterClientId)
    {
        if (NetworkManager == null || !NetworkManager.IsListening) return false;
        if (_isSceneOperationInProgress) return false;
        if (Phase != NetSessionPhase.Lobby) return false;

        if (onlyHostCanControlSession)
        {
            if (requesterClientId != NetworkManager.ServerClientId)
                return false;
        }

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
        if (_isSceneOperationInProgress) return false;
        if (Phase != NetSessionPhase.InRun) return false;

        if (onlyHostCanControlSession)
        {
            if (requesterClientId != NetworkManager.ServerClientId)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Old name expected by ElevatorButtonInteractable.
    /// Works both when called on server (exec immediately) or on a client (sends request).
    /// </summary>
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

    /// <summary>
    /// Old name expected by ElevatorButtonInteractable.
    /// Works both when called on server (exec immediately) or on a client (sends request).
    /// </summary>
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

    // ---------------------------------------------------------------------
    // Newer request-style API (you can use these anywhere)
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Server RPCs
    // NOTE: Your Netcode version warns about RequireOwnership being deprecated.
    // We keep it for compatibility and silence the warning explicitly.
    // ---------------------------------------------------------------------

#pragma warning disable CS0618
    [ServerRpc(RequireOwnership = false)]
#pragma warning restore CS0618
    private void RequestStartRunServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (!CanStartRun(rpcParams.Receive.SenderClientId))
        {
            if (verboseLogs)
            {
                Debug.LogWarning($"[NetGameSession] StartRun denied for sender {rpcParams.Receive.SenderClientId} (phase={Phase}, inProgress={_isSceneOperationInProgress}).");
            }
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
            {
                Debug.LogWarning($"[NetGameSession] ReturnToLobby denied for sender {rpcParams.Receive.SenderClientId} (phase={Phase}, inProgress={_isSceneOperationInProgress}).");
            }
            return;
        }

        ReturnToLobbyServer();
    }

    // ---------------------------------------------------------------------
    // Server-side flow
    // ---------------------------------------------------------------------

    private void StartRunServer()
    {
        SafeSetPhaseServer(NetSessionPhase.LoadingRun);

        if (!useNetworkSceneManagement)
        {
            SafeSetPhaseServer(NetSessionPhase.InRun);
            return;
        }

        if (!CanUseSceneManager(out var sceneManager))
        {
            SafeSetPhaseServer(NetSessionPhase.InRun);
            return;
        }

        _isSceneOperationInProgress = true;

        if (verboseLogs) Debug.Log($"[NetGameSession] Loading run scene: '{runSceneName}'");
        sceneManager.LoadScene(runSceneName, LoadSceneMode.Single);
    }

    private void ReturnToLobbyServer()
    {
        SafeSetPhaseServer(NetSessionPhase.ReturningToLobby);

        if (!useNetworkSceneManagement)
        {
            SafeSetPhaseServer(NetSessionPhase.Lobby);
            return;
        }

        if (!CanUseSceneManager(out var sceneManager))
        {
            SafeSetPhaseServer(NetSessionPhase.Lobby);
            return;
        }

        _isSceneOperationInProgress = true;

        if (verboseLogs) Debug.Log($"[NetGameSession] Loading lobby scene: '{lobbySceneName}'");
        sceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    // ---------------------------------------------------------------------
    // Scene Manager events (server)
    // ---------------------------------------------------------------------

    private void SubscribeToSceneEventsIfNeeded()
    {
        if (!useNetworkSceneManagement) return;
        if (_subscribedToSceneEvents) return;

        if (!CanUseSceneManager(out var sceneManager)) return;

        sceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        _subscribedToSceneEvents = true;

        if (verboseLogs) Debug.Log("[NetGameSession] Subscribed to SceneManager events.");
    }

    private void UnsubscribeFromSceneEventsIfNeeded()
    {
        if (!_subscribedToSceneEvents) return;

        if (NetworkManager == null || NetworkManager.SceneManager == null) return;

        NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        _subscribedToSceneEvents = false;

        if (verboseLogs) Debug.Log("[NetGameSession] Unsubscribed from SceneManager events.");
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;

        _isSceneOperationInProgress = false;

        if (verboseLogs)
        {
            int ok = clientsCompleted != null ? clientsCompleted.Count : 0;
            int ko = clientsTimedOut != null ? clientsTimedOut.Count : 0;
            Debug.Log($"[NetGameSession] Scene load completed: '{sceneName}'. Clients OK={ok}, TimedOut={ko}");
        }

        if (string.Equals(sceneName, runSceneName, StringComparison.Ordinal))
        {
            SafeSetPhaseServer(NetSessionPhase.InRun);
        }
        else if (string.Equals(sceneName, lobbySceneName, StringComparison.Ordinal))
        {
            SafeSetPhaseServer(NetSessionPhase.Lobby);
        }
        else
        {
            if (verboseLogs) Debug.LogWarning($"[NetGameSession] Loaded scene '{sceneName}' does not match Lobby/Run scene names.");
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private bool CanUseSceneManager(out NetworkSceneManager sceneManager)
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

    [ContextMenu("DEBUG: Force Phase -> Lobby (Server Only)")]
    private void DebugForceLobby()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetGameSession] DebugForceLobby ignored (not server).");
            return;
        }

        _isSceneOperationInProgress = false;
        SafeSetPhaseServer(NetSessionPhase.Lobby);
    }
}