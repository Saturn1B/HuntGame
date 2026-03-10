using UnityEngine;

[DisallowMultipleComponent]
public sealed class MouthBlendshapeTalkAnimator : MonoBehaviour
{
    [Header("Renderer")]
    [Tooltip("SkinnedMeshRenderer that contains the mouth blend shapes.")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

    [Header("Blend Shape Names")]
    [SerializeField] private string mouthCloseBlendShapeName = "mouth_close.didy1";
    [SerializeField] private string mouthOpenBlendShapeName = "mouth_open.body2";

    [Header("Silent Weights")]
    [SerializeField, Range(0f, 100f)] private float silentCloseWeight = 100f;
    [SerializeField, Range(0f, 100f)] private float silentOpenWeight = 0f;

    [Header("Talking Weights (Extremes)")]
    [SerializeField, Range(0f, 100f)] private float talkingCloseWeight = 0f;
    [SerializeField, Range(0f, 100f)] private float talkingOpenWeight = 100f;

    [Header("Talking Animation (Loop)")]
    [Tooltip("If true, mouth will oscillate while talking to simulate speech.")]
    [SerializeField] private bool animateWhileTalking = true;

    [Tooltip("Minimum open weight while talking (prevents mouth from staying fully closed).")]
    [SerializeField, Range(0f, 100f)] private float talkOpenMin = 25f;

    [Tooltip("Maximum open weight while talking.")]
    [SerializeField, Range(0f, 100f)] private float talkOpenMax = 100f;

    [Tooltip("Oscillation frequency while talking.")]
    [SerializeField, Range(0.1f, 20f)] private float talkFrequencyHz = 7f;

    [Header("Ping Talking (SteamVoiceSpeaker compatible)")]
    [Tooltip("How long we keep talking after the last PingTalking() call (seconds).")]
    [SerializeField, Range(0.05f, 1f)] private float pingHoldSeconds = 0.20f;

    [Header("Smoothing")]
    [Tooltip("Higher = faster response.")]
    [SerializeField, Range(1f, 60f)] private float smoothSpeed = 18f;

    private int _closeIndex = -1;
    private int _openIndex = -1;

    private bool _isTalking;
    private bool _hasTalkLevel;
    private float _talkLevel01;

    private float _lastPingTime = -999f;

    private float _currentClose;
    private float _currentOpen;

    private void Reset()
    {
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
    }

    private void Awake()
    {
        if (skinnedMeshRenderer == null)
        {
            Debug.LogError("[MouthBlendshapeTalkAnimator] Missing SkinnedMeshRenderer reference.", this);
            enabled = false;
            return;
        }

        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("[MouthBlendshapeTalkAnimator] SkinnedMeshRenderer has no sharedMesh.", this);
            enabled = false;
            return;
        }

        _closeIndex = mesh.GetBlendShapeIndex(mouthCloseBlendShapeName);
        _openIndex = mesh.GetBlendShapeIndex(mouthOpenBlendShapeName);

        if (_closeIndex < 0)
            Debug.LogError($"[MouthBlendshapeTalkAnimator] Blend shape not found: '{mouthCloseBlendShapeName}'", this);

        if (_openIndex < 0)
            Debug.LogError($"[MouthBlendshapeTalkAnimator] Blend shape not found: '{mouthOpenBlendShapeName}'", this);

        // Initialize to silent pose.
        _currentClose = silentCloseWeight;
        _currentOpen = silentOpenWeight;
        ApplyWeights(_currentClose, _currentOpen);
    }

    private void Update()
    {
        float openness01 = ComputeOpenness01();

        float targetClose = Mathf.Lerp(silentCloseWeight, talkingCloseWeight, openness01);
        float targetOpen = Mathf.Lerp(silentOpenWeight, talkingOpenWeight, openness01);

        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        _currentClose = Mathf.Lerp(_currentClose, targetClose, t);
        _currentOpen = Mathf.Lerp(_currentOpen, targetOpen, t);

        ApplyWeights(_currentClose, _currentOpen);
    }

    private float ComputeOpenness01()
    {
        bool pingTalking = (Time.unscaledTime - _lastPingTime) <= pingHoldSeconds;
        bool effectiveTalking = _isTalking || pingTalking;

        if (!effectiveTalking)
            return 0f;

        // If an external system provides an amplitude (0..1), it drives the mouth directly.
        if (_hasTalkLevel)
        {
            float openWeight = Mathf.Lerp(talkOpenMin, talkOpenMax, Mathf.Clamp01(_talkLevel01));
            return Mathf.Clamp01(openWeight / 100f);
        }

        // Otherwise, we either oscillate (talking animation) or stay fully open.
        if (!animateWhileTalking)
            return 1f;

        float wave01 = 0.5f + 0.5f * Mathf.Sin(Time.time * (Mathf.PI * 2f) * talkFrequencyHz);
        float open = Mathf.Lerp(talkOpenMin, talkOpenMax, wave01);
        return Mathf.Clamp01(open / 100f);
    }

    private void ApplyWeights(float closeWeight, float openWeight)
    {
        if (skinnedMeshRenderer == null)
            return;

        if (_closeIndex >= 0)
            skinnedMeshRenderer.SetBlendShapeWeight(_closeIndex, closeWeight);

        if (_openIndex >= 0)
            skinnedMeshRenderer.SetBlendShapeWeight(_openIndex, openWeight);
    }

    // --- Public API ---

    /// <summary>
    /// Call this whenever voice data is received for this player (e.g., from SteamVoiceSpeaker.PushSamples()).
    /// This keeps the mouth in "talking" state for pingHoldSeconds.
    /// </summary>
    public void PingTalking()
    {
        _lastPingTime = Time.unscaledTime;
    }

    /// <summary>
    /// Optional manual talk state. PingTalking() still works even if you never call this.
    /// </summary>
    public void SetTalking(bool isTalking)
    {
        _isTalking = isTalking;

        if (!isTalking)
        {
            _hasTalkLevel = false;
            _talkLevel01 = 0f;
        }
    }

    /// <summary>
    /// Optional amplitude (0..1). If used, it overrides the looping animation while talking.
    /// </summary>
    public void SetTalkLevel01(float level01)
    {
        _hasTalkLevel = true;
        _talkLevel01 = Mathf.Clamp01(level01);

        // This line is optional, but convenient if you want auto-talking from amplitude.
        _isTalking = _talkLevel01 > 0.02f;
    }

    private void OnValidate()
    {
        if (talkOpenMax < talkOpenMin)
            talkOpenMax = talkOpenMin;

        if (pingHoldSeconds < 0.05f)
            pingHoldSeconds = 0.05f;
    }
}