using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DungeonSteakhouse.Net.Players;
using DungeonSteakhouse.Net.Flow;

namespace DungeonSteakhouse.Net.Session
{
    public enum NetSessionPhase
    {
        Lobby = 0,
        LoadingRun = 10,
        InRun = 20,
        ReturningToLobby = 30
    }

    /// <summary>
    /// Server-authoritative session state machine.
    /// Responsibilities (v1):
    /// - Expose a replicated session phase (Lobby/Loading/InRun/Returning)
    /// - Validate and handle StartRun / ReturnToLobby requests (host-only)
    /// - Teleport newly connected players to appropriate spawn points
    /// - Provide simple server-side helpers (reset readiness, respawn)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetGameSession : NetworkBehaviour
    {
        public static NetGameSession Instance { get; private set; }

        [Header("References")]
        [SerializeField] private NetGameRoot netGameRoot;
        [SerializeField] private NetSceneFlow sceneFlow;

        [Header("Spawn")]
        [Tooltip("If true, the server will teleport players to a spawn point on connect and on phase changes.")]
        [SerializeField] private bool enableServerSpawning = true;

        [Tooltip("If true, players' Ready flag will be reset when returning to Lobby.")]
        [SerializeField] private bool resetReadyOnReturnToLobby = true;

        [Tooltip("If true, players' Ready flag will be reset when starting a run.")]
        [SerializeField] private bool resetReadyOnStartRun = true;

        private readonly NetworkVariable<int> _phase =
            new((int)NetSessionPhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetSessionPhase Phase => (NetSessionPhase)_phase.Value;

        public event Action<NetSessionPhase> PhaseChanged;

        private readonly List<NetSpawnPointGroup> _spawnGroupsCache = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NetGameSession] Duplicate instance detected. Destroying the new one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (netGameRoot == null)
                netGameRoot = NetGameRoot.Instance;

            if (sceneFlow == null && netGameRoot != null)
                sceneFlow = netGameRoot.SceneFlow;

            _phase.OnValueChanged += OnPhaseValueChanged;

            if (IsServer)
            {
                HookServerCallbacks();
                ForceSetPhaseFromSceneFlow();
                RebuildSpawnCache();
                TeleportAllToCurrentContext();
            }
        }

        public override void OnNetworkDespawn()
        {
            _phase.OnValueChanged -= OnPhaseValueChanged;

            if (IsServer)
                UnhookServerCallbacks();

            if (Instance == this)
                Instance = null;

            base.OnNetworkDespawn();
        }

        private void OnPhaseValueChanged(int previous, int current)
        {
            PhaseChanged?.Invoke((NetSessionPhase)current);
        }

