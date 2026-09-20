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

    // Front-to-back rig:
    // Bone = head/front anchor
    // Bone.001 = front body
    // Bone.002 = mid
    // Bone.003 = rear
    // Bone.004 = tail
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

    private bool restPoseCaptured;

    // TankFishAgent supplies the BODY-TRAIL direction, opposite the head turn.
    private float turnTarget;

    private float midTurn;
    private float rearTurn;
    private float tailTurn;

    private float midTurnVelocity;
    private float rearTurnVelocity;
    private float tailTurnVelocity;

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
        // Locomotion belongs to the gameplay root/head.
    }

    public void SetAquariumTurn(
        float normalizedTurn)
    {
        turnTarget =
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

        // The gameplay root/head has already entered the curve in Update().
        // The rest of the body follows in sequence rather than turning at once.
        midTurn =
            Mathf.SmoothDamp(
                midTurn,
                turnTarget,
                ref midTurnVelocity,
                0.10f,
                Mathf.Infinity,
                Time.deltaTime
            );

        rearTurn =
            Mathf.SmoothDamp(
                rearTurn,
                midTurn,
                ref rearTurnVelocity,
                0.13f,
                Mathf.Infinity,
                Time.deltaTime
            );

        tailTurn =
            Mathf.SmoothDamp(
                tailTurn,
                rearTurn,
                ref tailTurnVelocity,
                0.17f,
                Mathf.Infinity,
                Time.deltaTime
            );

        float turnMagnitude =
            Mathf.Clamp01(
                Mathf.Max(
                    Mathf.Abs(midTurn),
                    Mathf.Max(
                        Mathf.Abs(rearTurn),
                        Mathf.Abs(tailTurn)
                    )
                )
            );

        // If the authored wag is on the wrong half-cycle, do not let it fight
        // the turn. Slow the clip and blend its rear-bone influence toward the
        // rest pose while the turn-follow curve takes over.
        float targetAnimatorSpeed =
            Mathf.Lerp(
                1f,
                0.16f,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    turnMagnitude
                )
            );

        animatorSpeedCurrent =
            Mathf.MoveTowards(
                animatorSpeedCurrent,
                targetAnimatorSpeed,
                Time.deltaTime * 3.5f
            );

        if (animator != null)
        {
            animator.speed =
                animatorSpeedCurrent;
        }

        // Head and immediate front stay locked to the root. This is what makes
        // the head enter the path first; body curvature begins behind it.
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

        float authoredWeight =
            Mathf.Lerp(
                1f,
                0.10f,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    turnMagnitude
                )
            );

        ApplyTurnFollower(
            midBodyBone,
            midRestLocalPosition,
            midRestLocalRotation,
            authoredWeight,
            midTurn * 6f
        );

        ApplyTurnFollower(
            rearBodyBone,
            rearRestLocalPosition,
            rearRestLocalRotation,
            authoredWeight,
            rearTurn * 12f
        );

        ApplyTurnFollower(
            tailBone,
            tailRestLocalPosition,
            tailRestLocalRotation,
            authoredWeight,
            tailTurn * 19f
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

        // turnTarget already points toward the trailing/outside side of the
        // head turn. Hierarchical application makes the rear and tail lag.
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
