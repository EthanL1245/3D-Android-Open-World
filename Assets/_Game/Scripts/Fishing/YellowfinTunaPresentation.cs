using System.Collections.Generic;
using UnityEngine;

public class YellowfinTunaPresentation : MonoBehaviour
{
    [SerializeField]
    private Renderer[] renderers;

    [SerializeField]
    private Transform visualRoot;

    [Header("Aquarium Swim")]
    [SerializeField]
    private float swimStrength = 0.17f;

    [SerializeField]
    private float swimSpeed = 5.8f;

    [SerializeField]
    private float bodyLength = 0.92f;

    [Header("Held Fish")]
    [SerializeField]
    private float heldStrength = 0.19f;

    [SerializeField]
    private float heldSpeed = 5.8f;

    [SerializeField]
    private float heldYawDegrees = 2.2f;

    [SerializeField]
    private float heldRollDegrees = 1.6f;

    private struct PathSample
    {
        public float distance;
        public Vector3 forward;
    }

    private readonly List<PathSample> pathHistory =
        new List<PathSample>(192);

    private bool held;
    private float swimPhase;
    private float aquariumLocomotionSpeed = 0.45f;

    private float totalPathDistance;
    private Vector3 lastPathPosition;
    private bool pathInitialized;

    private float pathYaw25;
    private float pathYaw50;
    private float pathYaw75;
    private float pathYaw100;

    private float pathSideSign = 1f;

    private MaterialPropertyBlock block;
    private Quaternion visualBaseRotation;

    private static readonly int SwimStrengthId =
        Shader.PropertyToID(
            "_SwimStrength"
        );

    private static readonly int SwimSpeedId =
        Shader.PropertyToID(
            "_SwimSpeed"
        );

    private static readonly int SwimPhaseId =
        Shader.PropertyToID(
            "_SwimPhase"
        );

    private static readonly int PathYaw25Id =
        Shader.PropertyToID(
            "_PathYaw25"
        );

    private static readonly int PathYaw50Id =
        Shader.PropertyToID(
            "_PathYaw50"
        );

    private static readonly int PathYaw75Id =
        Shader.PropertyToID(
            "_PathYaw75"
        );

    private static readonly int PathYaw100Id =
        Shader.PropertyToID(
            "_PathYaw100"
        );

    private static readonly int PathSideSignId =
        Shader.PropertyToID(
            "_PathSideSign"
        );

    private void Awake()
    {
        ResolveReferences();

        swimPhase =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        block =
            new MaterialPropertyBlock();

        ResetPathHistory();
        ResolvePathSideSign();
        ApplyMaterialSettings();
    }

    public void Configure(
        Renderer[] targetRenderers)
    {
        renderers =
            targetRenderers;

        ResolveReferences();

        if (block == null)
        {
            block =
                new MaterialPropertyBlock();
        }

        ResetPathHistory();
        ResolvePathSideSign();
        ApplyMaterialSettings();
    }

    public void SetHeld(bool value)
    {
        held = value;

        if (held)
        {
            pathYaw25 = 0f;
            pathYaw50 = 0f;
            pathYaw75 = 0f;
            pathYaw100 = 0f;
        }
        else
        {
            ResetPathHistory();
        }

        ApplyMaterialSettings();
    }

    public void SetAquariumTurn(
        float normalizedTurn)
    {
        // Intentionally ignored.
        // The body follows the ACTUAL heading history of the head/root.
        // It does not deform from an instantaneous steering request.
    }

    public void SetAquariumLocomotion(
        float worldSpeed)
    {
        aquariumLocomotionSpeed =
            Mathf.Max(
                0f,
                worldSpeed
            );
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetPathHistory();
        ResolvePathSideSign();
        ApplyMaterialSettings();
    }

