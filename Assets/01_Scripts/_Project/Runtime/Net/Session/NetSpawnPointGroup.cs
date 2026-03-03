using UnityEngine;

namespace DungeonSteakhouse.Net.Session
{
    public enum NetSpawnContext
    {
        Lobby = 0,
        Run = 10
    }

    /// <summary>
    /// Simple spawn point group.
    /// - Place this in your scene and assign spawn points in the inspector.
    /// - The server will pick a spawn point based on clientId modulo count (deterministic).
    /// </summary>
    public sealed class NetSpawnPointGroup : MonoBehaviour
    {
        [Header("Context")]
        [SerializeField] private NetSpawnContext context = NetSpawnContext.Lobby;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] points;

        public NetSpawnContext Context => context;

        public bool TryGetSpawnPoint(ulong clientId, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            if (points == null || points.Length == 0)
                return false;

            int index = (int)(clientId % (ulong)points.Length);
            var t = points[index];

            if (t == null)
                return false;

            position = t.position;
            rotation = t.rotation;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (points == null)
                return;

            Gizmos.color = Color.green;
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (p == null)
                    continue;

                Gizmos.DrawWireSphere(p.position, 0.25f);
                Gizmos.DrawLine(p.position, p.position + p.forward * 0.75f);
            }
        }
#endif
    }
}