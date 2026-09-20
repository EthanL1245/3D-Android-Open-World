using UnityEngine;

public sealed class MackerelPresentation : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;
        ApplyState();
    }

    public void SetHeld(
        bool held)
    {
        ApplyState();
    }

    public void SetAquariumLocomotion(
        float worldSpeed)
    {
        // Keep the supplied authored animation at its intended timing.
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

    private void ApplyState()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        animator.speed = 1f;
    }
}
