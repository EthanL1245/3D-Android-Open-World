using UnityEngine;

public class YellowfinTunaPresentation : MonoBehaviour
{
    [SerializeField]
    private Renderer[] renderers;

    [SerializeField]
    private Transform visualRoot;

    [Header("Aquarium Swim")]
    [SerializeField]
    private float swimStrength = 0.115f;

    [SerializeField]
    private float swimSpeed = 5.6f;

    [Header("Held Fish")]
    [SerializeField]
    private float heldStrength = 0.145f;

    [SerializeField]
    private float heldSpeed = 5.8f;

    [SerializeField]
    private float heldYawDegrees = 2.2f;

    [SerializeField]
    private float heldRollDegrees = 1.6f;

    private bool held;
    private float phase;
    private float aquariumLocomotionSpeed = 0.45f;

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

    private void Awake()
    {
        ResolveReferences();

        phase =
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
        // Intentionally ignored.
        // The aquarium root/head steering controls direction.
        // No turn correction is layered onto the mesh anymore.
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

        float beat =
            Mathf.Sin(
                Time.time *
                speed +
                phase
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
                        Time.time *
                        speed *
                        0.5f +
                        phase +
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
                ? heldStrength
                : swimStrength;

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
                phase
            );

            renderer.SetPropertyBlock(
                block
            );
        }
    }
}
