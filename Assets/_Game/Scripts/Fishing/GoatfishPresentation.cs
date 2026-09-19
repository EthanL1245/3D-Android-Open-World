using UnityEngine;

public class GoatfishPresentation : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [Header("Animation")]
    [SerializeField]
    private float aquariumSpeed = 1.0f;

    [SerializeField]
    private float heldSpeed = 1.28f;

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

    private void Awake()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(
                    true
                );
        }

        ApplyState();
    }

    private void OnEnable()
    {
        ApplyState();
    }

    private void ApplyState()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;

        animator.speed =
            held
                ? heldSpeed
                : aquariumSpeed;
    }
}
