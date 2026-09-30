using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Prepared FBX + texture: automatic import, no Blender install or ZIP picker needed.
[InitializeOnLoad]
public static class BlacktipSharkSetup
{
    private const string PrefabPath="Assets/Resources/Fishing/BlacktipShark.prefab";
    private static bool running;
    static BlacktipSharkSetup()
    {
        EditorApplication.delayCall+=InstallWhenReady;
        EditorApplication.playModeStateChanged+=OnPlayModeChanged;
    }
    private static void InstallWhenReady()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)
        {EditorApplication.delayCall+=InstallWhenReady;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{EnsureInstalled();}catch(Exception e){Debug.LogError("[BLACKTIP SHARK] "+e);}
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.ExitingEditMode)return;
        try{EnsureInstalled();}
        catch(Exception e){EditorApplication.isPlaying=false;Debug.LogError("[BLACKTIP SHARK] Setup must finish before Play: "+e);}
    }
    public static void EnsureInstalled()
    {
        if(running)return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(IsReady(prefab))return;
        Install();
    }
    private static bool IsReady(GameObject prefab)
    {
        if(prefab==null || prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        var animator=prefab.GetComponentInChildren<Animator>(true);
        return animator!=null && animator.runtimeAnimatorController!=null
            && prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="AuthoredMouthAnchor")
            && prefab.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null && m.GetTexture("_BaseMap")!=null));
    }
    [MenuItem("Tools/Open World/Install Blacktip Reef Shark")]
    public static void Install()
    {
        if(running)return;
        running=true;
        try
        {
            SeaBassImporter.InstallModel("BlacktipShark",PrefabPath);
            if(!IsReady(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)))
                throw new InvalidDataException("Shark model, swim animation, mouth anchor or texture is missing.");
            FishingTuning.Reload();
            if(!FishingTuning.IsValid)throw new InvalidDataException(FishingTuning.ValidationError);
            AssetDatabase.SaveAssets();
            Debug.Log("[BLACKTIP SHARK] Ready: shared aquarium swimming/held flopping; Brinebreak + Deep Ocean 2%, Bloody Bait test 55%.");
        }
        finally{running=false;}
    }
}

public sealed class BlacktipSharkBuildGate : IPreprocessBuildWithReport
{
    public int callbackOrder=>-900;
    public void OnPreprocessBuild(BuildReport report)
    {
        try{BlacktipSharkSetup.EnsureInstalled();}
        catch(Exception e){throw new BuildFailedException("Blacktip shark setup failed: "+e.Message);}
    }
}
