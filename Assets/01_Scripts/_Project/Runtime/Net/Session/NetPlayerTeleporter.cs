using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DungeonSteakhouse.Net.Session
{
    /// <summary>
    /// Single responsibility: execute player teleportation (with deferred retry support).
    /// </summary>
    public class NetPlayerTeleporter : MonoBehaviour
    {
        [SerializeField] private NetPlayerSpawnResolver spawnResolver;
        [SerializeField] private int teleportRetryFrames = 2;
        [SerializeField] private bool verboseLogs = true;

        private NetSessionPhase _currentPhase = NetSessionPhase.Lobby;

        /// <summary>
        /// Must be called by NetGameSession when the phase changes so teleporter knows the context.
        /// </summary>
        public void SetCurrentPhase(NetSessionPhase phase) => _currentPhase = phase;

        public void TeleportAllPlayers(NetSpawnContext context)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || spawnResolver == null)
                return;

            foreach (var kvp in nm.ConnectedClients)
            {
                ulong clientId = kvp.Key;
                var c = kvp.Value;

                if (c == null || c.PlayerObject == null)
                    continue;

                if (!spawnResolver.TryGetSpawnPoint(context, clientId, out var pos, out var rot))
                    continue;

                c.PlayerObject.transform.SetPositionAndRotation(pos, rot);
            }
        }

        public void DeferredTeleportClient(ulong clientId)
        {
            StartCoroutine(DeferredTeleportClientRoutine(clientId));
        }

        private IEnumerator DeferredTeleportClientRoutine(ulong clientId)
        {
            for (int i = 0; i <= teleportRetryFrames; i++)
            {
                if (TryTeleportClientNow(clientId))
                    yield break;

                yield return null;
            }

            if (verboseLogs)
                Debug.LogWarning($"[NetPlayerTeleporter] Failed to teleport clientId={clientId} after {teleportRetryFrames + 1} frame(s). Check PlayerObject spawn + NetSpawnPointGroup setup.");
        }

        private bool TryTeleportClientNow(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || spawnResolver == null)
                return false;

            if (!nm.ConnectedClients.TryGetValue(clientId, out var client) || client == null || client.PlayerObject == null)
                return false;

            var ctx = (_currentPhase == NetSessionPhase.InRun || _currentPhase == NetSessionPhase.LoadingRun)
                ? NetSpawnContext.Run
                : NetSpawnContext.Lobby;

            if (!spawnResolver.TryGetSpawnPoint(ctx, clientId, out var pos, out var rot))
                return false;

            client.PlayerObject.transform.SetPositionAndRotation(pos, rot);
            return true;
        }
    }
}
