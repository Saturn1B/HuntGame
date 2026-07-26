using System.Collections.Generic;
using System.Linq;
using Steamworks;

namespace DungeonSteakhouse.Net.Steam
{
    // No MonoBehaviour needed — SteamFriends.GetFriends() is a pure static call.
    public static class SteamFriendsListProvider
    {
        public static IEnumerable<Friend> GetOnlineFriends()
        {
            return SteamFriends.GetFriends().Where(f => f.IsOnline);
        }
    }
}
