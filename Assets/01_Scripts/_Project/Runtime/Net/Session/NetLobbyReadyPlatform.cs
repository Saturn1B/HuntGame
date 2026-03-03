using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DungeonSteakhouse.Net.Players;
using DungeonSteakhouse.Net.Flow;

namespace DungeonSteakhouse.Net.Session
{
    /// <summary>
    /// Platform-based ready system:
    /// - Players are considered "ready" when they are inside this trigger (server-authoritative).
    /// - When ALL players are on the platform, a short countdown starts.
    /// - If the condition stays true until the countdown ends, the host starts the run.
    ///
    /// Requirements:
    /// - This GameObject must have a trigger collider.
    /// - The server/host must have physics running for this scene (typical NGO host).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetLobbyReadyPlatform : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetGameRoot netGameRoot;

        [Tooltip("Optional: if assigned, used to start the run. If null, will fallback to NetGameRoot.SceneFlow.")]
        [SerializeField] private NetGameSession gameSession;

        [Header("Countdown")]
        [Tooltip("Countdown duration before starting the run once everyone is on the platform.")]
        [Range(0.1f, 10f)]
        [SerializeField] private float countdownSeconds = 1.0f;

        [Tooltip("If true, resets all players to Not Ready after triggering the run start.")]
        [SerializeField] private bool resetReadyAfterStart = true;

        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private readonly Dictionary<ulong, int> _overlapCounts = new();
        private bool _countdownActive;
        private float _countdownEndTime;

        private void Awake()
        {
            if (netGameRoot == null)
                netGameRoot = NetGameRoot.Instance;

            if (gameSession == null)
                gameSession = NetGameSession.Instance;
        }

        private void Update()
        {
            if (!IsServerActiveInLobby())
                return;

            if (!_countdownActive)
                return;

            if (!AreAllPlayersOnPlatform())
            {
                CancelCountdown("Countdown cancelled: not all players are on the platform anymore.");
                return;
            }

            if (Time.unscaledTime >= _countdownEndTime)
            {
                _countdownActive = false;
                TryStartRun();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerActiveInLobby())
                return;

            if (!TryGetPlayerFromCollider(other, out var player))
                return;

            ulong clientId = player.OwnerClientId;

            int count = 0;
            _overlapCounts.TryGetValue(clientId, out count);
            count++;
            _overlapCounts[clientId] = count;

            // First overlap => considered on platform
            if (count == 1)
            {
                player.ServerSetReady(true);

                if (logDebug)
                    Debug.Log($"[NetLobbyReadyPlatform] Player entered platform: clientId={clientId} name='{player.DisplayName}'");
            }

            TryStartCountdownIfReady();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServerActiveInLobby())
                return;

            if (!TryGetPlayerFromCollider(other, out var player))
                return;

            ulong clientId = player.OwnerClientId;

            if (!_overlapCounts.TryGetValue(clientId, out int count))
                return;

            count = Mathf.Max(0, count - 1);

            if (count == 0)
            {
                _overlapCounts.Remove(clientId);
                player.ServerSetReady(false);

                if (logDebug)
                    Debug.Log($"[NetLobbyReadyPlatform] Player left platform: clientId={clientId} name='{player.DisplayName}'");

                if (_countdownActive)
                    CancelCountdown("Countdown cancelled: a player left the platform.");
            }
            else
            {
                _overlapCounts[clientId] = count;
            }
        }

        private bool IsServerActiveInLobby()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return false;

            // Only operate while in Hub/Lobby
            var flow = netGameRoot != null ? netGameRoot.SceneFlow : null;
            if (flow == null)
                return true; // fallback if flow not available

            return flow.Phase == NetRunPhase.Hub;
        }

        private bool TryGetPlayerFromCollider(Collider other, out NetPlayer player)
        {
            player = null;

            if (other == null)
                return false;

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null || !netObj.IsPlayerObject)
                return false;

            player = netObj.GetComponent<NetPlayer>();
            return player != null;
        }

        private bool AreAllPlayersOnPlatform()
        {
            if (netGameRoot == null || netGameRoot.PlayerRegistry == null)
                return false;

            var players = netGameRoot.PlayerRegistry.Players;
            if (players == null || players.Count == 0)
                return false;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null)
                    return false;

                // A player is "on platform" if overlap count > 0
                if (!_overlapCounts.TryGetValue(p.OwnerClientId, out int count) || count <= 0)
                    return false;
            }

            return true;
        }

        private void TryStartCountdownIfReady()
        {
            if (_countdownActive)
                return;

            if (!AreAllPlayersOnPlatform())
                return;

            _countdownActive = true;
            _countdownEndTime = Time.unscaledTime + Mathf.Max(0.1f, countdownSeconds);

            if (logDebug)
                Debug.Log($"[NetLobbyReadyPlatform] All players on platform. Starting countdown ({countdownSeconds:0.00}s).");
        }

        private void CancelCountdown(string reason)
        {
            _countdownActive = false;

            if (logDebug)
                Debug.Log($"[NetLobbyReadyPlatform] {reason}");
        }

        private void TryStartRun()
        {
            // Prefer session manager if present, otherwise fallback to scene flow.
            if (gameSession == null)
                gameSession = NetGameSession.Instance;

            if (gameSession != null)
            {
                if (logDebug)
                    Debug.Log("[NetLobbyReadyPlatform] Countdown complete. Requesting StartRun via NetGameSession.");

                gameSession.RequestStartRun();
                PostStartCleanup();
                return;
            }

            var flow = netGameRoot != null ? netGameRoot.SceneFlow : null;
            if (flow == null)
            {
                Debug.LogError("[NetLobbyReadyPlatform] Cannot start run: no NetGameSession and no NetSceneFlow available.");
                return;
            }

            if (logDebug)
                Debug.Log("[NetLobbyReadyPlatform] Countdown complete. Starting run via NetSceneFlow.");

            flow.TryStartRun();
            PostStartCleanup();
        }

        private void PostStartCleanup()
        {
            if (!resetReadyAfterStart)
                return;

            if (netGameRoot == null || netGameRoot.PlayerRegistry == null)
                return;

            foreach (var p in netGameRoot.PlayerRegistry.Players)
            {
                if (p == null)
                    continue;

                p.ServerSetReady(false);
            }

            _overlapCounts.Clear();
        }
    }
}