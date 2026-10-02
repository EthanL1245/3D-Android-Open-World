using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ShopWorldHUD predates the fifth rod and still builds four gear rows per category.
/// This appends the Level 5 rod through the HUD's existing Row/Result methods so it
/// has exactly the same visual style, purchase flow and equip behavior without
/// disturbing reel/line tier counts.
/// </summary>
[DefaultExecutionOrder(1800)]
public sealed class Level5FishingRodShopRuntime : MonoBehaviour
{
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly FieldInfo PageField=typeof(ShopWorldHUD).GetField("page",Flags);
    private static readonly MethodInfo RowMethod=typeof(ShopWorldHUD).GetMethod("Row",Flags);
    private static readonly MethodInfo ResultMethod=typeof(ShopWorldHUD).GetMethod("Result",Flags);

    private ShopWorldHUD hud;
    private ShopProgress progress;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(ShopWorldHUD target in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(target!=null && target.GetComponent<Level5FishingRodShopRuntime>()==null)
                target.gameObject.AddComponent<Level5FishingRodShopRuntime>();
    }

    private void Awake()
    {
        hud=GetComponent<ShopWorldHUD>();
        progress=FindFirstObjectByType<ShopProgress>();
        if(hud==null || PageField==null || RowMethod==null || ResultMethod==null)
        {
            Debug.LogError("Level5FishingRodShopRuntime could not bind the existing shop UI and was disabled.");
            enabled=false;
        }
    }

    private void LateUpdate()
    {
        if(!enabled || !ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+.08f;
        if(progress==null)progress=FindFirstObjectByType<ShopProgress>();
        if(progress==null)return;

        string page=PageField.GetValue(hud) as string;
        bool shop=string.Equals(page,"gear",StringComparison.Ordinal);
        bool equipment=string.Equals(page,"equipment",StringComparison.Ordinal);
        if(!shop && !equipment)return;

        bool owned=progress.Data.OwnsGear(GearKind.Rod,4);
        if((shop && owned) || (equipment && !owned))return;
        if(HasLevel5Row())return;

        bool equipped=owned && progress.Data.rodEquipped==4;
        string stats=FishingBurstDamageRuntime.NormalMinimumForTier(4)+"–"+FishingBurstDamageRuntime.NormalMaximumForTier(4)+
                     " damage / "+(FishingBurstDamageRuntime.CriticalChanceForTier(4)*100f).ToString("0")+"% critical (2×)";
        string action=equipped?"EQUIPPED":owned?"EQUIP":"BUY 25,000";
        bool nearGear=ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.Near("gear");
        bool can=owned?!equipped:(nearGear && progress.Data.coins>=ShopCatalog.RodPrices[4]);

        Action callback=()=>
        {
            bool wasOwned=progress.Data.OwnsGear(GearKind.Rod,4);
            bool result=wasOwned
                ? (!progress.ReadOnly && progress.Commit(progress.Data.Equip(GearKind.Rod,4)))
                : progress.BuyGear(GearKind.Rod,4);
            ResultMethod.Invoke(hud,new object[]{result,wasOwned?"Equipment updated.":"Purchased and equipped."});
        };

        RowMethod.Invoke(hud,new object[]{
            ShopCatalog.RodNames[4],
            stats,
            action,
            callback,
            can && !progress.ReadOnly,
            null,
            GearKind.Rod.ToString()
        });
    }

    private bool HasLevel5Row()
    {
        foreach(Text label in hud.GetComponentsInChildren<Text>(true))
            if(label!=null && string.Equals(label.text,ShopCatalog.RodNames[4],StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
}
