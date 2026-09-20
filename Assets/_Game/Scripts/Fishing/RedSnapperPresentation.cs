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
    private Transform frontBodyBone;
    private Transform midBodyBone;
    private Transform rearBodyBone;
    private Transform tailBone;

    private Vector3 headRestLocalPosition;
    private Quaternion headRestLocalRotation =
        Quaternion.identity;

    private Vector3 frontRestLocalPosition;
    private Quaternion frontRestLocalRotation =
        Quaternion.identity;

    private Vector3 midRestLocalPosition;
    private Quaternion midRestLocalRotation =
        Quaternion.identity;

    private Vector3 rearRestLocalPosition;
    private Quaternion rearRestLocalRotation =
        Quaternion.identity;

    private Vector3 tailRestLocalPosition;
    private Quaternion tailRestLocalRotation =
        Quaternion.identity;

    private bool restPoseCaptured;

    private Vector3 trail25 =
        Vector3.forward;

    private Vector3 trail50 =
        Vector3.forward;

    private Vector3 trail75 =
        Vector3.forward;

    private Vector3 trail100 =
        Vector3.forward;

    private Vector3 headForward =
        Vector3.forward;

    private bool hasTrail;
    private float animatorSpeedCurrent = 1f;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;

        ResolveBones();
        CaptureRestPose();
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
    }

    public void SetAquariumTurn(
        float normalizedTurn)
    {
        // Compatibility hook only.
        // Actual turn shape comes from the recorded head track.
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

        if (referenceSpace != null)
        {
            position =
                referenceSpace
                    .InverseTransformPoint(
                        headBone.position
                    );
        }
        else
        {
            position =
                headBone.position;
        }

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

        if (!hasTrail)
            return;

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

        float turnWeight =
            Mathf.InverseLerp(
                3f,
                34f,
                trailAngle
            );

        turnWeight =
            Mathf.SmoothStep(
                0f,
                1f,
                turnWeight
            );

        // During a real curve the track owns the rear body. Slow the authored
        // clip so a random tail-wag phase cannot fling against momentum.
        float targetAnimatorSpeed =
            Mathf.Lerp(
                1f,
                0.18f,
                turnWeight
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

        // Head is always the locomotive.
        if (headBone != null)
        {
            headBone.localPosition =
                headRestLocalPosition;

            headBone.localRotation =
                headRestLocalRotation;
        }

        float authoredWeight =
            Mathf.Lerp(
                1f,
                0.08f,
                turnWeight
            );

        // Put the chain lengths back at their authored rest distances, then
        // orient each cart toward the older tangent of the same head track.
        PrepareBone(
            frontBodyBone,
            frontRestLocalPosition,
            frontRestLocalRotation,
            authoredWeight
        );

        PrepareBone(
            midBodyBone,
            midRestLocalPosition,
            midRestLocalRotation,
            authoredWeight
        );

        PrepareBone(
            rearBodyBone,
            rearRestLocalPosition,
            rearRestLocalRotation,
            authoredWeight
        );

        PrepareBone(
            tailBone,
            tailRestLocalPosition,
            tailRestLocalRotation,
            authoredWeight
        );

        // Bone.001 controls where Bone.002 goes, Bone.002 controls Bone.003,
        // and Bone.003 controls Bone.004. This is literally a train of carts.
        AlignSegmentToTrack(
            frontBodyBone,
            midBodyBone,
            trail25
        );

        AlignSegmentToTrack(
            midBodyBone,
            rearBodyBone,
            trail50
        );

        AlignSegmentToTrack(
            rearBodyBone,
            tailBone,
            trail75
        );

        if (tailBone != null)
        {
            Quaternion tailCorrection =
                Quaternion.FromToRotation(
                    SafeDirection(
                        trail75,
                        headForward
                    ),
                    SafeDirection(
                        trail100,
                        trail75
                    )
                );

            tailBone.rotation =
                tailCorrection *
                tailBone.rotation;
        }
    }

    private static void PrepareBone(
        Transform bone,
        Vector3 restPosition,
        Quaternion restRotation,
        float authoredWeight)
    {
        if (bone == null)
            return;

        Vector3 authoredPosition =
            bone.localPosition;

        Quaternion authoredRotation =
            bone.localRotation;

        bone.localPosition =
            Vector3.Lerp(
                restPosition,
                authoredPosition,
                authoredWeight
            );

        bone.localRotation =
            Quaternion.Slerp(
                restRotation,
                authoredRotation,
                authoredWeight
            );
    }

    private static void AlignSegmentToTrack(
        Transform bone,
        Transform child,
        Vector3 desiredForwardWorld)
    {
        if (bone == null ||
            child == null)
        {
            return;
        }

        Vector3 currentTailDirection =
            child.position -
            bone.position;

        Vector3 desiredTailDirection =
            -SafeDirection(
                desiredForwardWorld,
                bone.forward
            );

        if (currentTailDirection.sqrMagnitude <
            0.000001f)
        {
            return;
        }

        Quaternion correction =
            Quaternion.FromToRotation(
                currentTailDirection.normalized,
                desiredTailDirection
            );

        bone.rotation =
            correction *
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

        rearBodyBone =
            FindBone(
                bones,
                "Bone.003"
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
                    "Bone.001"
                );

            frontBodyBone =
                FindBone(
                    bones,
                    "Bone.002"
                );

            midBodyBone =
                FindBone(
                    bones,
                    "Bone.003"
                );

            rearBodyBone =
                FindBone(
                    bones,
                    "Bone.004"
                );

            tailBone = null;
        }
    }

    private void CaptureRestPose()
    {
        if (restPoseCaptured ||
            headBone == null ||
            frontBodyBone == null)
        {
            return;
        }

        headRestLocalPosition =
            headBone.localPosition;

        headRestLocalRotation =
            headBone.localRotation;

        frontRestLocalPosition =
            frontBodyBone.localPosition;

        frontRestLocalRotation =
            frontBodyBone.localRotation;

        if (midBodyBone != null)
        {
            midRestLocalPosition =
                midBodyBone.localPosition;

            midRestLocalRotation =
                midBodyBone.localRotation;
        }

        if (rearBodyBone != null)
        {
            rearRestLocalPosition =
                rearBodyBone.localPosition;

            rearRestLocalRotation =
                rearBodyBone.localRotation;
        }

        if (tailBone != null)
        {
            tailRestLocalPosition =
                tailBone.localPosition;

            tailRestLocalRotation =
                tailBone.localRotation;
        }

        restPoseCaptured = true;
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

        animatorSpeedCurrent =
            held
                ? 1.08f
                : 1f;

        animator.speed =
            animatorSpeedCurrent;
    }
}
