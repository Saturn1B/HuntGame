using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HuntingGame.ProceduralGeneration
{
    public class DungeonSeedBroadcaster : NetworkBehaviour
    {
        // Server-written, everyone-read. Any client that syncs this object -- including a late
        // joiner -- receives the current value as part of its spawn snapshot, so generation is
        // never dependent on having been connected at the exact moment the seed was rolled.
        private readonly NetworkVariable<int> _seed = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                int seed = Random.Range(1, int.MaxValue);
                _seed.Value = seed;
                GenerateWithSeed(seed);
                return;
            }

            // Client (including a late joiner): the spawn snapshot already carries the seed
            // rolled by the server above, no need to wait for a ClientRpc.
            if (_seed.Value != 0)
                GenerateWithSeed(_seed.Value);
        }

        private void GenerateWithSeed(int seed)
        {
            Debug.Log($"[DungeonSeedBroadcaster] Generating with seed={seed}, IsServer={IsServer}");
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

            generator.GenerateWithSeed(seed);
        }
    }
}
