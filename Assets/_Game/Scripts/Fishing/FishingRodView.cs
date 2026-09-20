using UnityEngine;

/// <summary>One equipped assembly; only children of ReelMount receive reel curves.</summary>
public sealed class FishingRodView : MonoBehaviour
{
    [SerializeField] private Transform rodTip;
    [SerializeField] private Transform reelMount;
    [SerializeField] private Animation reelAnimation;
    [SerializeField] private AnimationClip reelClip;
    [SerializeField, Min(0f)] private float reelSpeed = 2f;
    [SerializeField] private Transform rodBendRoot;
    [SerializeField, Range(0f, 35f)] private float maximumBendDegrees = 22f;

    private Quaternion restingRotation;
    private Quaternion rodBendRestingRotation;
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

        if (rodBendRoot == null)
        {
            rodBendRoot =
                transform.Find("Rod");
        }

        if (rodBendRoot != null)
        {
            rodBendRestingRotation =
                rodBendRoot.localRotation;
        }

        // Existing saved prefabs may still serialize the old 1x value.
        // Upgrade them at runtime without requiring a prefab reinstall.
        reelSpeed =
            Mathf.Max(
                2f,
                reelSpeed
            );

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

    public void SetLinePull(
        Vector3 bobberWorldPosition,
        float tension,
        float deltaTime)
    {
        if (!initialized ||
            rodBendRoot == null ||
            rodTip == null)
        {
            return;
        }

        Vector3 pullWorld =
            bobberWorldPosition -
            rodTip.position;

        if (pullWorld.sqrMagnitude <
            0.0001f)
        {
            RelaxLinePull(deltaTime);
            return;
        }

        Vector3 pullLocal =
            transform.InverseTransformDirection(
                pullWorld.normalized
            );

        Vector3 restingAxis =
            rodBendRestingRotation *
            Vector3.up;

        Quaternion fullCorrection =
            Quaternion.FromToRotation(
                restingAxis,
                pullLocal
            );

        float bendAmount =
            Mathf.Lerp(
                0.28f,
                1f,
                Mathf.Clamp01(tension)
            );

        Quaternion limitedCorrection =
            Quaternion.RotateTowards(
                Quaternion.identity,
                fullCorrection,
                maximumBendDegrees *
                bendAmount
            );

        Quaternion target =
            limitedCorrection *
            rodBendRestingRotation;

        float smoothing =
            1f -
            Mathf.Exp(
                -12f *
                Mathf.Max(
                    0f,
                    deltaTime
                )
            );

        rodBendRoot.localRotation =
            Quaternion.Slerp(
                rodBendRoot.localRotation,
                target,
                smoothing
            );
    }

    public void RelaxLinePull(
        float deltaTime)
    {
        if (!initialized ||
            rodBendRoot == null)
        {
            return;
        }

        float smoothing =
            1f -
            Mathf.Exp(
                -10f *
                Mathf.Max(
                    0f,
                    deltaTime
                )
            );

        rodBendRoot.localRotation =
            Quaternion.Slerp(
                rodBendRoot.localRotation,
                rodBendRestingRotation,
                smoothing
            );
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
        if (initialized)
        {
            transform.localRotation =
                restingRotation;

            if (rodBendRoot != null)
            {
                rodBendRoot.localRotation =
                    rodBendRestingRotation;
            }
        }

        reelTime = 0f;
        SampleReel();
    }

    private void OnDisable()
    {
        ResetMotion();
    }
}
