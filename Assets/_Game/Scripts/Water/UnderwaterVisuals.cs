using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class UnderwaterVisuals : MonoBehaviour
{
    [SerializeField] private OceanWater oceanWater;
    [SerializeField] private Image underwaterOverlay;

    [Header("Underwater Look")]
    [SerializeField] private Color underwaterFogColor =
        new Color(0.015f, 0.16f, 0.22f, 1f);

    [SerializeField] private Color underwaterTint =
        new Color(0.00f, 0.24f, 0.34f, 1f);

    [SerializeField] private float underwaterFogDensity = 0.035f;
    [SerializeField] private float overlayAlpha = 0.15f;
    [SerializeField] private float transitionSpeed = 5f;

    private static readonly Color DeepFogColor =
        new Color(0.001f, 0.012f, 0.018f, 1f);

    private static readonly Color DeepTint =
        new Color(0.002f, 0.030f, 0.045f, 1f);

    private bool originalFogEnabled;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity;

    private DeepOceanWaterRuntime deepWater;
    private float underwaterBlend;
    private float deepVisualBlend;

    private void Awake()
    {
        CacheOriginalFog();
        ResolveWater();
    }

    private void Update()
    {
        ResolveWater();
        if (oceanWater == null)
            return;

        bool hasSurface =
            ShopWaterVolume.TrySurface(
                transform.position,
                oceanWater,
                out float surface
            );

        bool underwater =
            hasSurface &&
            transform.position.y < surface;

        float depthBlend =
            deepWater != null
                ? Mathf.Clamp01(deepWater.VisibilityBlend)
                : 0f;

        // When the camera sits right on the surface, the transparent water mesh can
        // fall behind the near clip plane while looking down. In deep water that used
        // to reveal the entire seabed for a frame or indefinitely. Keep a partial
        // water-column fog active in this narrow surface band whenever the player is
        // looking down, even if the camera origin is a few centimetres above a wave.
        float surfaceLookDownBlend = 0f;
        if (hasSurface && !underwater && depthBlend > 0.001f)
        {
            float heightAboveSurface =
                Mathf.Max(0f, transform.position.y - surface);

            float surfaceBand =
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        0.04f,
                        0.48f,
                        heightAboveSurface
                    )
                );

            float lookingDown =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(
                        0.05f,
                        0.60f,
                        -transform.forward.y
                    )
                );

            surfaceLookDownBlend =
                Mathf.Clamp01(
                    surfaceBand *
                    lookingDown *
                    depthBlend
                );
        }

        float targetBlend =
            underwater
                ? 1f
                : surfaceLookDownBlend;

        float targetDeepBlend =
            underwater
                ? depthBlend
                : surfaceLookDownBlend;

        underwaterBlend =
            Mathf.MoveTowards(
                underwaterBlend,
                targetBlend,
                transitionSpeed * Time.deltaTime
            );

        deepVisualBlend =
            Mathf.MoveTowards(
                deepVisualBlend,
                targetDeepBlend,
                transitionSpeed * Time.deltaTime
            );

        ApplyVisuals();
    }

    public void Configure(OceanWater water, Image overlay)
    {
        oceanWater = water;
        underwaterOverlay = overlay;
        deepWater =
            oceanWater != null
                ? oceanWater.GetComponent<DeepOceanWaterRuntime>()
                : null;
    }

    private void ResolveWater()
    {
        if (oceanWater == null)
            oceanWater = FindFirstObjectByType<OceanWater>();

        if (deepWater == null && oceanWater != null)
            deepWater = oceanWater.GetComponent<DeepOceanWaterRuntime>();
    }

    private void CacheOriginalFog()
    {
        originalFogEnabled = RenderSettings.fog;
        originalFogMode = RenderSettings.fogMode;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;
    }

    private void ApplyVisuals()
    {
        Color activeTint =
            Color.Lerp(
                underwaterTint,
                DeepTint,
                deepVisualBlend
            );

        float activeOverlayAlpha =
            Mathf.Lerp(
                overlayAlpha,
                0.34f,
                deepVisualBlend
            );

        if (underwaterOverlay != null)
        {
            Color color = activeTint;
            color.a = activeOverlayAlpha * underwaterBlend;
            underwaterOverlay.color = color;
        }

        if (underwaterBlend > 0.001f)
        {
            Color activeFogColor =
                Color.Lerp(
                    underwaterFogColor,
                    DeepFogColor,
                    deepVisualBlend
                );

            float activeFogDensity =
                Mathf.Lerp(
                    underwaterFogDensity,
                    0.13f,
                    deepVisualBlend
                );

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor =
                Color.Lerp(
                    originalFogColor,
                    activeFogColor,
                    underwaterBlend
                );

            RenderSettings.fogDensity =
                Mathf.Lerp(
                    originalFogDensity,
                    activeFogDensity,
                    underwaterBlend
                );
        }
        else
        {
            RestoreFog();
        }
    }

    private void RestoreFog()
    {
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
    }

    private void OnDisable()
    {
        underwaterBlend = 0f;
        deepVisualBlend = 0f;

        if (underwaterOverlay != null)
        {
            Color color = underwaterOverlay.color;
            color.a = 0f;
            underwaterOverlay.color = color;
        }

        RestoreFog();
    }
}
