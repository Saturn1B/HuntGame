using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using DungeonSteakhouse.Net.Players;

namespace DungeonSteakhouse.Net.UI
{
    public sealed class NetLobbyRosterTMP : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetGameRoot netGameRoot;

        [Header("UI (TMP)")]
        [SerializeField] private TMP_Text rosterText;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonLabel;

        [SerializeField] private Button startButton; // Host only (placeholder)
        [SerializeField] private TMP_Text startButtonLabel;

        private void Awake()
        {
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyClicked);

            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnEnable()
        {
            if (netGameRoot != null && netGameRoot.PlayerRegistry != null)
            {
                netGameRoot.PlayerRegistry.PlayerAdded += OnRegistryChanged;
                netGameRoot.PlayerRegistry.PlayerRemoved += OnRegistryChanged;
                netGameRoot.PlayerRegistry.PlayerUpdated += OnRegistryChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (netGameRoot != null && netGameRoot.PlayerRegistry != null)
            {
                netGameRoot.PlayerRegistry.PlayerAdded -= OnRegistryChanged;
                netGameRoot.PlayerRegistry.PlayerRemoved -= OnRegistryChanged;
                netGameRoot.PlayerRegistry.PlayerUpdated -= OnRegistryChanged;
            }
        }

        private void OnRegistryChanged(NetPlayer _)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (netGameRoot == null || netGameRoot.PlayerRegistry == null || rosterText == null)
                return;

            var sb = new StringBuilder(256);

            // NOTE: ServerClientId is static in NGO (usually 0).
            var serverId = NetworkManager.ServerClientId;

            foreach (var p in netGameRoot.PlayerRegistry.Players)
            {
                if (p == null)
                    continue;

                var isHost = (p.OwnerClientId == serverId);

                sb.Append(isHost ? "[HOST] " : "       ");
                sb.Append(p.DisplayName);
                sb.Append(p.IsReady ? "  ✓ Ready" : "  … Not ready");
                sb.AppendLine();
            }

            rosterText.text = sb.ToString();

            var localPlayer = GetLocalPlayer();
            if (readyButtonLabel != null)
                readyButtonLabel.text = (localPlayer != null && localPlayer.IsReady) ? "Unready" : "Ready";

            var isLocalHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

            if (startButton != null)
                startButton.interactable = isLocalHost && netGameRoot.PlayerRegistry.AreAllReady;

            if (startButtonLabel != null)
                startButtonLabel.text = "Start (Host)";
        }

        private void OnReadyClicked()
        {
            var localPlayer = GetLocalPlayer();
            if (localPlayer == null)
                return;

            localPlayer.ToggleReady();
        }

        private void OnStartClicked()
        {
            // Placeholder: next step will hook this into additive scene loading (Tavern -> Dungeon).
            Debug.Log("[NetLobbyRosterTMP] Start requested (placeholder).");
        }

        private static NetPlayer GetLocalPlayer()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null)
                return null;

            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj == null)
                return null;

            return playerObj.GetComponent<NetPlayer>();
        }
    }
}