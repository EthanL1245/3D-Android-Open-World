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

    private bool originalFogEnabled;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity;

    private float underwaterBlend;

    private void Awake()
    {
        CacheOriginalFog();

        if (oceanWater == null)
        {
            oceanWater = FindFirstObjectByType<OceanWater>();
        }
    }

    private void Update()
    {
        if (oceanWater == null)
        {
            oceanWater = FindFirstObjectByType<OceanWater>();

            if (oceanWater == null)
                return;
        }

        bool underwater = ShopWaterVolume.TrySurface(transform.position,oceanWater,out float surface) && transform.position.y < surface;

        underwaterBlend = Mathf.MoveTowards(
            underwaterBlend,
            underwater ? 1f : 0f,
            transitionSpeed * Time.deltaTime
        );

        ApplyVisuals();
    }

    public void Configure(OceanWater water, Image overlay)
    {
        oceanWater = water;
        underwaterOverlay = overlay;
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
        if (underwaterOverlay != null)
        {
            Color color = underwaterTint;
            color.a = overlayAlpha * underwaterBlend;
            underwaterOverlay.color = color;
        }

        if (underwaterBlend > 0.001f)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor =
                Color.Lerp(
                    originalFogColor,
                    underwaterFogColor,
                    underwaterBlend
                );

            RenderSettings.fogDensity =
                Mathf.Lerp(
                    originalFogDensity,
                    underwaterFogDensity,
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
        if (underwaterOverlay != null)
        {
            Color color = underwaterOverlay.color;
            color.a = 0f;
            underwaterOverlay.color = color;
        }

        RestoreFog();
    }
}
