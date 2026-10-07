using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time conversion of runtime island geometry into ordinary scene objects.</summary>
public static class FishingMapSceneEditor
{
    private const string Root = "Assets/_Game/EditableFishingMap";
    private const string Menu = "Tools/Open World/Make Main Fishing Map Editable";

    [MenuItem(Menu)]
    public static void MakeEditable()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before preparing the editable map.");
        var scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path) || EditorSceneManager.loadedSceneCount != 1 ||
            Find<FirstPersonController>(scene) == null || Find<OceanWater>(scene) == null)
            throw new InvalidOperationException("Open only the saved main fishing scene (PrototypeWorld) first.");

        var world = Find<IslandExpansionWorld>(scene);
        var snapper = Find<SnapperIslandRuntime>(scene);
        if (world != null && world.HasSavedLayout && snapper != null && snapper.HasSavedLayout)
        {
            Selection.activeGameObject = world.gameObject;
            Debug.Log("This map is already editable. Move its objects or sculpt its terrain outside Play Mode, then save the scene. No objects were regenerated.");
            return;
        }

        // Preserve the user's current scene, including unsaved placement edits.
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        string folder = Root + "/Map-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        AssetDatabase.CreateFolder(Root, Path.GetFileName(folder));
        string backup = folder + "/BeforeConversion.unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true))
            throw new IOException("Could not save the pre-conversion scene backup.");

        try
        {
            if (Find<ReefZone>(scene) == null) SuncrestReefSetup.Install();
            if (Find<ReefZone>(scene) == null)
                throw new InvalidOperationException("Suncrest installation did not complete. The map was not converted.");
            EditorUtility.DisplayProgressBar("Editable fishing map", "Generating island terrain and scenery...", .2f);
            world = Find<IslandExpansionWorld>(scene);
            if (world == null) world = new GameObject("Island Expansion").AddComponent<IslandExpansionWorld>();
            world.Build();
            if (!world.Ready) throw new InvalidOperationException("Island expansion did not complete.");

            snapper = Find<SnapperIslandRuntime>(scene);
            if (snapper == null) snapper = new GameObject("Snapper Island Runtime").AddComponent<SnapperIslandRuntime>();
            if (!snapper.HasSavedLayout)
            {
                // Never sculpt the source terrain asset, including when adding an island
                // to an expansion that has already been saved for editing.
                if (world.HasSavedLayout)
                {
                    var data = Object.Instantiate(world.Terrain.terrainData);
                    data.name = "Editable Main Fishing Terrain";
                    world.Terrain.terrainData = data;
                    var collider = world.Terrain.GetComponent<TerrainCollider>();
                    if (collider != null) collider.terrainData = data;
                }
                snapper.Build();
                if (!SnapperIslandRuntime.Ready || snapper.transform.Find("Snapper Coastal Rockscape") == null)
                    throw new InvalidOperationException("Snapper Island or its six rock assets did not finish loading.");
            }

            EditorUtility.DisplayProgressBar("Editable fishing map", "Saving terrain, materials and generated assets...", .8f);
            PersistGeneratedAssets(scene, folder);
            world.SaveLayoutForEditing();
            snapper.SaveLayoutForEditing();
            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(snapper);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the editable scene.");
            Physics.SyncTransforms();
            Selection.activeGameObject = snapper.gameObject;
            SceneView.RepaintAll();
            Debug.Log("Main fishing map saved for Scene editing. Move/rotate/scale rocks, edit terrain, then Ctrl+S outside Play Mode. Play and Android builds use the saved layout. Running this command again preserves existing edits. Backup: " + backup);
        }
        catch (Exception error)
        {
            Debug.LogError("Map conversion did not finish. Do not save the partial conversion. Reopen the backup at " + backup + " (and Save As your original scene path) before retrying. " + error);
            throw;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var result = root.GetComponentInChildren<T>();
            if (result != null) return result;
        }
        return null;
    }

    private static void PersistGeneratedAssets(Scene scene, string folder)
    {
        Object[] roots = scene.GetRootGameObjects().Cast<Object>().ToArray();
        // Terrain owns native height/alpha textures; save it first so Unity can
        // persist those internally instead of detaching them into loose assets.
        foreach (var terrain in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
            SaveAsset(terrain.terrainData, folder);
        foreach (var dependency in EditorUtility.CollectDependencies(roots))
        {
            if (dependency is Material || dependency is Texture2D || dependency is Mesh || dependency is TerrainLayer)
                SaveAsset(dependency, folder);
        }
        // All references still point to the same instances, now persistent assets.
        foreach (var dependency in EditorUtility.CollectDependencies(roots))
        {
            if ((dependency is TerrainData || dependency is TerrainLayer || dependency is Material || dependency is Mesh || dependency is Texture2D)
                && !EditorUtility.IsPersistent(dependency))
                throw new InvalidOperationException("Generated map asset was not saved: " + dependency.name);
        }
    }

    private static void SaveAsset(Object asset, string folder)
    {
        if (asset == null || EditorUtility.IsPersistent(asset)) return;
        string name = new string((asset.name ?? asset.GetType().Name)
            .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        if (string.IsNullOrEmpty(name)) name = asset.GetType().Name;
        asset.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset"));
        EditorUtility.SetDirty(asset);
    }
}

[CustomEditor(typeof(IslandExpansionWorld))]
public sealed class IslandExpansionWorldInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var world = (IslandExpansionWorld)target;
        EditorGUILayout.HelpBox(world.HasSavedLayout
            ? "Saved map: scene placements and terrain edits are used in Play and builds. Save edits outside Play Mode. Generation remains available for adding future islands without rebuilding this layout."
            : "This map currently generates at runtime. Prepare it once to edit the islands in Scene view.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Make Main Fishing Map Editable")) FishingMapSceneEditor.MakeEditable();
    }
}
