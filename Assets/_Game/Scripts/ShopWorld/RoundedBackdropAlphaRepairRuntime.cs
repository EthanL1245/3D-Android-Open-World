using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Final backdrop pass for the Tackle Store and Island / Fish Index.
/// LargeShopBackdropTheme embeds the user's latest rounded artwork as JPEG data.
/// JPEG itself has no alpha channel, so this component copies the decoded pixels into
/// RGBA32, removes only the neutral dark canvas surrounding the rounded blue panel,
/// and reapplies the resulting transparent sprite after the normal presentation pass.
/// </summary>
[DefaultExecutionOrder(7350)]
public sealed class RoundedBackdropAlphaRepairRuntime : MonoBehaviour
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo PageField = typeof(ShopWorldHUD).GetField("page", PrivateInstance);
    private static readonly FieldInfo ModalField = typeof(ShopWorldHUD).GetField("modal", PrivateInstance);
    private static readonly FieldInfo EncodedImageField = typeof(LargeShopBackdropTheme).GetField(
        "EncodedImage", BindingFlags.Static | BindingFlags.NonPublic);

    private const float Alpha = .84f;
    private ShopWorldHUD hud;
    private static Sprite transparentSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach (ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if (target != null && target.GetComponent<RoundedBackdropAlphaRepairRuntime>() == null)
                target.gameObject.AddComponent<RoundedBackdropAlphaRepairRuntime>();
    }

    private void Awake()
    {
        hud = GetComponent<ShopWorldHUD>();
        if (hud == null || PageField == null || ModalField == null || EncodedImageField == null)
            enabled = false;
    }

    private void LateUpdate()
    {
        if (!enabled) return;

        string page = PageField.GetValue(hud) as string;
        if (page != "gear" && page != "islands" && page != "reef-fish") return;

        GameObject modal = ModalField.GetValue(hud) as GameObject;
        if (modal == null || !modal.activeInHierarchy) return;

        Image image = modal.GetComponent<Image>();
        if (image == null) return;

        if (transparentSprite == null) transparentSprite = BuildTransparentSprite();
        if (transparentSprite == null) return;

        image.sprite = transparentSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.fillCenter = true;
        image.color = new Color(1f, 1f, 1f, Alpha);
    }

    private static Sprite BuildTransparentSprite()
    {
        string encoded = EncodedImageField.GetRawConstantValue() as string;
        if (string.IsNullOrEmpty(encoded)) return null;

        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException exception)
        {
            Debug.LogError("Backdrop image data is invalid: " + exception.Message);
            return null;
        }

        Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        decoded.wrapMode = TextureWrapMode.Clamp;
        decoded.filterMode = FilterMode.Bilinear;
        if (!decoded.LoadImage(bytes, false))
        {
            UnityEngine.Object.Destroy(decoded);
            Debug.LogError("Backdrop image could not be decoded by Unity.");
            return null;
        }

        Color32[] pixels = decoded.GetPixels32();
        int width = decoded.width;
        int height = decoded.height;
        UnityEngine.Object.Destroy(decoded);

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            int max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
            int min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));

            // The source's outside area is a near-neutral dark gray. Remove that
            // canvas and its compressed edge variations, while preserving the navy
            // panel because its blue channel is substantially stronger.
            if (max <= 70 && max - min <= 10)
                pixels[i] = new Color32(p.r, p.g, p.b, 0);
            else
                pixels[i] = new Color32(p.r, p.g, p.b, 255);
        }

        Texture2D rgba = new Texture2D(width, height, TextureFormat.RGBA32, false);
        rgba.name = "RoundedShopIndexBackdrop";
        rgba.wrapMode = TextureWrapMode.Clamp;
        rgba.filterMode = FilterMode.Bilinear;
        rgba.SetPixels32(pixels);
        rgba.Apply(false, true);

        return Sprite.Create(
            rgba,
            new Rect(0f, 0f, width, height),
            new Vector2(.5f, .5f),
            100f);
    }
}
