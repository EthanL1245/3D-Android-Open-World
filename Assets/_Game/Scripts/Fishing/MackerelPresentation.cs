using UnityEngine;

public sealed class MackerelPresentation : MonoBehaviour
{
    [SerializeField]
    private Animator animator;

    [Header("Authored Swim")]
    [SerializeField]
    private float bodyLengthsPerCycle = 0.96f;

    [SerializeField]
    private float minimumCruiseSpeed = 0.58f;

    [SerializeField]
    private float maximumCruiseSpeed = 0.98f;

    private bool held;

    private Transform headBone;
    private Transform frontBodyBone;
    private Transform midBodyBone;
    private Transform rearBodyBone;
    private Transform tailBone;

    private Vector3 headRestPosition;
    private Quaternion headRestRotation;

    private Vector3 frontRestPosition;
    private Quaternion frontRestRotation;

    private Vector3 midRestPosition;
    private Quaternion midRestRotation;

    private Vector3 rearRestPosition;
    private Quaternion rearRestRotation;

    private Vector3 tailRestPosition;
    private Quaternion tailRestRotation;

    private bool restPoseCaptured;

    private Vector3 headForward =
        Vector3.forward;

    private Vector3 trail25 =
        Vector3.forward;

    private Vector3 trail50 =
        Vector3.forward;

    private Vector3 trail75 =
        Vector3.forward;

    private Vector3 trail100 =
        Vector3.forward;

    private bool hasTrail;

    private float trackBlend;
    private float trackBlendVelocity;
    private float animatorSpeedCurrent = 1f;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;

