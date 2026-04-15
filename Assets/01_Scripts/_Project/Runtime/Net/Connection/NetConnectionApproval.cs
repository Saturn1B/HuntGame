using UnityEngine;
using Unity.Netcode;
using DungeonSteakhouse.Net.Session;

namespace DungeonSteakhouse.Net.Connection
{
    public sealed class NetConnectionApproval : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private NetGameConfig config;

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        private void Awake()
        {
            ValidateReferences();

            if (networkManager != null && networkManager.NetworkConfig != null)
            {
                // Enable connection approval (server-side).
                networkManager.NetworkConfig.ConnectionApproval = true;
            }
        }

        private void OnEnable()
        {
            if (networkManager != null)
                networkManager.ConnectionApprovalCallback += ApprovalCheck;
        }

        private void OnDisable()
        {
            if (networkManager != null)
                networkManager.ConnectionApprovalCallback -= ApprovalCheck;
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Defaults
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;

            if (config == null)
            {
                response.Reason = "Server configuration missing.";
                return;
            }

            // 1) Build version gate (payload)
            if (!NetConnectionPayloadCodec.TryDecode(request.Payload, out var payload))
            {
                response.Reason = "Missing connection payload.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(config.BuildVersion) &&
                !string.IsNullOrWhiteSpace(payload.buildVersion) &&
                payload.buildVersion != config.BuildVersion)
            {
                response.Reason = $"Build mismatch (server={config.BuildVersion}, client={payload.buildVersion}).";
                return;
            }

            // 2) Max players gate (ConnectedClientsIds includes server)
            if (networkManager != null && networkManager.ConnectedClientsIds != null)
            {
                if (networkManager.ConnectedClientsIds.Count >= config.MaxPlayers)
                {
                    response.Reason = "Lobby is full.";
                    return;
                }
            }

            // 3) No late-join (unless explicitly allowed)
            var inRun = NetSessionManager.Instance?.State == NetSessionState.InRun;
            if (inRun && !config.AllowLateJoin)
            {
                response.Reason = "Run already started (no late join).";
                return;
            }

            // Approved
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Reason = string.Empty;
        }

        private void ValidateReferences()
        {
            if (config == null)
                Debug.LogWarning("[NetConnectionApproval] NetGameConfig is not assigned.");

            if (networkManager == null)
                networkManager = NetworkManager.Singleton;

            if (networkManager == null)
                Debug.LogError("[NetConnectionApproval] NetworkManager is missing.");
        }
    }
}