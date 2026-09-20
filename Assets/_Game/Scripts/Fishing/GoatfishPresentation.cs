using UnityEngine;

public class GoatfishPresentation : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    private bool held;
    private float aquariumLocomotionSpeed = 0.48f;

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
        aquariumLocomotionSpeed =
            Mathf.Max(
                0f,
                worldSpeed
            );

        if (!held)
        {
            ApplyState();
        }
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

        if (held)
        {
            // Alive and active in the player's hands,
            // but not the very fast cycle used previously.
            animator.speed = 1.02f;
            return;
        }

        // The authored cycle looked too frantic. Keep it slower even
        // though the fish now travels about twice as fast through the tank.
        animator.speed =
            Mathf.Lerp(
                0.62f,
                0.78f,
                Mathf.InverseLerp(
                    0.70f,
                    1.05f,
                    aquariumLocomotionSpeed
                )
            );
    }
}
