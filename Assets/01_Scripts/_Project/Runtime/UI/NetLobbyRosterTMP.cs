using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using DungeonSteakhouse.Net.Players;
using DungeonSteakhouse.Net.Flow;
using DungeonSteakhouse.Net.Session;

namespace DungeonSteakhouse.Net.UI
{
    public sealed class NetLobbyRosterTMP : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetGameRoot netGameRoot;
        [SerializeField] private NetSceneFlow sceneFlow;
        [SerializeField] private NetGameSession gameSession;

        [Header("Debug UI")]
        [Tooltip("If false, the UI is read-only (no Ready/Start buttons). Useful when using platform-based ready.")]
        [SerializeField] private bool enableDebugButtons = true;

        [Header("UI (TMP)")]
        [SerializeField] private TMP_Text rosterText;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonLabel;

        [SerializeField] private Button startButton; // Host only
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

            if (gameSession == null)
                gameSession = NetGameSession.Instance;

            ApplyButtonVisibility();
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

        private void ApplyButtonVisibility()
        {
            if (readyButton != null)
                readyButton.gameObject.SetActive(enableDebugButtons);

            if (startButton != null)
                startButton.gameObject.SetActive(enableDebugButtons);
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

            bool canStart = false;
            if (gameSession != null)
                canStart = gameSession.CanStartRun();
            else
                canStart = netGameRoot.PlayerRegistry.AreAllReady;

            if (startButton != null)
                startButton.interactable = enableDebugButtons && isLocalHost && canStart;

            if (startButtonLabel != null)
                startButtonLabel.text = "Start Run (Host)";
        }

        private void OnReadyClicked()
        {
            if (!enableDebugButtons)
                return;

            var localPlayer = GetLocalPlayer();
            if (localPlayer == null)
                return;

            localPlayer.ToggleReady();
        }

        private void OnStartClicked()
        {
            if (!enableDebugButtons)
                return;

            if (gameSession == null)
                gameSession = NetGameSession.Instance;

            if (gameSession != null)
            {
                gameSession.RequestStartRun();
                return;
            }

            if (sceneFlow == null)
            {
                Debug.LogError("[NetLobbyRosterTMP] Missing NetSceneFlow reference.");
                return;
            }

            if (!sceneFlow.TryStartRun())
                Debug.LogWarning("[NetLobbyRosterTMP] StartRun failed (check console for details).");
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