using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonSteakhouse/Interaction/Actions Core/Door Toggle Rotate")]
public sealed class DoorToggleRotateActionCore : InteractionActionCore
{
    [Header("Target Transform")]
    [Tooltip("Optional child path to rotate. Leave empty to rotate the InteractableCore transform.")]
    [SerializeField] private string targetChildPath = "";

    [Header("Rotation")]
    [Tooltip("Local euler offset added to the cached 'closed' local rotation.")]
    [SerializeField] private Vector3 openLocalEulerOffset = new Vector3(0f, 90f, 0f);

    [Tooltip("Seconds to reach target rotation.")]
    [SerializeField, Range(0.05f, 5f)] private float duration = 0.35f;

    [Tooltip("Easing curve for the rotation animation.")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("State")]
    [Tooltip("If true: open when BoolState == true. If false: inverted logic.")]
    [SerializeField] private bool openWhenBoolTrue = true;

    public override void Execute(in InteractionContextCore ctx)
    {
        if (ctx.Target == null || ctx.State == null)
            return;

        // Toggle the replicated/local state
        ctx.State.BoolState = !ctx.State.BoolState;

        // Compute target rotations from the cached closed rotation
        Quaternion closed = ctx.Target.GetClosedLocalRotation();
        Quaternion open = closed * Quaternion.Euler(openLocalEulerOffset);

        bool isOpen = openWhenBoolTrue ? ctx.State.BoolState : !ctx.State.BoolState;
        Quaternion target = isOpen ? open : closed;

        Transform targetTransform = ResolveTargetTransform(ctx.Target, targetChildPath);
        if (targetTransform == null)
            return;

        // Run animation on the target (no extra component on the door)
        ctx.Target.RunTween(tweenKey: GetInstanceID(), routine: RotateLocalRoutine(targetTransform, target, duration, ease));
    }

    private static Transform ResolveTargetTransform(InteractableCore core, string childPath)
    {
        if (core == null)
            return null;

        if (string.IsNullOrWhiteSpace(childPath))
            return core.transform;

        Transform t = core.transform.Find(childPath);
        if (t == null)
            Debug.LogError($"[DoorToggleRotateActionCore] Child path not found: '{childPath}' on '{core.name}'", core);

        return t;
    }

    private static IEnumerator RotateLocalRoutine(Transform target, Quaternion targetRot, float duration, AnimationCurve ease)
    {
        Quaternion start = target.localRotation;

        if (duration <= 0.001f)
        {
            target.localRotation = targetRot;
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t01 = Mathf.Clamp01(time / duration);
            float eased = ease != null ? ease.Evaluate(t01) : t01;

            target.localRotation = Quaternion.Slerp(start, targetRot, eased);
            yield return null;
        }

        target.localRotation = targetRot;
    }
}