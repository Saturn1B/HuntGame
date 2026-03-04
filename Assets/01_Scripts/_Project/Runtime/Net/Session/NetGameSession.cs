using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using DungeonSteakhouse.Net;
using DungeonSteakhouse.Net.Session;

public enum NetSessionPhase : byte
{
    Lobby = 0,
    LoadingRun = 1,
    InRun = 2,
    ReturningToLobby = 3
}

[DisallowMultipleComponent]
public sealed class NetGameSession : NetworkBehaviour, INetGameSession
{
    public static NetGameSession Instance { get; private set; }

    [Header("Config (recommended)")]
    [SerializeField] private NetGameConfig config;

    [Header("Scene Names (fallback if Config is missing)")]
    [SerializeField] private string lobbySceneName = "Tavern";
    [SerializeField] private string runSceneName = "Dungeon";

    [Header("Authority")]
    [Tooltip("If true, only the host/server can start a run or return to lobby.")]
    [SerializeField] private bool onlyHostCanControlSession = true;

    [Header("Scene Management")]
    [Tooltip("If true, use NetworkSceneManager to load/unload scenes (Additive).")]
    [SerializeField] private bool useNetworkSceneManagement = true;

    [Header("Spawning")]
    [Tooltip("If true, the server will teleport PlayerObjects to NetSpawnPointGroup locations on connect and on phase changes.")]
    [SerializeField] private bool useSpawnPointGroups = true;

    [Tooltip("How many frames to wait after client connect before trying to teleport (helps when PlayerObject spawns 1 frame later).")]
    [Range(0, 10)]
    [SerializeField] private int teleportRetryFrames = 2;

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

    private string LobbySceneName => (config != null && !string.IsNullOrWhiteSpace(config.tavernSceneName)) ? config.tavernSceneName : lobbySceneName;
    private string RunSceneName => (config != null && !string.IsNullOrWhiteSpace(config.dungeonSceneName)) ? config.dungeonSceneName : runSceneName;

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
        OnPhaseChangedInternal(_phase.Value, _phase.Value);

        if (IsServer)
        {
            SafeSetPhaseServer(NetSessionPhase.Lobby);

            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnectedServer;
            }

