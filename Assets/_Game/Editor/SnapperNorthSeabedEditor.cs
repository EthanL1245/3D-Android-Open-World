using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Applies the localized Snapper bathymetry update to an already-baked, editable
/// fishing map. Saves a genuine independent terrain/scene backup first.
/// Hand-placed objects are not moved, destroyed, reparented or replaced.
/// </summary>
public static class SnapperNorthSeabedEditor
{
    [MenuItem("Tools/Open World/Snapper Island/Match Underwater Depth to North Side")]
    public static void ApplyToEditableMap()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Exit Play Mode", "Edit the fishing scene outside Play Mode.", "OK");
            return;
        }
        if (!FishingMapSceneEditor.TryOpenFishingScene(out Scene scene)) return;

        var terrain = Find<Terrain>(scene);
        var reef = Find<ReefZone>(scene);
        var water = Find<OceanWater>(scene);
        var world = Find<IslandExpansionWorld>(scene);
        var snapper = Find<SnapperIslandRuntime>(scene);

        if (terrain == null || reef == null || water == null || world == null || snapper == null ||
            !world.HasSavedLayout || !snapper.HasSavedLayout)
        {
            EditorUtility.DisplayDialog("Editable map required",
                "Run Tools > Open World > Make Main Fishing Map Editable first. It saves the complete scene before bathymetry can be edited safely.", "OK");
            return;
        }

        Vector3 center = SnapperIslandGeometry.Center(reef);
        if (snapper.HasUniformSeabed)
        {
            int additional = SnapperRockCollisionEditor.AddNearbyRockColliders(scene, center);
            if (additional > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[SNAPPER ISLAND] Uniform seabed already saved. No terrain edited. " + additional + " additional mesh colliders added.");
            return;
        }

        var original = terrain.terrainData;
        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (original == null) throw new InvalidOperationException("The fishing terrain has no TerrainData.");

        const string parentFolder = "Assets/_Game/EditableFishingMap";
        Directory.CreateDirectory(parentFolder);
        AssetDatabase.Refresh();
        string folder = parentFolder + "/SnapperSeabed-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        AssetDatabase.CreateFolder(parentFolder, Path.GetFileName(folder));

        // A scene copy alone is NOT a valid terrain backup: scenes normally
        // share TerrainData assets. Give the backup a separate frozen copy.
        var frozen = Object.Instantiate(original);
        frozen.name = "Snapper seabed pre-edit terrain backup";
        AssetDatabase.CreateAsset(frozen, folder + "/TerrainBeforeSnapperChange.asset");
        try
        {
            terrain.terrainData = frozen;
            if (terrainCollider != null) terrainCollider.terrainData = frozen;
            if (!EditorSceneManager.SaveScene(scene, folder + "/SceneBeforeSnapperChange.unity", true))
                throw new IOException("Could not save a pre-edit scene backup; nothing was changed.");
        }
        finally
        {
            terrain.terrainData = original;
            if (terrainCollider != null) terrainCollider.terrainData = original;
        }

        TerrainData updated = null;
        try
        {
            EditorUtility.DisplayProgressBar("Snapper underwater terrain",
                "Matching the northern bottom and preserving other islands...", .35f);
            updated = Object.Instantiate(original);
            updated.name = "Snapper north-matched editable seabed";
            AssetDatabase.CreateAsset(updated, folder + "/SnapperUpdatedTerrain.asset");

            Undo.RecordObject(terrain, "Replace Snapper bathymetry TerrainData");
            if (terrainCollider != null) Undo.RecordObject(terrainCollider, "Replace Snapper bathymetry collider");
            terrain.terrainData = updated;
            if (terrainCollider != null) terrainCollider.terrainData = updated;

            int modified = SnapperIslandRuntime.LevelSeabedAroundSnapper(terrain, center, water.BaseWaterLevel);
            EditorUtility.DisplayProgressBar("Snapper underwater terrain",
                "Adding collision to recognized Snapper rock meshes...", .75f);
            int addedColliders = SnapperRockCollisionEditor.AddNearbyRockColliders(scene, center);
            snapper.MarkUniformSeabedSaved();
            EditorUtility.SetDirty(updated);
            EditorUtility.SetDirty(snapper);
            EditorUtility.SetDirty(terrain);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Updated fishing scene could not be saved.");
            SceneView.RepaintAll();
            Debug.Log("[SNAPPER ISLAND] North-matched seabed saved (" + modified +
                " height samples), "+ addedColliders + " rock mesh colliders added. Backups: " + folder);
        }
        catch (Exception error)
        {
            // Keep the original unmodified TerrainData intact so a failure
            // cannot leave an unsaved, halfway sculpted map in memory.
            terrain.terrainData = original;
            if (terrainCollider != null) terrainCollider.terrainData = original;
            Debug.LogError("Snapper pass failed. Original TerrainData restored in memory. Recovery scene: " +
                folder + "/SceneBeforeSnapperChange.unity. " + error);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null) return result;
        }
        return null;
    }
}

