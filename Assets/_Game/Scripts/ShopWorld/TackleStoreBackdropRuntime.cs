using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only companion for the Tackle Store. The regular ShopMenu is
/// deliberately transparent while the store is open; this reapplies the shared
/// large translucent backdrop image so one large themed surface encompasses the
/// complete store UI without touching shop mechanics.
/// </summary>
[DefaultExecutionOrder(7000)]
public sealed class TackleStoreBackdropRuntime : MonoBehaviour
{
    private static readonly Color DefaultMenuColor = new Color(.025f, .055f, .07f, .98f);

    private ShopWorldHUD hud;
    private Image backdrop;
    private bool wasTackle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach (ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if (target != null && target.GetComponent<TackleStoreBackdropRuntime>() == null)
                target.gameObject.AddComponent<TackleStoreBackdropRuntime>();
    }

    private void Awake()
    {
        hud = GetComponent<ShopWorldHUD>();
    }

    private void LateUpdate()
    {
        if (hud == null) return;
        if (backdrop == null) backdrop = FindBackdrop();
        if (backdrop == null) return;

        bool tackle = ShopWorldHUD.MenuOpen && HasHeading("TACKLE STORE");
        if (tackle)
        {
            // ShopWorldHUD may rebuild after a purchase/equip and make ShopMenu
            // transparent again. Reasserting the shared backdrop here is cheap and
            // keeps the themed surface visible through every store refresh.
            LargeShopBackdropTheme.Apply(backdrop);
        }
        else if (wasTackle)
        {
            RestoreDefaultMenuAppearance();
        }

        wasTackle = tackle;
    }

    private Image FindBackdrop()
    {
        RectTransform[] transforms = hud.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            RectTransform current = transforms[i];
            if (current == null || current.name != "ShopMenu") continue;
            return current.GetComponent<Image>();
        }
        return null;
    }

    private bool HasHeading(string caption)
    {
        Text[] labels = hud.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] != null && string.Equals(labels[i].text, caption, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void RestoreDefaultMenuAppearance()
    {
        LargeShopBackdropTheme.RestoreDefault(backdrop, DefaultMenuColor);
    }

    private void OnDisable()
    {
        if (wasTackle) RestoreDefaultMenuAppearance();
        wasTackle = false;
    }
}
