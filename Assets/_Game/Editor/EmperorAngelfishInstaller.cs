using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Both supplied FBX rigs contain a single authored swim take. Held motion uses the shared controller.
[InitializeOnLoad]
public static class EmperorAngelfishInstaller
{
    private const string PrefabPath = "Assets/Resources/Fishing/EmperorAngelfish.prefab";
    private const string Source = "Assets/_Game/Reef/Source";
    private static bool installing;

    static EmperorAngelfishInstaller()
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
        catch (Exception e) { Debug.LogError("[EMPEROR ANGELFISH] Import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        if (Ready(PrefabPath) && Ready("Assets/Resources/Fishing/JuvenileEmperorAngelfish.prefab")) return;
        Rebuild();
    }

    private static bool Ready(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null || prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        var animator=prefab.GetComponentInChildren<Animator>(true);
        return animator!=null && animator.runtimeAnimatorController!=null &&
            animator.runtimeAnimatorController.animationClips.Any(c=>c!=null && c.name=="Swim");
    }

    [MenuItem("Tools/Open World/Rebuild Emperor Angelfish")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!File.Exists(Source + "/EmperorAngelfish.fbx") || !File.Exists(Source + "/EmperorAngelfishTexture.png") ||
                !File.Exists(Source + "/JuvenileEmperorAngelfish.fbx") || !File.Exists(Source + "/JuvenileEmperorAngelfishTexture.png"))
                throw new InvalidOperationException("Emperor Angelfish source assets are missing. Pull the complete update.");
            SeaBassImporter.InstallModel("EmperorAngelfish", PrefabPath, Source);
            SeaBassImporter.InstallModel("JuvenileEmperorAngelfish", "Assets/Resources/Fishing/JuvenileEmperorAngelfish.prefab", Source);
            Debug.Log("[EMPEROR ANGELFISH] Adult and juvenile fish installed with shared animation, mouth anchor and held-fish presentation.");
        }
        finally { installing = false; }
    }
}

public sealed class EmperorAngelfishBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => EmperorAngelfishInstaller.EnsureInstalled();
}
