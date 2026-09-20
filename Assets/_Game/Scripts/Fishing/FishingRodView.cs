using UnityEngine;

/// <summary>
/// Equipped rod/reel view. Reeling uses the authored reel animation, while
/// line load procedurally curves the actual RodBlank mesh toward the bobber.
/// </summary>
public sealed class FishingRodView : MonoBehaviour
{
    [SerializeField]
    private Transform rodTip;

    [SerializeField]
    private Transform reelMount;

    [SerializeField]
    private Animation reelAnimation;

    [SerializeField]
    private AnimationClip reelClip;

    [SerializeField, Min(0f)]
    private float reelSpeed = 2f;

    [Header("Rod Flex")]
    [SerializeField]
    private MeshFilter rodBlankFilter;

    [SerializeField, Range(0.05f, 0.45f)]
    private float bendStartHeight = 0.16f;

    [SerializeField, Range(20f, 85f)]
    private float maximumBendDegrees = 74f;

    private Quaternion restingRotation;
    private float reelTime;
    private bool initialized;

    private Transform rodTransform;

    private Mesh runtimeRodMesh;

    private Vector3[] restVerticesMesh;
    private Vector3[] restNormalsMesh;

    private Vector3[] restVerticesRod;
    private Vector3[] restNormalsRod;

    private Vector3[] bentVerticesMesh;
    private Vector3[] bentNormalsMesh;

    private Matrix4x4 meshToRod;
    private Matrix4x4 rodToMesh;

    private Vector3 restingTipLocalPosition;

    private float currentBendDegrees;
    private Vector3 currentPullDirectionRod =
        Vector3.up;

    public Transform RodTip => rodTip;

    public void Configure(
        Transform tip,
        Transform mount,
        Animation animation,
        AnimationClip clip)
    {
        rodTip = tip;
        reelMount = mount;
        reelAnimation = animation;
        reelClip = clip;
    }

    public void InitializePose()
    {
        restingRotation =
            transform.localRotation;

        rodTransform =
            rodTip != null
                ? rodTip.parent
                : transform.Find("Rod");

        if (rodTransform == null)
        {
            rodTransform =
                transform.Find("Rod");
        }

        if (rodBlankFilter == null &&
            rodTransform != null)
        {
            Transform blank =
                rodTransform.Find(
                    "RodBlank"
                );

            if (blank != null)
            {
                rodBlankFilter =
                    blank.GetComponent<MeshFilter>();
            }
        }

        if (rodTip != null)
        {
            restingTipLocalPosition =
                rodTip.localPosition;
        }

        BuildRuntimeRodMesh();

        // Existing prefabs can still carry old serialized values.
        reelSpeed =
            Mathf.Max(
                2f,
                reelSpeed
            );

        maximumBendDegrees =
            Mathf.Max(
                74f,
                maximumBendDegrees
            );

        initialized = true;
        ResetMotion();
    }

    public void TickReel(
        bool reeling,
        float deltaTime)
    {
        if (!initialized ||
            reelAnimation == null ||
            reelClip == null ||
            reelClip.length <= 0f)
        {
            return;
        }

        if (reeling)
        {
            reelTime =
                Mathf.Repeat(
                    reelTime +
                    Mathf.Max(
                        0f,
                        deltaTime
                    ) *
                    reelSpeed,
                    reelClip.length
                );
        }

        SampleReel();
    }

    public void SetLinePull(
        Vector3 bobberWorldPosition,
        float tension,
        float deltaTime)
    {
        if (!initialized ||
            rodTransform == null ||
            rodTip == null ||
            runtimeRodMesh == null)
        {
            return;
        }

        Vector3 bendBaseWorld =
            rodTransform.TransformPoint(
                new Vector3(
                    0f,
                    bendStartHeight,
                    0f
                )
            );

        Vector3 pullWorld =
            bobberWorldPosition -
            bendBaseWorld;

        if (pullWorld.sqrMagnitude <
            0.0001f)
        {
            RelaxLinePull(
                deltaTime
            );

            return;
        }

        Vector3 targetPullRod =
            rodTransform
                .InverseTransformDirection(
                    pullWorld.normalized
                );

        if (targetPullRod.sqrMagnitude <
            0.0001f)
        {
            targetPullRod =
                Vector3.up;
        }

        targetPullRod.Normalize();

        float targetAngle =
            Mathf.Clamp(
                Vector3.Angle(
                    Vector3.up,
                    targetPullRod
                ),
                18f,
                maximumBendDegrees
            );

        // Even moderate tension should visibly load the rod. High tension
        // approaches the full curved-blank range.
        float load =
            Mathf.Lerp(
                0.62f,
                1f,
                Mathf.Clamp01(
                    tension
                )
            );

        targetAngle *= load;

        float directionSmoothing =
            1f -
            Mathf.Exp(
                -16f *
                Mathf.Max(
                    0f,
                    deltaTime
                )
            );

        currentPullDirectionRod =
            Vector3.Slerp(
                currentPullDirectionRod,
                targetPullRod,
                directionSmoothing
            );

        if (currentPullDirectionRod.sqrMagnitude <
            0.0001f)
        {
            currentPullDirectionRod =
                Vector3.up;
        }

        currentPullDirectionRod.Normalize();

        currentBendDegrees =
            Mathf.Lerp(
                currentBendDegrees,
                targetAngle,
                directionSmoothing
            );

        ApplyRodCurve(
            currentPullDirectionRod,
            currentBendDegrees
        );
    }

