using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replaces legacy Bait4 placeholder pictures with snapshots of the actual
/// crankbait prefab. TackleLureShopExtension already renders each lure row by
/// variant; this additionally covers the top-left equipped-bait shortcut and
/// any generic lure row created by ShopWorldHUD.
/// </summary>
[DefaultExecutionOrder(1200)]
public sealed class LureGraphicOverride : MonoBehaviour
{
    private ShopWorldHUD hud;
    private ShopProgress progress;
    private ShopPreview preview;
    private readonly Dictionary<int,string> applied = new Dictionary<int,string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ShopProgress saved=FindFirstObjectByType<ShopProgress>();
        if(saved!=null)
            ShopCatalog.SetActiveLureVariant(saved.Data.lureEquipped);

        ShopWorldHUD target=FindFirstObjectByType<ShopWorldHUD>();
        if(target!=null && target.GetComponent<LureGraphicOverride>()==null)
            target.gameObject.AddComponent<LureGraphicOverride>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        if(progress!=null)
            ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);
    }

    private void LateUpdate()
    {
        if(hud==null || progress==null)return;

        int variant=Mathf.Clamp(progress.Data.lureEquipped,0,ShopCatalog.LureVariantCount-1);
        ShopCatalog.SetActiveLureVariant(variant);
        string key=ShopCatalog.LurePreviewKey(variant);
        bool lureIsEquipped=progress.Data.baitEquipped==ShopCatalog.StarterLure;

        RawImage[] images=hud.GetComponentsInChildren<RawImage>(true);
        for(int i=0;i<images.Length;i++)
        {
            RawImage image=images[i];
            if(image==null)continue;

            bool equippedShortcut=lureIsEquipped && string.Equals(image.name,"EquippedBaitPicture",StringComparison.Ordinal);
            bool genericLureRow=string.Equals(image.name,"3D item preview",StringComparison.Ordinal) && ParentMentionsLure(image.transform.parent);
            if(!equippedShortcut && !genericLureRow)continue;

            int id=image.GetInstanceID();
            if(applied.TryGetValue(id,out string oldKey) && oldKey==key && image.texture!=null && image.enabled)continue;

            // Hide the legacy Bait4 image immediately. ShopPreview renders on an
            // end-of-frame coroutine, so without this line the old placeholder was
            // visible for the first frame the BAIT/LURES page was opened.
            image.enabled=false;
            applied[id]=key;
            Preview().Attach(image,0,1f,key);
        }
    }

    private ShopPreview Preview()
    {
        if(preview!=null)return preview;
        ShopPreview[] existing=GetComponents<ShopPreview>();
        preview=existing.Length>0?existing[0]:gameObject.AddComponent<ShopPreview>();
        return preview;
    }

    private bool ParentMentionsLure(Transform parent)
    {
        if(parent==null)return false;
        Text[] labels=parent.GetComponentsInChildren<Text>(true);
        for(int i=0;i<labels.Length;i++)
        {
            Text label=labels[i];
            if(label==null || string.IsNullOrWhiteSpace(label.text))continue;
            string text=label.text.Trim();
            for(int lure=0;lure<ShopCatalog.LureNames.Length;lure++)
                if(string.Equals(text,ShopCatalog.LureNames[lure],StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        applied.Clear();
    }
}
