using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DungeonSteakhouse.Net
{
    public sealed class NetDemoUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private GameObject menuRoot; // panel to hide once connected
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private NetGameRoot netGameRoot;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);

            if (netGameRoot != null)
            {
                netGameRoot.StateChanged += OnNetStateChanged;
                OnNetStateChanged(netGameRoot.State); // apply initial state
            }
        }

        private void OnDestroy()
        {
            if (hostButton != null) hostButton.onClick.RemoveListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinClicked);

            if (netGameRoot != null)
                netGameRoot.StateChanged -= OnNetStateChanged;
        }

        private void OnNetStateChanged(NetGameState state)
        {
            bool showMenu = state == NetGameState.Offline;

            if (menuRoot == null)
                return;

            menuRoot.SetActive(showMenu);

            // The menu's root Canvas (Screen Space - Overlay) renders on top of everything,
            // including the 3D game view. Hiding only this panel left the Canvas' other children
            // (background, logo) active and visible over Elevator/Taverne/Dungeon once connected.
            // Disable the whole Canvas GameObject, not just this panel.
            Transform canvasRoot = menuRoot.transform.root;
            if (canvasRoot != null && canvasRoot != menuRoot.transform)
                canvasRoot.gameObject.SetActive(showMenu);
        }

        private void Update()
        {
            if (statusText == null || netGameRoot == null)
                return;

            statusText.text = netGameRoot.GetDebugStatus();
        }

        private void OnHostClicked()
        {
            if (netGameRoot == null)
            {
                Debug.LogError("[NetDemoUI] NetGameRoot reference is missing.");
                return;
            }

            netGameRoot.Host();
        }

        private void OnJoinClicked()
        {
            // Your current join flow is "Join via Steam invite / overlay".
            // This button is intentionally not trying to auto-join a lobby.
            Steamworks.SteamFriends.OpenOverlay("friends");
        }
    }
}