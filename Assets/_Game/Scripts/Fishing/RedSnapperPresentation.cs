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

    // Stable, non-animated locomotion guide captured from the authored
    // front-body bone structure. The swim animation is NOT allowed to
    // steer the fish laterally.
    private Vector3 headGuideLocalDirection =
        Vector3.forward;

    private Vector3 headRestLocalPosition;
    private Quaternion headRestLocalRotation =
        Quaternion.identity;

    private Transform headParentBone;
    private Vector3 headParentRestLocalPosition;
    private Quaternion headParentRestLocalRotation =
        Quaternion.identity;

    private bool headGuideCaptured;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;
        ResolveBones();
        CaptureStableHeadGuide();
        ApplyState();
    }

    public bool TryGetHeadForward(
        Transform referenceSpace,
        out Vector3 direction)
    {
        ResolveBones();
        CaptureStableHeadGuide();

        Vector3 worldDirection =
            transform.TransformDirection(
                headGuideLocalDirection
            );

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
        CaptureStableHeadGuide();
        ApplyState();
    }

    private void OnEnable()
    {
        ResolveAnimator();
        ResolveBones();
        CaptureStableHeadGuide();
        ApplyState();
    }

    private void LateUpdate()
    {
        if (held)
            return;

        // Animator has already evaluated this frame. Re-anchor the very front
        // of the skeleton so the Red Snapper's head does not wag the entire
        // fish sideways. Rear/body/tail child bones keep their authored
        // animation and therefore trail behind the stable head.
        if (headParentBone != null)
        {
            headParentBone.localPosition =
                headParentRestLocalPosition;

            headParentBone.localRotation =
                headParentRestLocalRotation;
        }

        if (headBone != null)
        {
            headBone.localPosition =
                headRestLocalPosition;

            headBone.localRotation =
                headRestLocalRotation;
        }
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

    private void CaptureStableHeadGuide()
    {
        if (headGuideCaptured)
            return;

        ResolveBones();

        if (headBone == null)
            return;

        Vector3 worldDirection =
            Vector3.zero;

        if (nextBodyBone != null)
        {
            worldDirection =
                headBone.position -
                nextBodyBone.position;
        }
        else if (tailBone != null)
        {
            worldDirection =
                headBone.position -
                tailBone.position;
        }

        if (worldDirection.sqrMagnitude <
            0.000001f)
        {
            return;
        }

        headGuideLocalDirection =
            transform
                .InverseTransformDirection(
                    worldDirection
                )
                .normalized;

        headRestLocalPosition =
            headBone.localPosition;

        headRestLocalRotation =
            headBone.localRotation;

        // If the imported rig has an extra front/root bone above Bone.001,
        // anchor it too. That prevents parent-bone sway from moving the head.
        Transform candidateParent =
            headBone.parent;

        if (candidateParent != null &&
            animator != null &&
            candidateParent != animator.transform)
        {
            headParentBone =
                candidateParent;

            headParentRestLocalPosition =
                headParentBone.localPosition;

            headParentRestLocalRotation =
                headParentBone.localRotation;
        }

        headGuideCaptured = true;
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
