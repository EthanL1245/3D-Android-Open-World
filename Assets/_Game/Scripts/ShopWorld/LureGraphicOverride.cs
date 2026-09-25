using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replaces legacy Bait4 placeholder pictures with snapshots of the actual
/// equipped crankbait prefab. TackleLureShopExtension already renders each lure
/// row by variant; this also covers the top-left equipped-bait shortcut and the
/// generic selected-lure row created by ShopWorldHUD.
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
        ShopWorldHUD target=FindFirstObjectByType<ShopWorldHUD>();
        if(target!=null && target.GetComponent<LureGraphicOverride>()==null)
            target.gameObject.AddComponent<LureGraphicOverride>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        preview=gameObject.AddComponent<ShopPreview>();
    }

    private void LateUpdate()
    {
        if(hud==null || progress==null || preview==null)return;
        if(progress.Data.baitEquipped!=ShopCatalog.StarterLure)
        {
            applied.Clear();
            return;
        }

        int variant=Mathf.Clamp(progress.Data.lureEquipped,0,ShopCatalog.LureVariantCount-1);
        ShopCatalog.SetActiveLureVariant(variant);
        string key=ShopCatalog.LurePreviewKey(variant);

        RawImage[] images=hud.GetComponentsInChildren<RawImage>(true);
        for(int i=0;i<images.Length;i++)
        {
            RawImage image=images[i];
            if(image==null)continue;

            bool equippedShortcut=string.Equals(image.name,"EquippedBaitPicture",StringComparison.Ordinal);
            bool genericLureRow=!equippedShortcut && string.Equals(image.name,"3D item preview",StringComparison.Ordinal) && ParentMentionsLure(image.transform.parent);
            if(!equippedShortcut && !genericLureRow)continue;

            int id=image.GetInstanceID();
            if(applied.TryGetValue(id,out string oldKey) && oldKey==key && image.texture!=null)continue;
            applied[id]=key;
            preview.Attach(image,0,1f,key);
        }
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
