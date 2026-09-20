using UnityEngine;

public class RedSnapperPresentation : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [Header("Authored Swim")]
    [SerializeField]
    private float bodyLengthsPerCycle = 0.92f;

    [SerializeField]
    private float minimumCruiseSpeed = 0.36f;

    [SerializeField]
    private float maximumCruiseSpeed = 0.56f;

    private bool held;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;
        ApplyState();
    }

    public void SetHeld(bool value)
    {
        held = value;
        ApplyState();
    }

    public void SetAquariumLocomotion(
        float worldSpeed)
    {
        // Authored animation timing stays fixed.
        // Tank locomotion matches itself to the clip instead.
    }

    public float GetRecommendedCruiseSpeed()
    {
        ResolveAnimator();

        float clipLength =
            GetSwimClipLength();

        const float normalizedBodyLength =
            0.92f;

        float desired =
            normalizedBodyLength *
            bodyLengthsPerCycle /
            Mathf.Max(
                0.10f,
                clipLength
            );

        return
            Mathf.Clamp(
                desired,
                minimumCruiseSpeed,
                maximumCruiseSpeed
            );
    }

    private void Awake()
    {
        ResolveAnimator();
        ApplyState();
    }

    private void OnEnable()
    {
        ResolveAnimator();
        ApplyState();
    }

    private void ResolveAnimator()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(
                    true
                );
        }
    }

    private float GetSwimClipLength()
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return 1f;
        }

        AnimationClip[] clips =
            animator
                .runtimeAnimatorController
                .animationClips;

        if (clips == null ||
            clips.Length == 0 ||
            clips[0] == null)
        {
            return 1f;
        }

        return
            Mathf.Max(
                0.10f,
                clips[0].length
            );
    }

    private void ApplyState()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;

        animator.speed =
            held
                ? 1.08f
                : 1.0f;
    }
}