/// <summary>
/// Adds precise static MeshColliders to the actual authored FBX rock meshes,
/// not to their parent bounding box. No mesh, UV, transform, material or
/// user-created hierarchy is changed. Model Read/Write is enabled if required
/// for reliable non-convex collision cooking on Android.
/// </summary>
public static class SnapperRockCollisionEditor
{
    [MenuItem("Tools/Open World/Snapper Island/Add Collisions to Selected Rock Formation")]
    public static void AddCollidersToSelection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Exit Play Mode", "Colliders must be added outside Play Mode so they save to your build.", "OK");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/PrototypeWorld.unity")
        {
            EditorUtility.DisplayDialog("Open main fishing scene", "Open PrototypeWorld.unity, then select the parent object of the main rock formation.", "OK");
            return;
        }
        var selected = Selection.gameObjects
            .Where(obj => obj != null && obj.scene == scene)
            .ToArray();
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("Select the rock formation",
                "In the Hierarchy select the Snapper rock formation's parent (or select its rock meshes), then run this command again.", "OK");
            return;
        }

        MeshFilter[] filters = selected.SelectMany(obj => obj.GetComponentsInChildren<MeshFilter>(true))
            .Distinct().ToArray();
        int changed = AddColliders(filters, out int skipped);
        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[SNAPPER ISLAND] Added/repaired " + changed +
                " mesh colliders; " + skipped + " meshes skipped. Press Ctrl+S to save the Scene.");
        }
        EditorUtility.DisplayDialog("Snapper rock collision",
            changed + " mesh colliders added/repaired. " + skipped + " meshes skipped.\n\n" +
            "No rock positions, rotations, scales, materials, or UVs were changed. Save the scene with Ctrl+S.", "OK");
    }

    // Called during the seabed repair. Only recognizes imported Snapper rock
    // assets or explicitly rock-named meshes around Snapper, never scenery on
    // other islands, fish, water or gameplay volumes.
    public static int AddNearbyRockColliders(Scene scene, Vector3 snapperCenter)
    {
        float radius = SnapperIslandGeometry.RadiusX + 72f;
        var rockFilters = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true))
            .Where(filter => filter != null && filter.sharedMesh != null &&
                filter.GetComponent<MeshRenderer>() != null)
            .Where(filter =>
            {
                Vector3 p = filter.transform.position - snapperCenter;
                if (p.x * p.x + p.z * p.z > radius * radius) return false;
                string assetPath = AssetDatabase.GetAssetPath(filter.sharedMesh) ?? "";
                if (assetPath.IndexOf("/SnapperIsland/Rocks/", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                // Objects created from a user's separate Blender rocks may be
                // named "Main Rock Formation" rather than using the six kit FBXs.
                for (Transform t = filter.transform; t != null; t = t.parent)
                {
                    string name = t.name.ToLowerInvariant();
                    if (name.Contains("rock") || name.Contains("cliff") || name.Contains("formation"))
                        return true;
                }
                return false;
            }).Distinct().ToArray();

        int result = AddColliders(rockFilters, out int skipped);
        if (skipped > 0)
            Debug.LogWarning("[SNAPPER ISLAND] Skipped " + skipped +
                " rock meshes with missing geometry or dynamic rigidbodies. Select that formation and use the collider command to inspect it.");
        return result;
    }

    private static int AddColliders(MeshFilter[] filters, out int skipped)
    {
        skipped = 0;
        int changed = 0;

        // Import FBXs as readable before adding non-convex static MeshColliders.
        // Changing this importer setting does not remap UVs or transform rocks.
        var importPaths = filters.Where(f => f != null && f.sharedMesh != null)
            .Select(f => AssetDatabase.GetAssetPath(f.sharedMesh))
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct().ToArray();
        foreach (string path in importPaths)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.isReadable) continue;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        foreach (var filter in filters)
        {
            if (filter == null || filter.sharedMesh == null ||
                filter.GetComponent<MeshRenderer>() == null)
            {
                skipped++;
                continue;
            }
            Rigidbody rigidbody = filter.GetComponentInParent<Rigidbody>();
            if (rigidbody != null && !rigidbody.isKinematic)
            {
                // A dynamic Rigidbody requires convex colliders and must not
                // acquire a non-convex collider as a side effect of this tool.
                skipped++;
                continue;
            }

            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
                changed++;
            }
            else if (collider.sharedMesh != filter.sharedMesh ||
                collider.convex || collider.isTrigger || !collider.enabled)
            {
                Undo.RecordObject(collider, "Repair Snapper rock mesh collision");
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
                collider.enabled = true;
                changed++;
            }
        }
        Physics.SyncTransforms();
        return changed;
    }
}
