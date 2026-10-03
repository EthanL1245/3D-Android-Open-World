using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Briefly announces meaningful world-area transitions at the top center of screen.
/// It tracks the actual travel destination / fishing biome instead of teleport calls,
/// so walking or boating naturally into Deep Ocean also gets the same presentation.
/// </summary>
[DefaultExecutionOrder(6700)]
public sealed class AreaEntryBannerRuntime : MonoBehaviour
{
    private const float StableSeconds = 0.18f;
    private const float HoldSeconds = 1.75f;
    private const float FadeSeconds = 0.65f;

    private ShopDimensionManager travel;
    private FirstPersonController player;
    private GameObject banner;
    private CanvasGroup group;
    private Text label;

    private string currentArea;
    private string candidateArea;
    private float candidateSince;
    private float shownAt = -100f;

    private void Awake()
    {
        travel = ShopDimensionManager.Instance;
        FishingSystem fishing = FindFirstObjectByType<FishingSystem>();
        player = fishing != null ? fishing.GetComponent<FirstPersonController>() : FindFirstObjectByType<FirstPersonController>();
        BuildUI();
        currentArea = ResolveArea();
        candidateArea = currentArea;
        candidateSince = Time.unscaledTime;
    }

    private void Update()
    {
        if (travel == null) travel = ShopDimensionManager.Instance;
        if (player == null)
        {
            FishingSystem fishing = FindFirstObjectByType<FishingSystem>();
            player = fishing != null ? fishing.GetComponent<FirstPersonController>() : FindFirstObjectByType<FirstPersonController>();
        }

        string next = ResolveArea();
        if (next != candidateArea)
        {
            candidateArea = next;
            candidateSince = Time.unscaledTime;
        }
        else if (!string.IsNullOrEmpty(next) && next != currentArea && Time.unscaledTime - candidateSince >= StableSeconds)
        {
            currentArea = next;
            Show(next);
        }

        UpdateFade();
    }

    private string ResolveArea()
    {
        if (travel != null)
        {
            if (travel.InShop) return "Tideglass Quay";
            if (travel.InHome) return "Home";
            if (travel.Traveling) return currentArea;
        }

        if (player == null) return currentArea;
        int biome = SnapperIslandGeometry.ResolveBiome(player.transform.position);
        if (biome >= 0 && biome < ReefCatalog.Zones.Length)
            return ReefCatalog.Zones[biome].name;
        return currentArea;
    }

    private void Show(string area)
    {
        if (label == null || group == null) return;
        label.text = "ENTERING  " + area.ToUpperInvariant();
        group.alpha = 1f;
        banner.SetActive(true);
        banner.transform.SetAsLastSibling();
        shownAt = Time.unscaledTime;
    }

    private void UpdateFade()
    {
        if (banner == null || group == null || !banner.activeSelf) return;
        float elapsed = Time.unscaledTime - shownAt;
        if (elapsed <= HoldSeconds)
        {
            group.alpha = 1f;
            return;
        }

        group.alpha = 1f - Mathf.Clamp01((elapsed - HoldSeconds) / FadeSeconds);
        if (elapsed >= HoldSeconds + FadeSeconds)
            banner.SetActive(false);
    }

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        banner = new GameObject("AreaEntryBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Text), typeof(Outline));
        banner.transform.SetParent(parent, false);
        RectTransform rect = banner.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, 1f);
        rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(1100f, 92f);
        rect.anchoredPosition = new Vector2(0f, -92f);

        label = banner.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 52;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 34;
        label.resizeTextMaxSize = 52;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, .91f, .62f, 1f);
        label.raycastTarget = false;

        Outline outline = banner.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .92f);
        outline.effectDistance = new Vector2(2.2f, -2.2f);
        outline.useGraphicAlpha = true;

        group = banner.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;
        banner.SetActive(false);
    }
}