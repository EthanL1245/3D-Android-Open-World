using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The rig is already exported and rolled onto its pale side. No Blender install
// is required after git pull; use the shared importer for every presentation path.
[InitializeOnLoad]
public static class WinterFlounderInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/WinterFlounder.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static WinterFlounderInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { EnsureInstalled(); }
            catch (Exception e) { EditorApplication.isPlaying = false; Debug.LogException(e); }
        };
    }

    private static void AutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += AutoInstall; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { EnsureInstalled(); }
        catch (Exception e) { Debug.LogError("[WINTER FLOUNDER] Import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<RedSnapperPresentation>() != null) return;
        Rebuild();
    }

    [MenuItem("Tools/Open World/Rebuild Winter Flounder")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/WinterFlounder.fbx") || !File.Exists(Source + "/WinterFlounderTexture.png"))
                throw new InvalidOperationException("Winter Flounder source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("WinterFlounder", PrefabPath, Source, preserveWorldUp:true);
            Debug.Log("[WINTER FLOUNDER] Textured side-swimming fish installed with shared animation, mouth anchor and held-fish presentation.");
        }
        finally { installing = false; }
    }
}

public sealed class WinterFlounderBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => WinterFlounderInstaller.EnsureInstalled();
}
