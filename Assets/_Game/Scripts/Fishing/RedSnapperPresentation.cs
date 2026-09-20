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

    private Vector3 headRestLocalPosition;
    private Quaternion headRestLocalRotation =
        Quaternion.identity;

    private Vector3 frontBodyRestLocalPosition;
    private Quaternion frontBodyRestLocalRotation =
        Quaternion.identity;

    private bool frontPoseCaptured;

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
        // Aquarium locomotion deliberately does NOT drive animation or
        // derive direction from animated bones. Like Yellowtail/Tuna, the
        // gameplay root owns heading and translation.
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

        // Animator has evaluated already. Put the actual head/front section
        // back at its authored rest pose so it can NEVER wag the locomotion
        // direction sideways. Bone.002 and everything behind it remains free
        // to use the supplied swimming animation.
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
