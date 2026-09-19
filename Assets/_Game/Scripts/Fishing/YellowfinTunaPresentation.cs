using UnityEngine;

public class YellowfinTunaPresentation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float swimAnimationSpeed = 1.0f;
    [SerializeField] private float heldAnimationSpeed = 1.35f;

    private bool held;

    public void Configure(Animator targetAnimator)
    {
        animator = targetAnimator;
        ApplySpeed();
    }

    public void SetHeld(bool value)
    {
        held = value;
        ApplySpeed();
    }

    private void OnEnable()
    {
        ApplySpeed();
    }

    private void ApplySpeed()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        animator.speed =
            held
                ? heldAnimationSpeed
                : swimAnimationSpeed;
    }
}
