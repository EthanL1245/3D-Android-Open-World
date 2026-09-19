using UnityEngine;

public class YellowfinTunaPresentation : MonoBehaviour
{
    [SerializeField]
    private Renderer[] renderers;

    [Header("Aquarium Swim")]
    [SerializeField]
    private float swimStrength = 0.12f;

    [SerializeField]
    private float swimSpeed = 7.0f;

    [Header("Held Fish")]
    [SerializeField]
    private float heldStrength = 0.055f;

    [SerializeField]
    private float heldSpeed = 5.0f;

    private bool held;
    private float phase;

    private MaterialPropertyBlock block;

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
        if (renderers == null ||
            renderers.Length == 0)
        {
            renderers =
                GetComponentsInChildren<Renderer>(
                    true
                );
        }

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
        ApplyAnimationSettings();
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
