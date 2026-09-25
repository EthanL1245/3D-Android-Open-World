using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Converts distance beyond Suncrest Reef into the open-ocean size bonus used by
/// FishingRules. The reef/coastal distribution is untouched; the bonus begins only
/// after the player has travelled beyond the authored reef and becomes very strong
/// far offshore.
/// </summary>
[DefaultExecutionOrder(-240)]
public sealed class OffshoreFishingRuntime : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ShopProgress progress=FindFirstObjectByType<ShopProgress>();
        if(progress!=null)
            ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);

        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<OffshoreFishingRuntime>()==null)
                system.gameObject.AddComponent<OffshoreFishingRuntime>();

        foreach(ShopWorldHUD hud in FindObjectsByType<ShopWorldHUD>(FindObjectsSortMode.None))
            if(hud!=null && hud.GetComponent<OffshoreFishingHudRuntime>()==null)
                hud.gameObject.AddComponent<OffshoreFishingHudRuntime>();
    }

    private void Update()
    {
        ReefZone zone=ReefZone.Active;
        if(zone==null)
        {
            FishingRules.OffshoreFactor=0f;
            return;
        }

        float beyondReef=zone.OpenOceanDistance(transform.position);
        // The first tens of metres beyond the reef stay close to the existing
        // experience. From 35 m to 240 m beyond it, the giant-fish bias ramps in.
        FishingRules.OffshoreFactor=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(35f,240f,beyondReef));
    }

    private void OnDisable(){FishingRules.OffshoreFactor=0f;}
    private void OnDestroy(){FishingRules.OffshoreFactor=0f;}
}

/// <summary>
/// Keeps the Fish Index honest about the much larger open-ocean size ceiling.
/// </summary>
[DefaultExecutionOrder(1250)]
public sealed class OffshoreFishingHudRuntime : MonoBehaviour
{
    private ShopWorldHUD hud;
    private float nextIndexScan;

    private void Awake(){hud=GetComponent<ShopWorldHUD>();}

    private void LateUpdate()
    {
        if(hud==null || !ShopWorldHUD.MenuOpen || Time.unscaledTime<nextIndexScan)return;
        nextIndexScan=Time.unscaledTime+.12f;
        RepairFishIndex();
    }

    private void RepairFishIndex()
    {
        if(!HasHeading("SUNCREST REEF • FISH INDEX"))return;

        foreach(int id in FishCatalog.ActiveIds)
        {
            string speciesName=FishCatalog.Get(id).Name;
            GameObject row=FindSpeciesRow(speciesName);
            if(row==null)continue;

            foreach(Text text in row.GetComponentsInChildren<Text>(true))
            {
                if(text==null || string.IsNullOrEmpty(text.text) || !text.text.Contains("Ocean:") || !text.text.Contains("Caught:"))continue;
                if(text.text.Contains("Open ocean:"))continue;

                float offshoreMax=FishingRules.OffshoreMaximumWeight(id);
                string value=text.text.Replace("Ocean:","Reef / coastal:");
                string extra="\nOpen ocean: up to "+FishCatalog.FormatWeight(offshoreMax)+
                             "\nOpen-ocean length: up to "+ShopCatalog.FishLength(id,offshoreMax).ToString("0.00")+" m";
                int caught=value.IndexOf("\nCaught:",StringComparison.Ordinal);
                value=caught>=0?value.Insert(caught,extra):value+extra;
                text.text=value;
                break;
            }
        }
    }

    private bool HasHeading(string caption)
    {
        foreach(Text text in hud.GetComponentsInChildren<Text>(true))
            if(text!=null && string.Equals(text.text,caption,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }

    private GameObject FindSpeciesRow(string species)
    {
        foreach(LayoutElement layout in hud.GetComponentsInChildren<LayoutElement>(true))
        {
            if(layout==null)continue;
            foreach(Text text in layout.GetComponentsInChildren<Text>(true))
                if(text!=null && string.Equals(text.text,species,StringComparison.OrdinalIgnoreCase))return layout.gameObject;
        }
        return null;
    }
}
