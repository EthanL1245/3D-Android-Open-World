using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fishing danger presentation. High tension keeps the existing red screen-edge
/// vignette. Separately, a fish approaching the equipped line's maximum distance
/// flashes a dedicated border around the fight-status panel without changing any
/// fishing mechanics, panel content, HP, or tension colors.
/// </summary>
[DefaultExecutionOrder(1600)]
public sealed class FishingEscapeVignette : MonoBehaviour
{
    // Shared phase keeps the distance text in sync with the screen edges.
    public static float WarningPulse => 0.5f + 0.5f * Mathf.Sin(Time.time * 11f);

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
    private Image lineWarningBorder;
    private Texture2D lineWarningBorderTexture;
    private Sprite lineWarningBorderSprite;
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
        BuildStatusBorder();
    }

    private void LateUpdate()
    {
        if (overlay == null) BuildOverlay();
        if (lineWarningBorder == null) BuildStatusBorder();

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

        float pulse = 0.72f + WarningPulse * 0.28f;
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

        if (lineWarningBorder == null)
            return;

        if (visibleLineRisk <= 0.001f)
        {
            lineWarningBorder.gameObject.SetActive(false);
            return;
        }

        lineWarningBorder.gameObject.SetActive(true);

        float flashSpeed = Mathf.Lerp(5.5f, 13.5f, visibleLineRisk);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * flashSpeed);

        // This is a separate border-only image positioned just OUTSIDE the panel's
        // authored cyan rim. Unlike Unity's Outline effect it never duplicates or
        // offsets the FightPanel graphic, so there is no second ghost panel border.
        // The warning itself stays continuously visible and simply swaps red/white.
        lineWarningBorder.color = pulse >= 0.5f
            ? new Color(1f, 0.025f, 0.015f, 1f)
            : Color.white;
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

    private void BuildStatusBorder()
    {
        if (lineWarningBorder != null || fishing == null)
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

        GameObject go =
            new GameObject(
                "LineLimitWarningBorder",
                typeof(RectTransform),
                typeof(Image)
            );
        go.transform.SetParent(panel, false);
        // Keep the warning behind every label/bar but above the panel's own Image.
        go.transform.SetAsFirstSibling();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        // The border texture is ~7 UI px thick. Expanding by 10 px keeps its
        // entire visible stroke outside the existing cyan panel border.
        rect.offsetMin = new Vector2(-10f, -10f);
        rect.offsetMax = new Vector2(10f, 10f);

        lineWarningBorderTexture = BuildBorderTexture();
        lineWarningBorderSprite =
            Sprite.Create(
                lineWarningBorderTexture,
                new Rect(0f, 0f, 96f, 96f),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(24f, 24f, 24f, 24f)
            );
        lineWarningBorderSprite.name = "FishingLineLimitWarningBorder_Runtime";

        lineWarningBorder = go.GetComponent<Image>();
        lineWarningBorder.sprite = lineWarningBorderSprite;
        lineWarningBorder.type = Image.Type.Sliced;
        lineWarningBorder.raycastTarget = false;
        lineWarningBorder.color = Color.white;
        go.SetActive(false);
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

    private static Texture2D BuildBorderTexture()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "FishingLineLimitWarningBorder_Runtime",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Match the rounded silhouette used by FishingHudTheme.Panel, but keep
            // ONLY a thick ring. The transparent center prevents any duplicated
            // panel surface from appearing during the warning flash.
            float qx = Mathf.Abs(x - 47.5f) - 28f;
            float qy = Mathf.Abs(y - 47.5f) - 28f;
            float d =
                new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                + Mathf.Min(Mathf.Max(qx, qy), 0f)
                - 17f;

            float outer = Mathf.Clamp01(0.5f - d);
            float inner = Mathf.Clamp01(-6.5f - d);
            float alpha = Mathf.Clamp01(outer - inner);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (vignetteTexture != null) Destroy(vignetteTexture);
        if (lineWarningBorderSprite != null) Destroy(lineWarningBorderSprite);
        if (lineWarningBorderTexture != null) Destroy(lineWarningBorderTexture);
    }
}
