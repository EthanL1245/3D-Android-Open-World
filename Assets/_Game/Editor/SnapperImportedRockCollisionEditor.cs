using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Optional one-time bake to serialize the same rock colliders into PrototypeWorld
/// so the Android player does not need to create missing colliders at scene load.
/// The runtime fallback remains for any newly duplicated, unbaked rocks.
/// </summary>
public static class SnapperImportedRockCollisionEditor
{
    [MenuItem("Tools/Open World/Manual Scenery/Bake Snapper Rock Colliders")]
    public static void BakeIntoScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[SNAPPER COLLISION] Exit Play Mode before baking rock colliders.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SnapperImportedRockCollision.ScenePath)
        {
            Debug.LogWarning("[SNAPPER COLLISION] Open Assets/Scenes/PrototypeWorld.unity before baking.");
            return;
        }

        int added = 0, existing = 0, skipped = 0;
        foreach (MeshFilter filter in SnapperImportedRockCollision.PlacedMeshes(scene))
        {
            // Only change objects whose mesh comes from the actual imported
            // rock assets. Do not touch other scenery nested under those roots.
            string source = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (!string.Equals(source, SnapperImportedRockCollision.NewRockFbx, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(source, SnapperImportedRockCollision.SingleRockBlend, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            if (filter.GetComponent<Collider>() != null)
            {
                existing++;
                continue;
            }

            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                skipped++;
                continue;
            }

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
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the Snapper rock collider edits.");
        }

        Debug.Log("[SNAPPER COLLISION] " + added + " mesh colliders added, " +
            existing + " already had colliders, " + skipped +
            " excluded. No rock transforms, meshes or materials modified." +
            " To include scene-baked colliders in Git, commit PrototypeWorld.unity.");
    }
}
