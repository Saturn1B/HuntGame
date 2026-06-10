using Unity.Netcode;
using UnityEngine;

namespace HuntingGame.ProceduralGeneration
{
    public class DungeonSeedBroadcaster : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            int seed = Random.Range(1, int.MaxValue);
            FindObjectOfType<DungeonGenerator>()?.GenerateWithSeed(seed);
            SyncSeedClientRpc(seed);
        }

        [ClientRpc]
        private void SyncSeedClientRpc(int seed)
        {
            if (IsServer) return;

            FindObjectOfType<DungeonGenerator>()?.GenerateWithSeed(seed);
        }
    }
}
