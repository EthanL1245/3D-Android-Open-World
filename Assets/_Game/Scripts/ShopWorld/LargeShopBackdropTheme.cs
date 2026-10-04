using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared large background artwork for the Tackle Store and Island / Fish Index.
/// The user's rounded wave panel is embedded as a compact image; its neutral dark
/// canvas is converted to transparency at runtime so only the rounded blue panel is
/// visible over the game.
/// </summary>
public static class LargeShopBackdropTheme
{
    private const float Alpha = 0.84f;
    private static Sprite cachedSprite;

    public static void Apply(Image target)
    {
        if (target == null) return;
        Sprite sprite = GetSprite();
        if (sprite == null) return;
        target.sprite = sprite;
        target.type = Image.Type.Simple;
        target.preserveAspect = false;
        target.fillCenter = true;
        target.color = new Color(1f, 1f, 1f, Alpha);
    }

    public static void RestoreDefault(Image target, Color color)
    {
        if (target == null) return;
        target.sprite = null;
        target.type = Image.Type.Simple;
        target.preserveAspect = false;
        target.color = color;
    }

    public static Sprite GetSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(EncodedImage); }
        catch (FormatException exception)
        {
            Debug.LogError("Backdrop image data is invalid: " + exception.Message);
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "LargeShopBackdrop";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        if (!texture.LoadImage(bytes, false))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogError("Backdrop image could not be decoded by Unity.");
            return null;
        }

        RemoveOutsideCanvas(texture);
        cachedSprite = UnityEngine.Sprite.Create(
            texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f), 100f);
        return cachedSprite;
    }

    private static void RemoveOutsideCanvas(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            int max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
            int min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));

            // Remove only the neutral dark canvas outside the already-rounded panel.
            // The navy panel and cyan glow have enough blue/cyan chroma to be kept.
            if (max <= 70 && max - min <= 10)
                pixels[i] = new Color32(p.r, p.g, p.b, 0);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private const string EncodedImage =
        "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";
}
