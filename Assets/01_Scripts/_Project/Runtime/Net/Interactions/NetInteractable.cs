using System;
using UnityEngine;
using Unity.Netcode;

public class NetInteractable : NetworkBehaviour
{
    [Header("Server Validation")]
    [SerializeField] private float maxUseDistance = 3f;
    [SerializeField] private float serverCooldownSeconds = 0.10f;
    [SerializeField] private Transform interactionPointOverride;

    [Header("Actions")]
    [SerializeField] private InteractionBinding[] bindings;

    [Serializable]
    public class InteractionBinding
    {
        public InteractionVerb verb = InteractionVerb.Use;
        public InteractionAction[] actions;
    }

    // Generic replicated state slots (optional but super practical).
    // Actions can use these without requiring extra scripts on the object.
    private readonly NetworkVariable<bool> _boolState = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<int> _intState = new NetworkVariable<int>(0);
    private readonly NetworkVariable<float> _floatState = new NetworkVariable<float>(0f);

    public NetworkVariable<bool> BoolState => _boolState;
    public NetworkVariable<int> IntState => _intState;
    public NetworkVariable<float> FloatState => _floatState;

    private double _lastUseServerTime;
    private Quaternion _cachedClosedLocalRotation;
    private bool _closedRotationCached;

    public override void OnNetworkSpawn()
    {
        // Cache the initial "closed" rotation so door-like actions can reuse it.
        _cachedClosedLocalRotation = transform.localRotation;
        _closedRotationCached = true;
    }

    public Quaternion GetClosedLocalRotation()
    {
        if (!_closedRotationCached)
        {
            _cachedClosedLocalRotation = transform.localRotation;
            _closedRotationCached = true;
        }

        return _cachedClosedLocalRotation;
    }

    public Vector3 GetInteractionPoint()
    {
        return interactionPointOverride != null ? interactionPointOverride.position : transform.position;
    }

    // Called by the server (from the player's ServerRpc).
    public bool TryUseServer(NetworkObject interactor, ulong senderClientId, InteractionVerb verb)
    {
        if (!IsServer)
        {
            Debug.LogError("TryUseServer must run on the server.");
            return false;
        }

        if (interactor == null)
            return false;

        // Cooldown (server-side anti-spam).
        double now = NetworkManager.ServerTime.Time;
        if (now - _lastUseServerTime < serverCooldownSeconds)
            return false;

        _lastUseServerTime = now;

        // Distance validation (server authoritative).
        float dist = Vector3.Distance(interactor.transform.position, GetInteractionPoint());
        if (dist > maxUseDistance)
        {
            Debug.LogWarning($"[Server] Interaction denied (too far). Sender={senderClientId}, Dist={dist:0.00}");
            return false;
        }

        var ctx = new InteractionContext(this, interactor, senderClientId, verb);

        // Execute matching bindings.
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].verb != verb)
                continue;

            var actions = bindings[i].actions;
            if (actions == null)
                continue;

            for (int a = 0; a < actions.Length; a++)
            {
                InteractionAction action = actions[a];
                if (action == null)
                    continue;

                if (!action.CanExecute(in ctx))
                    continue;

                action.Execute(in ctx);
            }
        }

        return true;
    }
}