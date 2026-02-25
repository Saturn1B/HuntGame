using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using DungeonSteakhouse.Net.Core;

namespace DungeonSteakhouse.Net.Players
{
    public sealed class NetPlayer : NetworkBehaviour
    {
        // Server-written, everyone-read.
        private readonly NetworkVariable<ulong> _platformUserId =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString64Bytes> _displayName =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isReady =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public ulong PlatformUserId => _platformUserId.Value;
        public string DisplayName => _displayName.Value.ToString();
        public bool IsReady => _isReady.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            NetGameRoot.Instance?.PlayerRegistry?.Register(this);

            _platformUserId.OnValueChanged += OnAnyValueChanged;
            _displayName.OnValueChanged += OnAnyValueChanged;
            _isReady.OnValueChanged += OnAnyValueChanged;

            if (IsOwner)
            {
                // Submit identity once we spawn.
                TrySubmitLocalIdentity();
            }
        }

        public override void OnNetworkDespawn()
        {
            _platformUserId.OnValueChanged -= OnAnyValueChanged;
            _displayName.OnValueChanged -= OnAnyValueChanged;
            _isReady.OnValueChanged -= OnAnyValueChanged;

            NetGameRoot.Instance?.PlayerRegistry?.Unregister(this);

            base.OnNetworkDespawn();
        }

        public void ToggleReady()
        {
            if (!IsOwner)
                return;

            SetReadyServerRpc(!IsReady);
        }

        private void TrySubmitLocalIdentity()
        {
            var root = NetGameRoot.Instance;
            var provider = root != null ? root.IdentityProvider : null;

            if (provider != null && provider.TryGetLocalIdentity(out NetLocalIdentity identity))
            {
                SubmitIdentityServerRpc(identity.PlatformUserId, identity.DisplayName);
            }
            else
            {
                // Fallback for editor / non-steam runs
                SubmitIdentityServerRpc(0, $"Player {OwnerClientId}");
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void SubmitIdentityServerRpc(ulong platformUserId, string displayName)
        {
            // Basic sanitization
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = $"Player {OwnerClientId}";

            if (displayName.Length > 48)
                displayName = displayName.Substring(0, 48);

            _platformUserId.Value = platformUserId;
            _displayName.Value = displayName;
        }

        [ServerRpc(RequireOwnership = true)]
        private void SetReadyServerRpc(bool ready)
        {
            _isReady.Value = ready;
        }

        private void OnAnyValueChanged<T>(T previousValue, T newValue)
        {
            NetGameRoot.Instance?.PlayerRegistry?.NotifyPlayerUpdated(this);
        }
    }
}