using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractableCore : MonoBehaviour, IInteractableState
{
    [Header("Validation")]
    [SerializeField] private float maxUseDistance = 3f;
    [SerializeField] private float cooldownSeconds = 0.10f;
    [SerializeField] private Transform interactionPointOverride;

    [Header("Bindings")]
    [SerializeField] private InteractionBinding[] bindings;

    [Serializable]
    public sealed class InteractionBinding
    {
        public InteractionVerb verb = InteractionVerb.Use;
        public InteractionActionCore[] actions;
    }

    [Header("Generic State (Standalone default)")]
    [SerializeField] private bool boolState = false;
    [SerializeField] private int intState = 0;
    [SerializeField] private float floatState = 0f;

    private double _lastUseTime;
    private Quaternion _cachedClosedLocalRotation;
    private bool _closedRotationCached;

    // Tween runner (generic, used by actions to animate without extra scripts on the object).
    private readonly Dictionary<int, Coroutine> _tweens = new Dictionary<int, Coroutine>(4);

    // --- IInteractableState ---
    public bool BoolState { get => boolState; set => boolState = value; }
    public int IntState { get => intState; set => intState = value; }
    public float FloatState { get => floatState; set => floatState = value; }

    private void Awake()
    {
        CacheClosedRotationIfNeeded();
    }

    public Vector3 GetInteractionPoint()
    {
        return interactionPointOverride != null ? interactionPointOverride.position : transform.position;
    }

    public Quaternion GetClosedLocalRotation()
    {
        CacheClosedRotationIfNeeded();
        return _cachedClosedLocalRotation;
    }

    private void CacheClosedRotationIfNeeded()
    {
        if (_closedRotationCached)
            return;

        _cachedClosedLocalRotation = transform.localRotation;
        _closedRotationCached = true;
    }

    /// <summary>
    /// Standalone (solo) execution. No Netcode involved.
    /// </summary>
    public bool TryUseLocal(Transform interactor, InteractionVerb verb)
    {
        return TryUseInternal(
            now: Time.timeAsDouble,
            interactor: interactor,
            interactorClientId: 0,
            verb: verb,
            state: this,
            isServerAuthoritative: false);
    }

    /// <summary>
    /// Shared execution used by both standalone and network adapters.
    /// The adapter supplies time and the state implementation.
    /// </summary>
    public bool TryUseInternal(
        double now,
        Transform interactor,
        ulong interactorClientId,
        InteractionVerb verb,
        IInteractableState state,
        bool isServerAuthoritative)
    {
        if (interactor == null || state == null)
            return false;

        // Cooldown (prevents spam).
        if (now - _lastUseTime < cooldownSeconds)
            return false;

        _lastUseTime = now;

        // Distance validation.
        float dist = Vector3.Distance(interactor.position, GetInteractionPoint());
        if (dist > maxUseDistance)
            return false;

        if (bindings == null || bindings.Length == 0)
            return false;

        var ctx = new InteractionContextCore(
            target: this,
            interactor: interactor,
            interactorClientId: interactorClientId,
            verb: verb,
            state: state,
            isServerAuthoritative: isServerAuthoritative);

        // Execute matching bindings.
        for (int i = 0; i < bindings.Length; i++)
        {
            InteractionBinding binding = bindings[i];
            if (binding == null || binding.verb != verb)
                continue;

            InteractionActionCore[] actions = binding.actions;
            if (actions == null)
                continue;

            for (int a = 0; a < actions.Length; a++)
            {
                InteractionActionCore action = actions[a];
                if (action == null)
                    continue;

                if (!action.CanExecute(in ctx))
                    continue;

                action.Execute(in ctx);
            }
        }

        return true;
    }

    /// <summary>
    /// Runs a coroutine and ensures only one coroutine per tweenKey is active at a time.
    /// Keeps runtime state on the GameObject, not in ScriptableObjects.
    /// </summary>
    public void RunTween(int tweenKey, IEnumerator routine)
    {
        if (routine == null)
            return;

        if (_tweens.TryGetValue(tweenKey, out Coroutine running) && running != null)
            StopCoroutine(running);

        Coroutine c = StartCoroutine(routine);
        _tweens[tweenKey] = c;
    }
}