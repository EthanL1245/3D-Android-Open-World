using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FishingTuningValidator
{
    private const string Prefix="Assets/Resources/FishingTuning/";

    static FishingTuningValidator()
    {
        EditorApplication.delayCall+=ValidateAfterReload;
    }

    [MenuItem("Tools/Open World/Validate Fishing Tuning")]
    public static void ValidateFromMenu()
    {
        Validate(true);
    }

    public static void Validate(bool showDialog)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        FishingTuning.Reload();
        if(FishingTuning.IsValid)
        {
            const string message="Fishing tuning OK: species stats, biome percentile distributions, and every biome + bait probability row are valid.";
            Debug.Log("[FISH TUNING] "+message);
            if(showDialog)EditorUtility.DisplayDialog("Fishing Tuning Valid",message,"OK");
        }
        else if(showDialog)
        {
            EditorUtility.DisplayDialog("Fishing Tuning Error",FishingTuning.ValidationError+"\n\nThe game will fall back to legacy rules until the CSV is fixed.","OK");
        }
    }

    private static void ValidateAfterReload()
    {
        Validate(false);
    }

    public static bool IsTuningAsset(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class FishingTuningAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
    {
        if(ContainsTuning(imported) || ContainsTuning(deleted) || ContainsTuning(moved) || ContainsTuning(movedFrom))
            EditorApplication.delayCall+=ValidateAfterAssetChange;
    }

    private static void ValidateAfterAssetChange()
    {
        FishingTuningValidator.Validate(false);
    }

    private static bool ContainsTuning(string[] paths)
    {
        if(paths==null)return false;
        for(int i=0;i<paths.Length;i++)if(FishingTuningValidator.IsTuningAsset(paths[i]))return true;
        return false;
    }
}