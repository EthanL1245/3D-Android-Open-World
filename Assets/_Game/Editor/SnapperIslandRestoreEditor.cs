using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time migration for locally saved maps from the retired rock redesign.</summary>
[InitializeOnLoad]
public static class SnapperIslandRestoreEditor
{
    private static bool repairing;
    static SnapperIslandRestoreEditor()
    {
        EditorApplication.delayCall+=TryAutomatic;
        EditorSceneManager.sceneOpened+=(scene,mode)=>EditorApplication.delayCall+=TryAutomatic;
    }

    private static T Find<T>(Scene scene) where T:Component
    {
        foreach(var root in scene.GetRootGameObjects())
        {
            var value=root.GetComponentInChildren<T>(true);
            if(value!=null)return value;
        }
        return null;
    }

    private static void TryAutomatic()
    {
        if(repairing || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            PrefabStageUtility.GetCurrentPrefabStage()!=null || SceneManager.sceneCount!=1)return;
        Scene scene=SceneManager.GetActiveScene();
        if(!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || EditorSceneManager.IsPreviewScene(scene))return;
        var snapper=Find<SnapperIslandRuntime>(scene);
        var world=Find<IslandExpansionWorld>(scene);
        if(world!=null && world.GetComponent<NonSnapperTerrainSurfaceState>()?.Restored==true)return;
        if(snapper==null || world==null || !snapper.HasSavedLayout || !world.HasSavedLayout || !snapper.NeedsSimpleIslandRepair)return;
        try { Repair(scene,world,snapper); }
        catch(Exception e) { Debug.LogError("Island restoration did not finish. Use Tools > Open World > Restore Simple Snapper and Terrain Textures. "+e); }
    }

    [MenuItem("Tools/Open World/Restore Simple Snapper and Terrain Textures")]
    public static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        if(!FishingMapSceneEditor.TryOpenFishingScene(out var scene))return;
        var world=Find<IslandExpansionWorld>(scene);
        var snapper=Find<SnapperIslandRuntime>(scene);
        if(world!=null && world.GetComponent<NonSnapperTerrainSurfaceState>()?.Restored==true)
        {
            EditorUtility.DisplayDialog("Restored surface layers are protected",
                "This legacy repair repaints all islands, including Snapper, and would overwrite your saved edits. It is disabled on maps repaired by Restore Grass and Rock (Except Snapper).", "OK");
            return;
        }
        if(world==null || snapper==null || !world.HasSavedLayout || !snapper.HasSavedLayout)
        {
            FishingMapSceneEditor.MakeEditable();
            scene=SceneManager.GetActiveScene();
            world=Find<IslandExpansionWorld>(scene);
            snapper=Find<SnapperIslandRuntime>(scene);
        }
        if(world==null || snapper==null || !world.HasSavedLayout || !snapper.HasSavedLayout)
            throw new InvalidOperationException("The main fishing map must finish conversion before restoration.");
        Repair(scene,world,snapper);
    }

    private static void Repair(Scene scene,IslandExpansionWorld world,SnapperIslandRuntime snapper)
    {
        if(repairing)return;
        repairing=true;
        string folder="Assets/_Game/EditableFishingMap/Restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        try
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            if(!EditorSceneManager.SaveScene(scene,folder+"/BeforeRestore.unity",true))
                throw new IOException("Could not back up the fishing scene; nothing was changed.");
            EditorUtility.DisplayProgressBar("Restore islands","Restoring Snapper and terrain surfaces...",.4f);
            world.Build(); // Saved maps only bind references; they never regenerate.
            if(!world.Ready)throw new InvalidOperationException("Saved terrain references could not be restored.");
            if(snapper.NeedsSimpleIslandRepair)snapper.RestoreSimpleIsland(true);
            else
            {
                // Explicit menu reruns may restore paint, but never resculpt an
                // already migrated island or reset the user's new object edits.
                var terrain=world.Terrain;
                terrain.terrainData=UnityEngine.Object.Instantiate(terrain.terrainData);
                var collider=terrain.GetComponent<TerrainCollider>();
                if(collider!=null)collider.terrainData=terrain.terrainData;
                IslandTerrainSurfaceRepair.RestoreWorldPaint(terrain,Find<ReefZone>(scene),world,world.Water.BaseWaterLevel);
                IslandTerrainSurfaceRepair.PaintSnapper(terrain,SnapperIslandGeometry.Center(Find<ReefZone>(scene)));
                AuthoredTerrainSurfaceTextures.ApplyToTerrain(terrain);
            }
            FishingMapSceneEditor.PersistGeneratedAssets(scene,folder);
            EditorUtility.SetDirty(snapper);
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the repaired scene.");
            Debug.Log("Restored simple Snapper without rocks, plus grass/stone/sand terrain paint. Other island heights and scenery were preserved. Backup: "+folder+"/BeforeRestore.unity");
            SceneView.RepaintAll();
        }
        catch
        {
            Debug.LogError("Restoration failed. Reopen "+folder+"/BeforeRestore.unity to recover the pre-repair scene before retrying.");
            throw;
        }
        finally { EditorUtility.ClearProgressBar();repairing=false; }
    }
}