    public void RelaxLinePull(
        float deltaTime)
    {
        if (!initialized ||
            runtimeRodMesh == null)
        {
            return;
        }

        float smoothing =
            1f -
            Mathf.Exp(
                -12f *
                Mathf.Max(
                    0f,
                    deltaTime
                )
            );

        currentBendDegrees =
            Mathf.Lerp(
                currentBendDegrees,
                0f,
                smoothing
            );

        currentPullDirectionRod =
            Vector3.Slerp(
                currentPullDirectionRod,
                Vector3.up,
                smoothing
            );

        ApplyRodCurve(
            currentPullDirectionRod,
            currentBendDegrees
        );
    }

    private void BuildRuntimeRodMesh()
    {
        if (rodBlankFilter == null ||
            rodBlankFilter.sharedMesh == null ||
            rodTransform == null)
        {
            return;
        }

        if (runtimeRodMesh != null)
        {
            Destroy(
                runtimeRodMesh
            );
        }

        runtimeRodMesh =
            Instantiate(
                rodBlankFilter.sharedMesh
            );

        runtimeRodMesh.name =
            rodBlankFilter
                .sharedMesh
                .name +
            "_RuntimeFlex";

        runtimeRodMesh.MarkDynamic();

        rodBlankFilter.sharedMesh =
            runtimeRodMesh;

        restVerticesMesh =
            runtimeRodMesh.vertices;

        restNormalsMesh =
            runtimeRodMesh.normals;

        if (restNormalsMesh == null ||
            restNormalsMesh.Length !=
                restVerticesMesh.Length)
        {
            runtimeRodMesh
                .RecalculateNormals();

            restNormalsMesh =
                runtimeRodMesh.normals;
        }

        restVerticesRod =
            new Vector3[
                restVerticesMesh.Length
            ];

        restNormalsRod =
            new Vector3[
                restVerticesMesh.Length
            ];

        bentVerticesMesh =
            new Vector3[
                restVerticesMesh.Length
            ];

        bentNormalsMesh =
            new Vector3[
                restVerticesMesh.Length
            ];

        meshToRod =
            rodTransform
                .worldToLocalMatrix *
            rodBlankFilter
                .transform
                .localToWorldMatrix;

        rodToMesh =
            meshToRod.inverse;

        for (int i = 0;
             i < restVerticesMesh.Length;
             i++)
        {
            restVerticesRod[i] =
                meshToRod.MultiplyPoint3x4(
                    restVerticesMesh[i]
                );

            restNormalsRod[i] =
                meshToRod
                    .MultiplyVector(
                        restNormalsMesh[i]
                    )
                    .normalized;
        }
    }

