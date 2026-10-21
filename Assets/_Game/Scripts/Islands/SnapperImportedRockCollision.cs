using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fills missing, static, non-convex rock collision on the hand-placed Snapper
/// Island rock models. Imported prefab children may already have colliders;
/// extracted/duplicated MeshFilter objects do not inherit FBX import colliders.
/// This never modifies position, rotation, scale, mesh or material.
/// </summary>
public static class SnapperImportedRockCollision
{
    public const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    public const string NewRockFbx = "Assets/Manually Added Assets/Snapper island Rocks.fbx";
    public const string SingleRockBlend = "Assets/Manually Added Assets/Snapper Island Rock Single.blend";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Also covers player configurations where the world is already loaded.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInitialScene()
    {
        EnsureColliders(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureColliders(scene);
    }

    public static bool IsTargetScene(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded &&
            string.Equals(scene.name, "PrototypeWorld", StringComparison.Ordinal);
    }

    public static bool IsTargetRootName(string name)
    {
        return string.Equals(name, "Snapper island Rocks", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Snapper Island Rock Single", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Snapper Island Rock Single (", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Snapper Island Rock Formation", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Snapper Island Main Rock Formation", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Enumerate only the imported group and placed single-rock instances.</summary>
    public static System.Collections.Generic.IEnumerable<MeshFilter> PlacedMeshes(Scene scene)
    {
        if (!IsTargetScene(scene)) yield break;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (IsTargetRootName(root.name))
            {
                foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mesh != null && mesh.sharedMesh != null) yield return mesh;
            }
            else if (root.name == "USER PLACED SCENERY - DO NOT MODIFY BY CODE")
            {
                foreach (Transform item in root.transform)
                {
                    if (!IsTargetRootName(item.name)) continue;
                    foreach (MeshFilter mesh in item.GetComponentsInChildren<MeshFilter>(true))
                        if (mesh != null && mesh.sharedMesh != null) yield return mesh;
                }
            }
        }
    }

    public static int EnsureColliders(Scene scene)
    {
        int added = 0;
        foreach (MeshFilter filter in PlacedMeshes(scene))
        {
            // Never overwrite manually configured colliders.
            if (filter.GetComponent<Collider>() != null) continue;
            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic) continue;

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.convex = false;
            collider.isTrigger = false;
            collider.sharedMesh = filter.sharedMesh;
            added++;
        }
        return added;
    }
}
