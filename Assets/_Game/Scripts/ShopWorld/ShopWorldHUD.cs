using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed class ShopWorldHUD : MonoBehaviour
{
    public static bool MenuOpen { get; private set; }
    private ShopProgress progress;
    private ShopDimensionManager travel;
    private FishingSystem fishing;
    private FirstPersonController player;
    private FishingHUD fishingHUD;
    private Font font;
    private GameObject root, modal, nearbyButton, exitButton;
    private RectTransform list;
    private ScrollRect scroll;
    private Text heading, wallet, feedback, nearLabel;
    private string page="travel", message="";
    private bool dirty;
    private readonly Color ink=new Color(0.025f,0.055f,0.07f,0.98f);
    private readonly Color teal=new Color(0.04f,0.36f,0.39f,1f);
    private readonly Color gold=new Color(0.89f,0.72f,0.40f,1f);
    private void Start()
    {
        progress=FindFirstObjectByType<ShopProgress>(); travel=ShopDimensionManager.Instance;
        if(progress==null || travel==null) { enabled=false; return; }
        fishing=progress.GetComponent<FishingSystem>(); player=progress.GetComponent<FirstPersonController>();
        fishingHUD=GetComponent<FishingHUD>(); font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build(); progress.Changed+=RefreshSoon; message=progress.Notice; MenuOpen=false;
    }
    private void OnDestroy() { if(progress!=null) progress.Changed-=RefreshSoon; MenuOpen=false; }
    private void RefreshSoon() => dirty=true;
    private void Update()
    {
        if(root==null) return;
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        { if(MenuOpen) Close(); else Open("travel"); }
        if(Keyboard.current!=null && Keyboard.current.tabKey.wasPressedThisFrame) Open("bag");
        string nearest=travel.Nearest();
        nearbyButton.SetActive(!MenuOpen && nearest!=null && !travel.Traveling);
        if(nearest!=null) nearLabel.text=nearest=="gear" ? "OPEN TACKLE STORE" : nearest=="market" ? "SELL FISH" : "VIEW HABITAT";
        if(!MenuOpen && nearest!=null && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame) Open(nearest);
        exitButton.SetActive(!MenuOpen && travel.InShop && player.IsSwimming);
        if(travel.Traveling) { feedback.text="Traveling..."; return; }
        if(travel.TravelError!=null) feedback.text=travel.TravelError;
        if(dirty && MenuOpen) { dirty=false; Refresh(false); }
    }
    public void Open(string destination)
    {
        if(root==null || travel.Traveling) return;
        if(!MenuOpen) fishing.PrepareForWorldTravel();
        MenuOpen=true; page=destination; modal.SetActive(true); root.transform.SetAsLastSibling();
        player.SetUIBlocked(true); if(fishingHUD!=null) fishingHUD.SetMenuCovered(true);
        Refresh(true);
    }
    public void Close()
    {
        if(travel!=null && travel.Traveling) return;
        MenuOpen=false;
        if(modal!=null) modal.SetActive(false);
        if(player!=null) player.SetUIBlocked(false);
        if(fishingHUD!=null) fishingHUD.SetMenuCovered(false);
        if(fishing!=null) fishing.SetShopWorld(travel!=null && travel.InShop);
    }
    private void Build()
    {
        root=Panel("TideglassHUD",transform,Color.clear); Full(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().raycastTarget=false;
        Button menu=ButtonAt(root.transform,"MENU / TRAVEL",()=>Open("travel"));
        Rect(menu.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-20),new Vector2(230,52));
        nearbyButton=ButtonAt(root.transform,"OPEN",()=>Open(travel.Nearest()??"shops")).gameObject;
        nearLabel=nearbyButton.GetComponentInChildren<Text>();
        Rect(nearbyButton.GetComponent<RectTransform>(),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0,130),new Vector2(300,58));
        exitButton=ButtonAt(root.transform,"EXIT WATER",()=>travel.ExitWater()).gameObject;
        Rect(exitButton.GetComponent<RectTransform>(),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0,195),new Vector2(230,54));
        modal=Panel("ShopMenu",root.transform,ink);
        var r=modal.GetComponent<RectTransform>(); r.anchorMin=new Vector2(0.08f,0.08f); r.anchorMax=new Vector2(0.92f,0.92f); r.offsetMin=r.offsetMax=Vector2.zero;
        heading=Label(modal.transform,"TIDEGLASS QUAY",32,gold); Anchor(heading.rectTransform,0,1,1,1,24,-62,-150,-12);
        wallet=Label(modal.transform,"",20,Color.white); Anchor(wallet.rectTransform,0,1,1,1,24,-92,-24,-62);
        Button close=ButtonAt(modal.transform,"CLOSE",Close); Rect(close.GetComponent<RectTransform>(),Vector2.one,Vector2.one,Vector2.one,new Vector2(-18,-16),new Vector2(118,42));
        string[] tabs={"travel","bag","equipment","shops"}; string[] names={"TRAVEL","FISH BAG","EQUIPMENT","DIRECTORY"};
        for(int i=0;i<tabs.Length;i++) { string tab=tabs[i]; var b=ButtonAt(modal.transform,names[i],()=>Open(tab)); Anchor(b.GetComponent<RectTransform>(),i*0.25f,1,(i+1)*0.25f,1,16,-148,-16,-103); }
        GameObject viewport=Panel("ScrollViewport",modal.transform,new Color(0,0,0,0.1f));
        var vr=viewport.GetComponent<RectTransform>(); Anchor(vr,0,0,1,1,20,70,-30,-164);
        viewport.AddComponent<RectMask2D>(); scroll=viewport.AddComponent<ScrollRect>();
        GameObject content=new GameObject("Rows",typeof(RectTransform)); content.transform.SetParent(viewport.transform,false);
        list=content.GetComponent<RectTransform>(); list.anchorMin=new Vector2(0,1); list.anchorMax=Vector2.one; list.pivot=new Vector2(0.5f,1); list.sizeDelta=Vector2.zero;
        var layout=content.AddComponent<VerticalLayoutGroup>(); layout.spacing=10; layout.childControlHeight=true; layout.childControlWidth=true; layout.childForceExpandHeight=false;
        content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=vr; scroll.content=list; scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=42;
        // Visible scrollbar, plus mouse wheel and touch dragging on the full list.
        var track=Panel("Scrollbar",modal.transform,new Color(0.12f,0.19f,0.21f,1)); Anchor(track.GetComponent<RectTransform>(),1,0,1,1,-22,70,-10,-164);
        var handle=Panel("Handle",track.transform,gold); Full(handle.GetComponent<RectTransform>());
        var bar=track.AddComponent<Scrollbar>(); bar.handleRect=handle.GetComponent<RectTransform>(); bar.targetGraphic=handle.GetComponent<Image>(); bar.direction=Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=bar;
        feedback=Label(modal.transform,"",18,gold); Anchor(feedback.rectTransform,0,0,1,0,24,10,-24,60);
        modal.SetActive(false); nearbyButton.SetActive(false); exitButton.SetActive(false);
    }
    private void Refresh(bool top)
    {
        float position=scroll.verticalNormalizedPosition;
        for(int i=list.childCount-1;i>=0;i--) { list.GetChild(i).gameObject.SetActive(false); Destroy(list.GetChild(i).gameObject); }
        wallet.text=$"{progress.Data.coins:N0} COINS   /   {progress.Data.bag.Count} FISH IN BAG   /   {(travel.InShop?"TIDEGLASS QUAY":"FISHING ISLAND")}";
        feedback.text=progress.ReadOnly ? progress.Notice : message;
        heading.text=page=="travel"?"TIDEGLASS QUAY / TRAVEL":page=="bag"?"YOUR FISH BAG":page=="equipment"?"YOUR EQUIPMENT":page=="gear"?"THE TACKLE ATELIER":page=="market"?"FRESH CATCH MARKET":page=="shops"?"QUAY DIRECTORY": "HABITAT GALLERY";
        if(page=="travel") TravelPage(); else if(page=="bag") BagPage(); else if(page=="equipment") EquipmentPage(false);
        else if(page=="gear") EquipmentPage(true); else if(page=="market") MarketPage(); else if(page=="sell-confirm") SellConfirmation();
        else if(page=="shops") Directory(); else HabitatPage(page);
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition=top?1f:position;
    }
    private void TravelPage()
    {
        Row("Fishing Island","Wild coastline. Cast, catch and explore.","TRAVEL",()=>travel.Travel(false),travel.InShop);
        Row("Tideglass Quay","Tackle atelier, fish market, aquarium gallery and pond gardens.","TRAVEL",()=>travel.Travel(true),!travel.InShop);
        Row("Your collection","Every catch is saved. Aquarium fish can always be returned to your bag.","OPEN BAG",()=>Open("bag"));
        if(travel.InShop) Row("Water access","Look down and move forward to dive. Hold Jump/Space to rise; Ctrl descends on keyboard. EXIT WATER returns you to a safe walkway.","EXIT WATER",()=>{travel.ExitWater();Close();});
    }
    private void BagPage()
    {
        if(progress.Data.bag.Count==0) { Row("No catches yet","Your reusable lure is always available. Travel to the island and cast.","TRAVEL",()=>Open("travel")); return; }
        foreach(var fish in new List<CaughtFishRecord>(progress.Data.bag))
        {
            var f=fish; Row(FishCatalog.Get(f.speciesId).Name,$"{f.weightKg:0.00} kg  /  {ShopCatalog.FishLength(f.speciesId,f.weightKg):0.00} m  /  value {FishCatalog.GetSellValue(f.speciesId,f.weightKg)} coins","HOLD",()=>{Close(); int index=progress.Data.bag.IndexOf(f); if(index>=0) fishing.HoldFish(index);});
        }
    }
    private void EquipmentPage(bool shop)
    {
        if(shop && !travel.Near("gear")) { Row("Visit the tackle counter","Purchases happen in the shop world.","DIRECTORY",()=>Open("shops")); return; }
        foreach(GearKind kind in Enum.GetValues(typeof(GearKind)))
        {
            for(int tier=0;tier<4;tier++)
            {
                int t=tier; GearKind k=kind; bool owned=t<=progress.Data.Owned(k), equipped=t==progress.Data.Equipped(k);
                if(!shop && !owned) continue;
                string stats=k==GearKind.Rod?$"{t*18}% more tension control":k==GearKind.Reel?$"{t*22}% faster tiring and retrieval":$"+{ShopCatalog.LineBonus[t]} m range; {t*12}% more line tolerance";
                string action=equipped?"EQUIPPED":owned?"EQUIP":$"BUY {ShopCatalog.GearPrice(k,t):N0}";
                bool can=owned?!equipped:t==progress.Data.Owned(k)+1 && progress.Data.coins>=ShopCatalog.GearPrice(k,t);
                Row(ShopCatalog.GearName(k,t),stats+(owned?"":" / Requires previous tier"),action,()=>
                {
                    bool result=owned?(!progress.ReadOnly && progress.Commit(progress.Data.Equip(k,t))):progress.BuyGear(k,t);
                    Result(result,owned?"Equipment updated.":"Purchased and equipped.");
                },can && !progress.ReadOnly);
            }
        }
        for(int i=0;i<4;i++)
        {
            int id=i; string effect=id==0?"Unlimited uses. Standard catch chances.":id==1?"35% shorter wait; one worm per cast.":id==2?"20% shorter wait; favors snapper and goatfish.":"Favors yellowtail and tuna; one squid per cast.";
            string count=id==0?"Unlimited":$"{progress.Data.bait[id]} remaining";
            if(shop && id>0) Row(ShopCatalog.BaitNames[id]+" / PACK OF 10",effect+" "+count,$"BUY {ShopCatalog.BaitPrices[id]}",()=>Result(progress.BuyBait(id),"Bait purchased and selected."),progress.Data.coins>=ShopCatalog.BaitPrices[id] && !progress.ReadOnly);
            Row(ShopCatalog.BaitNames[id],count+" / "+effect,progress.Data.baitEquipped==id?"SELECTED":"SELECT",()=>{if(progress.ReadOnly)return; progress.Data.baitEquipped=id; progress.Save(); Result(true,"Bait selected.");},progress.Data.baitEquipped!=id && (id==0 || progress.Data.bait[id]>0) && !progress.ReadOnly);
        }
    }
    private void MarketPage()
    {
        if(!travel.Near("market")) { Row("Visit the fish market","Sell fish from your bag. Fish in habitats are never sold automatically.","DIRECTORY",()=>Open("shops")); return; }
        if(progress.Data.bag.Count==0) { Row("Your bag is empty","Bring your next catch to the market.","FISHING ISLAND",()=>travel.Travel(false)); return; }
        Row("Sell bag contents","Review the total before selling all fish in the bag.","REVIEW ALL",()=>Open("sell-confirm"));
        foreach(var fish in new List<CaughtFishRecord>(progress.Data.bag))
        {
            var f=fish; int value=FishCatalog.GetSellValue(f.speciesId,f.weightKg);
            Row(FishCatalog.Get(f.speciesId).Name,$"{f.weightKg:0.00} kg / {ShopCatalog.FishLength(f.speciesId,f.weightKg):0.00} m",$"SELL {value}",()=>Result(progress.Sell(f),$"Sold for {value} coins."),!progress.ReadOnly);
        }
    }
    private void SellConfirmation()
    {
        long total=0; foreach(var f in progress.Data.bag) total+=FishCatalog.GetSellValue(f.speciesId,f.weightKg);
        Row($"Sell {progress.Data.bag.Count} fish for {total:N0} coins?","This empties your fish bag. Habitat residents stay where they are.","CONFIRM SALE",()=>
        {
            if(!travel.Near("market") || progress.ReadOnly) return;
            foreach(var f in new List<CaughtFishRecord>(progress.Data.bag)) progress.Data.Sell(f,FishCatalog.GetSellValue(f.speciesId,f.weightKg));
            progress.Save(); message="Fish sold."; Open("market");
        },!progress.ReadOnly && total<=int.MaxValue-progress.Data.coins);
        Row("Keep your catches","Return to individual fish sales.","BACK",()=>Open("market"));
    }
    private void Directory()
    {
        if(!travel.InShop) { Row("Visit Tideglass Quay","The shops and habitats are in a separate world.","TRAVEL",()=>travel.Travel(true)); return; }
        Row("The Tackle Atelier","Rods, reels, line extensions and bait.","VISIT",()=>Visit("gear"));
        Row("Fresh Catch Market","Sell individual fish or review a full-bag sale.","VISIT",()=>Visit("market"));
        foreach(var definition in ShopCatalog.Habitats)
        {
            var d=definition; Row(d.name,$"{d.price:N0} coins / {d.fishLimit} fish / {d.maxFishKg} kg each / {d.totalKg} kg total"+(d.swimmable?" / SWIM ACCESS":""),progress.Data.Habitat(d.id)!=null?"OWNED / VISIT":"VIEW",()=>Visit(d.id));
        }
    }
    private void Visit(string id) { travel.Visit(id); message=""; Open(id); }
    private void HabitatPage(string id)
    {
        var d=ShopCatalog.Habitat(id); if(d==null) { Directory(); return; }
        if(!travel.Near(id)) { Row(d.name,"Approach the sign to buy or manage this habitat.","VISIT",()=>Visit(id),travel.InShop); return; }
        var owned=progress.Data.Habitat(id);
        string capacity=$"{d.width} x {d.depth} x {d.height} m / {d.fishLimit} fish / max {d.maxFishKg} kg each / {d.totalKg} kg total";
        if(owned==null) { Row(d.name,capacity,$"BUY {d.price:N0}",()=>Result(progress.BuyHabitat(id),"Habitat purchased. Add fish from your bag below."),progress.Data.coins>=d.price && !progress.ReadOnly); return; }
        float kg=0; foreach(var f in owned.fish) kg+=f.weightKg;
        Row(d.name,$"OWNED / {owned.fish.Count}/{d.fishLimit} fish / {kg:0.00}/{d.totalKg} kg\n"+capacity,d.swimmable?"ENTER WATER":"VIEW",()=>{if(d.swimmable)travel.EnterHabitat(id);Close();});
        foreach(var fish in new List<CaughtFishRecord>(owned.fish))
        { var f=fish; Row("RESIDENT / "+FishCatalog.Get(f.speciesId).Name,$"{f.weightKg:0.00} kg","TO BAG",()=>Result(progress.Withdraw(id,f),"Fish returned to your bag."),!progress.ReadOnly); }
        foreach(var fish in new List<CaughtFishRecord>(progress.Data.bag))
        { var f=fish; string reason=progress.Data.Admission(id,f); Row("BAG / "+FishCatalog.Get(f.speciesId).Name,$"{f.weightKg:0.00} kg / "+(reason??"Fits this habitat."),"ADD FISH",()=>Result(progress.Deposit(id,f),"Fish added to habitat."),reason==null && !progress.ReadOnly); }
        if(progress.Data.bag.Count==0) Row("No fish in your bag","Catch more fish on the island, or retrieve residents from another habitat.","DIRECTORY",()=>Open("shops"));
    }
    private void Result(bool ok,string success) { message=ok?success:"Action unavailable. Check coins, ownership, capacity and distance to the shop."; Refresh(false); }
    private void Row(string title,string detail,string action,Action callback,bool enabled=true)
    {
        GameObject row=Panel("Item",list,new Color(0.07f,0.12f,0.14f,1));
        row.AddComponent<LayoutElement>().preferredHeight=116;
        var accent=Panel("Accent",row.transform,gold); Anchor(accent.GetComponent<RectTransform>(),0,0,0,1,0,0,4,0);
        var titleText=Label(row.transform,title,23,Color.white); Anchor(titleText.rectTransform,0,0.5f,1,1,18,0,-200,-8);
        var detailText=Label(row.transform,detail,17,new Color(0.65f,0.79f,0.8f)); Anchor(detailText.rectTransform,0,0,1,0.55f,18,6,-200,0);
        var b=ButtonAt(row.transform,action,callback); Rect(b.GetComponent<RectTransform>(),new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(-14,0),new Vector2(170,54));
        b.interactable=enabled;
    }
    private GameObject Panel(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false); go.GetComponent<Image>().color=color; return go;
    }
    private Text Label(Transform parent,string text,int size,Color color)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;t.supportRichText=false;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=13;t.resizeTextMaxSize=size; return t;
    }
    private Button ButtonAt(Transform parent,string text,Action action)
    {
        var go=Panel(text,parent,teal);var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<Image>();b.onClick.AddListener(()=>action());
        ColorBlock colors=b.colors;colors.disabledColor=new Color(0.4f,0.4f,0.4f,0.45f);b.colors=colors;
        var label=Label(go.transform,text,19,Color.white);label.alignment=TextAnchor.MiddleCenter;Full(label.rectTransform);return b;
    }
    private static void Full(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
    private static void Rect(RectTransform r,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size) {r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=pos;}
    private static void Anchor(RectTransform r,float x0,float y0,float x1,float y1,float l,float b,float right,float top) { r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(right,top); }
}
