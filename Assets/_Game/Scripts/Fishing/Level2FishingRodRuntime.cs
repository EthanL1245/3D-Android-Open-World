using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime support for the real Level 2 rod. It keeps Woodland Rod handling,
/// tension, casting and reel behavior unchanged, swaps only the rod visual,
/// doubles fish damage, and gives each damage pulse a 5% critical chance that
/// doubles the Level 2 rod's already-doubled damage again.
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
    private bool warnedMissing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<Level2FishingRodRuntime>()==null)
                system.gameObject.AddComponent<Level2FishingRodRuntime>();
        EnsureShopCleanup();
    }

    private static void EnsureShopCleanup()
    {
        foreach(ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(hud!=null && hud.GetComponent<RodShopCleanup>()==null)
                hud.gameObject.AddComponent<RodShopCleanup>();
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
        if(progress==null || fishing==null || rodRootField==null || rodViewField==null || rodPowerField==null ||
           stateField==null || hpField==null || maxHpField==null || healthField==null || pendingDamageField==null || unconsciousField==null)
        { enabled=false; return; }

        bool migrated=false;
        if(progress.Data.rodOwned>ShopCatalog.MaxRodTier){progress.Data.rodOwned=ShopCatalog.MaxRodTier;migrated=true;}
        if(progress.Data.rodEquipped>ShopCatalog.MaxRodTier){progress.Data.rodEquipped=ShopCatalog.MaxRodTier;migrated=true;}
        if(progress.Data.rodEquipped>progress.Data.rodOwned){progress.Data.rodEquipped=progress.Data.rodOwned;migrated=true;}
        if(migrated && !progress.ReadOnly)progress.Save();
        observedHp=(int)hpField.GetValue(fishing);
    }

    private void LateUpdate()
    {
        if(progress==null || fishing==null)return;
        EnsureShopCleanup();
        rodPowerField.SetValue(fishing,1f);
        ApplyVisual(progress.Data.rodEquipped>=1?1:0);
        ClearRodTint();
        ApplyDamageBonus();
    }

    private void ApplyDamageBonus()
    {
        int current=(int)hpField.GetValue(fishing);
        string state=stateField.GetValue(fishing)?.ToString()??string.Empty;
        bool unconscious=(bool)unconsciousField.GetValue(fishing);

        if(state!="Fighting" || unconscious)
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
        if(progress.Data.rodEquipped>=1 && woodlandDamage>0 && current>0)
        {
            bool critical=UnityEngine.Random.value<0.05f;
            int extraMultiplier=critical?3:1;
            int extra=Mathf.Min(current,woodlandDamage*extraMultiplier);
            if(extra>0)
            {
                current-=extra;
                hpField.SetValue(fishing,current);
                int max=Mathf.Max(1,(int)maxHpField.GetValue(fishing));
                healthField.SetValue(fishing,current/(float)max);
                int pending=(int)pendingDamageField.GetValue(fishing);
                pendingDamageField.SetValue(fishing,pending+extra);
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
            if(tier>0 && !warnedMissing)
            {
                warnedMissing=true;
                Debug.LogWarning("Level 2 rod model is not installed yet. Run Tools > Open World > Import Level 2 Fishing Rod (One Click). Woodland Rod visual is being used temporarily.");
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
        warnedMissing=false;
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

public sealed class RodShopCleanup : MonoBehaviour
{
    private float nextScan;
    private void LateUpdate()
    {
        if(!ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+0.08f;
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
            if(value.Contains("18% more tension control"))
                text.text=value.Replace("18% more tension control","2x fish damage • 5% critical chance (critical = 2x Level 2 damage)")
                               .Replace(" / Requires previous tier",string.Empty);
        }
    }

    private static GameObject FindRow(Transform start)
    {
        for(Transform t=start;t!=null;t=t.parent)
            if(t.GetComponent<LayoutElement>()!=null)return t.gameObject;
        return null;
    }
}
