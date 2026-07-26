using System.Collections.Generic;
using UnityEngine;

namespace HuntingGame.ProceduralGeneration
{
    /// <summary>
    /// Single responsibility: maintain a pool of inactive "ghost" room instances (one per
    /// RoomData) used to test placement/overlap before a room is actually spawned for real.
    /// Extracted out of DungeonGenerator.
    /// </summary>
    public sealed class DungeonGhostPool
    {
        private readonly Dictionary<RoomData, Room> _ghostPool = new();

        public void EnsureSetup(RoomData[] roomLibrary)
        {
            if (_ghostPool.Count > 0) return;

            foreach (var data in roomLibrary)
            {
                GameObject ghostObj = Object.Instantiate(data.roomPrefab);
                ghostObj.name = $"Ghost_{data.name}";
                ghostObj.SetActive(false);

                _ghostPool.Add(data, ghostObj.GetComponent<Room>());
            }
        }

        public Room GetGhost(RoomData data)
        {
            Room ghost = _ghostPool[data];
            ghost.gameObject.SetActive(true);
            return ghost;
        }
    }
}