        ResolveBones();
        CaptureRestPose();
        ApplyState();
    }

    public void SetHeld(
        bool value)
    {
        held = value;
        ApplyState();
    }

    public void SetAquariumLocomotion(
        float worldSpeed)
    {
        // Authored Mackerel swim remains at its intended timing.
    }

    public void SetAquariumTrail(
        Vector3 currentHeadForward,
        Vector3 forward25,
        Vector3 forward50,
        Vector3 forward75,
        Vector3 forward100)
    {
        if (held)
            return;

        headForward =
            SafeDirection(
                currentHeadForward,
                transform.forward
            );

        trail25 =
            SafeDirection(
                forward25,
                headForward
            );

        trail50 =
            SafeDirection(
                forward50,
                trail25
            );

        trail75 =
            SafeDirection(
                forward75,
                trail50
            );

        trail100 =
            SafeDirection(
                forward100,
                trail75
            );

        hasTrail = true;
    }

    public bool TryGetHeadPosition(
        Transform referenceSpace,
        out Vector3 position)
    {
        ResolveBones();
        CaptureRestPose();

        if (headBone == null)
        {
            position = Vector3.zero;
            return false;
        }

        position =
            referenceSpace != null
                ? referenceSpace
                    .InverseTransformPoint(
                        headBone.position
                    )
                : headBone.position;

        return true;
    }

    public bool TryGetHeadPositionWorld(
        out Vector3 position)
    {
        return
            TryGetHeadPosition(
                null,
                out position
            );
    }

    public float GetBodyLengthWorld()
    {
        ResolveBones();

        if (headBone == null ||
            tailBone == null)
        {
            return 0.92f;
        }

        return
            Mathf.Clamp(
                Vector3.Distance(
                    headBone.position,
                    tailBone.position
                ),
                0.30f,
                1.60f
            );
    }

    public float GetRecommendedCruiseSpeed()
    {
        ResolveAnimator();

        float clipLength =
            GetSwimClipLength();

        float desired =
            0.92f *
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
        CaptureRestPose();
        ApplyState();
    }

    private void OnEnable()
    {
        ResolveAnimator();
        ResolveBones();
        CaptureRestPose();
        ApplyState();
    }

    private void LateUpdate()
    {
        if (held)
            return;

        ResolveBones();
        CaptureRestPose();

        if (!hasTrail ||
            headBone == null ||
            frontBodyBone == null ||
            midBodyBone == null ||
            tailBone == null)
        {
            return;
        }

        float trailAngle =
            Mathf.Max(
                Vector3.Angle(
                    headForward,
                    trail50
                ),
                Vector3.Angle(
                    headForward,
                    trail100
                )
            );

        float targetTrackBlend =
            Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    6f,
                    24f,
                    trailAngle
                )
            );

        trackBlend =
            Mathf.SmoothDamp(
                trackBlend,
                targetTrackBlend,
                ref trackBlendVelocity,
                targetTrackBlend >
                    trackBlend
                    ? 0.10f
                    : 0.16f,
                Mathf.Infinity,
                Time.deltaTime
            );

        // Exactly like Red Snapper: straight swimming is 100% authored.
        if (trackBlend < 0.015f)
        {
            animatorSpeedCurrent =
                Mathf.MoveTowards(
                    animatorSpeedCurrent,
                    1f,
                    Time.deltaTime * 5f
                );

            if (animator != null)
            {
                animator.speed =
                    animatorSpeedCurrent;
            }

            return;
        }

        float targetAnimatorSpeed =
            Mathf.Lerp(
                1f,
                0.18f,
                trackBlend
            );

        animatorSpeedCurrent =
            Mathf.MoveTowards(
                animatorSpeedCurrent,
                targetAnimatorSpeed,
                Time.deltaTime * 4f
            );

        if (animator != null)
        {
            animator.speed =
                animatorSpeedCurrent;
        }

        headBone.localPosition =
            Vector3.Lerp(
                headBone.localPosition,
                headRestPosition,
                trackBlend
            );

        headBone.localRotation =
            Quaternion.Slerp(
                headBone.localRotation,
                headRestRotation,
                trackBlend
            );

        float authoredWeight =
            Mathf.Lerp(
                1f,
                0.08f,
                trackBlend
            );

        PrepareBone(
            frontBodyBone,
            frontRestPosition,
            frontRestRotation,
            authoredWeight
        );

        PrepareBone(
            midBodyBone,
            midRestPosition,
            midRestRotation,
            authoredWeight
        );

        if (rearBodyBone != null)
        {
            PrepareBone(
                rearBodyBone,
                rearRestPosition,
                rearRestRotation,
                authoredWeight
            );
        }

        PrepareBone(
            tailBone,
            tailRestPosition,
            tailRestRotation,
            Mathf.Lerp(
                1f,
                0f,
                trackBlend
            )
        );

        AlignSegmentToTrack(
            frontBodyBone,
            midBodyBone,
            trail25,
            trackBlend
        );

        if (rearBodyBone != null)
        {
            AlignSegmentToTrack(
                midBodyBone,
                rearBodyBone,
                trail50,
                trackBlend
            );

            AlignSegmentToTrack(
                rearBodyBone,
                tailBone,
                trail75,
                trackBlend
            );

            ApplyTailTrack(
                trail75,
                trail100
            );
        }
        else
        {
            AlignSegmentToTrack(
                midBodyBone,
                tailBone,
                trail50,
                trackBlend
            );

            ApplyTailTrack(
                trail50,
                trail100
            );
        }
    }

    private void ApplyTailTrack(
        Vector3 rearForward,
        Vector3 tailForward)
    {
        if (tailBone == null)
            return;

        Vector3 up =
            transform.up;

        Vector3 rearFlat =
            Vector3.ProjectOnPlane(
                SafeDirection(
                    rearForward,
                    headForward
                ),
                up
            );

        Vector3 tailFlat =
            Vector3.ProjectOnPlane(
                SafeDirection(
                    tailForward,
                    rearForward
                ),
                up
            );

        if (rearFlat.sqrMagnitude <
                0.0001f ||
            tailFlat.sqrMagnitude <
                0.0001f)
        {
            return;
        }

        float yaw =
            Vector3.SignedAngle(
                rearFlat.normalized,
                tailFlat.normalized,
                up
            );

        tailBone.rotation =
            Quaternion.AngleAxis(
                yaw *
                trackBlend,
                up
            ) *
            tailBone.rotation;
    }

    private static void PrepareBone(
        Transform bone,
        Vector3 restPosition,
        Quaternion restRotation,
        float authoredWeight)
    {
        if (bone == null)
            return;

        bone.localPosition =
            Vector3.Lerp(
                restPosition,
                bone.localPosition,
                authoredWeight
            );

        bone.localRotation =
            Quaternion.Slerp(
                restRotation,
                bone.localRotation,
                authoredWeight
            );
    }

    private void AlignSegmentToTrack(
        Transform bone,
        Transform child,
        Vector3 desiredForwardWorld,
        float blend)
    {
        if (bone == null ||
            child == null)
        {
            return;
        }

        Vector3 up =
            transform.up;

        Vector3 currentTailDirection =
            child.position -
            bone.position;

        Vector3 desiredTailDirection =
            -SafeDirection(
                desiredForwardWorld,
                currentTailDirection
            );

        Vector3 currentFlat =
            Vector3.ProjectOnPlane(
                currentTailDirection,
                up
            );

        Vector3 desiredFlat =
            Vector3.ProjectOnPlane(
                desiredTailDirection,
                up
            );

        if (currentFlat.sqrMagnitude <
                0.0001f ||
            desiredFlat.sqrMagnitude <
                0.0001f)
        {
            return;
        }

        float yaw =
            Vector3.SignedAngle(
                currentFlat.normalized,
                desiredFlat.normalized,
                up
            );

        bone.rotation =
            Quaternion.AngleAxis(
                yaw *
                Mathf.Clamp01(blend),
                up
            ) *
            bone.rotation;
    }

    private static Vector3 SafeDirection(
        Vector3 value,
        Vector3 fallback)
    {
        if (value.sqrMagnitude <
            0.000001f)
        {
            value = fallback;
        }

        if (value.sqrMagnitude <
            0.000001f)
        {
            value = Vector3.forward;
        }

        return value.normalized;
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
            frontBodyBone != null)
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
                "Bone"
            );

        frontBodyBone =
            FindBone(
                bones,
                "Bone.001"
            );

        midBodyBone =
            FindBone(
                bones,
                "Bone.002"
            );

        Transform bone3 =
            FindBone(
                bones,
                "Bone.003"
            );

        Transform bone4 =
            FindBone(
                bones,
                "Bone.004"
            );

        if (bone4 != null)
        {
            rearBodyBone = bone3;
            tailBone = bone4;
        }
        else
        {
            rearBodyBone = null;
            tailBone = bone3;
        }
    }

    private void CaptureRestPose()
    {
        if (restPoseCaptured ||
            headBone == null ||
            frontBodyBone == null ||
            midBodyBone == null ||
            tailBone == null)
        {
            return;
        }

        headRestPosition =
            headBone.localPosition;

        headRestRotation =
            headBone.localRotation;

        frontRestPosition =
            frontBodyBone.localPosition;

        frontRestRotation =
            frontBodyBone.localRotation;

        midRestPosition =
            midBodyBone.localPosition;

        midRestRotation =
            midBodyBone.localRotation;

        if (rearBodyBone != null)
        {
            rearRestPosition =
                rearBodyBone.localPosition;

            rearRestRotation =
                rearBodyBone.localRotation;
        }

        tailRestPosition =
            tailBone.localPosition;

        tailRestRotation =
            tailBone.localRotation;

        restPoseCaptured = true;
    }

    private static Transform FindBone(
        Transform[] bones,
        string name)
    {
        for (int i = 0;
             i < bones.Length;
             i++)
        {
            if (bones[i] != null &&
                bones[i].name == name)
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

        animatorSpeedCurrent =
            held
                ? 1.08f
                : 1f;

        trackBlend = 0f;
        trackBlendVelocity = 0f;

        animator.speed =
            animatorSpeedCurrent;
    }
}
