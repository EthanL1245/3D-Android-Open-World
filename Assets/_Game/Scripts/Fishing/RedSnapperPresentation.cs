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

    // This rig is ordered front-to-back:
    // Bone = head/front anchor
    // Bone.001 = front body
    // Bone.002+ = animated mid/rear body and tail
    private Transform headBone;
    private Transform frontBodyBone;
    private Transform midBodyBone;
    private Transform rearBodyBone;
    private Transform tailBone;

    private Vector3 headRestLocalPosition;
    private Quaternion headRestLocalRotation =
        Quaternion.identity;

    private Vector3 frontBodyRestLocalPosition;
    private Quaternion frontBodyRestLocalRotation =
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

    private bool frontPoseCaptured;

    private float aquariumTurnTarget;
    private float aquariumTurnCurrent;

    public void Configure(
        Animator targetAnimator)
    {
        animator = targetAnimator;

        ResolveBones();
        CaptureFrontPose();
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
        // The head/root owns locomotion. Rear bones only shape themselves
        // to match the current path curvature.
    }

    public void SetAquariumTurn(
        float normalizedTurn)
    {
        aquariumTurnTarget =
            held
                ? 0f
                : Mathf.Clamp(
                    normalizedTurn,
                    -1f,
                    1f
                );
    }

    public bool TryGetHeadPosition(
        Transform referenceSpace,
        out Vector3 position)
    {
        ResolveBones();
        CaptureFrontPose();

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
        CaptureFrontPose();
        ApplyState();
    }

    private void OnEnable()
    {
        ResolveAnimator();
        ResolveBones();
        CaptureFrontPose();
        ApplyState();
    }

    private void LateUpdate()
    {
        if (held)
            return;

        ResolveBones();
        CaptureFrontPose();

        aquariumTurnCurrent =
            Mathf.MoveTowards(
                aquariumTurnCurrent,
                aquariumTurnTarget,
                Time.deltaTime * 6.0f
            );

        // Animator has evaluated already. The actual head/front section is
        // restored to its authored rest pose so locomotion can never inherit
        // lateral head wag from the clip.
        if (headBone != null)
        {
            headBone.localPosition =
                headRestLocalPosition;

            headBone.localRotation =
                headRestLocalRotation;
        }

        if (frontBodyBone != null)
        {
            frontBodyBone.localPosition =
                frontBodyRestLocalPosition;

            frontBodyBone.localRotation =
                frontBodyRestLocalRotation;
        }

        float turn =
            aquariumTurnCurrent;

        float turnMagnitude =
            Mathf.Abs(
                turn
            );

        // During a hard turn, reduce the clip's independent tail wag so the
        // authored animation cannot point the tail against the actual curve.
        // The turn-follow bend then becomes the dominant motion.
        float authoredSwimWeight =
            Mathf.Lerp(
                1f,
                0.45f,
                turnMagnitude
            );

        ApplyTurnFollower(
            midBodyBone,
            midRestLocalPosition,
            midRestLocalRotation,
            authoredSwimWeight,
            turn * 7f
        );

        ApplyTurnFollower(
            rearBodyBone,
            rearRestLocalPosition,
            rearRestLocalRotation,
            authoredSwimWeight,
            turn * 14f
        );

        ApplyTurnFollower(
            tailBone,
            tailRestLocalPosition,
            tailRestLocalRotation,
            authoredSwimWeight,
            turn * 22f
        );
    }

    private void ApplyTurnFollower(
        Transform bone,
        Vector3 restPosition,
        Quaternion restRotation,
        float authoredWeight,
        float turnDegrees)
    {
        if (bone == null)
            return;

        Quaternion authoredRotation =
            bone.localRotation;

        Vector3 authoredPosition =
            bone.localPosition;

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

        // Apply turning in world-up yaw after the authored clip. Because the
        // bones are hierarchical, each farther-back segment accumulates the
        // curve and the tail naturally trails the head through the turn.
        bone.rotation =
            Quaternion.AngleAxis(
                turnDegrees,
                transform.up
            ) *
            bone.rotation;
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

        // Defensive fallback if a future exporter renames/removes the root
        // Bone but retains the numbered chain.
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

    private void CaptureFrontPose()
    {
        if (frontPoseCaptured ||
            headBone == null ||
            frontBodyBone == null)
        {
            return;
        }

        headRestLocalPosition =
            headBone.localPosition;

        headRestLocalRotation =
            headBone.localRotation;

        frontBodyRestLocalPosition =
            frontBodyBone.localPosition;

        frontBodyRestLocalRotation =
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

        frontPoseCaptured = true;
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
