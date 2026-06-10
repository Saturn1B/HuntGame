using System.Collections;
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
            SyncSeedClientRpc(seed);                              // broadcast befor
            FindObjectOfType<DungeonGenerator>()?.GenerateWithSeed(seed); 
        }

        [ClientRpc]
        private void SyncSeedClientRpc(int seed)
        {
            Debug.Log($"[Client] SyncSeedClientRpc reçu, seed={seed}, IsServer={IsServer}");
            if (IsServer) return;
            StartCoroutine(WaitAndGenerate(seed));
        }

        private IEnumerator WaitAndGenerate(int seed)
        {
            DungeonGenerator generator = null;
            while (generator == null)
            {
                generator = FindObjectOfType<DungeonGenerator>();
                yield return null;
            }
            Debug.Log($"[Client] DungeonGenerator trouvé, génération avec seed={seed}");
            generator.GenerateWithSeed(seed);
        }
    }
}