        private void HookServerCallbacks()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedServer;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedServer;
            }
        }

        private void UnhookServerCallbacks()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedServer;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedServer;
            }
        }

        private void OnClientConnectedServer(ulong clientId)
        {
            if (!IsServer)
                return;

            if (!enableServerSpawning)
                return;

            TeleportClientToCurrentContext(clientId);
        }

        private void OnClientDisconnectedServer(ulong clientId)
        {
            // Reserved for future: clean per-client session state here.
        }

        /// <summary>
        /// Host-only request to start the run.
        /// </summary>
        public void RequestStartRun()
        {
            if (!IsSpawned)
                return;

            RequestStartRunServerRpc();
        }

        /// <summary>
        /// Host-only request to return to the lobby.
        /// </summary>
        public void RequestReturnToLobby()
        {
            if (!IsSpawned)
                return;

            RequestReturnToLobbyServerRpc();
        }

        public bool CanStartRun()
        {
            if (netGameRoot == null || netGameRoot.PlayerRegistry == null || sceneFlow == null)
                return false;

            if (Phase != NetSessionPhase.Lobby)
                return false;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
                return false;

            if (!netGameRoot.PlayerRegistry.AreAllReady)
                return false;

            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestStartRunServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer)
                return;

            ulong sender = rpcParams.Receive.SenderClientId;

            // Host-only guard
            if (sender != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"[NetGameSession] Ignored StartRun request from non-host clientId={sender}.");
                return;
            }

            if (netGameRoot == null || netGameRoot.PlayerRegistry == null || sceneFlow == null)
            {
                Debug.LogError("[NetGameSession] Missing references. Cannot start run.");
                return;
            }

            if (Phase != NetSessionPhase.Lobby)
            {
                Debug.LogWarning($"[NetGameSession] StartRun ignored because phase is {Phase}.");
                return;
            }

            if (!netGameRoot.PlayerRegistry.AreAllReady)
            {
                Debug.LogWarning("[NetGameSession] StartRun ignored because not all players are Ready.");
                return;
            }

            if (resetReadyOnStartRun)
                ServerSetAllPlayersReady(false);

            bool ok = sceneFlow.TryStartRun();
            if (!ok)
            {
                Debug.LogWarning("[NetGameSession] sceneFlow.TryStartRun() failed.");
                return;
            }

            _phase.Value = (int)NetSessionPhase.LoadingRun;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestReturnToLobbyServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer)
                return;

            ulong sender = rpcParams.Receive.SenderClientId;

            // Host-only guard
            if (sender != NetworkManager.ServerClientId)
            {
                Debug.LogWarning($"[NetGameSession] Ignored ReturnToLobby request from non-host clientId={sender}.");
                return;
            }

            if (sceneFlow == null)
            {
                Debug.LogError("[NetGameSession] Missing NetSceneFlow reference. Cannot return to lobby.");
                return;
            }

            if (Phase != NetSessionPhase.InRun)
            {
                Debug.LogWarning($"[NetGameSession] ReturnToLobby ignored because phase is {Phase}.");
                return;
            }

            bool ok = sceneFlow.TryReturnToHub();
            if (!ok)
            {
                Debug.LogWarning("[NetGameSession] sceneFlow.TryReturnToHub() failed.");
                return;
            }

            _phase.Value = (int)NetSessionPhase.ReturningToLobby;
        }

        private void Update()
        {
            if (!IsServer || sceneFlow == null)
                return;

            // Keep phase in sync with actual scene flow on the server.
            if (Phase == NetSessionPhase.LoadingRun && sceneFlow.Phase == NetRunPhase.InRun)
            {
                _phase.Value = (int)NetSessionPhase.InRun;

                if (enableServerSpawning)
                {
                    RebuildSpawnCache();
                    TeleportAllToCurrentContext();
                }
            }

            if (Phase == NetSessionPhase.ReturningToLobby && sceneFlow.Phase == NetRunPhase.Hub)
            {
                _phase.Value = (int)NetSessionPhase.Lobby;

                if (resetReadyOnReturnToLobby)
                    ServerSetAllPlayersReady(false);

                if (enableServerSpawning)
                {
                    RebuildSpawnCache();
                    TeleportAllToCurrentContext();
                }
            }

            // Safety: if someone manually loads/unloads scenes, recover.
            if (Phase == NetSessionPhase.Lobby && sceneFlow.Phase == NetRunPhase.InRun)
                _phase.Value = (int)NetSessionPhase.InRun;

            if (Phase == NetSessionPhase.InRun && sceneFlow.Phase == NetRunPhase.Hub)
                _phase.Value = (int)NetSessionPhase.Lobby;
        }

        private void ForceSetPhaseFromSceneFlow()
        {
            if (sceneFlow == null)
                return;

            _phase.Value = (sceneFlow.Phase == NetRunPhase.InRun)
                ? (int)NetSessionPhase.InRun
                : (int)NetSessionPhase.Lobby;
        }

        private void ServerSetAllPlayersReady(bool ready)
        {
            if (netGameRoot == null || netGameRoot.PlayerRegistry == null)
                return;

            foreach (var p in netGameRoot.PlayerRegistry.Players)
            {
                if (p == null)
                    continue;

                p.ServerSetReady(ready);
            }
        }

        private NetSpawnContext GetCurrentSpawnContext()
        {
            if (sceneFlow != null && sceneFlow.Phase == NetRunPhase.InRun)
                return NetSpawnContext.Run;

            return NetSpawnContext.Lobby;
        }

        private void RebuildSpawnCache()
        {
            _spawnGroupsCache.Clear();
            _spawnGroupsCache.AddRange(FindObjectsByType<NetSpawnPointGroup>(FindObjectsSortMode.None));
        }

        private bool TryGetSpawnPoint(ulong clientId, NetSpawnContext context, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;

            if (_spawnGroupsCache.Count == 0)
                RebuildSpawnCache();

            NetSpawnPointGroup best = null;
            for (int i = 0; i < _spawnGroupsCache.Count; i++)
            {
                var g = _spawnGroupsCache[i];
                if (g == null)
                    continue;

                if (g.Context != context)
                    continue;

                best = g;
                break;
            }

            if (best == null)
                return false;

            return best.TryGetSpawnPoint(clientId, out pos, out rot);
        }

        private void TeleportAllToCurrentContext()
        {
            if (!IsServer || !enableServerSpawning)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null)
                return;

            foreach (var clientId in nm.ConnectedClientsIds)
                TeleportClientToCurrentContext(clientId);
        }

        private void TeleportClientToCurrentContext(ulong clientId)
        {
            if (!IsServer)
                return;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
                return;

            var context = GetCurrentSpawnContext();
            if (!TryGetSpawnPoint(clientId, context, out var pos, out var rot))
                return;

            // Server sets its own instance.
            client.PlayerObject.transform.SetPositionAndRotation(pos, rot);

            // Also instruct the owning client to set the same transform (important with owner-authoritative movement).
            var rpcParams = new RpcParams
            {
                Send = new RpcSendParams
                {
                    Target = RpcTarget.Single(clientId, RpcTargetUse.Temp)
                }
            };

            ApplySpawnForOwnerClientRpc(pos, rot, rpcParams);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ApplySpawnForOwnerClientRpc(Vector3 pos, Quaternion rot, RpcParams rpcParams = default)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
                return;

            nm.LocalClient.PlayerObject.transform.SetPositionAndRotation(pos, rot);
        }
    }
}