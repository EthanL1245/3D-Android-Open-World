using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Completes material assignment on the authored raft paddles only.
///
/// The Blender import can expose fewer material slots than a paddle mesh has
/// submeshes. Unity then draws uncovered blade/end-cap submeshes with its grey
/// fallback material, while an unidentified paddle can inherit the raft texture.
/// This repair identifies both paddles from their LOCAL mesh shape (so diagonal
/// world-space rotation cannot make an oar look square), includes small renderer
/// pieces attached at the paddle endpoints, then maps the supplied Oar Texture.jpg
/// to every material slot on those oar pieces. Raft/deck renderers are left alone.
/// </summary>
public sealed class RaftPaddleTextureFix : AssetPostprocessor
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/Authored/Oar Texture.jpg";
    private const string MaterialFolder = "Assets/_Game/Boats/Raft/Authored/Materials";
    private const string PaddleMaterialPath = MaterialFolder + "/PaddleComplete.mat";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    // v3 intentionally reruns once on projects that already received an earlier fix.
    private const string MarkerName = "RaftPaddleTextureComplete_v3";

    [InitializeOnLoadMethod]
    private static void QueueInitialFix()
    {
        // Run after the main raft importer has had a chance to recreate the visual.
        EditorApplication.delayCall += () => EditorApplication.delayCall += ApplyIfNeeded;
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool relevant = false;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i];
            if (path == PrefabPath || path == OarTexturePath || path.EndsWith("/Raft.blend", StringComparison.OrdinalIgnoreCase))
            {
                relevant = true;
                break;
            }
        }

        if (relevant) EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Tools/Open World/Repair Raft Paddle Texture")]
    private static void RepairOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft Paddle", "Exit Play Mode first.", "OK");
            return;
        }

        bool changed = ApplyIfNeeded(true);
        if (!changed)
            EditorUtility.DisplayDialog("Raft Paddle", "The paddles are already fully textured, or the authored raft has not been imported yet.", "OK");
    }

    private static void ApplyIfNeeded()
    {
        ApplyIfNeeded(false);
    }

    private static bool ApplyIfNeeded(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (prefab == null || oarTexture == null) return false;

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        if (prefabWrapper == null) return false;
        if (!force && prefabWrapper.Find(MarkerName) != null) return false;

        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            List<Renderer> paddles = FindPaddleRenderers(wrapper);
            if (paddles.Count == 0)
            {
                Debug.LogWarning("Raft paddle texture repair could not identify the authored paddle renderer(s). No raft objects were changed.");
                return false;
            }

            Material paddleMaterial = BuildPaddleMaterial(oarTexture);
            int repairedSlots = 0;

            for (int p = 0; p < paddles.Count; p++)
            {
                Renderer paddle = paddles[p];
                if (paddle == null) continue;

                int subMeshCount = GetSubMeshCount(paddle);
                Material[] current = paddle.sharedMaterials;
                int slotCount = Mathf.Max(1, Mathf.Max(subMeshCount, current != null ? current.Length : 0));
                Material[] completed = new Material[slotCount];
                for (int i = 0; i < completed.Length; i++) completed[i] = paddleMaterial;

                // Every slot on an identified oar piece receives ONLY the user's
                // oar texture. This covers blade/shaft submeshes and separate caps.
                paddle.sharedMaterials = completed;
                repairedSlots += completed.Length;
            }

            RemoveMarker(wrapper, "RaftPaddleTextureComplete_v1");
            RemoveMarker(wrapper, "RaftPaddleTextureComplete_v2");
            RemoveMarker(wrapper, MarkerName);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log("Raft paddle texture completed: " + paddles.Count + " oar renderer piece(s), " + repairedSlots + " material slot(s), all using Oar Texture.jpg. Raft/deck materials were left alone.");
            if (force)
                EditorUtility.DisplayDialog("Raft Paddles Fixed", "Oar Texture.jpg is now mapped to every material slot of both paddles, including separately-rendered end caps when present. The raft itself was not retextured.", "OK");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveMarker(Transform wrapper, string name)
    {
        Transform marker = wrapper.Find(name);
        if (marker != null) UnityEngine.Object.DestroyImmediate(marker.gameObject);
    }

    private static List<Renderer> FindPaddleRenderers(Transform wrapper)
    {
        Renderer[] all = wrapper.GetComponentsInChildren<Renderer>(true);
        List<Renderer> named = new List<Renderer>();
        List<Renderer> longCandidates = new List<Renderer>();
        List<Renderer> result = new List<Renderer>();
        if (all == null || all.Length == 0) return result;

        bool haveOverall = false;
        Bounds overall = default;
        for (int i = 0; i < all.Length; i++)
        {
            Renderer renderer = all[i];
            if (renderer == null) continue;
            if (!haveOverall) { overall = renderer.bounds; haveOverall = true; }
            else overall.Encapsulate(renderer.bounds);
        }

        for (int i = 0; i < all.Length; i++)
        {
            Renderer renderer = all[i];
            if (renderer == null) continue;
            if (HasPaddleName(renderer)) AddUnique(named, renderer);
            if (LooksLikeNarrowPaddle(renderer)) AddUnique(longCandidates, renderer);
        }

        // Explicit authored names/material names are strongest evidence. Keep all of
        // them, then make sure a matching mirrored core paddle is not missed merely
        // because only one side retained a useful Blender material name.
        for (int i = 0; i < named.Count; i++) AddUnique(result, named[i]);

        Renderer namedCore = FirstLongRenderer(named);
        if (namedCore != null)
        {
            AddUnique(result, namedCore);
            Renderer mirror = BestMirror(namedCore, longCandidates, overall);
            if (mirror != null) AddUnique(result, mirror);
        }

        // If Blender used generic names such as Plane/Plane.001, find the two oars
        // as a same-shaped mirrored pair. Shape comes from Mesh.bounds in LOCAL
        // space, not Renderer.bounds, so the diagonal pose shown on the raft cannot
        // defeat the long/narrow test.
        if (CountLongRenderers(result) < 2)
        {
            Renderer pairA = null, pairB = null;
            float bestPairScore = float.NegativeInfinity;
            for (int i = 0; i < longCandidates.Count; i++)
            for (int j = i + 1; j < longCandidates.Count; j++)
            {
                Renderer a = longCandidates[i], b = longCandidates[j];
                if (!SimilarShape(a, b)) continue;
                if (!OppositeSides(a, b, overall)) continue;

                float score = PaddleScore(a, overall) + PaddleScore(b, overall);
                if (SameMesh(a, b)) score += 18f;
                if (score > bestPairScore)
                {
                    bestPairScore = score;
                    pairA = a;
                    pairB = b;
                }
            }

            if (pairA != null)
            {
                AddUnique(result, pairA);
                AddUnique(result, pairB);
            }
        }

        // Some Blender exports make the little end faces separate mesh renderers.
        // Add ONLY tiny pieces that physically touch an endpoint of a positively
        // identified long paddle. This fixes grey end caps without painting deck
        // boards, seats, ropes, or the raft body with the oar texture.
        IncludeEndpointPieces(result, all);

        // If both paddles live inside one renderer, the long-shape fallback can
        // legitimately produce one result. Its material array is expanded to all
        // submeshes by ApplyIfNeeded, which covers both oars and their caps.
        if (result.Count == 0 && longCandidates.Count > 0)
        {
            Renderer best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < longCandidates.Count; i++)
            {
                float score = PaddleScore(longCandidates[i], overall);
                if (score > bestScore) { bestScore = score; best = longCandidates[i]; }
            }
            AddUnique(result, best);
            IncludeEndpointPieces(result, all);
        }

        return result;
    }

    private static Renderer FirstLongRenderer(List<Renderer> renderers)
    {
        for (int i = 0; i < renderers.Count; i++)
            if (LooksLikeNarrowPaddle(renderers[i])) return renderers[i];
        return null;
    }

    private static int CountLongRenderers(List<Renderer> renderers)
    {
        int count = 0;
        for (int i = 0; i < renderers.Count; i++)
            if (LooksLikeNarrowPaddle(renderers[i])) count++;
        return count;
    }

    private static void IncludeEndpointPieces(List<Renderer> selected, Renderer[] all)
    {
        List<Renderer> cores = new List<Renderer>();
        for (int i = 0; i < selected.Count; i++)
            if (LooksLikeNarrowPaddle(selected[i])) cores.Add(selected[i]);

        for (int c = 0; c < cores.Count; c++)
        {
            Renderer core = cores[c];
            if (!TryGetWorldEndpoints(core, out Vector3 a, out Vector3 b, out float thickness)) continue;
            float touchRadius = Mathf.Clamp(thickness * 0.85f, 0.10f, 0.32f);

            for (int i = 0; i < all.Length; i++)
            {
                Renderer part = all[i];
                if (part == null || selected.Contains(part)) continue;

                Vector3 size = part.bounds.size;
                float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                // End caps are small. Never absorb a plank/seat just because the
                // oar happens to pass visually near it.
                if (largest > 0.75f) continue;

                float da = Vector3.Distance(part.bounds.ClosestPoint(a), a);
                float db = Vector3.Distance(part.bounds.ClosestPoint(b), b);
                if (Mathf.Min(da, db) <= touchRadius)
                    AddUnique(selected, part);
            }
        }
    }

    private static bool TryGetWorldEndpoints(Renderer renderer, out Vector3 a, out Vector3 b, out float thickness)
    {
        a = b = Vector3.zero;
        thickness = 0f;
        Mesh mesh = GetMesh(renderer);
        if (mesh == null) return false;

        Bounds bounds = mesh.bounds;
        Vector3 size = bounds.size;
        int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        Vector3 direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        float extent = axis == 0 ? bounds.extents.x : axis == 1 ? bounds.extents.y : bounds.extents.z;
        Vector3 localA = bounds.center - direction * extent;
        Vector3 localB = bounds.center + direction * extent;
        a = renderer.transform.TransformPoint(localA);
        b = renderer.transform.TransformPoint(localB);

        Vector3 scaled = LocalMeshSize(renderer);
        float largest = Mathf.Max(scaled.x, Mathf.Max(scaled.y, scaled.z));
        float smallest = Mathf.Min(scaled.x, Mathf.Min(scaled.y, scaled.z));
        float middle = scaled.x + scaled.y + scaled.z - largest - smallest;
        thickness = Mathf.Max(0.05f, middle);
        return true;
    }

    private static Renderer BestMirror(Renderer source, List<Renderer> candidates, Bounds overall)
    {
        Renderer best = null;
        float score = float.NegativeInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            Renderer candidate = candidates[i];
            if (candidate == null || candidate == source) continue;
            if (!SimilarShape(source, candidate) || !OppositeSides(source, candidate, overall)) continue;
            float value = PaddleScore(candidate, overall) + (SameMesh(source, candidate) ? 18f : 0f);
            if (value > score) { score = value; best = candidate; }
        }
        return best;
    }

    private static bool HasPaddleName(Renderer renderer)
    {
        string name = renderer.name ?? string.Empty;
        if (name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        Transform t = renderer.transform.parent;
        for (int depth = 0; t != null && depth < 2; depth++, t = t.parent)
        {
            string parentName = t.name ?? string.Empty;
            if (parentName.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                parentName.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        Material[] materials = renderer.sharedMaterials;
        for (int m = 0; materials != null && m < materials.Length; m++)
        {
            Material material = materials[m];
            if (material == null) continue;
            string materialName = material.name ?? string.Empty;
            if (materialName.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool LooksLikeNarrowPaddle(Renderer renderer)
    {
        Vector3 size = LocalMeshSize(renderer);
        if (size == Vector3.zero) return false;
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float middle = size.x + size.y + size.z - largest - smallest;
        float ratio = largest / Mathf.Max(0.01f, middle);
        return largest >= 1.55f && largest <= 5.0f && middle <= 1.25f && smallest <= 0.75f && ratio >= 2.65f;
    }

    private static Vector3 LocalMeshSize(Renderer renderer)
    {
        Mesh mesh = GetMesh(renderer);
        if (mesh == null) return Vector3.zero;
        Vector3 s = mesh.bounds.size;
        Vector3 scale = renderer.transform.lossyScale;
        return new Vector3(Mathf.Abs(s.x * scale.x), Mathf.Abs(s.y * scale.y), Mathf.Abs(s.z * scale.z));
    }

    private static float PaddleScore(Renderer renderer, Bounds overall)
    {
        Vector3 size = LocalMeshSize(renderer);
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float middle = size.x + size.y + size.z - largest - smallest;
        float ratio = largest / Mathf.Max(0.01f, middle);
        Vector3 offset = renderer.bounds.center - overall.center;
        offset.y = 0f;
        float horizontalRadius = Mathf.Max(0.01f, Mathf.Max(overall.extents.x, overall.extents.z));
        float outside = offset.magnitude / horizontalRadius;
        return ratio * 7f + outside * 18f + largest - middle * 2f;
    }

    private static bool OppositeSides(Renderer a, Renderer b, Bounds overall)
    {
        Vector3 oa = a.bounds.center - overall.center;
        Vector3 ob = b.bounds.center - overall.center;
        oa.y = 0f; ob.y = 0f;
        if (oa.sqrMagnitude < 0.01f || ob.sqrMagnitude < 0.01f) return false;
        return Vector3.Dot(oa.normalized, ob.normalized) < -0.20f;
    }

    private static bool SimilarShape(Renderer a, Renderer b)
    {
        Vector3 sa = SortedSize(LocalMeshSize(a));
        Vector3 sb = SortedSize(LocalMeshSize(b));
        return Similar(sa.x, sb.x, 0.30f) && Similar(sa.y, sb.y, 0.30f) && Similar(sa.z, sb.z, 0.30f);
    }

    private static Vector3 SortedSize(Vector3 size)
    {
        float x = size.x, y = size.y, z = size.z;
        if (x > y) Swap(ref x, ref y);
        if (y > z) Swap(ref y, ref z);
        if (x > y) Swap(ref x, ref y);
        return new Vector3(x, y, z);
    }

    private static void Swap(ref float a, ref float b)
    {
        float t = a; a = b; b = t;
    }

    private static bool Similar(float a, float b, float tolerance)
    {
        float max = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
        return Mathf.Abs(a - b) / max <= tolerance;
    }

    private static bool SameMesh(Renderer a, Renderer b)
    {
        Mesh ma = GetMesh(a), mb = GetMesh(b);
        return ma != null && ma == mb;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static void AddUnique(List<Renderer> list, Renderer renderer)
    {
        if (renderer != null && !list.Contains(renderer)) list.Add(renderer);
    }

    private static int GetSubMeshCount(Renderer renderer)
    {
        Mesh mesh = GetMesh(renderer);
        return mesh != null ? mesh.subMeshCount : 0;
    }

    private static Material BuildPaddleMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No supported lit shader is available for the paddle material.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(PaddleMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PaddleMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.name = "PaddleComplete";
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
