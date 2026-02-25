using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DungeonSteakhouse.Net
{
    public sealed class NetDemoUI : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private NetGameRoot netGameRoot;

        [Header("UI")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;

        [SerializeField] private TextMeshProUGUI statusText; // Optional (legacy UI Text)

        private void Awake()
        {
            if (hostButton != null)
                hostButton.onClick.AddListener(OnHostClicked);

            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);
        }

        private void OnDestroy()
        {
            if (hostButton != null)
                hostButton.onClick.RemoveListener(OnHostClicked);

            if (joinButton != null)
                joinButton.onClick.RemoveListener(OnJoinClicked);
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
            Debug.Log("[NetDemoUI] To join: accept a Steam invite or use Steam overlay to join a friend.");
        }
    }
}