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

    [Header("Held Fish")]
    [SerializeField]
    private float heldStrength = 0.19f;

    [SerializeField]
    private float heldSpeed = 5.8f;

    [SerializeField]
    private float heldYawDegrees = 2.2f;

    [SerializeField]
    private float heldRollDegrees = 1.6f;

    private bool held;
    private float swimPhase;
    private float aquariumLocomotionSpeed = 0.45f;

    private float trailYaw25;
    private float trailYaw50;
    private float trailYaw75;
    private float trailYaw100;
    private float pathSideSign = 1f;

    private MaterialPropertyBlock block;
    private Quaternion visualBaseRotation;

    private static readonly int SwimStrengthId =
        Shader.PropertyToID("_SwimStrength");

    private static readonly int SwimSpeedId =
        Shader.PropertyToID("_SwimSpeed");

    private static readonly int SwimPhaseId =
        Shader.PropertyToID("_SwimPhase");

    private static readonly int TrailYaw25Id =
        Shader.PropertyToID("_TrailYaw25");

    private static readonly int TrailYaw50Id =
        Shader.PropertyToID("_TrailYaw50");

    private static readonly int TrailYaw75Id =
        Shader.PropertyToID("_TrailYaw75");

    private static readonly int TrailYaw100Id =
        Shader.PropertyToID("_TrailYaw100");

    private static readonly int PathSideSignId =
        Shader.PropertyToID("_PathSideSign");

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

        ResolvePathSideSign();
        ApplyMaterialSettings();
    }

    public void SetHeld(bool value)
    {
        held = value;

        if (held)
        {
            ClearTrail();
        }

        ApplyMaterialSettings();
    }

    public void SetAquariumTurn(
        float normalizedTurn)
    {
        // Compatibility hook only.
        // Actual body turning comes from the recorded head path.
    }

    public void SetAquariumTrail(
        Vector3 headForwardWorld,
        Vector3 forward25World,
        Vector3 forward50World,
        Vector3 forward75World,
        Vector3 forward100World)
    {
        if (held)
            return;

        trailYaw25 =
            RelativeYawRadians(
                headForwardWorld,
                forward25World
            );

        trailYaw50 =
            RelativeYawRadians(
                headForwardWorld,
                forward50World
            );

        trailYaw75 =
            RelativeYawRadians(
                headForwardWorld,
                forward75World
            );

        trailYaw100 =
            RelativeYawRadians(
                headForwardWorld,
                forward100World
            );
    }

    public bool TryGetHeadPosition(
        out Vector3 worldPosition)
    {
        ResolveReferences();

        float length =
            GetBodyLengthWorld();

        worldPosition =
            transform.position +
            transform.forward *
            length *
            0.50f;

        return length > 0.001f;
    }

    public float GetBodyLengthWorld()
    {
        ResolveReferences();

        if (renderers == null ||
            renderers.Length == 0)
        {
            return 0.92f;
        }

        Vector3 forward =
            transform.forward.normalized;

        bool found = false;
        float minProjection = 0f;
        float maxProjection = 0f;

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null)
                continue;

            Bounds bounds =
                renderer.bounds;

            Vector3 center =
                bounds.center;

            Vector3 extents =
                bounds.extents;

            float centerProjection =
                Vector3.Dot(
                    center,
                    forward
                );

            float projectedExtent =
                Mathf.Abs(forward.x) *
                    extents.x +
                Mathf.Abs(forward.y) *
                    extents.y +
                Mathf.Abs(forward.z) *
                    extents.z;

            float localMin =
                centerProjection -
                projectedExtent;

            float localMax =
                centerProjection +
                projectedExtent;

            if (!found)
            {
                minProjection = localMin;
                maxProjection = localMax;
                found = true;
            }
            else
            {
                minProjection =
                    Mathf.Min(
                        minProjection,
                        localMin
                    );

                maxProjection =
                    Mathf.Max(
                        maxProjection,
                        localMax
                    );
            }
        }

        if (!found)
            return 0.92f;

        return
            Mathf.Clamp(
                maxProjection -
                minProjection,
                0.35f,
                1.60f
            );
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

        float beat =
            Mathf.Sin(
                swimPhase
            );

        if (held)
        {
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
        }
        else
        {
            // The head/root owns direction. Only the body behind it deforms.
            visualRoot.localRotation =
                visualBaseRotation;
        }

        ApplyMaterialSettings();
    }

    private static float RelativeYawRadians(
        Vector3 currentForward,
        Vector3 historicalForward)
    {
        Vector3 currentFlat =
            Vector3.ProjectOnPlane(
                currentForward,
                Vector3.up
            );

        Vector3 historicalFlat =
            Vector3.ProjectOnPlane(
                historicalForward,
                Vector3.up
            );

        if (currentFlat.sqrMagnitude <
                0.0001f ||
            historicalFlat.sqrMagnitude <
                0.0001f)
        {
            return 0f;
        }

        float degrees =
            Vector3.SignedAngle(
                currentFlat.normalized,
                historicalFlat.normalized,
                Vector3.up
            );

        // Aquarium turns should never require the mesh to fold through itself.
        degrees =
            Mathf.Clamp(
                degrees,
                -72f,
                72f
            );

        return
            degrees *
            Mathf.Deg2Rad;
    }

    private void ClearTrail()
    {
        trailYaw25 = 0f;
        trailYaw50 = 0f;
        trailYaw75 = 0f;
        trailYaw100 = 0f;
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

        Vector4 sideValue =
            renderers[0]
                .sharedMaterial
                .GetVector(
                    "_SideAxis"
                );

        Vector3 sideAxis =
            new Vector3(
                sideValue.x,
                sideValue.y,
                sideValue.z
            );

        if (sideAxis.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        Vector3 sideWorld =
            renderers[0]
                .transform
                .TransformDirection(
                    sideAxis.normalized
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
                Mathf.Sign(dot);
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
                TrailYaw25Id,
                held ? 0f : trailYaw25
            );

            block.SetFloat(
                TrailYaw50Id,
                held ? 0f : trailYaw50
            );

            block.SetFloat(
                TrailYaw75Id,
                held ? 0f : trailYaw75
            );

            block.SetFloat(
                TrailYaw100Id,
                held ? 0f : trailYaw100
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
