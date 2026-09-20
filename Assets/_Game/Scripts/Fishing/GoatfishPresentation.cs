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

        // Match stroke cadence to actual tank travel.
        // At our normal 0.43-0.53 m/s cruise this is roughly 0.68-0.82x.
        animator.speed =
            Mathf.Lerp(
                0.60f,
                0.88f,
                Mathf.InverseLerp(
                    0.28f,
                    0.62f,
                    aquariumLocomotionSpeed
                )
            );
    }
}
