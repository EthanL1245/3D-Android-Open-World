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
    private float swimSpeed = 8.0f;

    [SerializeField]
    private float bodyYawDegrees = 2.4f;

    [SerializeField]
    private float bodyRollDegrees = 0.55f;

    [Header("Held Fish")]
    [SerializeField]
    private float heldStrength = 0.085f;

    [SerializeField]
    private float heldSpeed = 5.5f;

    [SerializeField]
    private float heldYawDegrees = 3.2f;

    [SerializeField]
    private float heldRollDegrees = 1.2f;

    private bool held;
    private float phase;

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

        ApplyAnimationSettings();
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

        ApplyAnimationSettings();
    }

    public void SetHeld(bool value)
    {
        held = value;
        ApplyAnimationSettings();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyAnimationSettings();
    }

    private void Update()
    {
        if (visualRoot == null)
            return;

        float speed =
            held
                ? heldSpeed
                : swimSpeed;

        float beat =
            Mathf.Sin(
                Time.time *
                speed +
                phase
            );

        float yaw =
            beat *
            (
                held
                    ? heldYawDegrees
                    : bodyYawDegrees
            );

        float roll =
            Mathf.Sin(
                Time.time *
                speed *
                0.5f +
                phase +
                0.8f
            ) *
            (
                held
                    ? heldRollDegrees
                    : bodyRollDegrees
            );

        visualRoot.localRotation =
            visualBaseRotation *
            Quaternion.Euler(
                0f,
                yaw,
                roll
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

    private void ApplyAnimationSettings()
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
                : swimSpeed;

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
