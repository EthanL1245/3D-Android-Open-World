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

        var world = Find<IslandExpansionWorld>(scene);
        // There may be an inactive, pre-Suncrest Terrain left in older scenes.
        // ALWAYS operate on the actual shared terrain used by fishing and builds.
        var terrain = world != null ? world.Terrain : null;
        var reef = Find<ReefZone>(scene);
        var water = Find<OceanWater>(scene);
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
    private const string MainScenePath = "Assets/Scenes/PrototypeWorld.unity";

    [MenuItem("Tools/Open World/Snapper Island/Auto-Find and Fix Rock Collisions")]
    public static void AutoFindAndFix()
    {
        if (!TryGetMainScene(out Scene scene)) return;
        if (!TryFindSnapperCenter(scene, out Vector3 center)) return;

        MeshFilter[] candidates = FindNearbyRockMeshes(scene, center);
        if (candidates.Length == 0)
        {
            int nearbyMeshCount = CountNearbyRenderedMeshes(scene, center);
            EditorUtility.DisplayDialog("No Snapper rock meshes found",
                "The scene contains " + nearbyMeshCount + " rendered mesh objects near Snapper Island, but none are identifiable as rock meshes.\n\n" +
                "If your formation is visible, click a specific rock in the Scene view or expand its parent in the Hierarchy and select a child with a Mesh Filter. Then run Add Collisions to Selected Rock Formation.\n\n" +
                "If the formation appears only in Play Mode, it is runtime-generated and cannot be saved by this editor command until it is baked into the scene.", "OK");
            return;
        }

        int goodBefore = CountHealthyColliders(candidates);
        int changed = AddColliders(candidates, out int skipped);
        Report(scene, "Auto-detected Snapper rock meshes", candidates.Length, goodBefore, changed, skipped);
    }

    [MenuItem("Tools/Open World/Snapper Island/Add Collisions to Selected Rock Formation")]
    public static void AddCollidersToSelection()
    {
        if (!TryGetMainScene(out Scene scene)) return;
        var selected = Selection.gameObjects
            .Where(obj => obj != null && obj.scene == scene)
            .ToArray();

        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No scene objects selected",
                "Select the main rock formation in the Hierarchy (or click one of its individual rock meshes). Alternatively, use Auto-Find and Fix Rock Collisions.", "OK");
            return;
        }

        MeshFilter[] filters = selected
            .SelectMany(obj => obj.GetComponentsInChildren<MeshFilter>(true))
            .Where(filter => filter != null)
            .Distinct()
            .ToArray();

        if (filters.Length == 0)
        {
            string names = string.Join(", ", selected.Take(3).Select(obj => obj.name));
            if (TryFindSnapperCenter(scene, out Vector3 center))
            {
                MeshFilter[] candidates = FindNearbyRockMeshes(scene, center);
                if (candidates.Length > 0)
                {
                    bool scan = EditorUtility.DisplayDialog("Selected object has no rock mesh",
                        "Selected: " + names + "\n\n" +
                        "No Mesh Filter was found on this object or any of its children. " +
                        "However, I found " + candidates.Length + " nearby Snapper rock meshes.\n\n" +
                        "Add collision to those detected rocks instead? Nothing will be repositioned.", "Fix detected rocks", "Cancel");
                    if (scan)
                    {
                        int good = CountHealthyColliders(candidates);
                        int added = AddColliders(candidates, out int skipped);
                        Report(scene, "Auto-detected Snapper rock meshes", candidates.Length, good, added, skipped);
                    }
                    return;
                }
            }
            EditorUtility.DisplayDialog("Selected object contains no Mesh Filter",
                "Selected: " + names + "\n\n" +
                "There are no Mesh Filter components on this selection or any children. " +
                "Select an actual visible rock (an FBX instance with a Mesh Filter), not Terrain, Island Expansion, or an empty parent.\n\n" +
                "The FBX existing in the Project window is not enough: a copy must be placed in the Scene.", "OK");
            return;
        }

        int already = CountHealthyColliders(filters);
        int changed = AddColliders(filters, out int skippedCount);
        Report(scene, "Selected rock formation", filters.Length, already, changed, skippedCount);
    }

    // Used by the seabed editor after its safe TerrainData backup. Does not
    // alter scene transforms or meshes and only matches named/FBX rock objects.
    public static int AddNearbyRockColliders(Scene scene, Vector3 snapperCenter)
    {
        MeshFilter[] rocks = FindNearbyRockMeshes(scene, snapperCenter);
        int modified = AddColliders(rocks, out int skipped);
        if (rocks.Length == 0)
            Debug.LogWarning("[SNAPPER ISLAND] No identifiable rock mesh objects found in saved fishing scene. Any locally placed main formation can be selected and fixed via the Snapper Island collision menu.");
        else if (skipped != 0)
            Debug.LogWarning("[SNAPPER ISLAND] " + skipped + " rock meshes could not receive static colliders; inspect dynamic Rigidbody or missing mesh components.");
        return modified;
    }

    private static bool TryGetMainScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Exit Play Mode",
                "Rock collisions must be added in Scene edit mode. Play Mode changes do not save.", "OK");
            return false;
        }
        if (scene.path != MainScenePath)
        {
            EditorUtility.DisplayDialog("Open the main fishing map",
                "Double-click Assets/Scenes/PrototypeWorld.unity, then run this tool again. Do not run it on a separate shop scene.", "OK");
            return false;
        }
        return true;
    }

    private static bool TryFindSnapperCenter(Scene scene, out Vector3 center)
    {
        center = Vector3.zero;
        var reef = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ReefZone>(true))
            .FirstOrDefault();
        if (reef == null)
        {
            EditorUtility.DisplayDialog("Snapper map not found",
                "The loaded PrototypeWorld scene has no ReefZone. Run Make Main Fishing Map Editable first, then save the scene.", "OK");
            return false;
        }
        center = SnapperIslandGeometry.Center(reef);
        return true;
    }

    private static MeshFilter[] FindNearbyRockMeshes(Scene scene, Vector3 snapperCenter)
    {
        float radius = SnapperIslandGeometry.RadiusX + 90f;
        float radiusSq = radius * radius;
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true))
            .Where(filter => filter != null && filter.sharedMesh != null &&
                filter.gameObject.activeInHierarchy)
            .Where(filter =>
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) return false;
                Bounds bounds = renderer.bounds;
                float nearestX = Mathf.Clamp(snapperCenter.x, bounds.min.x, bounds.max.x);
                float nearestZ = Mathf.Clamp(snapperCenter.z, bounds.min.z, bounds.max.z);
                float dx = nearestX - snapperCenter.x;
                float dz = nearestZ - snapperCenter.z;
                if (dx * dx + dz * dz > radiusSq) return false;

                string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh) ?? "";
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject) ?? "";
                if (IsSnapperRockAsset(meshPath) || IsSnapperRockAsset(prefabPath))
                    return true;

                // Allow user-authored FBX formations whose assets aren't in the
                // built-in Snapper/Rocks folder, but only with rock-related names.
                if (LooksLikeRock(filter.gameObject.name) || LooksLikeRock(filter.sharedMesh.name)) return true;
                for (Transform parent = filter.transform.parent; parent != null; parent = parent.parent)
                    if (LooksLikeRock(parent.name)) return true;
                return false;
            })
            .Distinct()
            .ToArray();
    }

    private static bool IsSnapperRockAsset(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.IndexOf("/SnapperIsland/Rocks/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeRock(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string lower = name.ToLowerInvariant();
        return lower.Contains("rock") || lower.Contains("cliff") ||
            lower.Contains("boulder") || lower.Contains("outcrop") ||
            lower.Contains("formation");
    }

    private static int CountNearbyRenderedMeshes(Scene scene, Vector3 center)
    {
        float radius = SnapperIslandGeometry.RadiusX + 90f;
        float r2 = radius * radius;
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
            .Count(renderer =>
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                    return false;
                Bounds b = renderer.bounds;
                float dx = Mathf.Clamp(center.x, b.min.x, b.max.x) - center.x;
                float dz = Mathf.Clamp(center.z, b.min.z, b.max.z) - center.z;
                return dx * dx + dz * dz <= r2;
            });
    }

    private static bool Healthy(MeshFilter filter)
    {
        if (filter == null || filter.sharedMesh == null) return false;
        var collider = filter.GetComponent<MeshCollider>();
        return collider != null && collider.enabled && !collider.isTrigger &&
            !collider.convex && collider.sharedMesh == filter.sharedMesh;
    }

    private static int CountHealthyColliders(MeshFilter[] filters)
        => filters.Count(Healthy);

    private static void Report(Scene scene, string scope, int candidates, int already,
        int repaired, int skipped)
    {
        if (repaired > 0) EditorSceneManager.MarkSceneDirty(scene);
        string message = scope + "\n\n" +
            candidates + " mesh filters inspected.\n" +
            already + " already had correct MeshColliders.\n" +
            repaired + " colliders added or repaired.\n" +
            skipped + " skipped.\n\n" +
            (candidates == 0
                ? "No rock meshes were found. Check the Scene Hierarchy or run Auto-Find."
                : repaired + already == 0
                    ? "No usable static rock meshes found. Inspect the selected object's Mesh Filter and Rigidbody."
                    : "Mesh colliders match the existing rock meshes. No rock positions, UVs or materials were changed.") +
            "\n\nPress Ctrl+S to save the scene.";
        Debug.Log("[SNAPPER ISLAND] " + message);
        EditorUtility.DisplayDialog("Snapper rock collision", message, "OK");
    }

    private static int AddColliders(MeshFilter[] filters, out int skipped)
    {
        skipped = 0;
        int changed = 0;
        if (filters.Length == 0) return 0;

        // FBX meshes must be readable for collision cooking. This is an importer
        // setting only; manually placed transforms, materials and UVs stay put.
        var importPaths = filters.Where(f => f != null && f.sharedMesh != null)
            .Select(f => AssetDatabase.GetAssetPath(f.sharedMesh))
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct()
            .ToArray();
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
                // Dynamic rigidbodies cannot use exact non-convex mesh collision.
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
            else if (!Healthy(filter))
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
