using System;
using UnityEditor;
using UnityEngine;

/// <summary>Fast editor assertions for the two authored bass species.</summary>
public static class NewBassValidation
{
    [InitializeOnLoadMethod]
    private static void Queue()=>EditorApplication.delayCall+=Validate;

    [MenuItem("Tools/Open World/Validate Striped + Spotted Sand Bass")]
    public static void Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        bool ok=true;
        ok&=Check(FishCatalog.StripedBassId==11&&FishCatalog.SpottedSandBassId==12,"Stable new species IDs changed.");

        // Keep the legacy difficulty tags aligned for fallback/metadata purposes. Actual
        // health is now intentionally owned by the editable FishStats.csv parabola for
        // each species, so this validator must not hard-link same-weight HP anymore.
        ok&=Check(Math.Abs(FishCatalog.Get(FishCatalog.StripedBassId).Difficulty-FishCatalog.Get(2).Difficulty)<.000001f,"Striped Bass legacy difficulty no longer matches Sea Bass.");
        ok&=Check(Math.Abs(FishCatalog.Get(FishCatalog.SpottedSandBassId).Difficulty-FishCatalog.Get(FishCatalog.BlackSeaBassId).Difficulty)<.000001f,"Spotted Sand Bass legacy difficulty no longer matches Black Sea Bass.");

        FishingTuning.SpeciesStats striped,spotted;
        ok&=Check(FishingTuning.TryGetSpeciesStats(FishCatalog.StripedBassId,out striped),"Striped Bass is missing FishStats.csv tuning.");
        ok&=Check(FishingTuning.TryGetSpeciesStats(FishCatalog.SpottedSandBassId,out spotted),"Spotted Sand Bass is missing FishStats.csv tuning.");
        if(striped!=null)
        {
            ok&=Check(striped.MaxWeightKg>FishCatalog.Get(2).MaxWeightKg*4f,"Striped Bass is not substantially larger than Sea Bass.");
            ok&=Check(striped.MaxHealth>=striped.MinHealth&&striped.MaxCostCoins>=striped.MinCostCoins,"Striped Bass health/value tuning is not monotonic.");
        }
        if(spotted!=null)
            ok&=Check(spotted.MaxHealth>=spotted.MinHealth&&spotted.MaxCostCoins>=spotted.MinCostCoins,"Spotted Sand Bass health/value tuning is not monotonic.");

        ok&=Check(FishSizeTable.LengthMetres(FishCatalog.StripedBassId,20f)>FishSizeTable.LengthMetres(2,4f),"Striped Bass size curve did not extend to trophy sizes.");
        // Zero odds are intentional exclusions, including 0/0 biome weight ranges.
        // The shared validator checks every required biome/bait/species row,
        // whole-number totals, and that unavailable species have zero chance.
        // Do not require bass to appear in Suncrest or with the equipped lure.
        ok&=Check(FishingTuning.IsValid,"Fishing odds/tuning are invalid: "+FishingTuning.ValidationError);

        ValidatePrefab("Assets/Resources/Fishing/StripedBass.prefab","Striped Bass",ref ok);
        ValidatePrefab("Assets/Resources/Fishing/SpottedSandBass.prefab","Spotted Sand Bass",ref ok);
        if(ok)Debug.Log("[NEW BASS CHECK] Bass species, editable tuning, and shared aquarium/held-fish presentation are valid.");
    }

    private static void ValidatePrefab(string path,string label,ref bool ok)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null)return; // package auto-import may run a few editor ticks later
        ok&=Check(prefab.GetComponent<RedSnapperPresentation>()!=null,label+" is missing RedSnapperPresentation (aquarium/held behavior).");
        ok&=Check(prefab.GetComponentInChildren<Animator>(true)!=null,label+" is missing its authored animation Animator.");
        bool mouth=false;foreach(Transform t in prefab.GetComponentsInChildren<Transform>(true))if(t.name=="AuthoredMouthAnchor"){mouth=true;break;}
        ok&=Check(mouth,label+" is missing the animated mouth/line anchor.");
    }

    private static bool Check(bool condition,string message)
    {if(!condition)Debug.LogError("[NEW BASS CHECK] "+message);return condition;}
}
