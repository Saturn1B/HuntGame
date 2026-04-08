using UnityEngine;

namespace DungeonSteakhouse.Net.Session
{
    /// <summary>
    /// Single responsibility: find the correct spawn point for a given client.
    /// </summary>
    public class NetPlayerSpawnResolver : MonoBehaviour
    {
        public bool TryGetSpawnPoint(NetSpawnContext context, ulong clientId, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            var groups = Object.FindObjectsByType<NetSpawnPointGroup>(FindObjectsSortMode.None);
            if (groups == null || groups.Length == 0)
                return false;

            NetSpawnPointGroup group = null;
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].Context == context)
                {
                    group = groups[i];
                    break;
                }
            }

            if (group == null)
                return false;

            return group.TryGetSpawnPoint(clientId, out position, out rotation);
        }
    }
}
