using UnityEngine;
using Unity.Netcode;
using DungeonSteakhouse.Net.Flow;

namespace DungeonSteakhouse.Net.Connection
{
    public sealed class NetConnectionApproval : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private NetGameConfig config;

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetSceneFlow sceneFlow;

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

            if (!string.IsNullOrWhiteSpace(config.buildVersion) &&
                !string.IsNullOrWhiteSpace(payload.buildVersion) &&
                payload.buildVersion != config.buildVersion)
            {
                response.Reason = $"Build mismatch (server={config.buildVersion}, client={payload.buildVersion}).";
                return;
            }

            // 2) Max players gate (ConnectedClientsIds includes server)
            if (networkManager != null && networkManager.ConnectedClientsIds != null)
            {
                if (networkManager.ConnectedClientsIds.Count >= config.maxPlayers)
                {
                    response.Reason = "Lobby is full.";
                    return;
                }
            }

            // 3) No late-join (unless explicitly allowed)
            var inRun = (sceneFlow != null && sceneFlow.Phase == NetRunPhase.InRun);
            if (inRun && !config.allowLateJoin)
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

            if (sceneFlow == null)
                Debug.LogWarning("[NetConnectionApproval] NetSceneFlow is not assigned (late-join gate will be weaker).");
        }
    }
}