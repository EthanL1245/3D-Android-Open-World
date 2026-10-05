using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared presentation for the store and both index pages.</summary>
public static class LargeShopBackdropTheme
{
    private const string BackdropName = "SharedShopIndexArtwork";
    private const float Alpha = .90f;
    private static Sprite cachedSprite;
    private static bool attemptedLoad;

    public static void Apply(Image target)
    {
        if (target == null) return;
        Sprite sprite = GetSprite();
        if (sprite == null) return;
        Transform child = target.transform.Find(BackdropName);
        Image artwork;
        if (child == null)
        {
            var go = new GameObject(BackdropName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(target.transform, false);
            go.transform.SetAsFirstSibling();
            artwork = go.GetComponent<Image>();
            artwork.raycastTarget = false;
            var rect = artwork.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            // Header/tabs fill the modal width: expose the luminous edge just outside.
            rect.offsetMin = new Vector2(-8f, -8f);
            rect.offsetMax = new Vector2(8f, 8f);
        }
        else artwork = child.GetComponent<Image>();
        artwork.gameObject.SetActive(true);
        artwork.sprite = sprite;
        artwork.type = Image.Type.Sliced;
        artwork.preserveAspect = false;
        artwork.fillCenter = true;
        artwork.color = new Color(1f, 1f, 1f, Alpha);
        // Only the artwork is translucent; labels and controls retain their opacity.
        target.sprite = null;
        target.color = Color.clear;
    }

    public static void RestoreDefault(Image target, Color color)
    {
        if (target == null) return;
        Transform artwork = target.transform.Find(BackdropName);
        if (artwork != null) artwork.gameObject.SetActive(false);
        target.sprite = null;
        target.type = Image.Type.Simple;
        target.preserveAspect = false;
        target.color = color;
    }

    public static Sprite GetSprite()
    {
        if (cachedSprite != null || attemptedLoad) return cachedSprite;
        attemptedLoad = true;
        // Original uploaded PNG, byte-for-byte. No base64 or importer rescaling.
        TextAsset source = Resources.Load<TextAsset>("ShopIndexBackdrop.png");
        var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (source == null || !original.LoadImage(source.bytes, false))
        {
            Object.Destroy(original);
            Debug.LogError("Could not load the supplied shop/index backdrop PNG.");
            return null;
        }
        // Remove the ragged exterior glow. Image top-left bounds: (28,59)-(1645,879).
        const int left = 28, bottom = 62, width = 1617, height = 820;
        if (original.width != 1672 || original.height != 941)
        {
            Object.Destroy(original);
            Debug.LogError("Shop/index backdrop PNG dimensions have changed.");
            return null;
        }
        Color[] pixels = original.GetPixels(left, bottom, width, height);
        Object.Destroy(original);
        Resources.UnloadAsset(source);
        // Concentric with FishingHudTheme's 17-unit corners plus our 8-unit inset.
        // Nine-slicing preserves this curvature instead of stretching it oval.
        const float radius = 25f;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float dx = Mathf.Max(radius - Mathf.Min(x + .5f, width - x - .5f), 0f);
            float dy = Mathf.Max(radius - Mathf.Min(y + .5f, height - y - .5f), 0f);
            pixels[y * width + x].a *= Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "SuppliedRoundedShopIndexBackdrop";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        cachedSprite = Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(32, 32, 32, 32));
        return cachedSprite;
    }
}
