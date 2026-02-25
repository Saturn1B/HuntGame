using UnityEngine;
using Steamworks;
using DungeonSteakhouse.Net.Core;

namespace DungeonSteakhouse.Net.Steam
{
    public sealed class SteamIdentityProvider : MonoBehaviour, INetIdentityProvider
    {
        public bool TryGetLocalIdentity(out NetLocalIdentity identity)
        {
            if (!SteamBootstrap.Ready)
            {
                identity = default;
                return false;
            }

            identity = new NetLocalIdentity(
                platformUserId: (ulong)SteamClient.SteamId,
                displayName: SteamClient.Name
            );
            return true;
        }
    }
}