using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only pulse shared by upgradeable cards and their enlarged preview.
/// Border, subtle white background wash and lettering breathe in phase, including
/// while the game is paused. All graphic overlays ignore pointer input.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpecialAbilityUpgradePulse : MonoBehaviour
{
    private const float PeriodSeconds = 1.25f;
    private static Sprite borderSprite, washSprite;

    private Image border, wash;
    private Text[] labels;
    private Color[] originalColors;
    private bool ready;

    public void Initialize(Image artwork, params Text[] textElements)
    {
        if (ready || artwork == null) return;
        labels = textElements;
        originalColors = new Color[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            originalColors[i] = labels[i] != null ? labels[i].color : Color.white;

        if (washSprite == null) washSprite = MakeRoundedSprite(false);
        if (borderSprite == null) borderSprite = MakeRoundedSprite(true);

        wash = CreateOverlay(artwork.transform, "UpgradeWhiteTone", washSprite);
        border = CreateOverlay(artwork.transform, "UpgradeFlashingWhiteBorder", borderSprite);
        ready = true;
        ApplyPulse();
    }

    private static Image CreateOverlay(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        // Behind all card text, but above the original card background.
        go.transform.SetAsFirstSibling();
        var graphic = go.GetComponent<Image>();
        graphic.sprite = sprite;
        graphic.type = Image.Type.Sliced;
        graphic.raycastTarget = false;
        return graphic;
    }

    private static Sprite MakeRoundedSprite(bool outlineOnly)
    {
        const int size = 64;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float qx = Mathf.Abs(x - 31.5f) - 20.5f;
            float qy = Mathf.Abs(y - 31.5f) - 20.5f;
            float d = new Vector2(Mathf.Max(0f, qx), Mathf.Max(0f, qy)).magnitude
                + Mathf.Min(Mathf.Max(qx, qy), 0f) - 10f;
            float a = outlineOnly
                ? Mathf.Clamp01(.5f - d) * Mathf.Clamp01(d + 4f)
                : Mathf.Clamp01(.5f - d);
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = outlineOnly ? "AbilityUpgradeBorder" : "AbilityUpgradeWhiteWash";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(14, 14, 14, 14));
    }

    private void Update() { ApplyPulse(); }

    private void ApplyPulse()
    {
        if (!ready) return;
        float strength = .5f + .5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / PeriodSeconds));
        border.color = new Color(1f, 1f, 1f, .25f + .75f * strength);
        wash.color = new Color(1f, 1f, 1f, .025f + .115f * strength);
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] != null)
                labels[i].color = Color.Lerp(originalColors[i], Color.white,
                    .06f + .32f * strength);
    }

    private void OnDisable()
    {
        if (!ready) return;
        if (border != null) border.color = Color.clear;
        if (wash != null) wash.color = Color.clear;
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] != null) labels[i].color = originalColors[i];
    }
}
