using UnityEditor;
using UnityEngine;

/// <summary>Switches only the two authored ocean materials for matched A/B builds.</summary>
public static class OceanWaterLookTools
{
    private const string Keyword = "_WATER_REFERENCE_LOOK";
    private static readonly string[] MaterialPaths =
    {
        "Assets/_Game/Water/Materials/OceanWater.mat",
        "Assets/_Game/Reef/Generated/SuncrestWater.mat"
    };

    [MenuItem("Tools/Open World/Water Look/Use Improved Look")]
    private static void UseImprovedLook() => SetLook(true);

    [MenuItem("Tools/Open World/Water Look/Use Original Look (Performance Baseline)")]
    private static void UseOriginalLook() => SetLook(false);

    private static void SetLook(bool improved)
    {
        int changed = 0;
        foreach (string path in MaterialPaths)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.HasProperty("_ReferenceLook"))
            {
                Debug.LogWarning("Ocean look material missing or shader not imported: " + path);
                continue;
            }

            Undo.RecordObject(material, "Change Ocean Water Look");
            material.SetFloat("_ReferenceLook", improved ? 1f : 0f);
            if (improved) material.EnableKeyword(Keyword);
            else material.DisableKeyword(Keyword);
            EditorUtility.SetDirty(material);
            changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Ocean water: {(improved ? "improved" : "original baseline")} look on {changed} materials. " +
                  "Compare matched builds on your Android device; the FPS budget requires measured results.");
    }
}