    private void LateUpdate()
    {
        if (visualRoot == null)
            return;

        float speed =
            held
                ? heldSpeed
                : GetAquariumSwimSpeed();

        swimPhase +=
            speed *
            Time.deltaTime;

        if (swimPhase >
            Mathf.PI * 2f)
        {
            swimPhase -=
                Mathf.PI * 2f;
        }

        if (held)
        {
            float beat =
                Mathf.Sin(
                    swimPhase
                );

            visualRoot.localRotation =
                visualBaseRotation *
                Quaternion.Euler(
                    0f,
                    beat *
                    heldYawDegrees,
                    Mathf.Sin(
                        swimPhase *
                        0.5f +
                        0.8f
                    ) *
                    heldRollDegrees
                );

            pathYaw25 = 0f;
            pathYaw50 = 0f;
            pathYaw75 = 0f;
            pathYaw100 = 0f;
        }
        else
        {
            // Head remains exactly on the gameplay locomotion root.
            visualRoot.localRotation =
                visualBaseRotation;

            RecordPathHistory();
            UpdatePathFollowerAngles();
        }

        ApplyMaterialSettings();
    }

    private void ResetPathHistory()
    {
        pathHistory.Clear();

        totalPathDistance = 0f;

        lastPathPosition =
            transform.position;

        pathInitialized = true;

        Vector3 forward =
            GetFlatForward();

        pathHistory.Add(
            new PathSample
            {
                distance = 0f,
                forward = forward
            }
        );

        pathYaw25 = 0f;
        pathYaw50 = 0f;
        pathYaw75 = 0f;
        pathYaw100 = 0f;
    }

    private void RecordPathHistory()
    {
        if (!pathInitialized)
        {
            ResetPathHistory();
            return;
        }

        Vector3 position =
            transform.position;

        float moved =
            Vector3.Distance(
                position,
                lastPathPosition
            );

        // A large jump means this fish was teleported/rebuilt, not that it
        // swam through that distance. Start a fresh trail.
        if (moved > 0.30f)
        {
            ResetPathHistory();
            return;
        }

        Vector3 forward =
            GetFlatForward();

        if (moved > 0.0001f)
        {
            totalPathDistance +=
                moved;
        }

        // Record every rendered frame. Heading changes matter even when the
        // frame-to-frame travel distance is small.
        pathHistory.Add(
            new PathSample
            {
                distance =
                    totalPathDistance,
                forward =
                    forward
            }
        );

        lastPathPosition =
            position;

        float keepDistance =
            Mathf.Max(
                1.35f,
                bodyLength * 1.45f
            );

        float oldestAllowed =
            totalPathDistance -
            keepDistance;

        while (pathHistory.Count > 3 &&
               pathHistory[1].distance <
               oldestAllowed)
        {
            pathHistory.RemoveAt(0);
        }

        // Avoid an unbounded list if the fish barely moves for a long time.
        while (pathHistory.Count > 220)
        {
            pathHistory.RemoveAt(0);
        }
    }

    private void UpdatePathFollowerAngles()
    {
        Vector3 currentForward =
            GetFlatForward();

        pathYaw25 =
            SampleRelativeYaw(
                currentForward,
                bodyLength * 0.25f
            );

        pathYaw50 =
            SampleRelativeYaw(
                currentForward,
                bodyLength * 0.50f
            );

        pathYaw75 =
            SampleRelativeYaw(
                currentForward,
                bodyLength * 0.75f
            );

        pathYaw100 =
            SampleRelativeYaw(
                currentForward,
                bodyLength * 1.00f
            );
    }

    private float SampleRelativeYaw(
        Vector3 currentForward,
        float distanceBehind)
    {
        Vector3 historicalForward =
            SampleHistoricalForward(
                Mathf.Max(
                    0f,
                    distanceBehind
                )
            );

        float yawDegrees =
            Vector3.SignedAngle(
                currentForward,
                historicalForward,
                Vector3.up
            );

        // Do not allow a numerical wrap at +/-180 to flip the body.
        yawDegrees =
            Mathf.Clamp(
                yawDegrees,
                -170f,
                170f
            );

        return
            yawDegrees *
            Mathf.Deg2Rad;
    }

