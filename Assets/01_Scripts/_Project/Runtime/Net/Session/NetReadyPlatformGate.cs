using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DungeonSteakhouse.Net;
using DungeonSteakhouse.Net.Flow;
using DungeonSteakhouse.Net.Players;

namespace DungeonSteakhouse.Net.Session
{
    public enum NetReadyGateMode
    {
        HubOnly = 0,
        InRunOnly = 1,
        HubAndRun = 2
    }

    /// <summary>
    /// Server-authoritative ready gate based on players overlapping a trigger platform.
    /// - Tracks per-client overlap count (safe for multi-collider players).
    /// - Confirms "all ready" only after everyone stays on the platform for a short countdown.
    /// - Optionally replicates a bool via a NetInteractable (BoolState).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class NetReadyPlatformGate : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private NetReadyGateMode mode = NetReadyGateMode.HubOnly;

        [Header("References")]
        [SerializeField] private NetGameRoot netGameRoot;

        [Header("Optional replicated indicator")]
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

        public NetReadyGateMode Mode => mode;
        public bool AllReadyConfirmedServer => _allReadyConfirmedServer;

        private void Awake()
        {
            if (netGameRoot == null)
                netGameRoot = NetGameRoot.Instance;

            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning("[NetReadyPlatformGate] Collider is not marked as Trigger. The gate will not work.");
        }

        private void Update()
        {
            if (!IsActiveOnServer())
                return;

            if (_allReadyConfirmedServer)
            {
                if (!AreAllPlayersOnPlatform())
                    SetAllReadyConfirmed(false, "AllReady lost: a player left the platform.");

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
            if (!IsActiveOnServer())
                return;

            if (!TryGetPlayerClientId(other, out ulong clientId, out string displayName))
                return;

            _overlapCounts.TryGetValue(clientId, out int count);
            count++;
            _overlapCounts[clientId] = count;

            if (count == 1 && logDebug)
                Debug.Log($"[NetReadyPlatformGate] Player entered platform: clientId={clientId} name='{displayName}'");

            TryStartConfirmCountdownIfReady();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsActiveOnServer())
                return;

            if (!TryGetPlayerClientId(other, out ulong clientId, out string displayName))
                return;

            if (!_overlapCounts.TryGetValue(clientId, out int count))
                return;

            count = Mathf.Max(0, count - 1);

            if (count == 0)
            {
                _overlapCounts.Remove(clientId);

                if (logDebug)
                    Debug.Log($"[NetReadyPlatformGate] Player left platform: clientId={clientId} name='{displayName}'");

                if (_countdownActive)
                    CancelCountdown("Countdown cancelled: a player left the platform.");

                if (_allReadyConfirmedServer)
                    SetAllReadyConfirmed(false, "AllReady lost: a player left the platform.");
            }
            else
            {
                _overlapCounts[clientId] = count;
            }
        }

        public void ServerClearConfirmedReady()
        {
            if (!IsActiveOnServer())
                return;

            SetAllReadyConfirmed(false, "AllReady cleared by server.");
            _countdownActive = false;
        }

        private bool IsActiveOnServer()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return false;

            var flow = netGameRoot != null ? netGameRoot.SceneFlow : null;
            if (flow == null)
                return true;

            return mode switch
            {
                NetReadyGateMode.HubOnly => flow.Phase == NetRunPhase.Hub,
                NetReadyGateMode.InRunOnly => flow.Phase == NetRunPhase.InRun,
                NetReadyGateMode.HubAndRun => true,
                _ => true
            };
        }

        private bool TryGetPlayerClientId(Collider other, out ulong clientId, out string displayName)
        {
            clientId = 0;
            displayName = "<unknown>";

            if (other == null)
                return false;

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null || !netObj.IsPlayerObject)
                return false;

            clientId = netObj.OwnerClientId;

            var player = netObj.GetComponent<NetPlayer>();
            if (player != null)
                displayName = player.DisplayName;

            return true;
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

                if (!_overlapCounts.TryGetValue(p.OwnerClientId, out int count) || count <= 0)
                    return false;
            }

            return true;
        }

        private void TryStartConfirmCountdownIfReady()
        {
            if (_allReadyConfirmedServer || _countdownActive)
                return;

            if (!AreAllPlayersOnPlatform())
                return;

            if (confirmCountdownSeconds <= 0.0f)
            {
                SetAllReadyConfirmed(true, "AllReady confirmed (no countdown).");
                return;
            }

            _countdownActive = true;
            _countdownEndTime = Time.unscaledTime + Mathf.Max(0.05f, confirmCountdownSeconds);

            if (logDebug)
                Debug.Log($"[NetReadyPlatformGate] All players on platform. Confirm countdown started ({confirmCountdownSeconds:0.00}s).");
        }

        private void CancelCountdown(string reason)
        {
            _countdownActive = false;

            if (logDebug)
                Debug.Log($"[NetReadyPlatformGate] {reason}");
        }

        private void SetAllReadyConfirmed(bool value, string reason)
        {
            if (_allReadyConfirmedServer == value)
                return;

            _allReadyConfirmedServer = value;

            if (allReadyIndicator != null && allReadyIndicator.IsServer)
                allReadyIndicator.BoolState.Value = value;

            if (logDebug)
                Debug.Log($"[NetReadyPlatformGate] {reason} -> AllReadyConfirmedServer={value}");
        }
    }
}
