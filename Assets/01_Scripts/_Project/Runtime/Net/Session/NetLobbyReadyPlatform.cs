using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DungeonSteakhouse.Net.Players;
using DungeonSteakhouse.Net.Flow;
// DungeonSteakhouse.Net.Interactions; // NetInteractable lives here in your project (adjust if needed)

namespace DungeonSteakhouse.Net.Session
{
    /// <summary>
    /// Platform-based ready system (server-authoritative):
    /// - Players are "ready" when inside this trigger.
    /// - When ALL players are on the platform, a short countdown starts.
    /// - If the condition stays true until countdown ends, AllReadyConfirmed becomes true.
    /// - NO automatic start run here (button will handle it).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetLobbyReadyPlatform : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetGameRoot netGameRoot;

        [Header("Optional network indicator")]
        [Tooltip("Optional: a NetInteractable used as a replicated indicator (BoolState).")]
        [SerializeField] private NetInteractable allReadyIndicator;

        [Header("Countdown")]
        [Tooltip("Countdown duration before confirming 'all ready' once everyone is on the platform.")]
        [Range(0.0f, 10f)]
        [SerializeField] private float confirmCountdownSeconds = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private readonly Dictionary<ulong, int> _overlapCounts = new();

        private bool _countdownActive;
        private float _countdownEndTime;

        private bool _allReadyConfirmedServer;

        /// <summary>True on the server when all players stayed on platform until countdown ended.</summary>
        public bool AllReadyConfirmedServer => _allReadyConfirmedServer;

        private void Awake()
        {
            if (netGameRoot == null)
                netGameRoot = NetGameRoot.Instance;
        }

        private void Update()
        {
            if (!IsServerActiveInLobby())
                return;

            // If already confirmed, keep it true only while everyone stays on platform.
            if (_allReadyConfirmedServer)
            {
                if (!AreAllPlayersOnPlatform())
                {
                    SetAllReadyConfirmed(false, "AllReady lost: a player left the platform.");
                }
                return;
            }

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
                SetAllReadyConfirmed(true, "AllReady confirmed (countdown complete).");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerActiveInLobby())
                return;

            if (!TryGetPlayerFromCollider(other, out var player))
                return;

            ulong clientId = player.OwnerClientId;

            _overlapCounts.TryGetValue(clientId, out int count);
            count++;
            _overlapCounts[clientId] = count;

            // First overlap => considered on platform
            if (count == 1)
            {
                player.ServerSetReady(true);

                if (logDebug)
                    Debug.Log($"[NetLobbyReadyPlatform] Player entered platform: clientId={clientId} name='{player.DisplayName}'");
            }

            TryStartConfirmCountdownIfReady();
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

                // If we were confirmed, we must drop it when someone leaves.
                if (_allReadyConfirmedServer)
                    SetAllReadyConfirmed(false, "AllReady lost: a player left the platform.");
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

        private void TryStartConfirmCountdownIfReady()
        {
            if (_allReadyConfirmedServer)
                return;

            if (_countdownActive)
                return;

            if (!AreAllPlayersOnPlatform())
                return;

            // If countdown disabled, confirm immediately.
            if (confirmCountdownSeconds <= 0.0f)
            {
                SetAllReadyConfirmed(true, "AllReady confirmed (no countdown).");
                return;
            }

            _countdownActive = true;
            _countdownEndTime = Time.unscaledTime + Mathf.Max(0.05f, confirmCountdownSeconds);

            if (logDebug)
                Debug.Log($"[NetLobbyReadyPlatform] All players on platform. Confirm countdown started ({confirmCountdownSeconds:0.00}s).");
        }

        private void CancelCountdown(string reason)
        {
            _countdownActive = false;

            if (logDebug)
                Debug.Log($"[NetLobbyReadyPlatform] {reason}");
        }

        private void SetAllReadyConfirmed(bool value, string reason)
        {
            if (_allReadyConfirmedServer == value)
                return;

            _allReadyConfirmedServer = value;

            // Optional replicated indicator using NetInteractable.BoolState
            if (allReadyIndicator != null && allReadyIndicator.IsServer)
                allReadyIndicator.BoolState.Value = value;

            if (logDebug)
                Debug.Log($"[NetLobbyReadyPlatform] {reason} -> AllReadyConfirmedServer={value}");
        }

        /// <summary>
        /// Optional: call this from server when starting the run to clear the confirmed state.
        /// </summary>
        public void ServerClearConfirmedReady()
        {
            if (!IsServerActiveInLobby())
                return;

            SetAllReadyConfirmed(false, "AllReady cleared by server.");
            _countdownActive = false;
        }
    }
}