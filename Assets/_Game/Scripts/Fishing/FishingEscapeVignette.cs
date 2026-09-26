using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a red edge vignette when a hooked fish is close to snapping the line.
/// It is presentation-only and reads the existing FishingSystem risk state, so it
/// cannot change fight balance, tension, damage or escape timing.
/// </summary>
[DefaultExecutionOrder(1600)]
public sealed class FishingEscapeVignette : MonoBehaviour
{
    private FishingSystem fishing;
    private FieldInfo stateField;
    private FieldInfo tensionField;
    private FieldInfo lineBreakTimerField;
    private FieldInfo unconsciousField;
    private FieldInfo gameplayCanvasField;

    private RawImage overlay;
    private Texture2D vignetteTexture;
    private float visibleRisk;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if (system != null && system.GetComponent<FishingEscapeVignette>() == null)
                system.gameObject.AddComponent<FishingEscapeVignette>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        System.Type type = typeof(FishingSystem);
        stateField = type.GetField("state", flags);
        tensionField = type.GetField("fightTension", flags);
        lineBreakTimerField = type.GetField("lineBreakTimer", flags);
        unconsciousField = type.GetField("fishUnconscious", flags);
        gameplayCanvasField = type.GetField("gameplayCanvas", flags);
    }

    private void Start()
    {
        BuildOverlay();
    }

    private void LateUpdate()
    {
        if (overlay == null) BuildOverlay();
        if (overlay == null || fishing == null || stateField == null || tensionField == null ||
            lineBreakTimerField == null || unconsciousField == null)
            return;

        string state = stateField.GetValue(fishing)?.ToString() ?? string.Empty;
        bool fighting = state == "Fighting" && !(bool)unconsciousField.GetValue(fishing);
        float targetRisk = 0f;

        if (fighting)
        {
            float tension = Mathf.Clamp01((float)tensionField.GetValue(fishing));
            float breakTimer = Mathf.Max(0f, (float)lineBreakTimerField.GetValue(fishing));

            // The actual line starts accumulating failure time at 98.5% tension
            // and snaps at 0.55 s. Begin warning a little earlier so the player
            // sees danger building rather than getting a surprise failure.
            float tensionRisk = Mathf.InverseLerp(0.88f, 0.985f, tension) * 0.68f;
            float snapRisk = Mathf.InverseLerp(0.06f, 0.55f, breakTimer);
            targetRisk = Mathf.Clamp01(Mathf.Max(tensionRisk, snapRisk));
        }

        float speed = targetRisk > visibleRisk ? 5.5f : 8f;
        visibleRisk = Mathf.MoveTowards(visibleRisk, targetRisk, Time.deltaTime * speed);

        if (visibleRisk <= 0.001f)
        {
            overlay.gameObject.SetActive(false);
            return;
        }

        overlay.gameObject.SetActive(true);
        overlay.transform.SetAsLastSibling();

        float pulse = 0.86f + Mathf.Sin(Time.time * 11f) * 0.14f;
        float alpha = Mathf.Lerp(0.03f, 0.58f, Mathf.SmoothStep(0f, 1f, visibleRisk)) * pulse;
        overlay.color = new Color(0.92f, 0.015f, 0.01f, alpha);
    }

    private void BuildOverlay()
    {
        if (overlay != null || fishing == null || gameplayCanvasField == null) return;
        Canvas canvas = gameplayCanvasField.GetValue(fishing) as Canvas;
        if (canvas == null) canvas = FindFirstObjectByType<FishingHUD>()?.GetComponent<Canvas>();
        if (canvas == null) return;

        GameObject go = new GameObject("Fishing Escape Danger Vignette", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlay = go.GetComponent<RawImage>();
        overlay.raycastTarget = false;
        vignetteTexture = BuildTexture();
        overlay.texture = vignetteTexture;
        overlay.color = new Color(0.92f, 0.015f, 0.01f, 0f);
        go.SetActive(false);
    }

    private static Texture2D BuildTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "FishingEscapeVignette_Runtime",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
            float ny = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
            float edge = Mathf.Max(nx, ny);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1f, edge));
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (vignetteTexture != null) Destroy(vignetteTexture);
    }
}
