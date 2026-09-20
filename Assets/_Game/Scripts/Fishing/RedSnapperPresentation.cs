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

    private Transform headBone;
    private Transform nextBodyBone;
    private Transform tailBone;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;
        ResolveBones();
        ApplyState();
    }

    public bool TryGetHeadForward(
        Transform referenceSpace,
        out Vector3 direction)
    {
        ResolveBones();

        Vector3 worldDirection =
            Vector3.zero;

        // Prefer the front-most body segment. This represents the direction
        // the head is actually pointing and is not distorted by tail wag.
        if (headBone != null &&
            nextBodyBone != null)
        {
            worldDirection =
                headBone.position -
                nextBodyBone.position;
        }
        else if (headBone != null &&
                 tailBone != null)
        {
            worldDirection =
                headBone.position -
                tailBone.position;
        }

        if (worldDirection.sqrMagnitude <
            0.000001f)
        {
            direction = Vector3.forward;
            return false;
        }

        if (referenceSpace != null)
        {
            direction =
                referenceSpace
                    .InverseTransformDirection(
                        worldDirection
                    )
                    .normalized;
        }
        else
        {
            direction =
                worldDirection.normalized;
        }

        return true;
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
        ResolveBones();
        ApplyState();
    }

    private void OnEnable()
    {
        ResolveAnimator();
        ResolveBones();
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

    private void ResolveBones()
    {
        if (headBone != null &&
            (
                nextBodyBone != null ||
                tailBone != null
            ))
        {
            return;
        }

        Transform[] bones =
            GetComponentsInChildren<Transform>(
                true
            );

        headBone =
            FindBone(
                bones,
                "Bone.001"
            );

        nextBodyBone =
            FindBone(
                bones,
                "Bone.002"
            );

        tailBone =
            FindBone(
                bones,
                "Bone.004"
            );

        if (headBone == null)
        {
            headBone =
                FindBone(
                    bones,
                    "Bone"
                );

            nextBodyBone =
                FindBone(
                    bones,
                    "Bone.001"
                );
        }
    }

    private static Transform FindBone(
        Transform[] bones,
        string boneName)
    {
        if (bones == null)
            return null;

        for (int i = 0;
             i < bones.Length;
             i++)
        {
            if (bones[i] != null &&
                bones[i].name ==
                boneName)
            {
                return bones[i];
            }
        }

        return null;
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
