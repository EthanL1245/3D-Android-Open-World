#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds precise, non-convex collision on the replacement imported Suncrest main
/// rock mesh objects in Edit Mode, without changing artist transforms. Saves only when explicitly using the menu
/// command, so opening Unity never silently overwrites user scene edits.
/// </summary>
[InitializeOnLoad]
public static class SuncrestMainRockCollisionEditor
{
    static SuncrestMainRockCollisionEditor()
    {
        EditorApplication.delayCall += AutoBake;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += AutoBake;
    }

    private static bool CanBake()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               !EditorApplication.isCompiling &&
               SceneManager.GetActiveScene().path == SuncrestMainRockCollision.ScenePath;
    }

    private static void AutoBake()
    {
        if (!CanBake()) return;
        Scene scene = SceneManager.GetActiveScene();
        int added = Bake(scene) + SuncrestPlacedRockCollision.EnsureColliders(scene);
        if (added > 0) EditorSceneManager.MarkSceneDirty(scene);
        if (added > 0)
            Debug.Log("[SUNCREST ROCK] Added " + added +
                " MeshColliders in Scene view. Ctrl+S saves them into PrototypeWorld.");
    }

    [MenuItem("Tools/Open World/Manual Scenery/Bake Suncrest Main Rock Colliders")]
    public static void BakeIntoScene()
    {
        if (!CanBake())
        {
            Debug.LogWarning("[SUNCREST ROCK] Open PrototypeWorld and exit Play Mode first.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        int added = Bake(scene) + SuncrestPlacedRockCollision.EnsureColliders(scene);
        if (added > 0) EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Suncrest Main Rock colliders.");
        Debug.Log("[SUNCREST ROCK] Colliders saved in the scene (" +
            added + " newly added). No artist transforms or materials modified.");
    }

    private static int Bake(Scene scene)
    {
        int added = 0;
        foreach (MeshFilter filter in SuncrestMainRockCollision.PlacedMeshes(scene))
        {
            // Never add components to unrelated scenery; target the FBX mesh itself.
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (!string.Equals(path, SuncrestMainRockCollision.RockFbx,
                    StringComparison.OrdinalIgnoreCase)) continue;
            if (SuncrestMainRockCollision.HasSolidShape(filter)) continue;
            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic) continue;

            MeshCollider collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
            collider.convex = false;
            collider.isTrigger = false;
            collider.sharedMesh = filter.sharedMesh;
            EditorUtility.SetDirty(collider);
            added++;
        }
        if (added > 0)
        {
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
        }
        return added;
    }
}
#endif
