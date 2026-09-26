using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Completes material assignment on the authored raft paddles only.
///
/// The Blender import can expose fewer material slots than a paddle mesh has
/// submeshes. Unity then draws the uncovered blade/end-cap submeshes with its grey
/// fallback material, while an unidentified paddle can inherit the raft texture.
/// This repair identifies the paddle renderer(s), expands every paddle to all of its
/// submeshes and maps the supplied Oar Texture.jpg across every one of those slots.
/// No raft/deck renderer is intentionally changed.
/// </summary>
public sealed class RaftPaddleTextureFix : AssetPostprocessor
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/Authored/Oar Texture.jpg";
    private const string MaterialFolder = "Assets/_Game/Boats/Raft/Authored/Materials";
    private const string PaddleMaterialPath = MaterialFolder + "/PaddleComplete.mat";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    // v2 intentionally reruns once on existing projects that already received v1.
    private const string MarkerName = "RaftPaddleTextureComplete_v2";

    [InitializeOnLoadMethod]
    private static void QueueInitialFix()
    {
        // Run one editor tick after the main raft importer gets its own delayCall.
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

                // Every material slot on an identified paddle receives ONLY the
                // user's oar texture, including the blade and both end-cap submeshes.
                paddle.sharedMaterials = completed;
                repairedSlots += completed.Length;
            }

            Transform oldV1 = wrapper.Find("RaftPaddleTextureComplete_v1");
            if (oldV1 != null) UnityEngine.Object.DestroyImmediate(oldV1.gameObject);
            Transform oldMarker = wrapper.Find(MarkerName);
            if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log("Raft paddle texture completed: " + paddles.Count + " paddle renderer(s), " + repairedSlots + " material slot(s), all using the supplied Oar Texture.jpg. Raft/deck materials were left alone.");
            if (force)
                EditorUtility.DisplayDialog("Raft Paddles Fixed", "The supplied Oar Texture.jpg is now mapped to every submesh of the identified paddles, including both end caps. The raft itself was not retextured.", "OK");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static List<Renderer> FindPaddleRenderers(Transform wrapper)
    {
        Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
        List<Renderer> explicitMatches = new List<Renderer>();
        List<Renderer> narrowCandidates = new List<Renderer>();
        if (renderers == null || renderers.Length == 0) return explicitMatches;

        bool haveOverall = false;
        Bounds overall = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (!haveOverall) { overall = renderer.bounds; haveOverall = true; }
            else overall.Encapsulate(renderer.bounds);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            if (HasPaddleName(renderer))
            {
                AddUnique(explicitMatches, renderer);
                continue;
            }

            if (LooksLikeNarrowPaddle(renderer))
                narrowCandidates.Add(renderer);
        }

        // Prefer authored names/material names when Blender supplied them. If only
        // one side carries a useful name, locate its same-shaped mirror so the other
        // oar cannot remain on the raft texture.
        if (explicitMatches.Count > 0)
        {
            if (explicitMatches.Count == 1)
            {
                Renderer mirror = BestMirror(explicitMatches[0], narrowCandidates, overall);
                if (mirror != null) AddUnique(explicitMatches, mirror);
            }
            return explicitMatches;
        }

        // Generic Blender names (Plane, Plane.001, ...) require geometry fallback.
        // Select a mirrored pair of long, narrow renderers on opposite sides of the
        // raft instead of blindly texturing every long plank on the deck.
        Renderer pairA = null, pairB = null;
        float bestPairScore = float.NegativeInfinity;
        for (int i = 0; i < narrowCandidates.Count; i++)
        for (int j = i + 1; j < narrowCandidates.Count; j++)
        {
            Renderer a = narrowCandidates[i], b = narrowCandidates[j];
            if (!SimilarShape(a, b)) continue;
            if (!OppositeSides(a, b, overall)) continue;

            float score = PaddleScore(a, overall) + PaddleScore(b, overall);
            if (SameMesh(a, b)) score += 12f;
            if (score > bestPairScore)
            {
                bestPairScore = score;
                pairA = a;
                pairB = b;
            }
        }

        List<Renderer> result = new List<Renderer>();
        if (pairA != null)
        {
            result.Add(pairA);
            result.Add(pairB);
            return result;
        }

        // A single renderer may contain both paddles. Keep the old conservative
        // fallback, choosing only the strongest candidate rather than touching raft
        // boards that merely happen to be narrow.
        Renderer best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < narrowCandidates.Count; i++)
        {
            float score = PaddleScore(narrowCandidates[i], overall);
            if (score > bestScore) { bestScore = score; best = narrowCandidates[i]; }
        }
        if (best != null) result.Add(best);
        return result;
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
            float value = PaddleScore(candidate, overall) + (SameMesh(source, candidate) ? 12f : 0f);
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
        Vector3 size = renderer.bounds.size;
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float middle = size.x + size.y + size.z - largest - smallest;
        float ratio = largest / Mathf.Max(0.01f, middle);
        return largest >= 1.65f && largest <= 4.5f && middle <= 1.15f && smallest <= 0.65f && ratio >= 2.8f;
    }

    private static float PaddleScore(Renderer renderer, Bounds overall)
    {
        Vector3 size = renderer.bounds.size;
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float middle = size.x + size.y + size.z - largest - smallest;
        float ratio = largest / Mathf.Max(0.01f, middle);
        Vector3 offset = renderer.bounds.center - overall.center;
        offset.y = 0f;
        float horizontalRadius = Mathf.Max(0.01f, Mathf.Max(overall.extents.x, overall.extents.z));
        float outside = offset.magnitude / horizontalRadius;
        return ratio * 6f + outside * 18f + largest - middle * 2f;
    }

    private static bool OppositeSides(Renderer a, Renderer b, Bounds overall)
    {
        Vector3 oa = a.bounds.center - overall.center;
        Vector3 ob = b.bounds.center - overall.center;
        oa.y = 0f; ob.y = 0f;
        if (oa.sqrMagnitude < 0.01f || ob.sqrMagnitude < 0.01f) return false;
        return Vector3.Dot(oa.normalized, ob.normalized) < -0.25f;
    }

    private static bool SimilarShape(Renderer a, Renderer b)
    {
        Vector3 sa = SortedSize(a.bounds.size);
        Vector3 sb = SortedSize(b.bounds.size);
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
