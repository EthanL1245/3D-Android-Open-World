using UnityEngine;

/// <summary>One equipped assembly; only children of ReelMount receive reel curves.</summary>
public sealed class FishingRodView : MonoBehaviour
{
    [SerializeField] private Transform rodTip;
    [SerializeField] private Transform reelMount;
    [SerializeField] private Animation reelAnimation;
    [SerializeField] private AnimationClip reelClip;
    [SerializeField, Min(0f)] private float reelSpeed = 1f;
    private Quaternion restingRotation;
    private float reelTime;
    private bool initialized;

    public Transform RodTip => rodTip;

    public void Configure(Transform tip, Transform mount, Animation animation, AnimationClip clip)
    {
        rodTip = tip;
        reelMount = mount;
        reelAnimation = animation;
        reelClip = clip;
    }

    public void InitializePose()
    {
        restingRotation = transform.localRotation;
        initialized = true;
        ResetMotion();
    }

    // Called by FishingSystem after resolving the actual fight state, so catch,
    // break, release, and unequip all stop cranking in the same frame.
    public void TickReel(bool reeling, float deltaTime)
    {
        if (!initialized || reelAnimation == null || reelClip == null || reelClip.length <= 0f)
            return;
        if (reeling)
            reelTime = Mathf.Repeat(reelTime + Mathf.Max(0f, deltaTime) * reelSpeed, reelClip.length);
        SampleReel();
    }

    private void SampleReel()
    {
        if (reelAnimation == null || reelClip == null || !reelAnimation.gameObject.activeInHierarchy)
            return;
        if (!reelAnimation.IsPlaying(reelClip.name))
            reelAnimation.Play(reelClip.name);
        AnimationState clipState = reelAnimation[reelClip.name];
        if (clipState == null) return;
        clipState.speed = 0f;
        clipState.time = reelTime;
        reelAnimation.Sample();
    }

    public void SetCastPose(float progress)
    {
        if (!initialized) return;
        float t = Mathf.Clamp01(progress);
        float pitch = t < 0.25f
            ? Mathf.Lerp(0f, -48f, Mathf.SmoothStep(0f, 1f, t / 0.25f))
            : t < 0.48f
                ? Mathf.Lerp(-48f, 12f, Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.23f))
                : Mathf.Lerp(12f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.48f) / 0.52f));
        transform.localRotation = restingRotation * Quaternion.Euler(pitch, 0f, 0f);
    }

    public void ResetMotion()
    {
        if (initialized) transform.localRotation = restingRotation;
        reelTime = 0f;
        SampleReel();
    }

    private void OnDisable()
    {
        ResetMotion();
    }
}
