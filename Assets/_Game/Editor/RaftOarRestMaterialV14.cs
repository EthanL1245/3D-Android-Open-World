using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v14 fixes the last raft/oar material edge case using the evidence from the user's
/// v13 diagnostics.
///
/// v13 inspected the final "oar" submesh on the merged raft renderer and found only
/// ONE tiny component: 2 triangles, ~0.355 m long. That cannot be either moving oar.
/// Therefore the final submesh is the stationary oar-rest/support geometry, while the
/// actual moving paddles are rendered elsewhere and already look correct.
///
/// This pass is deliberately material-only: it changes ONLY the final tiny submesh on
/// the merged raft renderer from Oar Texture back to the raft material. It does not
/// touch any other renderer, mesh triangles, animation, transforms, UVs, colliders or
/// boat gameplay. The moving oars therefore keep their existing oar texture.
/// </summary>
public static class RaftOarRestMaterialV14
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string MarkerName = "RaftOarRestMaterial_v14";
    private const string V12Marker = "RaftMaterialRecovery_v12";
    private const string V13Marker = "RaftMovingOarsOnly_v13";

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        // Stop v13 from retrying its now-proven-wrong assumption on every reload.
        EditorApplication.delayCall += BlockV13Retry;

        // Apply after the v12 material restoration has had time to settle.
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Make Oar Rests Use Raft Texture (v14)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v14", "Exit Play Mode first.", "OK");
            return;
        }

        BlockV13Retry();
        if (!Apply(true))
            EditorUtility.DisplayDialog("Raft v14", "No change was saved. Check the Console for [RAFT V14] diagnostics.", "OK");
    }

    private static void BlockV13Retry()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null || readWrapper.Find(V13Marker) != null) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null || wrapper.Find(V13Marker) != null) return;
            new GameObject(V13Marker).transform.SetParent(wrapper, false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[RAFT V14] Disabled obsolete v13 retry; v13 diagnostics proved the merged renderer's final slot is only the stationary rest/support piece.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return false;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) return false;
        if (!force && readWrapper.Find(MarkerName) != null) return false;
        if (readWrapper.Find(V12Marker) == null)
        {
            Debug.LogWarning("[RAFT V14] Waiting for v12 raft-material recovery first.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer merged = FindMergedSplitRenderer(wrapper);
            Mesh mesh = GetMesh(merged);
            if (merged == null || mesh == null || mesh.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V14] Could not find the v12 merged split renderer.");
                return false;
            }

            int restSlot = mesh.subMeshCount - 1;
            if (mesh.GetTopology(restSlot) != MeshTopology.Triangles)
            {
                Debug.LogError("[RAFT V14] Final split slot is not triangle geometry; refusing to change materials.");
                return false;
            }

            int[] restIndices = mesh.GetIndices(restSlot);
            int restTriangles = restIndices.Length / 3;
            if (!TryMeasureSubmesh(mesh, restIndices, merged.transform, root.transform, out Bounds restBounds))
            {
                Debug.LogError("[RAFT V14] Could not measure the final split slot; refusing to change materials.");
                return false;
            }

            float largest = Mathf.Max(restBounds.size.x, Mathf.Max(restBounds.size.y, restBounds.size.z));

            // The user's exact diagnostic was 2 triangles and ~0.355 m length. Keep
            // a little tolerance for import scaling, but never repaint a large/complex
            // paddle submesh by accident.
            if (restTriangles <= 0 || restTriangles > 12 || largest > 0.80f)
            {
                Debug.LogError(
                    "[RAFT V14] Safety stop: final slot no longer looks like the tiny stationary oar rest. " +
                    "triangles=" + restTriangles + " largestDimension=" + largest.ToString("0.000") +
                    " m. No prefab change was saved.");
                return false;
            }

            Material[] materials = merged.sharedMaterials ?? Array.Empty<Material>();
            if (materials.Length < mesh.subMeshCount)
            {
                Debug.LogError("[RAFT V14] Renderer has fewer material slots than mesh submeshes.");
                return false;
            }

            Material raftMaterial = FindRaftMaterial(materials, restSlot);
            if (raftMaterial == null)
            {
                Debug.LogError("[RAFT V14] Could not find the restored raft material on this renderer.");
                return false;
            }

            List<string> preservedOarRenderers = FindOtherOarRenderers(wrapper, merged);

            // MATERIAL ASSIGNMENT ONLY. Do not touch the mesh: slot geometry stays
            // exactly where it is, but the stationary rest now visually belongs to
            // the raft. All other renderers (including the moving paddles) are left
            // completely untouched.
            Material oldRestMaterial = materials[restSlot];
            materials[restSlot] = raftMaterial;
            merged.sharedMaterials = materials;
            EditorUtility.SetDirty(merged);

            RemoveMarker(wrapper, MarkerName);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);
            if (wrapper.Find(V13Marker) == null)
                new GameObject(V13Marker).transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Verify the serialized final slot matches the raft material.
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform savedWrapper = saved != null ? saved.transform.Find(WrapperPath) : null;
            Renderer savedMerged = savedWrapper != null ? FindMergedSplitRenderer(savedWrapper) : null;
            Material[] savedMaterials = savedMerged != null ? savedMerged.sharedMaterials : null;
            if (savedMaterials == null || savedMaterials.Length <= restSlot || savedMaterials[restSlot] == null ||
                savedMaterials[restSlot] != raftMaterial)
            {
                Debug.LogError("[RAFT V14] Save verification failed; final rest slot did not serialize to the raft material.");
                return false;
            }

            Debug.Log(
                "[RAFT V14] SUCCESS — stationary oar rest/support now uses the raft material; moving oar renderers were untouched. " +
                "restSlot=" + restSlot + " triangles=" + restTriangles +
                " bounds=" + restBounds.size.ToString("F3") +
                " oldMaterial=" + MaterialName(oldRestMaterial) +
                " newMaterial=" + MaterialName(raftMaterial) +
                " preservedOarRenderers=" + (preservedOarRenderers.Count > 0 ? string.Join(" | ", preservedOarRenderers) : "none-detected"));

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft v14 Complete",
                    "The small stationary parts the oars sit on now use the raft texture. The actual moving oar renderers were not changed.",
                    "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V14] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindMergedSplitRenderer(Transform wrapper)
    {
        Renderer best = null;
        long bestScore = long.MinValue;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null || mesh.subMeshCount < 2) continue;

            long score = mesh.vertexCount;
            string meshName = mesh.name ?? string.Empty;
            if (meshName.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) score += 10000000L;
            if (meshName.IndexOf("RaftMovingOars", StringComparison.OrdinalIgnoreCase) >= 0) score += 9000000L;
            if ((renderer.name ?? string.Empty).IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0) score += 1000000L;

            if (score > bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }
        return best;
    }

    private static Material FindRaftMaterial(Material[] materials, int excludeSlot)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (i == excludeSlot) continue;
            Material material = materials[i];
            if (material == null || IsOarMaterial(material)) continue;
            Texture texture = material.mainTexture;
            if ((material.name ?? string.Empty).IndexOf("Raft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (texture != null && texture.name.IndexOf("Raft", StringComparison.OrdinalIgnoreCase) >= 0))
                return material;
        }

        return materials.Where((m, i) => i != excludeSlot && m != null && !IsOarMaterial(m)).FirstOrDefault();
    }

    private static List<string> FindOtherOarRenderers(Transform wrapper, Renderer merged)
    {
        List<string> result = new List<string>();
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer == merged) continue;
            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            if (!materials.Any(IsOarMaterial)) continue;
            result.Add(AnimationUtility.CalculateTransformPath(renderer.transform, wrapper));
        }
        return result;
    }

    private static bool TryMeasureSubmesh(Mesh mesh, int[] indices, Transform rendererTransform, Transform boatRoot, out Bounds bounds)
    {
        bounds = default;
        Vector3[] vertices;
        try { vertices = mesh.vertices; }
        catch { return false; }
        if (vertices == null || vertices.Length == 0 || indices == null || indices.Length == 0) return false;

        bool have = false;
        foreach (int index in indices)
        {
            if (index < 0 || index >= vertices.Length) continue;
            Vector3 world = rendererTransform.TransformPoint(vertices[index]);
            Vector3 local = boatRoot.InverseTransformPoint(world);
            if (!have) { bounds = new Bounds(local, Vector3.zero); have = true; }
            else bounds.Encapsulate(local);
        }
        return have;
    }

    private static bool IsOarMaterial(Material material)
    {
        if (material == null) return false;
        string name = material.name ?? string.Empty;
        if (name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        Texture texture = material.mainTexture;
        return texture != null && (texture.name.IndexOf("Oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   texture.name.IndexOf("Paddle", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string MaterialName(Material material)
    {
        if (material == null) return "NULL";
        return material.name + "/" + (material.mainTexture != null ? material.mainTexture.name : "no-texture");
    }

    private static void RemoveMarker(Transform wrapper, string name)
    {
        Transform marker = wrapper.Find(name);
        if (marker != null) UnityEngine.Object.DestroyImmediate(marker.gameObject);
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }
}
