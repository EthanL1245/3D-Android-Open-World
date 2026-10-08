using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit ownership boundary for user-placed rocks and scenery. Runtime world
/// generation owns its own roots; only the user edits the objects under this root.
/// </summary>
[InitializeOnLoad]
public static class UserPlacedSceneryEditor
{
    private const string FishingScene = "Assets/Scenes/PrototypeWorld.unity";
    private const string ManifestPath = "Assets/_Game/EditableFishingMap/UserPlacedSceneryManifest.json";

    [Serializable]
    private sealed class SceneryEntry
    {
        public string name;
        public string sceneObjectId;
        public string prefabAssetPath;
        public string meshAssetPath;
        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 scale;
        public int childObjectCount;
    }

    [Serializable]
    private sealed class SceneryManifest
    {
        public string scene;
        public string ownershipRule;
        public int placedObjectCount;
        public List<SceneryEntry> objects = new List<SceneryEntry>();
    }

    static UserPlacedSceneryEditor()
    {
        EditorSceneManager.sceneSaved += OnSceneSaved;
    }

    private static void OnSceneSaved(Scene scene)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == FishingScene)
            ExportManifest(scene);
    }

    public static GameObject EnsureRoot(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Open the fishing scene before creating its manual scenery layer.");

        foreach (var root in scene.GetRootGameObjects())
        {
            var marker = root.GetComponent<UserPlacedScenery>();
            if (marker != null) return marker.gameObject;
        }

        // If a marker was mistakenly nested in procedural scenery, stop: moving
        // it without asking could change hand-authored scene hierarchy.
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<UserPlacedScenery>(true) != null)
                throw new InvalidOperationException("A USER PLACED SCENERY layer is nested under another object. Move that layer to the scene root to keep it safe from generators.");

        var created = new GameObject(UserPlacedScenery.RootName);
        if (created.scene != scene) SceneManager.MoveGameObjectToScene(created, scene);
        Undo.RegisterCreatedObjectUndo(created, "Create user-placed scenery layer");
        created.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        created.transform.localScale = Vector3.one;
        created.AddComponent<UserPlacedScenery>();
        EditorSceneManager.MarkSceneDirty(scene);
        return created;
    }

    [MenuItem("Tools/Open World/Select USER PLACED SCENERY Layer")]
    public static void SelectRoot()
    {
        if (!FishingMapSceneEditor.TryOpenFishingScene(out var scene)) return;
        if (!IsEditable(scene))
        {
            EditorUtility.DisplayDialog("Convert the map first",
                "Run Tools > Open World > Make Main Fishing Map Editable once, outside Play Mode. Then place your Blender rocks into the USER PLACED SCENERY layer.", "OK");
            return;
        }
        Selection.activeGameObject = EnsureRoot(scene);
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Open World/Protect Selected Scenery")]
    public static void ProtectSelected()
    {
        // Read selection before TryOpenFishingScene, since it may open another scene.
        Transform[] chosen = Selection.transforms;
        if (!FishingMapSceneEditor.TryOpenFishingScene(out var scene)) return;
        if (!IsEditable(scene))
        {
            EditorUtility.DisplayDialog("Convert the map first",
                "Run Tools > Open World > Make Main Fishing Map Editable first. Otherwise the generated terrain would move underneath your rocks at runtime.", "OK");
            return;
        }
        var root = EnsureRoot(scene).transform;
        var selectionSet = new HashSet<Transform>(chosen);
        var moved = chosen.Where(t => t != null && t.gameObject.scene == scene && t != root &&
            !UserPlacedScenery.Contains(t) && !HasSelectedAncestor(t, selectionSet)).ToArray();
        if (moved.Length == 0)
        {
            EditorUtility.DisplayDialog("No unprotected objects selected",
                "Select rocks or other scenery in the Hierarchy, then run Protect Selected Scenery.", "OK");
            return;
        }

        foreach (var target in moved)
            Undo.SetTransformParent(target, root, "Protect hand-placed scenery");
        Selection.objects = moved.Select(t => (UnityEngine.Object)t.gameObject).ToArray();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Protected " + moved.Length + " scene objects. Save PrototypeWorld with Ctrl+S. This layer is excluded from world generators.");
    }

    private static bool HasSelectedAncestor(Transform target, HashSet<Transform> chosen)
    {
        for (var parent = target.parent; parent != null; parent = parent.parent)
            if (chosen.Contains(parent)) return true;
        return false;
    }

    private static bool IsEditable(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var world = root.GetComponentInChildren<IslandExpansionWorld>(true);
            if (world != null && world.HasSavedLayout)
                return true;
        }
        return false;
    }

    [MenuItem("Tools/Open World/Export USER PLACED SCENERY Manifest")]
    public static void ExportNow()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != FishingScene)
        {
            EditorUtility.DisplayDialog("Open PrototypeWorld", "Open Assets/Scenes/PrototypeWorld.unity first.", "OK");
            return;
        }
        ExportManifest(scene);
    }

    /// <summary>
    /// A readable, version-controlled list of manual placements so future code
    /// changes can inspect positions without parsing Unity's giant scene YAML.
    /// Re-exported whenever the fishing scene is saved outside Play Mode.
    /// </summary>
    public static void ExportManifest(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.path != FishingScene) return;
        UserPlacedScenery marker = null;
        foreach (var obj in scene.GetRootGameObjects())
        {
            marker = obj.GetComponent<UserPlacedScenery>();
            if (marker != null) break;
        }
        if (marker == null) return;

        var snapshot = new SceneryManifest
        {
            scene = FishingScene,
            ownershipRule = "HUMAN-OWNED: never programmatically move, rescale, delete, replace, repaint, or reparent any descendant of USER PLACED SCENERY. Use the scene and this manifest as input for future world code.",
            placedObjectCount = marker.transform.childCount
        };
        foreach (Transform item in marker.transform)
        {
            var mesh = item.GetComponentInChildren<MeshFilter>(true);
            snapshot.objects.Add(new SceneryEntry
            {
                name = item.name,
                sceneObjectId = GlobalObjectId.GetGlobalObjectIdSlow(item.gameObject).ToString(),
                prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject) ?? "",
                meshAssetPath = mesh != null && mesh.sharedMesh != null ? AssetDatabase.GetAssetPath(mesh.sharedMesh) : "",
                position = item.position,
                rotationEuler = item.eulerAngles,
                scale = item.localScale,
                childObjectCount = item.GetComponentsInChildren<Transform>(true).Length
            });
        }

        string json = JsonUtility.ToJson(snapshot, true) + "\n";
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
        if (File.Exists(ManifestPath) && File.ReadAllText(ManifestPath) == json) return;
        File.WriteAllText(ManifestPath, json);
        AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate);
    }
}
