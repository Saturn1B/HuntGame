using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonSteakhouse.Net.Players
{
    public sealed class NetPlayerRegistry : MonoBehaviour
    {
        public event Action<NetPlayer> PlayerAdded;
        public event Action<NetPlayer> PlayerRemoved;
        public event Action<NetPlayer> PlayerUpdated;

        private readonly List<NetPlayer> _players = new();

        public IReadOnlyList<NetPlayer> Players => _players;

        public bool AreAllReady =>
            _players.Count > 0 && _players.All(p => p != null && p.IsReady);

        public void Register(NetPlayer player)
        {
            if (player == null)
                return;

            if (_players.Contains(player))
                return;

            _players.Add(player);
            PlayerAdded?.Invoke(player);
        }

        public void Unregister(NetPlayer player)
        {
            if (player == null)
                return;

            if (_players.Remove(player))
                PlayerRemoved?.Invoke(player);
        }

        public void NotifyPlayerUpdated(NetPlayer player)
        {
            if (player == null)
                return;

            if (_players.Contains(player))
                PlayerUpdated?.Invoke(player);
        }
    }
}