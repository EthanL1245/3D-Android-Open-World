using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared large background artwork for the Tackle Store and Island / Fish Index.
/// Uses a pre-cleaned transparent PNG. The data is split into small constants so it
/// cannot be truncated/corrupted by source transport.
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

        string encoded =
            LargeBackdropData.Part00 + LargeBackdropData.Part01 +
            LargeBackdropData.Part02 + LargeBackdropData.Part03 +
            LargeBackdropData.Part04 + LargeBackdropData.Part05 +
            LargeBackdropData.Part06 + LargeBackdropData.Part07 +
            LargeBackdropData.Part08 + LargeBackdropData.Part09;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException exception)
        {
            Debug.LogError("Backdrop PNG data is invalid: " + exception.Message);
            return null;
        }

        // PNG signature check prevents repeatedly handing corrupt bytes to Unity.
        if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E ||
            bytes[3] != 0x47 || bytes[4] != 0x0D || bytes[5] != 0x0A ||
            bytes[6] != 0x1A || bytes[7] != 0x0A)
        {
            Debug.LogError("Backdrop PNG data failed its signature check.");
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "RoundedShopIndexBackdrop";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        if (!texture.LoadImage(bytes, false))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogError("Backdrop PNG could not be decoded by Unity.");
            return null;
        }

        cachedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(.5f, .5f),
            100f);
        return cachedSprite;
    }
}
