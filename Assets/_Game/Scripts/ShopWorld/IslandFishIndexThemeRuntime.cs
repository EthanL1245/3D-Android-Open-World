using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only skin for the Island / Fish Index pages. It deliberately leaves
/// every existing RectTransform untouched so preview sizes, card heights, button hit
/// areas and scrolling remain exactly as authored by the underlying index UI.
///
/// Important: the Island Index can be opened through FishingMenuSafetyRuntime without
/// ShopWorldHUD.MenuOpen being true, so this skin keys off the HUD's real page/modal
/// state instead of MenuOpen.
/// </summary>
[DefaultExecutionOrder(7100)]
public sealed class IslandFishIndexThemeRuntime : MonoBehaviour
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo PageField = typeof(ShopWorldHUD).GetField("page", PrivateInstance);
    private static readonly FieldInfo ModalField = typeof(ShopWorldHUD).GetField("modal", PrivateInstance);
    private static readonly FieldInfo HeadingField = typeof(ShopWorldHUD).GetField("heading", PrivateInstance);

    private static readonly Color DefaultMenuColor = new Color(.025f, .055f, .07f, .98f);
    private static readonly Color BrightGold = new Color(1f, .86f, .28f, 1f);
    private static readonly Color DetailCyan = new Color(.70f, .90f, .96f, 1f);
    private static readonly Color DisabledTint = new Color(.52f, .64f, .68f, .72f);

    private ShopWorldHUD hud;
    private Font shopFont;
    private Image modal;
    private bool wasIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach (ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if (target != null && target.GetComponent<IslandFishIndexThemeRuntime>() == null)
                target.gameObject.AddComponent<IslandFishIndexThemeRuntime>();
    }

    private void Awake()
    {
        hud = GetComponent<ShopWorldHUD>();
        // This is the same font used by the current Tackle Store implementation.
        shopFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void LateUpdate()
    {
        if (hud == null || PageField == null || ModalField == null) return;

        string page = PageField.GetValue(hud) as string;
        GameObject modalObject = ModalField.GetValue(hud) as GameObject;
        bool indexPage = page == "islands" || page == "reef-fish";
        bool indexOpen = indexPage && modalObject != null && modalObject.activeInHierarchy;

        if (!indexOpen)
        {
            // Do not erase the Tackle Store's own themed backdrop during a direct
            // transition to that store. Its presentation companion owns that state.
            if (wasIndex && page != "gear") RestoreBackdrop();
            wasIndex = false;
            return;
        }

        modal = modalObject.GetComponent<Image>();
        if (modal == null) return;

        Text pageHeading = HeadingField != null ? HeadingField.GetValue(hud) as Text : null;
        if (pageHeading == null) pageHeading = FindIndexHeading();
        if (pageHeading == null) return;

        // Keep the shortcut and opened page terminology identical from now on.
        if (page == "islands") pageHeading.text = "ISLAND / FISH INDEX";

        ApplyBackdrop();
        StyleGlobalChrome(pageHeading);
        StyleRows();
        StyleAllVisibleText(pageHeading);
        wasIndex = true;
    }

    private Text FindIndexHeading()
    {
        Text[] labels = hud.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            string value = label.text ?? string.Empty;
            if (string.Equals(value, "ISLAND / BIOME INDEX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ISLAND / FISH INDEX", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith(" • FISH INDEX", StringComparison.OrdinalIgnoreCase))
                return label;
        }
        return null;
    }

    private void ApplyBackdrop()
    {
        FishingHudTheme.Panel(modal.gameObject);
        modal.color = Color.white;
    }

    private void RestoreBackdrop()
    {
        if (modal == null) return;
        modal.sprite = null;
        modal.type = Image.Type.Simple;
        modal.color = DefaultMenuColor;
    }

    private void StyleGlobalChrome(Text pageHeading)
    {
        pageHeading.font = shopFont;
        pageHeading.fontStyle = FontStyle.Bold;
        // Preserve the old yellow heading cue, but make it brighter/cleaner.
        pageHeading.color = BrightGold;

        Image viewport = FindImage("ScrollViewport");
        if (viewport != null) viewport.color = new Color(0f, 0f, 0f, .04f);

        Image scrollbar = FindImage("Scrollbar");
        if (scrollbar != null)
        {
            FishingHudTheme.Panel(scrollbar.gameObject, 3);
            scrollbar.color = Color.white;
        }

        Image handle = FindImage("Handle");
        if (handle != null)
        {
            FishingHudTheme.Panel(handle.gameObject, 1);
            handle.color = Color.white;
        }

        Button[] buttons = modal.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy) continue;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null || !string.Equals(label.text, "CLOSE", StringComparison.OrdinalIgnoreCase)) continue;
            FishingHudTheme.Panel(button.gameObject);
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = Color.white;
            break;
        }
    }

    private void StyleRows()
    {
        RectTransform rows = FindRect("Rows");
        if (rows == null) return;

        for (int i = 0; i < rows.childCount; i++)
        {
            Transform child = rows.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy) continue;

            Button cardButton = child.GetComponent<Button>();
            Image panel = child.GetComponent<Image>();

            if (panel != null)
            {
                // The actual Island/Biome cards are generated by
                // IslandBiomeIndexPatchRuntime. Re-skin those exact cards here rather
                // than replacing them, which preserves all image and hitbox sizing.
                FishingHudTheme.Panel(child.gameObject);
                panel.color = cardButton != null && !cardButton.interactable ? DisabledTint : Color.white;
            }

            Transform accent = FindDirectChild(child, "Accent");
            if (accent != null)
            {
                Image accentImage = accent.GetComponent<Image>();
                if (accentImage != null) accentImage.color = FishingHudTheme.Cyan;
            }

            Button[] actions = child.GetComponentsInChildren<Button>(true);
            for (int b = 0; b < actions.Length; b++)
            {
                Button action = actions[b];
                if (action == null || !action.gameObject.activeInHierarchy) continue;
                // A whole-card button stays navy. Dedicated TELEPORT/FISH INDEX/etc.
                // controls use the same teal action surface as the Tackle Store.
                if (action.transform == child) continue;

                FishingHudTheme.Panel(action.gameObject, 1);
                Image actionImage = action.GetComponent<Image>();
                if (actionImage != null) actionImage.color = action.interactable ? Color.white : DisabledTint;
            }
        }
    }

    private void StyleAllVisibleText(Text pageHeading)
    {
        Text[] labels = modal.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null || !label.gameObject.activeInHierarchy) continue;

            label.font = shopFont;

            Color original = label.color;
            if (label == pageHeading || IsGold(original))
            {
                label.color = BrightGold;
                label.fontStyle = FontStyle.Bold;
                continue;
            }

            string value = label.text ?? string.Empty;
            if (value.IndexOf("COINS", StringComparison.OrdinalIgnoreCase) >= 0 &&
                value.IndexOf("FISH IN BAG", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                label.color = DetailCyan;
                label.fontStyle = FontStyle.Normal;
                continue;
            }

            if (IsDetail(original))
            {
                label.color = DetailCyan;
                label.fontStyle = FontStyle.Normal;
                continue;
            }

            Button ownerButton = label.GetComponentInParent<Button>();
            if (ownerButton != null)
            {
                label.fontStyle = FontStyle.Bold;
                if (ownerButton.interactable && IsNearWhite(label.color)) label.color = Color.white;
                continue;
            }

            // On the actual Island/Biome cards, the title is the preserved bright
            // yellow above, while the description adopts the Tackle Store's cyan
            // detail-text treatment.
            Transform card = FindAncestorWithPrefix(label.transform, "BiomeCard_");
            if (card != null && IsNearWhite(label.color))
            {
                label.color = DetailCyan;
                label.fontStyle = FontStyle.Normal;
                continue;
            }

            // Fish-index row titles remain white/bold like tackle-store item names.
            if (IsNearWhite(label.color)) label.fontStyle = FontStyle.Bold;
        }
    }

    private static bool IsGold(Color color)
    {
        return color.r > .72f && color.g > .52f && color.g < .94f && color.b < .62f;
    }

    private static bool IsDetail(Color color)
    {
        return color.r > .48f && color.r < .82f && color.g > .68f && color.b > .70f;
    }

    private static bool IsNearWhite(Color color)
    {
        return color.r > .82f && color.g > .82f && color.b > .82f;
    }

    private Image FindImage(string objectName)
    {
        RectTransform rect = FindRect(objectName);
        return rect != null ? rect.GetComponent<Image>() : null;
    }

    private RectTransform FindRect(string objectName)
    {
        RectTransform[] transforms = hud.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < transforms.Length; i++)
            if (transforms[i] != null && transforms[i].name == objectName)
                return transforms[i];
        return null;
    }

    private static Transform FindDirectChild(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == objectName) return child;
        }
        return null;
    }

    private static Transform FindAncestorWithPrefix(Transform child, string prefix)
    {
        Transform current = child != null ? child.parent : null;
        while (current != null)
        {
            if (current.name.StartsWith(prefix, StringComparison.Ordinal)) return current;
            current = current.parent;
        }
        return null;
    }

    private void OnDisable()
    {
        if (wasIndex) RestoreBackdrop();
        wasIndex = false;
    }
}
