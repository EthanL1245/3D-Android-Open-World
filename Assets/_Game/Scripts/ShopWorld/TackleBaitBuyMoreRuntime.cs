using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a dedicated BUY button to consumable-bait rows in the tabbed Tackle Store.
///
/// Shrimp and Squid are inventory stacks, so owning some must never hide the ability
/// to buy another pack. Their left/action button remains loadout management
/// (EQUIP / EQUIPPED), while the second button always buys another pack of 10 when
/// affordable. Buying stock deliberately does not change the currently equipped bait.
///
/// The current scroll position is captured before a purchase and restored after
/// ShopWorldHUD rebuilds the list, so stocking up never jumps the player to the top.
/// </summary>
[DefaultExecutionOrder(6000)]
public sealed class TackleBaitBuyMoreRuntime : MonoBehaviour
{
    private static readonly Color Teal=new Color(.04f,.36f,.39f,1f);
    private static readonly Color Disabled=new Color(.17f,.23f,.24f,1f);

    private ShopWorldHUD hud;
    private ShopProgress progress;
    private Font font;
    private float nextScan;
    private float pendingScroll=-1f;
    private int restoreFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallAfterLoad()
    {
        foreach(ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(target!=null && target.GetComponent<TackleBaitBuyMoreRuntime>()==null)
                target.gameObject.AddComponent<TackleBaitBuyMoreRuntime>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void LateUpdate()
    {
        if(hud==null)return;
        if(progress==null)progress=FindFirstObjectByType<ShopProgress>();
        if(progress==null)return;

        RestoreScrollIfNeeded();

        if(!ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+.08f;
        if(!HasHeading("TACKLE STORE"))return;

        Transform store=FindDeepChild(hud.transform,"TabbedTackleStore");
        if(store==null || !IsBaitTab(store))return;

        EnsureConsumableRow(store,2);
        EnsureConsumableRow(store,3);
    }

    private void EnsureConsumableRow(Transform store,int id)
    {
        Transform row=FindDirectOrDeepChild(store,ShopCatalog.BaitNames[id]);
        if(row==null)return;

        int amount=progress.Data.bait[id];
        bool equipped=progress.Data.baitEquipped==id;

        // The original tabbed row has one action button. Once a consumable is owned
        // that button already equips it. When the stack is empty its captured callback
        // would buy-and-auto-equip, so make it an explicit disabled EQUIP button instead;
        // the dedicated stock button below owns all purchasing.
        Button equipButton=FindPrimaryActionButton(row);
        if(equipButton!=null)
        {
            RectTransform equipRect=equipButton.GetComponent<RectTransform>();
            SetRightButtonRect(equipRect,42f);
            Text label=equipButton.GetComponentInChildren<Text>(true);
            string caption=equipped?"EQUIPPED":"EQUIP";
            if(label!=null)label.text=caption;
            equipButton.gameObject.name=caption;
            bool canEquip=amount>0 && !equipped && !progress.ReadOnly;
            equipButton.interactable=canEquip;
            Image image=equipButton.GetComponent<Image>();
            if(image!=null)image.color=canEquip?Teal:Disabled;
        }

        Transform existing=row.Find("BUY MORE BAIT");
        Button buyButton;
        if(existing==null)
        {
            GameObject go=new GameObject("BUY MORE BAIT",typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(row,false);
            Image image=go.GetComponent<Image>();
            image.color=Teal;
            buyButton=go.GetComponent<Button>();
            buyButton.targetGraphic=image;
            buyButton.transition=Selectable.Transition.None;
            int selectedId=id;
            buyButton.onClick.AddListener(()=>BuyAnotherPack(selectedId));

            Text label=CreateText(go.transform,"",25,Color.white,TextAnchor.MiddleCenter);
            Full(label.rectTransform);
        }
        else buyButton=existing.GetComponent<Button>();

        if(buyButton==null)return;
        SetRightButtonRect(buyButton.GetComponent<RectTransform>(),-42f);
        int price=ShopCatalog.BaitPrices[id];
        Text buyLabel=buyButton.GetComponentInChildren<Text>(true);
        if(buyLabel!=null)buyLabel.text="BUY "+price.ToString("N0");

        bool nearGear=ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.Near("gear");
        bool canBuy=!progress.ReadOnly && progress.CanTrade && nearGear && amount<=9990 && progress.Data.coins>=price;
        buyButton.interactable=canBuy;
        Image buyImage=buyButton.GetComponent<Image>();
        if(buyImage!=null)buyImage.color=canBuy?Teal:Disabled;
    }

    private void BuyAnotherPack(int id)
    {
        if(progress==null || progress.ReadOnly || !progress.CanTrade)return;
        if(ShopDimensionManager.Instance==null || !ShopDimensionManager.Instance.Near("gear"))return;
        if(id<2 || id>3 || progress.Data.bait[id]>9990)return;

        int price=ShopCatalog.BaitPrices[id];
        if(progress.Data.coins<price)return;

        ScrollRect scroll=FindScroll();
        if(scroll!=null)pendingScroll=scroll.verticalNormalizedPosition;
        restoreFrames=4;

        // Buying inventory and equipping inventory are intentionally separate in the
        // tabbed store. Do not call ShopProgress.BuyBait here because legacy purchase
        // behavior also equips the purchased bait.
        if(!progress.Data.Spend(price))return;
        progress.Data.bait[id]+=10;
        progress.Save();
    }

    private void RestoreScrollIfNeeded()
    {
        if(restoreFrames<=0 || pendingScroll<0f)return;
        ScrollRect scroll=FindScroll();
        if(scroll==null)return;

        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition=Mathf.Clamp01(pendingScroll);
        restoreFrames--;
        if(restoreFrames<=0)pendingScroll=-1f;
    }

    private ScrollRect FindScroll()
    {
        return hud!=null?hud.GetComponentInChildren<ScrollRect>(true):null;
    }

    private bool HasHeading(string caption)
    {
        Text[] labels=hud.GetComponentsInChildren<Text>(true);
        for(int i=0;i<labels.Length;i++)
            if(labels[i]!=null && string.Equals(labels[i].text,caption,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private static bool IsBaitTab(Transform store)
    {
        Transform heading=store.Find("CategoryHeading");
        if(heading==null)return false;
        Text label=heading.GetComponentInChildren<Text>(true);
        return label!=null && string.Equals(label.text,"BAIT",StringComparison.OrdinalIgnoreCase);
    }

    private static Button FindPrimaryActionButton(Transform row)
    {
        Button[] buttons=row.GetComponentsInChildren<Button>(true);
        for(int i=0;i<buttons.Length;i++)
            if(buttons[i]!=null && buttons[i].transform.parent==row && buttons[i].gameObject.name!="BUY MORE BAIT")return buttons[i];
        return null;
    }

    private static void SetRightButtonRect(RectTransform rect,float y)
    {
        if(rect==null)return;
        rect.anchorMin=new Vector2(1f,.5f);
        rect.anchorMax=new Vector2(1f,.5f);
        rect.pivot=new Vector2(1f,.5f);
        rect.sizeDelta=new Vector2(205f,64f);
        rect.anchoredPosition=new Vector2(-14f,y);
    }

    private Text CreateText(Transform parent,string value,int size,Color color,TextAnchor alignment)
    {
        GameObject go=new GameObject("Label",typeof(RectTransform),typeof(Text));
        go.transform.SetParent(parent,false);
        Text text=go.GetComponent<Text>();
        text.font=font;
        text.text=value;
        text.fontSize=size;
        text.color=color;
        text.alignment=alignment;
        text.raycastTarget=false;
        text.resizeTextForBestFit=true;
        text.resizeTextMinSize=18;
        text.resizeTextMaxSize=size;
        return text;
    }

    private static Transform FindDirectOrDeepChild(Transform root,string name)
    {
        Transform direct=root.Find(name);
        if(direct!=null)return direct;
        return FindDeepChild(root,name);
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        if(root==null)return null;
        Transform[] all=root.GetComponentsInChildren<Transform>(true);
        for(int i=0;i<all.Length;i++)
            if(all[i]!=null && string.Equals(all[i].name,name,StringComparison.Ordinal))return all[i];
        return null;
    }

    private static void Full(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;
        rect.anchorMax=Vector2.one;
        rect.offsetMin=Vector2.zero;
        rect.offsetMax=Vector2.zero;
    }
}
