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

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            _generator.GenerateWithSeed(seed);
            SyncSeedToClientsClientRpc(seed);
        }

        [ClientRpc]
        private void SyncSeedToClientsClientRpc(int seed)
        {
            if (NetworkManager.Singleton.IsServer) return;

            _generator.GenerateWithSeed(seed);
        }
    }
}