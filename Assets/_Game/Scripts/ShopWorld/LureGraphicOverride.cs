using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the permanent-lure variant synchronized and owns only the persistent
/// top-left equipped-lure shortcut preview. BAIT/LURES rows themselves are rendered
/// by ShopWorldHUD/ShopPreview, so there are no competing RenderTexture writers.
/// </summary>
[DefaultExecutionOrder(-450)]
public sealed class LureGraphicOverride : MonoBehaviour
{
    private ShopProgress progress;
    private ShopPreview shortcutPreview;
    private RawImage shortcutImage;
    private int appliedVariant=-1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ShopProgress saved=FindFirstObjectByType<ShopProgress>();
        if(saved!=null)ShopCatalog.SetActiveLureVariant(saved.Data.lureEquipped);

        ShopWorldHUD target=FindFirstObjectByType<ShopWorldHUD>();
        if(target!=null && target.GetComponent<LureGraphicOverride>()==null)
            target.gameObject.AddComponent<LureGraphicOverride>();
    }

    private void Awake()
    {
        progress=FindFirstObjectByType<ShopProgress>();
        SyncVariant();
    }

    private void LateUpdate()
    {
        if(progress==null)return;
        SyncVariant();

        if(progress.Data.baitEquipped!=ShopCatalog.StarterLure)
        {
            appliedVariant=-1;
            return;
        }

        if(shortcutImage==null)
        {
            foreach(RawImage raw in GetComponentsInChildren<RawImage>(true))
                if(raw!=null && string.Equals(raw.name,"EquippedBaitPicture",StringComparison.Ordinal))
                {shortcutImage=raw;break;}
            if(shortcutImage==null)return;
        }

        int variant=Mathf.Clamp(progress.Data.lureEquipped,0,ShopCatalog.LureVariantCount-1);
        string key=ShopCatalog.LurePreviewKey(variant);
        string expected="Inventory "+key;
        bool valid=shortcutImage.texture!=null &&
            string.Equals(shortcutImage.texture.name,expected,StringComparison.Ordinal) &&
            shortcutImage.enabled;

        if(valid && appliedVariant==variant)return;
        if(shortcutPreview==null)shortcutPreview=gameObject.AddComponent<ShopPreview>();

        appliedVariant=variant;
        shortcutPreview.Attach(shortcutImage,0,1f,key);
    }

    private void SyncVariant()
    {
        if(progress!=null)ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);
    }
}
