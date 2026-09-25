using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the two-reel catalog authoritative at runtime without disturbing the
/// existing fishing state machine. Any legacy placeholder reel tier is migrated
/// to the real Level 2 reel. Level 2 uses the same mechanics/animation as the
/// starter reel, with a 1.20x reeling multiplier and its own visual prefab.
/// </summary>
[DefaultExecutionOrder(950)]
public sealed class Level2FishingReelRuntime : MonoBehaviour
{
    private static readonly string[] ReelParts={"ReelFootAndBody","Rotor","ReelHousing","Spool","Handle"};
    private FishingSystem fishing;
    private ShopProgress progress;
    private FieldInfo reelPowerField;
    private FieldInfo rodRootField;
    private int appliedTier=-1;
    private bool warnedMissing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<Level2FishingReelRuntime>()==null)
                system.gameObject.AddComponent<Level2FishingReelRuntime>();
        foreach(ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(hud!=null && hud.GetComponent<ReelShopCleanup>()==null)
                hud.gameObject.AddComponent<ReelShopCleanup>();
    }

    private void Awake()
    {
        fishing=GetComponent<FishingSystem>();
        progress=GetComponent<ShopProgress>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        reelPowerField=typeof(FishingSystem).GetField("reelPower",flags);
        rodRootField=typeof(FishingSystem).GetField("rodRoot",flags);
    }

    private void Start()
    {
        if(progress==null || fishing==null || reelPowerField==null || rodRootField==null){enabled=false;return;}
        bool migrated=false;
        if(progress.Data.reelOwned>ShopCatalog.MaxReelTier){progress.Data.reelOwned=ShopCatalog.MaxReelTier;migrated=true;}
        if(progress.Data.reelEquipped>ShopCatalog.MaxReelTier){progress.Data.reelEquipped=ShopCatalog.MaxReelTier;migrated=true;}
        if(progress.Data.reelEquipped>progress.Data.reelOwned){progress.Data.reelEquipped=progress.Data.reelOwned;migrated=true;}
        if(migrated && !progress.ReadOnly)progress.Save();
        Apply();
    }

    private void LateUpdate()
    {
        if(progress==null || fishing==null)return;
        // FishingSystem's own shop-gear method predates the real two-reel catalog
        // and used 22% per placeholder tier. Override only this one multiplier.
        reelPowerField.SetValue(fishing,progress.Data.reelEquipped>=1?1.20f:1f);
        ApplyVisual(progress.Data.reelEquipped>=1?1:0);
    }

    private void Apply()
    {
        reelPowerField.SetValue(fishing,progress.Data.reelEquipped>=1?1.20f:1f);
        ApplyVisual(progress.Data.reelEquipped>=1?1:0);
    }

    private void ApplyVisual(int tier)
    {
        GameObject rodRoot=rodRootField.GetValue(fishing) as GameObject;
        if(rodRoot==null)return;
        if(appliedTier==tier)return;

        string resource=ShopCatalog.ReelPrefabResource(tier);
        GameObject sourcePrefab=Resources.Load<GameObject>(resource);
        if(sourcePrefab==null)
        {
            if(tier>0 && !warnedMissing)
            {
                warnedMissing=true;
                Debug.LogWarning("Level 2 reel model is not installed yet. Run Tools > Open World > Import Level 2 Fishing Reel (One Click). The starter visual is being used temporarily.");
            }
            return;
        }

        Transform sourceMount=FindDeepChild(sourcePrefab.transform,"ReelMount");
        Transform targetMount=FindDeepChild(rodRoot.transform,"ReelMount");
        if(sourceMount==null || targetMount==null)return;

        foreach(string partName in ReelParts)
        {
            Transform source=sourceMount.Find(partName);
            Transform target=targetMount.Find(partName);
            if(source==null || target==null)continue;
            MeshFilter sourceFilter=source.GetComponent<MeshFilter>();
            MeshFilter targetFilter=target.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer=source.GetComponent<MeshRenderer>();
            MeshRenderer targetRenderer=target.GetComponent<MeshRenderer>();
            if(sourceFilter!=null && targetFilter!=null)targetFilter.sharedMesh=sourceFilter.sharedMesh;
            if(sourceRenderer!=null && targetRenderer!=null)
            {
                targetRenderer.sharedMaterials=sourceRenderer.sharedMaterials;
                // Remove the old placeholder-tier gold tint. The authored texture
                // should appear exactly as imported for both real reels.
                targetRenderer.SetPropertyBlock(null);
            }
        }
        appliedTier=tier;warnedMissing=false;
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t!=null && t.name==name)return t;
        return null;
    }
}

/// <summary>Removes the retired placeholder reel rows and corrects the Level 2 description.</summary>
public sealed class ReelShopCleanup : MonoBehaviour
{
    private float nextScan;
    private void LateUpdate()
    {
        if(!ShopWorldHUD.MenuOpen || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+0.08f;
        Text[] texts=GetComponentsInChildren<Text>(true);
        foreach(Text text in texts)
        {
            if(text==null)continue;
            string value=text.text??string.Empty;
            if(value=="REMOVED REEL" || value=="Smooth Drag" || value=="Precision Drag" || value=="Deepwater Pro")
            {
                GameObject row=FindRow(text.transform);
                if(row!=null)row.SetActive(false);
                continue;
            }
            if(value.Contains("22% faster tiring and retrieval"))
                text.text=value.Replace("22% faster tiring and retrieval","20% faster reeling").Replace(" / Requires previous tier",string.Empty);
        }
    }

    private static GameObject FindRow(Transform start)
    {
        for(Transform t=start;t!=null;t=t.parent)
            if(t.GetComponent<LayoutElement>()!=null)return t.gameObject;
        return null;
    }
}