            HookSceneEventsIfNeeded();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnectedServer;
        }

        UnhookSceneEventsIfNeeded();

        _phase.OnValueChanged -= OnPhaseChangedInternal;

        base.OnNetworkDespawn();
    }

    private void OnPhaseChangedInternal(NetSessionPhase previous, NetSessionPhase current)
    {
        if (verboseLogs)
            Debug.Log($"[NetGameSession] Phase changed: {previous} -> {current}");

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
    // Server flow (Additive, aligned with NetGameConfig + NetSceneFlow)
    // -------------------------

    private void StartRunServer()
    {
        SafeSetPhaseServer(NetSessionPhase.LoadingRun);

        if (!useNetworkSceneManagement)
        {
            SafeSetPhaseServer(NetSessionPhase.InRun);
            ServerTeleportAllPlayers(NetSpawnContext.Run);
            return;
        }

        if (!TryGetSceneManager(out var sceneManager))
        {
            SafeSetPhaseServer(NetSessionPhase.InRun);
            ServerTeleportAllPlayers(NetSpawnContext.Run);
            return;
        }

        // If already loaded, just switch phase and teleport
        var runScene = SceneManager.GetSceneByName(RunSceneName);
        if (runScene.IsValid() && runScene.isLoaded)
        {
            if (verboseLogs)
                Debug.LogWarning($"[NetGameSession] Run scene '{RunSceneName}' is already loaded. Forcing InRun + teleport.");

            SafeSetPhaseServer(NetSessionPhase.InRun);
            ServerTeleportAllPlayers(NetSpawnContext.Run);
            return;
        }

        _sceneOpInProgress = true;

        if (verboseLogs)
            Debug.Log($"[NetGameSession] Loading run scene (Additive): '{RunSceneName}'");

        var status = sceneManager.LoadScene(RunSceneName, LoadSceneMode.Additive);
        if (status != SceneEventProgressStatus.Started)
        {
            _sceneOpInProgress = false;
            Debug.LogWarning($"[NetGameSession] LoadScene '{RunSceneName}' failed with status={status}. Check Build Settings + VerifySceneBeforeLoading.");
            // Fallback to phase so you don't get stuck
            SafeSetPhaseServer(NetSessionPhase.InRun);
            ServerTeleportAllPlayers(NetSpawnContext.Run);
        }
    }

    private void ReturnToLobbyServer()
    {
        SafeSetPhaseServer(NetSessionPhase.ReturningToLobby);

        if (!useNetworkSceneManagement)
        {
            SafeSetPhaseServer(NetSessionPhase.Lobby);
            ServerTeleportAllPlayers(NetSpawnContext.Lobby);
            return;
        }

        if (!TryGetSceneManager(out var sceneManager))
        {
            SafeSetPhaseServer(NetSessionPhase.Lobby);
            ServerTeleportAllPlayers(NetSpawnContext.Lobby);
            return;
        }

        var runScene = SceneManager.GetSceneByName(RunSceneName);
        if (!runScene.IsValid() || !runScene.isLoaded)
        {
            if (verboseLogs)
                Debug.LogWarning($"[NetGameSession] Run scene '{RunSceneName}' is not loaded. Forcing Lobby + teleport.");

            SafeSetPhaseServer(NetSessionPhase.Lobby);
            ServerTeleportAllPlayers(NetSpawnContext.Lobby);
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
            ServerTeleportAllPlayers(NetSpawnContext.Lobby);
        }
    }

    // -------------------------
    // Scene events
    // -------------------------

    private void HookSceneEventsIfNeeded()
    {
        if (_sceneEventsHooked)
            return;

        if (!useNetworkSceneManagement)
            return;

        if (NetworkManager == null || NetworkManager.SceneManager == null)
            return;

        // Align with your project (Additive flow)
        NetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

        // Respect config's active scene sync preference if present
        if (config != null)
            NetworkManager.SceneManager.ActiveSceneSynchronizationEnabled = config.syncActiveScene;

        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        _sceneEventsHooked = true;

        if (verboseLogs)
            Debug.Log("[NetGameSession] Hooked NetworkSceneManager.OnSceneEvent.");
    }

    private void UnhookSceneEventsIfNeeded()
    {
        if (!_sceneEventsHooked)
            return;

        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;

        _sceneEventsHooked = false;

        if (verboseLogs)
            Debug.Log("[NetGameSession] Unhooked NetworkSceneManager.OnSceneEvent.");
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (!IsServer)
            return;

        // Only consider server-local events for "operation done" / teleport decisions
        if (NetworkManager == null || sceneEvent.ClientId != NetworkManager.ServerClientId)
            return;

        if (sceneEvent.SceneName != RunSceneName)
            return;

        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
        {
            _sceneOpInProgress = false;
            SafeSetPhaseServer(NetSessionPhase.InRun);
            ServerTeleportAllPlayers(NetSpawnContext.Run);

            if (verboseLogs)
                Debug.Log($"[NetGameSession] LoadEventCompleted for '{RunSceneName}' -> InRun + teleport Run.");
        }

        if (sceneEvent.SceneEventType == SceneEventType.UnloadEventCompleted)
        {
            _sceneOpInProgress = false;
            SafeSetPhaseServer(NetSessionPhase.Lobby);
            ServerTeleportAllPlayers(NetSpawnContext.Lobby);

            if (verboseLogs)
                Debug.Log($"[NetGameSession] UnloadEventCompleted for '{RunSceneName}' -> Lobby + teleport Lobby.");
        }
    }

    // -------------------------
    // Spawn handling
    // -------------------------

    private void OnClientConnectedServer(ulong clientId)
    {
        if (!IsServer)
            return;

        if (!useSpawnPointGroups)
            return;

        StartCoroutine(ServerDeferredTeleportClient(clientId));
    }

    private IEnumerator ServerDeferredTeleportClient(ulong clientId)
    {
        for (int i = 0; i <= teleportRetryFrames; i++)
        {
            if (TryTeleportClientNow(clientId))
                yield break;

            yield return null;
        }

        if (verboseLogs)
            Debug.LogWarning($"[NetGameSession] Failed to teleport clientId={clientId} after {teleportRetryFrames + 1} frame(s). Check PlayerObject spawn + NetSpawnPointGroup setup.");
    }

    private bool TryTeleportClientNow(ulong clientId)
    {
        if (NetworkManager == null)
            return false;

        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) || client == null || client.PlayerObject == null)
            return false;

        var ctx = (Phase == NetSessionPhase.InRun || Phase == NetSessionPhase.LoadingRun) ? NetSpawnContext.Run : NetSpawnContext.Lobby;

        if (!TryGetSpawnPoint(ctx, clientId, out var pos, out var rot))
            return false;

        client.PlayerObject.transform.SetPositionAndRotation(pos, rot);
        return true;
    }

    private void ServerTeleportAllPlayers(NetSpawnContext context)
    {
        if (!IsServer || !useSpawnPointGroups || NetworkManager == null)
            return;

        foreach (var kvp in NetworkManager.ConnectedClients)
        {
            ulong clientId = kvp.Key;
            var c = kvp.Value;

            if (c == null || c.PlayerObject == null)
                continue;

            if (!TryGetSpawnPoint(context, clientId, out var pos, out var rot))
                continue;

            c.PlayerObject.transform.SetPositionAndRotation(pos, rot);
        }
    }

    private bool TryGetSpawnPoint(NetSpawnContext context, ulong clientId, out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = Quaternion.identity;

        var groups = UnityEngine.Object.FindObjectsByType<NetSpawnPointGroup>(FindObjectsSortMode.None);
        if (groups == null || groups.Length == 0)
            return false;

        NetSpawnPointGroup group = null;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].Context == context)
            {
                group = groups[i];
                break;
            }
        }

        if (group == null)
            return false;

        return group.TryGetSpawnPoint(clientId, out pos, out rot);
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