using System;
using UnityEditor;
using UnityEngine;

/// <summary>Fast editor assertions for the two newly supplied bass species.</summary>
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
        ok&=Check(Math.Abs(FishCatalog.Get(FishCatalog.StripedBassId).Difficulty-FishCatalog.Get(2).Difficulty)<.000001f,"Striped Bass difficulty no longer matches Sea Bass.");
        ok&=Check(Math.Abs(FishCatalog.Get(FishCatalog.SpottedSandBassId).Difficulty-FishCatalog.Get(FishCatalog.BlackSeaBassId).Difficulty)<.000001f,"Spotted Sand Bass difficulty no longer matches Black Sea Bass.");
        foreach(float kg in new[]{1f,2f,4f})
            ok&=Check(FishingRules.MaxHealth(FishCatalog.StripedBassId,kg)==FishingRules.MaxHealth(2,kg),"Striped Bass same-weight HP differs from Sea Bass at "+kg+" kg.");
        foreach(float kg in new[]{.5f,1f,1.8f})
            ok&=Check(FishingRules.MaxHealth(FishCatalog.SpottedSandBassId,kg)==FishingRules.MaxHealth(FishCatalog.BlackSeaBassId,kg),"Spotted Sand Bass same-weight HP differs from Black Sea Bass at "+kg+" kg.");
        ok&=Check(FishCatalog.Get(FishCatalog.StripedBassId).MaxWeightKg>FishCatalog.Get(2).MaxWeightKg*4f,"Striped Bass is not substantially larger than Sea Bass.");
        ok&=Check(FishSizeTable.LengthMetres(FishCatalog.StripedBassId,20f)>FishSizeTable.LengthMetres(2,4f),"Striped Bass size curve did not extend to trophy sizes.");
        ok&=Check(ReefCatalog.EquippedChance(FishCatalog.StripedBassId,ShopCatalog.StarterLure)>0f&&ReefCatalog.EquippedChance(FishCatalog.SpottedSandBassId,ShopCatalog.StarterLure)>0f,"New bass are missing from fishing odds.");

        ValidatePrefab("Assets/Resources/Fishing/StripedBass.prefab","Striped Bass",ref ok);
        ValidatePrefab("Assets/Resources/Fishing/SpottedSandBass.prefab","Spotted Sand Bass",ref ok);
        if(ok)Debug.Log("[NEW BASS CHECK] Gameplay tuning is consistent. Any installed prefabs use the shared aquarium/held-fish presentation contract.");
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
