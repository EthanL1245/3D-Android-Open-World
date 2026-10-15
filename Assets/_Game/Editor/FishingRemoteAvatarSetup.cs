using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Creates a Resources reference to the source FBX and its embedded clips.
// The original FBX importer settings, textures, materials and clips are unchanged.
[InitializeOnLoad]
public sealed class FishingRemoteAvatarSetup : IPreprocessBuildWithReport
{
    internal const string SourcePath = "Assets/Manually Added Assets/Female Character Model.fbx";
    private const string ResourceFolder = "Assets/Resources/Multiplayer";
    private const string CatalogPath = ResourceFolder + "/FemaleRemoteAvatar.asset";

    static FishingRemoteAvatarSetup()
    {
        EditorApplication.delayCall += SetupOnLoad;
    }

    public int callbackOrder => -200;

    private static void SetupOnLoad()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            EnsureSetup(false);
    }

    [MenuItem("Tools/Open World/Rebuild Female Multiplayer Avatar")]
    public static void Rebuild()
    {
        if (!EnsureSetup(true))
            EditorUtility.DisplayDialog("Female Multiplayer Avatar",
                "The FBX or its walking clip could not be found. Verify the model's Animation tab and try again.", "OK");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!EnsureSetup(false))
            throw new BuildFailedException("Missing female multiplayer avatar or walk clip. Open Tools/Open World/Rebuild Female Multiplayer Avatar and check the imported FBX animation.");
    }

    internal static bool EnsureSetup(bool verbose)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (model == null) return false;

        AnimationClip walk = null;
        AnimationClip idle = null;
        AnimationClip fallback = null;
        UnityEngine.Object[] embedded = AssetDatabase.LoadAllAssetsAtPath(SourcePath);
        for (int i = 0; i < embedded.Length; i++)
        {
            AnimationClip clip = embedded[i] as AnimationClip;
            if (clip == null || clip.length < .05f || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                continue;

            if (clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 && idle == null)
                idle = clip;

            if ((clip.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 clip.name.IndexOf("walking", StringComparison.OrdinalIgnoreCase) >= 0) && walk == null)
                walk = clip;

            if (fallback == null && clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) < 0 &&
                clip.name.IndexOf("pose", StringComparison.OrdinalIgnoreCase) < 0)
                fallback = clip;
        }
        if (walk == null) walk = fallback; // FBX takes can have generic names, e.g. "Take 001".
        if (walk == null) return false;

        if (!AssetDatabase.IsValidFolder(ResourceFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Multiplayer");

        FishingRemoteAvatarAsset catalog = AssetDatabase.LoadAssetAtPath<FishingRemoteAvatarAsset>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<FishingRemoteAvatarAsset>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        bool changed = catalog.ModelPrefab != model || catalog.WalkClip != walk || catalog.IdleClip != idle;
        if (changed)
        {
            catalog.ModelPrefab = model;
            catalog.WalkClip = walk;
            catalog.IdleClip = idle;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        if (verbose || changed)
            Debug.Log("Multiplayer: female FBX connected with walk animation '" + walk.name +
                      "' and " + (idle != null ? "idle animation '" + idle.name + "'." : "walk start pose while idle.") +
                      " Existing FBX materials and animations are preserved.");
        return true;
    }
}

// Keep the generated build reference current when the user reimports the FBX.
public sealed class FishingRemoteAvatarImportWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (importedAssets[i] != FishingRemoteAvatarSetup.SourcePath) continue;
            EditorApplication.delayCall += () => FishingRemoteAvatarSetup.EnsureSetup(false);
            break;
        }
    }
}