    private void ApplyRodCurve(
        Vector3 pullDirectionRod,
        float bendDegrees)
    {
        if (runtimeRodMesh == null ||
            restVerticesRod == null ||
            rodTip == null)
        {
            return;
        }

        float tipHeight =
            Mathf.Max(
                bendStartHeight +
                0.05f,
                restingTipLocalPosition.y
            );

        float flexibleLength =
            tipHeight -
            bendStartHeight;

        float bendRadians =
            Mathf.Deg2Rad *
            Mathf.Clamp(
                bendDegrees,
                0f,
                maximumBendDegrees
            );

        Vector3 lateral =
            Vector3.ProjectOnPlane(
                pullDirectionRod,
                Vector3.up
            );

        if (lateral.sqrMagnitude <
            0.0001f ||
            bendRadians <
            0.0001f)
        {
            RestoreStraightRod();
            return;
        }

        lateral.Normalize();

        Vector3 bendAxis =
            Vector3.Cross(
                Vector3.up,
                lateral
            );

        if (bendAxis.sqrMagnitude <
            0.0001f)
        {
            RestoreStraightRod();
            return;
        }

        bendAxis.Normalize();

        for (int i = 0;
             i < restVerticesRod.Length;
             i++)
        {
            Vector3 rest =
                restVerticesRod[i];

            if (rest.y <=
                bendStartHeight)
            {
                bentVerticesMesh[i] =
                    restVerticesMesh[i];

                bentNormalsMesh[i] =
                    restNormalsMesh[i];

                continue;
            }

            float t =
                Mathf.Clamp01(
                    (
                        rest.y -
                        bendStartHeight
                    ) /
                    flexibleLength
                );

            float localAngle =
                bendRadians *
                t;

            float arcUp;
            float arcSide;

            if (bendRadians >
                0.0001f)
            {
                arcUp =
                    Mathf.Sin(
                        localAngle
                    ) /
                    bendRadians *
                    flexibleLength;

                arcSide =
                    (
                        1f -
                        Mathf.Cos(
                            localAngle
                        )
                    ) /
                    bendRadians *
                    flexibleLength;
            }
            else
            {
                arcUp =
                    flexibleLength *
                    t;

                arcSide = 0f;
            }

            Vector3 centerRod =
                Vector3.up *
                (
                    bendStartHeight +
                    arcUp
                ) +
                lateral *
                arcSide;

            Vector3 radial =
                new Vector3(
                    rest.x,
                    0f,
                    rest.z
                );

            Quaternion sectionRotation =
                Quaternion.AngleAxis(
                    localAngle *
                    Mathf.Rad2Deg,
                    bendAxis
                );

            Vector3 bentRod =
                centerRod +
                sectionRotation *
                radial;

            Vector3 bentNormalRod =
                sectionRotation *
                restNormalsRod[i];

            bentVerticesMesh[i] =
                rodToMesh
                    .MultiplyPoint3x4(
                        bentRod
                    );

            bentNormalsMesh[i] =
                rodToMesh
                    .MultiplyVector(
                        bentNormalRod
                    )
                    .normalized;
        }

        runtimeRodMesh.vertices =
            bentVerticesMesh;

        runtimeRodMesh.normals =
            bentNormalsMesh;

        runtimeRodMesh
            .RecalculateBounds();

        float tipAngle =
            bendRadians;

        float tipUp =
            Mathf.Sin(
                tipAngle
            ) /
            bendRadians *
            flexibleLength;

        float tipSide =
            (
                1f -
                Mathf.Cos(
                    tipAngle
                )
            ) /
            bendRadians *
            flexibleLength;

        rodTip.localPosition =
            Vector3.up *
            (
                bendStartHeight +
                tipUp
            ) +
            lateral *
            tipSide;
    }

    private void RestoreStraightRod()
    {
        if (runtimeRodMesh == null ||
            restVerticesMesh == null)
        {
            return;
        }

        runtimeRodMesh.vertices =
            restVerticesMesh;

        if (restNormalsMesh != null &&
            restNormalsMesh.Length ==
                restVerticesMesh.Length)
        {
            runtimeRodMesh.normals =
                restNormalsMesh;
        }

        runtimeRodMesh
            .RecalculateBounds();

        if (rodTip != null)
        {
            rodTip.localPosition =
                restingTipLocalPosition;
        }
    }

    private void SampleReel()
    {
        if (reelAnimation == null ||
            reelClip == null ||
            !reelAnimation
                .gameObject
                .activeInHierarchy)
        {
            return;
        }

        if (!reelAnimation.IsPlaying(
                reelClip.name))
        {
            reelAnimation.Play(
                reelClip.name
            );
        }

        AnimationState clipState =
            reelAnimation[
                reelClip.name
            ];

        if (clipState == null)
            return;

        clipState.speed = 0f;
        clipState.time = reelTime;

        reelAnimation.Sample();
    }

    public void SetCastPose(
        float progress)
    {
        if (!initialized)
            return;

        float t =
            Mathf.Clamp01(
                progress
            );

        float pitch =
            t < 0.25f
                ? Mathf.Lerp(
                    0f,
                    -48f,
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t / 0.25f
                    )
                )
                : t < 0.48f
                    ? Mathf.Lerp(
                        -48f,
                        12f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            (
                                t -
                                0.25f
                            ) /
                            0.23f
                        )
                    )
                    : Mathf.Lerp(
                        12f,
                        0f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            (
                                t -
                                0.48f
                            ) /
                            0.52f
                        )
                    );

        transform.localRotation =
            restingRotation *
            Quaternion.Euler(
                pitch,
                0f,
                0f
            );
    }

    public void ResetMotion()
    {
        if (initialized)
        {
            transform.localRotation =
                restingRotation;

            currentBendDegrees = 0f;

            currentPullDirectionRod =
                Vector3.up;

            RestoreStraightRod();
        }

        reelTime = 0f;

        SampleReel();
    }

    private void OnDisable()
    {
        ResetMotion();
    }

    private void OnDestroy()
    {
        if (runtimeRodMesh != null)
        {
            Destroy(
                runtimeRodMesh
            );

            runtimeRodMesh = null;
        }
    }
}
