using System;
using System.Collections.Generic;
using System.Linq;
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
    private GameObject root, modal, nearbyButton;
    private RectTransform list;
    private ScrollRect scroll;
    private Text heading, wallet, feedback, nearLabel;
    private string page="travel", message="";
    private bool dirty;
    private string shopSession, bagPicker;
    private int bagSort, speciesFilter=-1;
    private GameObject menuShortcut;
    private readonly List<GameObject> navigationTabs=new List<GameObject>();
    private static readonly string[] SortNames={"Newest first","Heaviest first","Lightest first","Highest value","Species name"};
    private ShopPreview previews;
    private readonly Color ink=new Color(0.025f,0.055f,0.07f,0.98f);
    private readonly Color teal=new Color(0.04f,0.36f,0.39f,1f);
    private readonly Color gold=new Color(0.89f,0.72f,0.40f,1f);
    private void Start()
    {
        progress=FindFirstObjectByType<ShopProgress>(); travel=ShopDimensionManager.Instance;
        if(progress==null || travel==null) { enabled=false; return; }
        fishing=progress.GetComponent<FishingSystem>(); player=progress.GetComponent<FirstPersonController>();
        fishingHUD=GetComponent<FishingHUD>(); font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        previews=gameObject.AddComponent<ShopPreview>();
        Build(); progress.Changed+=RefreshSoon; message=progress.Notice; MenuOpen=false;
    }
    private void OnDestroy() { if(progress!=null) progress.Changed-=RefreshSoon; MenuOpen=false; }
    private void RefreshSoon() => dirty=true;
    private void Update()
    {
        if(root==null) return;
        if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        { if(MenuOpen) Close(); else Open("travel"); }
        menuShortcut.SetActive(!MenuOpen);
        if(shopSession==null && Keyboard.current!=null && Keyboard.current.tabKey.wasPressedThisFrame) Open("bag");
        string nearest=travel.Nearest();
        nearbyButton.SetActive(!MenuOpen && nearest!=null && !travel.Traveling);
        if(nearest!=null) nearLabel.text=nearest=="gear" ? "OPEN TACKLE STORE" : nearest=="market" ? "SELL FISH" : "VIEW HABITAT";
        if(!MenuOpen && nearest!=null && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame) Open(nearest);

        if(travel.Traveling) { feedback.text="Traveling..."; return; }
        if(travel.TravelError!=null) feedback.text=travel.TravelError;
        if(dirty && MenuOpen) { dirty=false; Refresh(false); }
    }
    public void Open(string destination)
    {
        if(root==null || travel.Traveling) return;
        if(MenuOpen && shopSession!=null && destination!=shopSession && !(shopSession=="market" && destination=="sell-confirm"))return;
        if(MenuOpen && page==destination && !dirty)return;
        if(!MenuOpen)
        {
            shopSession=destination=="gear" || destination=="market"?destination:null;
            fishing.PrepareForMenu();
        }
        MenuOpen=true; page=destination; modal.SetActive(true); root.transform.SetAsLastSibling();
        player.SetMenuOpen(true); if(fishingHUD!=null) fishingHUD.SetMenuCovered(true);
        Refresh(true);
    }
    public void Close()
    {
        if(travel!=null && travel.Traveling) return;
        MenuOpen=false;shopSession=null;bagPicker=null;
        if(previews!=null)previews.Suspend();
        if(modal!=null) modal.SetActive(false);
        if(player!=null) player.SetMenuOpen(false);
        if(fishingHUD!=null) fishingHUD.SetMenuCovered(false);

    }
    private void Build()
    {
        root=Panel("TideglassHUD",transform,Color.clear); Full(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().raycastTarget=false;
        Button menu=ButtonAt(root.transform,"MENU / TRAVEL",()=>Open("travel"));
        menuShortcut=menu.gameObject;
        Rect(menu.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-20),new Vector2(260,64));
        nearbyButton=ButtonAt(root.transform,"OPEN",()=>Open(travel.Nearest()??"travel")).gameObject;
        nearLabel=nearbyButton.GetComponentInChildren<Text>();
        Rect(nearbyButton.GetComponent<RectTransform>(),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0,130),new Vector2(300,58));
        modal=Panel("ShopMenu",root.transform,ink);
        var r=modal.GetComponent<RectTransform>(); r.anchorMin=new Vector2(0.045f,0.045f); r.anchorMax=new Vector2(0.955f,0.955f); r.offsetMin=r.offsetMax=Vector2.zero;
        heading=Label(modal.transform,"MENU / TRAVEL",36,gold); Anchor(heading.rectTransform,0,1,1,1,24,-62,-150,-12);
        wallet=Label(modal.transform,"",22,Color.white); Anchor(wallet.rectTransform,0,1,1,1,24,-92,-24,-62);
        Button close=ButtonAt(modal.transform,"CLOSE",Close); Rect(close.GetComponent<RectTransform>(),Vector2.one,Vector2.one,Vector2.one,new Vector2(-18,-16),new Vector2(160,64));
        string[] tabs={"travel","bag","equipment"}; string[] names={"TRAVEL","FISH BAG","EQUIPMENT"};
        for(int i=0;i<tabs.Length;i++) { string tab=tabs[i]; var b=ButtonAt(modal.transform,names[i],()=>Open(tab)); navigationTabs.Add(b.gameObject); Anchor(b.GetComponent<RectTransform>(),i/3f,1,(i+1)/3f,1,16,-148,-16,-103); }
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
        modal.SetActive(false); nearbyButton.SetActive(false);
    }
    private void Refresh(bool top)
    {
        dirty=false;
        float position=scroll.verticalNormalizedPosition;
        for(int i=list.childCount-1;i>=0;i--) { list.GetChild(i).gameObject.SetActive(false); Destroy(list.GetChild(i).gameObject); }
        wallet.text=$"{progress.Data.coins:N0} COINS   /   {progress.Data.bag.Count}/{ShopLedger.BagLimit} FISH IN BAG   /   {(travel.InHome?"HOME":travel.InShop?"TIDEGLASS QUAY":"SUNCREST REEF")}";
        feedback.text=progress.ReadOnly ? progress.Notice : message;
        heading.text=shopSession=="gear"?"TACKLE STORE":shopSession=="market"?"SELL FISH":"MENU / TRAVEL";
        foreach(var tab in navigationTabs)tab.SetActive(shopSession==null);
        if(page=="travel") TravelPage(); else if(page=="bag") BagPage(); else if(page=="equipment") EquipmentPage(false);
        else if(page=="gear") EquipmentPage(true); else if(page=="market") MarketPage(); else if(page=="sell-confirm") SellConfirmation();
        else if(page=="islands") IslandIndex(); else if(page=="reef-fish") ReefFishIndex();
        else HabitatPage(page);
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition=top?1f:position;
    }
    private void TravelPage()
    {
        string[] names={"SUNCREST REEF","TIDEGLASS QUAY","HOME"};
        string[] details={"Beginner island • beach • shallow reef","View habitats • buy upgrades • sell your catch","Your garden • aquarium • pond • fish collection"};
        Color[] colors={new Color(0.06f,0.30f,0.39f),new Color(0.29f,0.23f,0.13f),new Color(0.12f,0.30f,0.22f)};
        var cards=Panel("Destinations",list,Color.clear);
        cards.AddComponent<LayoutElement>().preferredHeight=280;
        for(int i=0;i<3;i++)
        {
            int destination=i;
            var button=ButtonAt(cards.transform,names[i]+"\n\n"+details[i]+(travel.Destination==i?"\n\nYOU ARE HERE":"\n\nTRAVEL →"),()=>travel.Travel(destination));
            Anchor(button.GetComponent<RectTransform>(),i/3f,0,(i+1)/3f,1,8,8,-8,-8);
            var caption=button.GetComponentInChildren<Text>().rectTransform;caption.offsetMin=new Vector2(16,16);caption.offsetMax=new Vector2(-16,-16);
            button.GetComponent<Image>().color=colors[i];
            button.GetComponentInChildren<Text>().fontSize=34;
            button.GetComponentInChildren<Text>().resizeTextMaxSize=34;
            button.interactable=travel.Destination!=i;
            if(travel.Destination==i)button.GetComponent<Image>().color=new Color(0.16f,0.22f,0.24f);
        }
        Row("ISLANDS & FISH", "Discover regions, unlock status, species and rarity.", "VIEW INDEX",()=>Open("islands"));
    }
    private void IslandIndex()
    {
        foreach (var zone in ReefCatalog.Zones)
        {
            var entry=zone;
            Row(entry.name,entry.description,entry.Unlocked?"FISH INDEX":"LOCKED",()=>Open("reef-fish"),entry.Unlocked);
        }
        Row("MORE HORIZONS", "Additional islands and open-ocean biomes will have their own unlocks and fish indices.", "COMING LATER",()=>{},false);
        Row("TRAVEL", "Return to destinations", "BACK",()=>Open("travel"));
    }
    private void ReefFishIndex()
    {
        Row(ReefCatalog.StarterName, "All seven species • base catch chances below. Specialty bait changes these odds.", "ISLAND INDEX",()=>Open("islands"));
        foreach(int id in FishCatalog.ActiveIds.OrderByDescending(ReefCatalog.Weight))
        {
            var species=FishCatalog.Get(id);
            Row(species.Name,ReefCatalog.Rarity(id)+" • "+ReefCatalog.Weight(id).ToString("0")+"% base chance", "UNLOCKED",()=>{},false,
                fish:new CaughtFishRecord{speciesId=id,weightKg=species.MinWeightKg});
        }
    }
    private void BagPage()
    {
        var species=progress.Data.bag.Select(f=>f.speciesId).Distinct().OrderBy(id=>FishCatalog.Get(id).Name).ToList();
        if(speciesFilter!=-1 && !species.Contains(speciesFilter))speciesFilter=-1;
        var controls=Panel("Sort and filter",list,Color.clear);controls.AddComponent<LayoutElement>().preferredHeight=64;
        var sort=ButtonAt(controls.transform,"SORT: "+SortNames[bagSort],()=>{bagPicker=bagPicker=="sort"?null:"sort";Refresh(true);});
        Anchor(sort.GetComponent<RectTransform>(),0,0,0.5f,1,0,0,-6,0);
        var filter=ButtonAt(controls.transform,"SPECIES: "+(speciesFilter<0?"All":FishCatalog.Get(speciesFilter).Name),()=>{bagPicker=bagPicker=="species"?null:"species";Refresh(true);});
        Anchor(filter.GetComponent<RectTransform>(),0.5f,0,1,1,6,0,0,0);
        if(bagPicker=="sort")
        {
            for(int i=0;i<SortNames.Length;i++){int choice=i;Row(SortNames[i],"Choose how catches are ordered.",bagSort==i?"SELECTED":"SELECT",()=>{bagSort=choice;bagPicker=null;Refresh(true);});}
            return;
        }
        if(bagPicker=="species")
        {
            Row("All species","Show every catch.","SELECT",()=>{speciesFilter=-1;bagPicker=null;Refresh(true);});
            foreach(int id in species){int choice=id;Row(FishCatalog.Get(id).Name,$"{progress.Data.bag.Count(f=>f.speciesId==id)} in bag","SELECT",()=>{speciesFilter=choice;bagPicker=null;Refresh(true);});}
            return;
        }
        if(progress.Data.bag.Count==0) {Row("No catches yet","Your reusable lure is always available. Travel to the island and cast.","TRAVEL",()=>Open("travel"));return;}
        IEnumerable<CaughtFishRecord> fish=progress.Data.bag.Where(f=>speciesFilter<0 || f.speciesId==speciesFilter);
        switch(bagSort)
        {
            case 1:fish=fish.OrderByDescending(f=>f.weightKg).ThenByDescending(f=>f.caughtUtcTicks);break;
            case 2:fish=fish.OrderBy(f=>f.weightKg).ThenByDescending(f=>f.caughtUtcTicks);break;
            case 3:fish=fish.OrderByDescending(f=>FishCatalog.GetSellValue(f.speciesId,f.weightKg));break;
            case 4:fish=fish.OrderBy(f=>FishCatalog.Get(f.speciesId).Name).ThenByDescending(f=>f.weightKg);break;
            default:fish=fish.OrderByDescending(f=>f.caughtUtcTicks);break;
        }
        foreach(var record in fish)
        {
            var f=record;Row(FishCatalog.Get(f.speciesId).Name,$"{f.weightKg:0.00} kg / {ShopCatalog.FishLength(f.speciesId,f.weightKg):0.00} m / value {FishCatalog.GetSellValue(f.speciesId,f.weightKg)} coins",fishing.IsHolding(f)?"PUT AWAY":"HOLD",()=>{Close();int index=progress.Data.bag.IndexOf(f);if(index>=0)fishing.HoldFish(index);},fish:f);
        }
    }
    private void EquipmentPage(bool shop)
    {
        if(shop && !travel.Near("gear")) { Row("Visit the tackle counter","Return to the counter to make purchases.","CLOSE",Close); return; }
        foreach(GearKind kind in Enum.GetValues(typeof(GearKind)))
        {
            for(int tier=0;tier<4;tier++)
            {
                int t=tier; GearKind k=kind; bool owned=t<=progress.Data.Owned(k), equipped=t==progress.Data.Equipped(k);
                if((!shop && !owned) || (shop && owned)) continue;
                string stats=k==GearKind.Rod?$"{t*18}% more tension control":k==GearKind.Reel?$"{t*22}% faster tiring and retrieval":$"+{ShopCatalog.LineBonus[t]} m range; {t*12}% more line tolerance";
                string action=equipped?"EQUIPPED":owned?"EQUIP":$"BUY {ShopCatalog.GearPrice(k,t):N0}";
                bool can=owned?!equipped:t==progress.Data.Owned(k)+1 && progress.Data.coins>=ShopCatalog.GearPrice(k,t);
                Row(ShopCatalog.GearName(k,t),stats+(owned?"":" / Requires previous tier"),action,()=>
                {
                    bool result=owned?(!progress.ReadOnly && progress.Commit(progress.Data.Equip(k,t))):progress.BuyGear(k,t);
                    Result(result,owned?"Equipment updated.":"Purchased and equipped.");
                },can && !progress.ReadOnly,gear:k.ToString());
            }
        }
        for(int i=0;i<4;i++)
        {
            int id=i; string effect=id==0?"Unlimited uses. Standard catch chances.":id==1?"35% shorter wait; one worm per cast.":id==2?"20% shorter wait; favors snapper and goatfish.":"Favors yellowtail and tuna; one squid per cast.";
            string count=id==0?"Unlimited":$"{progress.Data.bait[id]} remaining";
            if(shop && id>0) Row(ShopCatalog.BaitNames[id]+" / PACK OF 10",effect+" "+count,$"BUY {ShopCatalog.BaitPrices[id]}",()=>Result(progress.BuyBait(id),"Bait purchased and selected."),progress.Data.coins>=ShopCatalog.BaitPrices[id] && !progress.ReadOnly,gear:"Bait"+id);
            if(!shop)Row(ShopCatalog.BaitNames[id],count+" / "+effect,progress.Data.baitEquipped==id?"SELECTED":"SELECT",()=>{if(progress.ReadOnly)return; progress.Data.baitEquipped=id; progress.Save(); Result(true,"Bait selected.");},progress.Data.baitEquipped!=id && (id==0 || progress.Data.bait[id]>0) && !progress.ReadOnly,gear:"Bait"+id);
        }
    }
    private void MarketPage()
    {
        if(!travel.Near("market")) { Row("Visit the fish market","Return to the market counter to sell fish.","CLOSE",Close); return; }
        if(progress.Data.bag.Count==0) { Row("Your bag is empty","Bring your next catch to the market.","CLOSE",Close); return; }
        Row("Sell bag contents","Review the total before selling all fish in the bag.","REVIEW ALL",()=>Open("sell-confirm"));
        foreach(var fish in new List<CaughtFishRecord>(progress.Data.bag))
        {
            var f=fish; int value=FishCatalog.GetSellValue(f.speciesId,f.weightKg);
            Row(FishCatalog.Get(f.speciesId).Name,$"{f.weightKg:0.00} kg / {ShopCatalog.FishLength(f.speciesId,f.weightKg):0.00} m",$"SELL {value}",()=>Result(progress.Sell(f),$"Sold for {value} coins."),!progress.ReadOnly,fish:f);
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
    private void HabitatPage(string id)
    {
        var d=ShopCatalog.Habitat(id); if(d==null) {TravelPage();return;}
        if(!travel.Near(id)) { Row(d.name,"Approach its sign to view this habitat.","TRAVEL",()=>Open("travel"));return; }
        var owned=progress.Data.Habitat(id);
        string capacity=$"{d.width} × {d.depth} × {d.height} m / {d.fishLimit} fish / max {d.maxFishKg} kg each / {d.totalKg} kg total";
        if(travel.InShop)
        {
            int cost=progress.Data.UpgradeCost(id);
            var current=progress.Data.CurrentHabitat(d.pond);
            string detail=owned!=null?"This habitat is installed at Home.":cost>=0?"Upgrades your Home in place. Residents stay with their habitat.":"Purchase the preceding size first. You own one aquarium and one pond.";
            Row(d.name,capacity+"\n"+detail,owned!=null?"OWNED":cost>=0?$"UPGRADE {cost:N0}":"LOCKED",()=>Result(progress.BuyHabitat(id),"Your Home habitat has been upgraded."),cost>=0 && progress.Data.coins>=cost && !progress.ReadOnly);
            return;
        }
        if(owned==null || !travel.InHome)return;
        float kg=0;foreach(var f in owned.fish)kg+=f.weightKg;
        Row(d.name,$"{owned.fish.Count}/{d.fishLimit} fish • {kg:0.00}/{d.totalKg} kg\n"+capacity,"HOME",()=>{},false);
        foreach(var fish in new List<CaughtFishRecord>(owned.fish))
        {var f=fish;Row(FishCatalog.Get(f.speciesId).Name,$"RESIDENT • {f.weightKg:0.00} kg",progress.Data.BagFull?"BAG FULL":"TO BAG",()=>Result(progress.Withdraw(id,f),"Fish returned to bag."),!progress.ReadOnly && !progress.Data.BagFull,fish:f);}
        foreach(var fish in new List<CaughtFishRecord>(progress.Data.bag))
        {var f=fish;string reason=progress.Data.Admission(id,f);Row(FishCatalog.Get(f.speciesId).Name,$"BAG • {f.weightKg:0.00} kg • "+(reason??"Fits this habitat"),"ADD FISH",()=>Result(progress.Deposit(id,f),"Fish added to habitat."),reason==null && !progress.ReadOnly,fish:f);}
    }
    private void Result(bool ok,string success) { message=ok?success:"Action unavailable. Check coins, ownership, capacity and distance to the shop."; Refresh(false); }
    private void Row(string title,string detail,string action,Action callback,bool enabled=true,CaughtFishRecord fish=null,string gear=null)
    {
        GameObject row=Panel("Item",list,new Color(0.07f,0.12f,0.14f,1));
        row.AddComponent<LayoutElement>().preferredHeight=184;
        var accent=Panel("Accent",row.transform,gold); Anchor(accent.GetComponent<RectTransform>(),0,0,0,1,0,0,4,0);
        var titleText=Label(row.transform,title,26,Color.white); Anchor(titleText.rectTransform,0,0.5f,1,1,(fish!=null || gear!=null?170:22),0,-232,-10);
        var detailText=Label(row.transform,detail,21,new Color(0.65f,0.79f,0.8f)); Anchor(detailText.rectTransform,0,0,1,0.55f,(fish!=null || gear!=null?170:22),10,-232,0);
        var b=ButtonAt(row.transform,action,callback); Rect(b.GetComponent<RectTransform>(),new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(-14,0),new Vector2(205,76));
        b.interactable=enabled;
        if(!enabled)b.GetComponent<Image>().color=new Color(0.17f,0.23f,0.24f);
        if(fish!=null || gear!=null)
        {
            var picture=new GameObject("3D item preview",typeof(RectTransform),typeof(RawImage));picture.transform.SetParent(row.transform,false);
            Rect(picture.GetComponent<RectTransform>(),new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(14,0),new Vector2(140,140));
            var image=picture.GetComponent<RawImage>();image.raycastTarget=false;
            previews.Attach(image,fish!=null?fish.speciesId:0,fish!=null?fish.weightKg:1,gear);
        }
    }
    private GameObject Panel(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false); go.GetComponent<Image>().color=color; return go;
    }
    private Text Label(Transform parent,string text,int size,Color color)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.font=font;t.text=text;size=Mathf.RoundToInt(size*1.2f);t.fontSize=size;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;t.supportRichText=false;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=22;t.resizeTextMaxSize=size; return t;
    }
    private Button ButtonAt(Transform parent,string text,Action action)
    {
        var go=Panel(text,parent,teal);var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<Image>();b.transition=Selectable.Transition.None;b.onClick.AddListener(()=>action());
        ColorBlock colors=b.colors;colors.disabledColor=new Color(0.4f,0.4f,0.4f,0.45f);b.colors=colors;
        var label=Label(go.transform,text,23,Color.white);label.alignment=TextAnchor.MiddleCenter;Full(label.rectTransform);return b;
    }
    private static void Full(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
    private static void Rect(RectTransform r,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size) {r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=pos;}
    private static void Anchor(RectTransform r,float x0,float y0,float x1,float y1,float l,float b,float right,float top) { r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(right,top); }
}
