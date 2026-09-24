using System;
using UnityEngine;
using UnityEngine.UI;

// Adds permanent lure variants to the existing Tackle Store without replacing
// the established ShopWorldHUD. Every lure deliberately reuses the same Bait4
// preview model; only its name, tuning and ownership differ.
public sealed class TackleLureShopExtension : MonoBehaviour
{
    private ShopWorldHUD hud;
    private ShopProgress progress;
    private ShopPreview preview;
    private GameObject section;
    private Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        var target=FindFirstObjectByType<ShopWorldHUD>();
        if(target!=null && target.GetComponent<TackleLureShopExtension>()==null)
            target.gameObject.AddComponent<TackleLureShopExtension>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        preview=GetComponent<ShopPreview>();
        if(preview==null)preview=gameObject.AddComponent<ShopPreview>();
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void LateUpdate()
    {
        if(hud==null || progress==null)return;
        ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);
        SyncShortcutName();

        bool tackle=ShopWorldHUD.MenuOpen && HasHeading("TACKLE STORE");
        bool baitPage=ShopWorldHUD.MenuOpen && HasHeading("BAIT / LURES");
        if(!tackle && !baitPage)
        {
            section=null;
            return;
        }

        RectTransform rows=FindRows();
        if(rows==null)return;
        if(section==null || section.transform.parent!=rows)
        {
            if(!tackle && OwnedAlternativeCount()==0)return;
            BuildSection(rows,tackle);
        }
    }

    private bool HasHeading(string caption)
    {
        var labels=hud.GetComponentsInChildren<Text>(true);
        for(int i=0;i<labels.Length;i++)
            if(labels[i]!=null && string.Equals(labels[i].text,caption,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private RectTransform FindRows()
    {
        var transforms=hud.GetComponentsInChildren<RectTransform>(true);
        for(int i=0;i<transforms.Length;i++)
            if(transforms[i]!=null && transforms[i].name=="Rows")return transforms[i];
        return null;
    }

    private void BuildSection(RectTransform rows,bool shop)
    {
        var existing=rows.Find("LureShopSection");
        if(existing!=null)Destroy(existing.gameObject);

        section=new GameObject("LureShopSection",typeof(RectTransform),typeof(Image),typeof(LayoutElement),typeof(VerticalLayoutGroup));
        section.transform.SetParent(rows,false);
        var background=section.GetComponent<Image>();
        background.color=new Color(.035f,.11f,.13f,.94f);
        var layout=section.GetComponent<VerticalLayoutGroup>();
        layout.padding=new RectOffset(14,14,12,14);layout.spacing=8;
        layout.childControlWidth=true;layout.childForceExpandWidth=true;
        layout.childControlHeight=true;layout.childForceExpandHeight=false;
        var sectionSize=section.GetComponent<LayoutElement>();
        sectionSize.preferredHeight=shop?650f:Mathf.Max(160f,70f+OwnedAlternativeCount()*142f);

        var title=CreateText(section.transform,shop?"PERMANENT LURES":"OWNED LURES",30,Color.white,TextAnchor.MiddleLeft);
        title.GetComponent<LayoutElement>().preferredHeight=48f;

        for(int variant=0;variant<ShopCatalog.LureVariantCount;variant++)
        {
            bool owned=progress.Data.OwnsLure(variant);
            bool equipped=progress.Data.baitEquipped==ShopCatalog.StarterLure && progress.Data.lureEquipped==variant;
            if(!shop && (!owned || equipped))continue;
            CreateLureRow(section.transform,variant,shop,owned,equipped);
        }
    }

    private int OwnedAlternativeCount()
    {
        int count=0;
        for(int i=0;i<ShopCatalog.LureVariantCount;i++)
            if(progress.Data.OwnsLure(i) && !(progress.Data.baitEquipped==ShopCatalog.StarterLure && progress.Data.lureEquipped==i))count++;
        return count;
    }

    private void CreateLureRow(Transform parent,int variant,bool shop,bool owned,bool equipped)
    {
        var row=new GameObject("Lure_"+variant,typeof(RectTransform),typeof(Image),typeof(LayoutElement),typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent,false);
        row.GetComponent<Image>().color=new Color(.06f,.18f,.20f,.96f);
        row.GetComponent<LayoutElement>().preferredHeight=132f;
        var horizontal=row.GetComponent<HorizontalLayoutGroup>();
        horizontal.padding=new RectOffset(10,10,8,8);horizontal.spacing=12;
        horizontal.childControlHeight=true;horizontal.childForceExpandHeight=true;
        horizontal.childControlWidth=false;horizontal.childForceExpandWidth=false;

        var imageObject=new GameObject("LurePreview",typeof(RectTransform),typeof(RawImage),typeof(LayoutElement));
        imageObject.transform.SetParent(row.transform,false);
        var imageLayout=imageObject.GetComponent<LayoutElement>();imageLayout.preferredWidth=112f;imageLayout.minWidth=112f;
        var raw=imageObject.GetComponent<RawImage>();raw.color=Color.white;raw.raycastTarget=false;
        preview.Attach(raw,0,1f,"Bait4");

        var info=new GameObject("Info",typeof(RectTransform),typeof(LayoutElement),typeof(VerticalLayoutGroup));
        info.transform.SetParent(row.transform,false);
        var infoLayout=info.GetComponent<LayoutElement>();infoLayout.flexibleWidth=1f;infoLayout.minWidth=260f;
        var infoGroup=info.GetComponent<VerticalLayoutGroup>();infoGroup.spacing=2;infoGroup.childControlHeight=true;infoGroup.childForceExpandHeight=false;
        var name=CreateText(info.transform,ShopCatalog.LureNames[variant],26,Color.white,TextAnchor.MiddleLeft);name.GetComponent<LayoutElement>().preferredHeight=38f;
        var description=CreateText(info.transform,ShopCatalog.LureDescriptions[variant],19,new Color(.78f,.90f,.91f),TextAnchor.MiddleLeft);description.GetComponent<LayoutElement>().preferredHeight=72f;

        string action;
        bool interactable=true;
        if(equipped){action="EQUIPPED";interactable=false;}
        else if(owned)action="EQUIP";
        else if(shop){action=ShopCatalog.LurePrices[variant].ToString("N0")+" COINS";interactable=progress.Data.coins>=ShopCatalog.LurePrices[variant];}
        else {action="LOCKED";interactable=false;}

        int selected=variant;
        var button=CreateButton(row.transform,action,()=>SelectLure(selected,owned));
        var buttonLayout=button.GetComponent<LayoutElement>();buttonLayout.preferredWidth=180f;buttonLayout.minWidth=180f;
        button.interactable=interactable;
    }

    private void SelectLure(int variant,bool wasOwned)
    {
        bool changed=wasOwned ? progress.EquipLure(variant) : progress.BuyLure(variant);
        if(!changed)return;
        ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);
        SyncShortcutName();
        if(section!=null)Destroy(section);
        section=null;
    }

    private Text CreateText(Transform parent,string value,int size,Color color,TextAnchor alignment)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text),typeof(LayoutElement));
        go.transform.SetParent(parent,false);
        var text=go.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;text.raycastTarget=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        return text;
    }

    private Button CreateButton(Transform parent,string caption,Action action)
    {
        var go=new GameObject(caption,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));
        go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.color=new Color(.02f,.46f,.52f,.96f);
        var button=go.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
        var label=CreateText(go.transform,caption,21,Color.white,TextAnchor.MiddleCenter);
        var rect=label.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
        return button;
    }

    private void SyncShortcutName()
    {
        if(progress.Data.baitEquipped!=ShopCatalog.StarterLure)return;
        var raws=hud.GetComponentsInChildren<RawImage>(true);
        Transform shortcut=null;
        for(int i=0;i<raws.Length;i++)
            if(raws[i]!=null && raws[i].name=="EquippedBaitPicture"){shortcut=raws[i].transform.parent;break;}
        if(shortcut==null)return;
        var labels=shortcut.GetComponentsInChildren<Text>(true);
        for(int i=0;i<labels.Length;i++)
        {
            var label=labels[i];
            if(label==null || string.IsNullOrWhiteSpace(label.text))continue;
            for(int lure=0;lure<ShopCatalog.LureNames.Length;lure++)
            {
                if(string.Equals(label.text,ShopCatalog.LureNames[lure],StringComparison.OrdinalIgnoreCase) || string.Equals(label.text,ShopCatalog.LureNames[lure].ToUpperInvariant(),StringComparison.OrdinalIgnoreCase))
                {label.text=ShopCatalog.LureNames[progress.Data.lureEquipped].ToUpperInvariant();return;}
            }
        }
    }
}
