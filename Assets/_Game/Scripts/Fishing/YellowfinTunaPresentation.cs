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

    // Thunniform turn model:
    // the head/front body stays comparatively rigid.
    // Only the posterior body and tail trail the head with delay.
    private float aquariumTurnTarget;

    private float rearTurn;
    private float tailTurn;

    private float rearTurnVelocity;
    private float tailTurnVelocity;

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

        if (held)
        {
            aquariumTurnTarget = 0f;
        }

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

        // Rear body begins following after the head starts turning.
        rearTurn =
            Mathf.SmoothDamp(
                rearTurn,
                turnTarget,
                ref rearTurnVelocity,
                0.13f,
                Mathf.Infinity,
                Time.deltaTime
            );

        // Tail follows the rear body, never the head directly.
        // This creates a smooth trailing response instead of an instant bend.
        tailTurn =
            Mathf.SmoothDamp(
                tailTurn,
                rearTurn,
                ref tailTurnVelocity,
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
            // Head/front remains exactly aligned to the locomotion root.
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
                TurnRearId,
                held
                    ? 0f
                    : rearTurn
            );

            block.SetFloat(
                TurnTailId,
                held
                    ? 0f
                    : tailTurn
            );

            renderer.SetPropertyBlock(
                block
            );
        }
    }
}
