using System.Collections.Generic;
using UnityEngine;
using DungeonSteakhouse.Net.Steam;

namespace DungeonSteakhouse.Net.UI
{
    public sealed class FriendsListPanel : MonoBehaviour
    {
        [SerializeField] private Transform listContainer;
        [SerializeField] private FriendRowView rowPrefab;


        private readonly List<FriendRowView> _rows = new();

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            foreach (var row in _rows) Destroy(row.gameObject);
            _rows.Clear();
        }



        private void Refresh()
        {
            foreach (var friend in SteamFriendsListProvider.GetOnlineFriends())
            {
                var row = Instantiate(rowPrefab, listContainer);
                row.Setup(friend);
                row.Clicked += OnRowClicked;
                _rows.Add(row);
            }
        }

        private void OnRowClicked(FriendRowView row)
        {
            if (SteamInviteService.Instance == null)
            {
                Debug.LogWarning("[FriendsListPanel] SteamInviteService not ready yet.");
                return;
            }

            SteamInviteService.Instance.SendInvite(row.FriendId);
        }
    }
}