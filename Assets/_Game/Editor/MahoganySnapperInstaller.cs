using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Uses the same authored FBX/animation/texture pipeline as other upright snappers.
[InitializeOnLoad]
public static class MahoganySnapperInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/MahoganySnapper.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing, warned;

    static MahoganySnapperInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) EnsureInstalled();
        };
    }

    private static void AutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += AutoInstall; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { EnsureInstalled(); }
        catch (Exception ex) { Debug.LogError("[MAHOGANY SNAPPER] Import failed: " + ex); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<RedSnapperPresentation>() != null) return;
        if (!File.Exists(Source + "/MahoganySnapper.fbx") ||
            !File.Exists(Source + "/MahoganySnapperTexture.png"))
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[MAHOGANY SNAPPER] Authored animated FBX/texture missing. " +
                    "Fishing is enabled but the authored prefab cannot be generated yet.");
            }
            return;
        }
        Rebuild();
    }

    [MenuItem("Tools/Open World/Rebuild Mahogany Snapper")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/MahoganySnapper.fbx") ||
                !File.Exists(Source + "/MahoganySnapperTexture.png"))
                throw new InvalidOperationException("MahoganySnapper.fbx or MahoganySnapperTexture.png missing from " + Source);
            SeaBassImporter.InstallModel("MahoganySnapper", PrefabPath, Source);
            Debug.Log("[MAHOGANY SNAPPER] Shared animation, atlas, mouth anchor and held-fish prefab installed.");
        }
        finally { installing = false; }
    }
}

public sealed class MahoganySnapperBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => MahoganySnapperInstaller.EnsureInstalled();
}
