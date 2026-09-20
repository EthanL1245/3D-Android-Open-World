using UnityEngine;

public class GoatfishPresentation : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [Header("Authored Swim")]
    [SerializeField]
    private float bodyLengthsPerCycle = 1.0f;

    [SerializeField]
    private float minimumCruiseSpeed = 0.82f;

    [SerializeField]
    private float maximumCruiseSpeed = 1.18f;

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
        // Locomotion no longer changes animation playback.
        // The authored clip always runs at its intended 1.0x speed.
    }

    public float GetRecommendedCruiseSpeed()
    {
        ResolveAnimator();

        float clipLength =
            GetSwimClipLength();

        float fishBodyLength =
            0.92f;

        float desired =
            fishBodyLength *
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

        // Use the supplied animation exactly at authored timing.
        animator.speed = 1.0f;
    }
}
