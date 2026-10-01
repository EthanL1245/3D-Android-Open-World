using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Prepared exports use the same authored-fish pipeline as the existing species.
[InitializeOnLoad]
public static class SnapperUpdateSetup
{
    private const string Stamp="Library/SnapperUpdate-20261001-v1.txt";
    private const string Red="Assets/Resources/Fishing/RedSnapper.prefab";
    private const string Yellow="Assets/Resources/Fishing/YellowtailSnapper.prefab";
    private static readonly string[] Dependencies={
        "Assets/_Game/Fishing/RedSnapper/Source/RedSnapper.fbx",
        "Assets/_Game/Fishing/RedSnapper/Source/RedSnapper_Texture.png",
        "Assets/_Game/Reef/Source/YellowtailSnapper.fbx",
        "Assets/_Game/Reef/Source/YellowtailSnapperTexture.png",Red,Yellow
    };
    private static bool running;
    static SnapperUpdateSetup()
    {
        EditorApplication.delayCall+=InstallWhenReady;
        EditorApplication.playModeStateChanged+=OnPlayModeChanged;
    }
    private static void InstallWhenReady()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)
        {EditorApplication.delayCall+=InstallWhenReady;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}catch(Exception e){Debug.LogError("[SNAPPER UPDATE] "+e);}
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.ExitingEditMode)return;
        try{EnsureInstalled();}
        catch(Exception e){EditorApplication.isPlaying=false;Debug.LogError("[SNAPPER UPDATE] Setup must finish before Play: "+e);}
    }
    private static string Signature()=>string.Join("\n",Dependencies.Select(p=>p+":"+AssetDatabase.GetAssetDependencyHash(p)));
    public static void EnsureInstalled()
    {
        if(running)return;
        if(File.Exists(Stamp) && IsReady(Red) && IsReady(Yellow) && File.ReadAllText(Stamp)==Signature())return;
        Install();
    }
    private static bool IsReady(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(prefab==null || prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        var animator=prefab.GetComponentInChildren<Animator>(true);
        var transforms=prefab.GetComponentsInChildren<Transform>(true);
        var renderers=prefab.GetComponentsInChildren<Renderer>(true);
        return animator!=null && animator.runtimeAnimatorController!=null
            && animator.runtimeAnimatorController.animationClips.Any(c=>c!=null && c.name=="Swim" && c.length>0)
            && new[]{"Bone","Bone.001","Bone.002","Bone.003","Bone.004","AuthoredMouthAnchor"}.All(n=>transforms.Any(t=>t.name==n))
            && renderers.Length>0 && renderers.All(r=>r.sharedMaterials.All(m=>m!=null && m.GetTexture("_BaseMap")!=null));
    }
    [MenuItem("Tools/Open World/Install Updated Snappers")]
    public static void Install()
    {
        if(running)return;
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before updating fish assets.");
        running=true;
        try
        {
            RedSnapperImporter.InstallPrepared();
            SeaBassImporter.InstallModel("YellowtailSnapper",Yellow);
            if(!IsReady(Red) || !IsReady(Yellow))
                throw new InvalidDataException("Snapper texture, swim clip, five-bone presentation or mouth anchor is missing.");
            FishingTuning.Reload();
            if(!FishingTuning.IsValid)throw new InvalidDataException(FishingTuning.ValidationError);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Stamp,Signature());
            Debug.Log("[SNAPPER UPDATE] Fixed Red Snapper and starter-island Yellowtail Snapper ready with shared trail swimming and held flops.");
        }
        finally{running=false;}
    }
}

public sealed class SnapperUpdateBuildGate : IPreprocessBuildWithReport
{
    public int callbackOrder=>-800;
    public void OnPreprocessBuild(BuildReport report)
    {
        try{SnapperUpdateSetup.EnsureInstalled();}
        catch(Exception e){throw new BuildFailedException("Snapper setup failed: "+e.Message);}
    }
}
