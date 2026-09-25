using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime support for upgraded fishing rods. All upgraded rods retain Woodland
/// Rod casting, line tension, flex and reel behavior. Level 2 deals 2x Woodland
/// damage with a 5% 2x critical; Level 3 deals 3x Woodland damage with an 8%
/// 2x critical. Only the visible RodBlank and damage output change.
/// </summary>
[DefaultExecutionOrder(960)]
public sealed class Level2FishingRodRuntime : MonoBehaviour
{
    private FishingSystem fishing;
    private ShopProgress progress;
    private FieldInfo rodRootField, rodViewField, rodPowerField;
    private FieldInfo stateField, hpField, maxHpField, healthField, pendingDamageField, unconsciousField;
    private int appliedTier=-1;
    private int observedHp=-1;
    private int warnedTier=-1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<Level2FishingRodRuntime>()==null)
                system.gameObject.AddComponent<Level2FishingRodRuntime>();
        EnsureShopCleanup();
        EnsureDamagePresentation();
    }

    private static void EnsureShopCleanup()
    {
        foreach(ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(hud!=null && hud.GetComponent<RodShopCleanup>()==null)
                hud.gameObject.AddComponent<RodShopCleanup>();
    }

    private static void EnsureDamagePresentation()
    {
        foreach(FishingHUD hud in FindObjectsByType<FishingHUD>(FindObjectsSortMode.None))
            if(hud!=null && hud.GetComponent<FishingDamagePresentation>()==null)
                hud.gameObject.AddComponent<FishingDamagePresentation>();
    }

    private void Awake()
    {
        fishing=GetComponent<FishingSystem>();
        progress=GetComponent<ShopProgress>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        Type type=typeof(FishingSystem);
        rodRootField=type.GetField("rodRoot",flags);
        rodViewField=type.GetField("rodView",flags);
        rodPowerField=type.GetField("rodPower",flags);
        stateField=type.GetField("state",flags);
        hpField=type.GetField("fishHealthPoints",flags);
        maxHpField=type.GetField("fishMaxHealth",flags);
        healthField=type.GetField("fishHealth",flags);
        pendingDamageField=type.GetField("pendingDamage",flags);
        unconsciousField=type.GetField("fishUnconscious",flags);
    }

    private void Start()
    {
        EnsureShopCleanup();
        EnsureDamagePresentation();
        if(progress==null || fishing==null || rodRootField==null || rodViewField==null || rodPowerField==null ||
           stateField==null || hpField==null || maxHpField==null || healthField==null || pendingDamageField==null || unconsciousField==null)
        { enabled=false; return; }

        if(progress.Data.EnsureGearOwnership() && !progress.ReadOnly)progress.Save();
        observedHp=(int)hpField.GetValue(fishing);
    }

    private void LateUpdate()
    {
        if(progress==null || fishing==null)return;
        EnsureShopCleanup();
        EnsureDamagePresentation();
        // Upgraded rods do not receive the old placeholder tension-control bonus.
        rodPowerField.SetValue(fishing,1f);
        int tier=Mathf.Clamp(progress.Data.rodEquipped,0,ShopCatalog.MaxRodTier);
        ApplyVisual(tier);
        ClearRodTint();
        ApplyDamageBonus(tier);
    }

    private void ApplyDamageBonus(int tier)
    {
        int current=(int)hpField.GetValue(fishing);
        string state=stateField.GetValue(fishing)?.ToString()??string.Empty;
        bool unconscious=(bool)unconsciousField.GetValue(fishing);

        if(state!="Fighting")
        {
            FishingDamagePresentation.ClearPendingCritical();
            observedHp=current;
            return;
        }
        if(unconscious)
        {
            observedHp=current;
            return;
        }

        if(observedHp<0 || current>observedHp)
        {
            observedHp=current;
            return;
        }

        int woodlandDamage=observedHp-current;
        if(tier>0 && woodlandDamage>0 && current>0)
        {
            int normalMultiplier=tier>=2?3:2;
            float criticalChance=tier>=2?0.08f:0.05f;
            bool critical=UnityEngine.Random.value<criticalChance;
            int totalMultiplier=critical?normalMultiplier*2:normalMultiplier;
            int extraMultiplier=totalMultiplier-1;
            int extra=Mathf.Min(current,woodlandDamage*extraMultiplier);
            if(extra>0)
            {
                current-=extra;
                hpField.SetValue(fishing,current);
                int max=Mathf.Max(1,(int)maxHpField.GetValue(fishing));
                healthField.SetValue(fishing,current/(float)max);
                int pending=(int)pendingDamageField.GetValue(fishing);
                pendingDamageField.SetValue(fishing,pending+extra);
                if(critical)FishingDamagePresentation.MarkCriticalHit();
            }
        }
        observedHp=current;
    }

    private void ApplyVisual(int tier)
    {
        GameObject rodRoot=rodRootField.GetValue(fishing) as GameObject;
        if(rodRoot==null || appliedTier==tier)return;

        GameObject sourcePrefab=Resources.Load<GameObject>(ShopCatalog.RodPrefabResource(tier));
        if(sourcePrefab==null)
        {
            if(tier>0 && warnedTier!=tier)
            {
                warnedTier=tier;
                string level=(tier+1).ToString(CultureInfo.InvariantCulture);
                Debug.LogWarning("Level "+level+" rod model is not installed yet. Run Tools > Open World > Import Level "+level+" Fishing Rod (One Click). Woodland Rod visual is being used temporarily.");
            }
            return;
        }

        Transform source=FindDeepChild(sourcePrefab.transform,"RodBlank");
        Transform target=FindDeepChild(rodRoot.transform,"RodBlank");
        if(source==null || target==null)return;

        MeshFilter sourceFilter=source.GetComponent<MeshFilter>();
        MeshFilter targetFilter=target.GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer=source.GetComponent<MeshRenderer>();
        MeshRenderer targetRenderer=target.GetComponent<MeshRenderer>();
        if(sourceFilter==null || targetFilter==null || sourceRenderer==null || targetRenderer==null)return;

        targetFilter.sharedMesh=sourceFilter.sharedMesh;
        targetRenderer.sharedMaterials=sourceRenderer.sharedMaterials;
        targetRenderer.SetPropertyBlock(null);

        FishingRodView view=rodViewField.GetValue(fishing) as FishingRodView;
        if(view!=null)view.InitializePose();
        appliedTier=tier;
        warnedTier=-1;
    }

    private void ClearRodTint()
    {
        GameObject rodRoot=rodRootField.GetValue(fishing) as GameObject;
        if(rodRoot==null)return;
        Transform blank=FindDeepChild(rodRoot.transform,"RodBlank");
        MeshRenderer renderer=blank!=null?blank.GetComponent<MeshRenderer>():null;
        if(renderer!=null)renderer.SetPropertyBlock(null);
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            if(t!=null && t.name==name)return t;
        return null;
    }
}

