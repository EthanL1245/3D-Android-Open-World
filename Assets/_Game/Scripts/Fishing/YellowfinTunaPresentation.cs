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

    // Signed turn request from TankFishAgent.
    // The head/root still owns locomotion. These staged followers make the
    // turn propagate backward through the body over TIME instead of making
    // the whole tuna bend in the same frame.
    private float aquariumTurnTarget;

    private float turnFront;
    private float turnMid;
    private float turnRear;
    private float turnTail;

    private float turnFrontVelocity;
    private float turnMidVelocity;
    private float turnRearVelocity;
    private float turnTailVelocity;

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

    private static readonly int TurnFrontId =
        Shader.PropertyToID(
            "_TurnFront"
        );

    private static readonly int TurnMidId =
        Shader.PropertyToID(
            "_TurnMid"
        );

    private static readonly int TurnRearId =
        Shader.PropertyToID(
            "_TurnRear"
        );

    private static readonly int TurnTailId =
        Shader.PropertyToID(
            "_TurnTail"
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

        ApplyMaterialSettings();
    }

    public void SetHeld(bool value)
    {
        held = value;
        ApplyMaterialSettings();
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

        float turnTarget =
            held
                ? 0f
                : aquariumTurnTarget;

        // Head/front reacts first. Every stage behind it follows the stage
        // ahead with a slightly longer response time. This is deliberately
        // a chained follower, not four copies of the same target.
        turnFront =
            Mathf.SmoothDamp(
                turnFront,
                turnTarget,
                ref turnFrontVelocity,
                0.07f,
                Mathf.Infinity,
                Time.deltaTime
            );

        turnMid =
            Mathf.SmoothDamp(
                turnMid,
                turnFront,
                ref turnMidVelocity,
                0.11f,
                Mathf.Infinity,
                Time.deltaTime
            );

        turnRear =
            Mathf.SmoothDamp(
                turnRear,
                turnMid,
                ref turnRearVelocity,
                0.15f,
                Mathf.Infinity,
                Time.deltaTime
            );

        turnTail =
            Mathf.SmoothDamp(
                turnTail,
                turnRear,
                ref turnTailVelocity,
                0.19f,
                Mathf.Infinity,
                Time.deltaTime
            );

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
            // Keep the head/visual aligned exactly to the locomotion root.
            visualRoot.localRotation =
                visualBaseRotation;
        }

        ApplyMaterialSettings();
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
                TurnFrontId,
                held
                    ? 0f
                    : turnFront
            );

            block.SetFloat(
                TurnMidId,
                held
                    ? 0f
                    : turnMid
            );

            block.SetFloat(
                TurnRearId,
                held
                    ? 0f
                    : turnRear
            );

            block.SetFloat(
                TurnTailId,
                held
                    ? 0f
                    : turnTail
            );

            renderer.SetPropertyBlock(
                block
            );
        }
    }
}
