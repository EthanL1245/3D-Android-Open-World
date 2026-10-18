using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual-only Burning Fish status effect. Uses the six user-supplied transparent
/// flame frames in a horizontal atlas, cycling quickly and unpredictably around
/// both status icons while tinting the active fishing line red. No gameplay damage.
/// </summary>
[DefaultExecutionOrder(5000)]
[DisallowMultipleComponent]
public sealed class FishingBurnVisuals : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (FishingSystem fishing in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if (fishing != null && fishing.GetComponent<FishingBurnVisuals>() == null)
                fishing.gameObject.AddComponent<FishingBurnVisuals>();
    }
    private const float FrameInterval = .095f;
    private static Texture2D atlas;
    private static bool atlasLoaded;

    private FishingSystem fishing;
    private FishingHUD hud;
    private FishingBurstDamageRuntime damage;
    private LineRenderer lastLine;
    private Material lastMaterial;
    private Color savedLineColor;
    private bool lineRed;
    private RawImage worldIcon, hudIcon, worldFlames, hudFlames;
    private bool burning;
    private float nextFrame;
    private int frame = -1;

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        damage = GetComponent<FishingBurstDamageRuntime>();
    }

    private static void LoadAtlas()
    {
        if (atlasLoaded) return;
        atlasLoaded = true;
        TextAsset source = Resources.Load<TextAsset>("Fishing/StatusIcons/BurningFlamesAtlas");
        if (source == null)
        {
            Debug.LogWarning("Burning Fish: Resources/Fishing/StatusIcons/BurningFlamesAtlas.txt not found.");
            return;
        }
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(Convert.FromBase64String(source.text.Trim()), true))
            {
                Destroy(tex);
                return;
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            atlas = tex;
        }
        catch (Exception error) { Debug.LogWarning("Could not load Burning Fish flame animation: " + error.Message); }
    }

    private void LateUpdate()
    {
        if (fishing == null || damage == null) return;
        if (hud == null) hud = GetComponent<FishingHUD>();
        if (hud == null) hud = FindFirstObjectByType<FishingHUD>();
        bool active = damage.BurningSeconds > .0001f;
        if (active && !atlasLoaded) LoadAtlas();

        if (active && !burning)
        {
            burning = true;
            frame = -1;
            nextFrame = 0f;
        }
        if (!active && burning)
        {
            burning = false;
            RestoreLine();
        }
        if (!active)
        {
            if (worldFlames != null) worldFlames.gameObject.SetActive(false);
            if (hudFlames != null) hudFlames.gameObject.SetActive(false);
            return;
        }

        SetLineRed();
        if (atlas == null) return;
        EnsureIcons();
        if (Time.unscaledTime >= nextFrame)
        {
            int choice = UnityEngine.Random.Range(0, 6);
            if (choice == frame) choice = (choice + UnityEngine.Random.Range(1, 6)) % 6;
            frame = choice;
            nextFrame = Time.unscaledTime + FrameInterval;
        }
        Rect frameUv = new Rect(frame / 6f, 0f, 1f / 6f, 1f);
        if (worldFlames != null) { worldFlames.gameObject.SetActive(true); worldFlames.uvRect = frameUv; }
        if (hudFlames != null) { hudFlames.gameObject.SetActive(true); hudFlames.uvRect = frameUv; }
    }

    private void EnsureIcons()
    {
        RawImage world = fishing.PresentationFishStatusIcon;
        RawImage combat = hud != null ? hud.CombatFishStatusIcon : null;
        if (worldIcon != world)
        {
            worldIcon = world;
            worldFlames = CreateFlames(world, "BurningWorldFishStatus");
        }
        if (hudIcon != combat)
        {
            hudIcon = combat;
            hudFlames = CreateFlames(combat, "BurningFightPanelStatus");
        }
    }

    private static RawImage CreateFlames(RawImage icon, string name)
    {
        if (icon == null) return null;
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(icon.transform, false);
        var rect = go.GetComponent<RectTransform>();
        // The transparent hole stays centered on the fish-status icon; the
        // flames extend above and around all four edges, without blocking text.
        rect.anchorMin = new Vector2(-.33f, -.30f);
        rect.anchorMax = new Vector2(1.33f, 1.33f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<RawImage>();
        image.texture = atlas;
        image.raycastTarget = false;
        image.color = Color.white;
        image.uvRect = new Rect(0, 0, 1f / 6f, 1f);
        go.SetActive(false);
        return image;
    }

    private void SetLineRed()
    {
        LineRenderer line = fishing.PresentationLine;
        if (line == null) { RestoreLine(); return; }
        Material mat = line.material;
        if (mat == null) return;
        if (lineRed && (line != lastLine || mat != lastMaterial)) RestoreLine();
        if (!lineRed)
        {
            lastLine = line;
            lastMaterial = mat;
            savedLineColor = mat.HasProperty("_BaseColor")
                ? mat.GetColor("_BaseColor")
                : mat.HasProperty("_Color") ? mat.color : Color.white;
            lineRed = true;
        }
        Color burningRed = new Color(1f, .075f, .035f, savedLineColor.a);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", burningRed);
        else if (mat.HasProperty("_Color")) mat.color = burningRed;
    }

    private void RestoreLine()
    {
        if (lineRed && lastMaterial != null)
        {
            if (lastMaterial.HasProperty("_BaseColor")) lastMaterial.SetColor("_BaseColor", savedLineColor);
            else if (lastMaterial.HasProperty("_Color")) lastMaterial.color = savedLineColor;
        }
        lastLine = null;
        lastMaterial = null;
        lineRed = false;
    }

    private void OnDisable()
    {
        burning = false;
        if (worldFlames != null) worldFlames.gameObject.SetActive(false);
        if (hudFlames != null) hudFlames.gameObject.SetActive(false);
        RestoreLine();
    }

    private void OnDestroy() { RestoreLine(); }
}