/// <summary>
/// Keeps the generated Tackle Store UI aligned with the real gear rules without
/// changing travel/equipment menus. Every purchasable tackle item may be bought
/// directly; no previous tier is required.
/// </summary>
public sealed class RodShopCleanup : MonoBehaviour
{
    private float nextScan;
    private ShopProgress progress;

    private void Awake(){progress=FindFirstObjectByType<ShopProgress>();}

    private void LateUpdate()
    {
        if(!ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+0.05f;
        if(progress==null)progress=FindFirstObjectByType<ShopProgress>();

        foreach(Text text in GetComponentsInChildren<Text>(true))
        {
            if(text==null)continue;
            string value=text.text??string.Empty;
            if(value=="REMOVED ROD" || value=="Offshore Carbon" || value=="Bluewater Elite")
            {
                GameObject row=FindRow(text.transform);
                if(row!=null)row.SetActive(false);
                continue;
            }

            if(value.Contains(" / Requires previous tier"))
                text.text=value.Replace(" / Requires previous tier",string.Empty);

            GameObject parentRow=FindRow(text.transform);
            if(parentRow==null)continue;
            if(RowContains(parentRow,"Woodland Rod") && text.text.Contains("0% more tension control"))
                text.text=text.text.Replace("0% more tension control","Damage: 2–4 per burst");
            else if(RowContains(parentRow,"Level 2 Fishing Rod") && (text.text.Contains("18% more tension control") || text.text.Contains("2x fish damage")))
                text.text="Damage: 4–8 per burst • 5% critical chance • critical hits deal 2x damage";
            else if(RowContains(parentRow,"Level 3 Fishing Rod") && (text.text.Contains("36% more tension control") || text.text.Contains("3x fish damage")))
                text.text="Damage: 6–12 per burst • 8% critical chance • critical hits deal 2x damage";
        }

        // ShopWorldHUD's original rows disabled a BUY button unless the previous
        // tier was owned. The ledger now accepts any tier directly, so make the
        // button reflect only affordability/read-only state instead.
        foreach(Button button in GetComponentsInChildren<Button>(true))
        {
            Text label=button.GetComponentInChildren<Text>(true);
            if(label==null)continue;
            string value=label.text??string.Empty;
            if(!value.StartsWith("BUY ",StringComparison.OrdinalIgnoreCase))continue;
            int price=ParseBuyPrice(value);
            if(price<0)continue;
            button.interactable=progress!=null && !progress.ReadOnly && progress.Data.coins>=price;
        }
    }

    private static int ParseBuyPrice(string label)
    {
        string digits="";
        for(int i=4;i<label.Length;i++)if(char.IsDigit(label[i]))digits+=label[i];
        return int.TryParse(digits,NumberStyles.None,CultureInfo.InvariantCulture,out int price)?price:-1;
    }

    private static bool RowContains(GameObject row,string value)
    {
        foreach(Text t in row.GetComponentsInChildren<Text>(true))
            if(t!=null && string.Equals(t.text,value,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private static GameObject FindRow(Transform start)
    {
        for(Transform t=start;t!=null;t=t.parent)
            if(t.GetComponent<LayoutElement>()!=null)return t.gameObject;
        return null;
    }
}
