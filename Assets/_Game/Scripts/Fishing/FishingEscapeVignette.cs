using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fishing danger presentation. High tension keeps the existing red screen-edge
/// vignette. Separately, a fish approaching the equipped line's maximum distance
/// flashes a red OUTLINE around the fight-status panel without changing any of the
/// panel, HP, or tension colors themselves.
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
    private FieldInfo maximumLineDistanceField;
    private FieldInfo fishingLineField;

    private RawImage overlay;
    private Texture2D vignetteTexture;
    private Outline lineWarningOutline;
    private float visibleRisk;
    private float visibleLineRisk;

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
        maximumLineDistanceField = type.GetField("maximumLineDistance", flags);
        fishingLineField = type.GetField("fishingLine", flags);
    }

    private void Start()
    {
        BuildOverlay();
        BuildStatusOutline();
    }

    private void LateUpdate()
    {
        if (overlay == null) BuildOverlay();
        if (lineWarningOutline == null) BuildStatusOutline();

        if (fishing == null || stateField == null || tensionField == null ||
            lineBreakTimerField == null || unconsciousField == null)
            return;

        string state = stateField.GetValue(fishing)?.ToString() ?? string.Empty;
        bool fighting = state == "Fighting" && !(bool)unconsciousField.GetValue(fishing);

        UpdateTensionVignette(fighting);
        UpdateLineDistanceWarning(fighting);
    }

    private void UpdateTensionVignette(bool fighting)
    {
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

        if (overlay == null)
            return;

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

    private void UpdateLineDistanceWarning(bool fighting)
    {
        float targetRisk = 0f;

        if (fighting &&
            maximumLineDistanceField != null &&
            fishingLineField != null)
        {
            float maximum =
                Mathf.Max(
                    0.01f,
                    (float)maximumLineDistanceField.GetValue(fishing)
                );

            LineRenderer line =
                fishingLineField.GetValue(fishing) as LineRenderer;

            float length = RenderedLineLength(line);

            // The range limiter fails the fishing action once the rendered line
            // exceeds the equipped line maximum. Begin a subtle border warning at
            // 80%, then flash rapidly as the fish approaches that hard limit.
            targetRisk =
                Mathf.Clamp01(
                    Mathf.InverseLerp(
                        maximum * 0.80f,
                        maximum * 0.985f,
                        length
                    )
                );
        }

        float speed = targetRisk > visibleLineRisk ? 7f : 10f;
        visibleLineRisk =
            Mathf.MoveTowards(
                visibleLineRisk,
                targetRisk,
                Time.deltaTime * speed
            );

        if (lineWarningOutline == null)
            return;

        if (visibleLineRisk <= 0.001f)
        {
            lineWarningOutline.effectColor = new Color(1f, 0f, 0f, 0f);
            return;
        }

        float flashSpeed = Mathf.Lerp(5.5f, 13.5f, visibleLineRisk);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * flashSpeed);
        float alpha =
            Mathf.Lerp(0.18f, 1f, visibleLineRisk) *
            Mathf.Lerp(0.35f, 1f, pulse);

        float thickness = Mathf.Lerp(2f, 7f, visibleLineRisk);
        lineWarningOutline.effectDistance = new Vector2(thickness, -thickness);
        lineWarningOutline.effectColor = new Color(1f, 0.025f, 0.015f, alpha);
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

    private void BuildStatusOutline()
    {
        if (lineWarningOutline != null || fishing == null)
            return;

        Canvas canvas =
            gameplayCanvasField != null
                ? gameplayCanvasField.GetValue(fishing) as Canvas
                : null;

        FishingHUD hud =
            canvas != null
                ? canvas.GetComponent<FishingHUD>()
                : FindFirstObjectByType<FishingHUD>();

        if (hud == null)
            return;

        Transform panel = FindDeepChild(hud.transform, "FightPanel");
        if (panel == null || panel.GetComponent<Graphic>() == null)
            return;

        lineWarningOutline = panel.gameObject.AddComponent<Outline>();
        lineWarningOutline.useGraphicAlpha = false;
        lineWarningOutline.effectDistance = new Vector2(2f, -2f);
        lineWarningOutline.effectColor = new Color(1f, 0f, 0f, 0f);
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == name)
                return children[i];
        }

        return null;
    }

    private static float RenderedLineLength(LineRenderer line)
    {
        if (line == null || !line.enabled || line.positionCount < 2)
            return 0f;

        float length = 0f;
        Vector3 previous = LinePointWorld(line, 0);

        for (int i = 1; i < line.positionCount; i++)
        {
            Vector3 current = LinePointWorld(line, i);
            length += Vector3.Distance(previous, current);
            previous = current;
        }

        return length;
    }

    private static Vector3 LinePointWorld(LineRenderer line, int index)
    {
        Vector3 point = line.GetPosition(index);
        return line.useWorldSpace ? point : line.transform.TransformPoint(point);
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
