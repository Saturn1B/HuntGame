using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(InteractableCore))]
public sealed class NetInteractableAdapter : NetworkBehaviour, IInteractableState
{
    private readonly NetworkVariable<bool> _boolState = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> _intState = new NetworkVariable<int>(0);
    private readonly NetworkVariable<float> _floatState = new NetworkVariable<float>(0f);

    private InteractableCore _core;

    public bool BoolState
    {
        get => _boolState.Value;
        set
        {
            if (!IsServer) { Debug.LogError("[NetInteractableAdapter] BoolState can only be set on server."); return; }
            _boolState.Value = value;
        }
    }

    public int IntState
    {
        get => _intState.Value;
        set
        {
            if (!IsServer) { Debug.LogError("[NetInteractableAdapter] IntState can only be set on server."); return; }
            _intState.Value = value;
        }
    }

    public float FloatState
    {
        get => _floatState.Value;
        set
        {
            if (!IsServer) { Debug.LogError("[NetInteractableAdapter] FloatState can only be set on server."); return; }
            _floatState.Value = value;
        }
    }

    private void Awake()
    {
        _core = GetComponent<InteractableCore>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        // Initialize replicated state from the core's default standalone state.
        // This keeps the "default closed" door setup consistent in both solo and multiplayer.
        _boolState.Value = _core.BoolState;
        _intState.Value = _core.IntState;
        _floatState.Value = _core.FloatState;
    }

    // Same idea as your existing NetInteractable.TryUseServer(...) :contentReference[oaicite:0]{index=0}
    // Called by server-side code (typically from a player ServerRpc).
    public bool TryUseServer(NetworkObject interactor, ulong senderClientId, InteractionVerb verb)
    {
        if (!IsServer)
        {
            Debug.LogError("[NetInteractableAdapter] TryUseServer must run on the server.");
            return false;
        }

        if (interactor == null)
            return false;

        return _core.TryUseInternal(
            now: NetworkManager.ServerTime.Time,
            interactor: interactor.transform,
            interactorClientId: senderClientId,
            verb: verb,
            state: this, // IMPORTANT: actions write to replicated state
            isServerAuthoritative: true);
    }
}