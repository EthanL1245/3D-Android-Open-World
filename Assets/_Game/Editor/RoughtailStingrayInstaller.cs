using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The rig is already exported and rolled onto its pale side. No Blender install
// is required after git pull; use the shared importer for every presentation path.
[InitializeOnLoad]
public static class RoughtailStingrayInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/RoughtailStingray.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static RoughtailStingrayInstaller()
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
        catch (Exception e) { Debug.LogError("[ROUGHTAIL STINGRAY] Import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<RedSnapperPresentation>() != null)
        {
            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip != null && clip.name == CaughtFishFlop.HeldStruggleStateName) return;
        }
        Rebuild();
    }

    [MenuItem("Tools/Open World/Rebuild Roughtail Stingray")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/RoughtailStingray.fbx") || !File.Exists(Source + "/RoughtailStingrayTexture.png"))
                throw new InvalidOperationException("Roughtail Stingray source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("RoughtailStingray", PrefabPath, Source, preserveWorldUp:true, importHeldStruggle:true);
            Debug.Log("[ROUGHTAIL STINGRAY] Textured ray installed with swimming and player-held struggle animations.");
        }
        finally { installing = false; }
    }
}

public sealed class RoughtailStingrayBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => RoughtailStingrayInstaller.EnsureInstalled();
}
