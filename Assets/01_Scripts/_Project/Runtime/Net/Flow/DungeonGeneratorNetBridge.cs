using DungeonSteakhouse.Net.Session;
using Unity.Netcode;
using UnityEngine;

namespace HuntingGame.ProceduralGeneration
{
    [RequireComponent(typeof(DungeonGenerator))]
    public class DungeonGeneratorNetBridge : NetworkBehaviour
    {
        private DungeonGenerator _generator;

        private void Awake()
        {
            _generator = GetComponent<DungeonGenerator>();
        }

        private void OnEnable()
        {
            if (NetSessionManager.Instance == null) return;

            NetSessionManager.Instance.StateChanged += OnSessionStateChanged;

            // Event may have already fired before this scene was loaded
            if (NetworkManager.Singleton.IsServer
                && NetSessionManager.Instance.State == NetSessionState.StartingRun)
            {
                GenerateDungeon();
            }
        }

        private void OnDisable()
        {
            if (NetSessionManager.Instance != null)
                NetSessionManager.Instance.StateChanged -= OnSessionStateChanged;
        }

        private void OnSessionStateChanged(NetSessionState previous, NetSessionState next)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            if (next != NetSessionState.StartingRun) return;

            GenerateDungeon();
        }

        private void GenerateDungeon()
        {
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            _generator.GenerateWithSeed(seed);
            SyncSeedToClientsClientRpc(seed);
        }

        [ClientRpc]
        private void SyncSeedToClientsClientRpc(int seed)
        {
            // Host already generated, skip
            if (NetworkManager.Singleton.IsServer) return;

            _generator.GenerateWithSeed(seed);
        }
    }
}