    private Vector3 SampleHistoricalForward(
        float distanceBehind)
    {
        if (pathHistory.Count == 0)
        {
            return GetFlatForward();
        }

        float targetDistance =
            totalPathDistance -
            distanceBehind;

        if (targetDistance <=
            pathHistory[0].distance)
        {
            return
                pathHistory[0]
                    .forward;
        }

        for (int i =
                pathHistory.Count - 1;
             i > 0;
             i--)
        {
            PathSample newer =
                pathHistory[i];

            PathSample older =
                pathHistory[i - 1];

            if (older.distance <=
                    targetDistance &&
                newer.distance >=
                    targetDistance)
            {
                float span =
                    newer.distance -
                    older.distance;

                float t =
                    span > 0.00001f
                        ? (
                            targetDistance -
                            older.distance
                          ) /
                          span
                        : 0f;

                Vector3 blended =
                    Vector3.Slerp(
                        older.forward,
                        newer.forward,
                        Mathf.Clamp01(t)
                    );

                if (blended.sqrMagnitude >
                    0.0001f)
                {
                    return
                        blended.normalized;
                }

                return older.forward;
            }
        }

        return
            pathHistory[
                pathHistory.Count - 1
            ].forward;
    }

    private Vector3 GetFlatForward()
    {
        Vector3 forward =
            Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up
            );

        if (forward.sqrMagnitude <
            0.0001f)
        {
            forward =
                Vector3.forward;
        }

        return forward.normalized;
    }

    private void ResolvePathSideSign()
    {
        pathSideSign = 1f;

        if (renderers == null ||
            renderers.Length == 0 ||
            renderers[0] == null ||
            renderers[0].sharedMaterial == null)
        {
            return;
        }

        Vector4 sideProperty =
            renderers[0]
                .sharedMaterial
                .GetVector(
                    "_SideAxis"
                );

        Vector3 sideAxisObject =
            new Vector3(
                sideProperty.x,
                sideProperty.y,
                sideProperty.z
            );

        if (sideAxisObject.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        Vector3 sideWorld =
            renderers[0]
                .transform
                .TransformDirection(
                    sideAxisObject.normalized
                );

        float dot =
            Vector3.Dot(
                sideWorld.normalized,
                transform.right
            );

        if (Mathf.Abs(dot) >
            0.05f)
        {
            pathSideSign =
                Mathf.Sign(
                    dot
                );
        }
    }

    private float GetAquariumSwimSpeed()
    {
        return
            Mathf.Lerp(
                swimSpeed * 0.90f,
                swimSpeed * 1.08f,
                Mathf.InverseLerp(
                    0.32f,
                    0.58f,
                    aquariumLocomotionSpeed
                )
            );
    }

    private void ResolveReferences()
    {
        if (renderers == null ||
            renderers.Length == 0)
        {
            renderers =
                GetComponentsInChildren<Renderer>(
                    true
                );
        }

        if (visualRoot == null)
        {
            visualRoot =
                transform.Find(
                    "Visual"
                );
        }

        if (visualRoot != null)
        {
            visualBaseRotation =
                visualRoot.localRotation;
        }
    }

    private void ApplyMaterialSettings()
    {
        if (renderers == null ||
            renderers.Length == 0)
        {
            return;
        }

        if (block == null)
        {
            block =
                new MaterialPropertyBlock();
        }

        float strength =
            held
                ? Mathf.Max(
                    heldStrength,
                    0.19f
                )
                : Mathf.Max(
                    swimStrength,
                    0.17f
                );

        float speed =
            held
                ? heldSpeed
                : GetAquariumSwimSpeed();

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(
                block
            );

            block.SetFloat(
                SwimStrengthId,
                strength
            );

            block.SetFloat(
                SwimSpeedId,
                speed
            );

            block.SetFloat(
                SwimPhaseId,
                swimPhase
            );

            block.SetFloat(
                PathYaw25Id,
                held
                    ? 0f
                    : pathYaw25
            );

            block.SetFloat(
                PathYaw50Id,
                held
                    ? 0f
                    : pathYaw50
            );

            block.SetFloat(
                PathYaw75Id,
                held
                    ? 0f
                    : pathYaw75
            );

            block.SetFloat(
                PathYaw100Id,
                held
                    ? 0f
                    : pathYaw100
            );

            block.SetFloat(
                PathSideSignId,
                pathSideSign
            );

            renderer.SetPropertyBlock(
                block
            );
        }
    }
}
