using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using SteamImage = Steamworks.Data.Image;

namespace DungeonSteakhouse.Net.UI
{
    public sealed class FriendRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Button selectButton;

        public SteamId FriendId { get; private set; }
        public event Action<FriendRowView> Clicked;

        private void Awake() => selectButton.onClick.AddListener(() => Clicked?.Invoke(this));

        public void Setup(Friend friend)
        {
            FriendId = friend.Id;
            nameText.text = friend.Name;
            _ = LoadAvatarAsync(friend);
        }

        private async Task LoadAvatarAsync(Friend friend)
        {
            SteamImage? image = await friend.GetMediumAvatarAsync();
            if (image == null || avatarImage == null)
                return;

            var tex = new Texture2D((int)image.Value.Width, (int)image.Value.Height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(image.Value.Data);
            tex.Apply();

            avatarImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
}