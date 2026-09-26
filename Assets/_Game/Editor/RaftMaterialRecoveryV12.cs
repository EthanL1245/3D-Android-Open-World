using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v12 final material recovery for the authored raft.
///
/// Root cause fixed here: RaftPaddleTextureFix used renderer-level assignment. Once
/// the merged raft renderer contained an oar-named material, that legacy pass could
/// treat the WHOLE merged raft renderer as a paddle and replace every material slot
/// with the oar material. That is why the oars looked correct while the raft itself
/// also became the oar texture, and why later material repairs appeared to be undone.
///
/// The legacy renderer-level fixer is now retired. v12 deliberately DOES NOT try to
/// rediscover paddle geometry. The working split already created a dedicated final
/// oar submesh. v12 keeps that mesh exactly as-is, restores genuine Raft_* materials
/// to every original submesh slot, and assigns OarUserTexture only to the final oar
/// slot. No vertices, triangles, UVs, transforms, animation, colliders or gameplay
/// components are changed.
/// </summary>
public static class RaftMaterialRecoveryV12
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string RaftTexturePath = "Assets/_Game/Boats/Raft/Authored/Raft Texture.jpg";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string AuthoredMaterialFolder = "Assets/_Game/Boats/Raft/Authored/Materials";
    private const string RecoveryFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string OarMaterialPath = RecoveryFolder + "/OarUserTexture.mat";
    private const string MarkerName = "RaftMaterialRecovery_v12";

    private static readonly string[] StopMarkers =
    {
        "RaftOarUserTexture_v7",
        "RaftOarUserTexture_v8",
        "RaftMaterialRecovery_v9",
        "RaftMaterialRecovery_v10",
        "RaftMaterialRecovery_v11"
    };

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        // Block all older automatic passes immediately, then restore the materials
        // after Unity has finished this script-reload/import cycle.
        EditorApplication.delayCall += InstallStopMarkers;
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Restore Raft Material + Keep Working Oars (v12)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v12 Recovery", "Exit Play Mode first.", "OK");
            return;
        }

        InstallStopMarkers();
        if (!Apply(true))
            EditorUtility.DisplayDialog(
                "Raft v12 Recovery",
                "No change was saved. Check the Console for [RAFT V12] diagnostics.",
                "OK");
    }

    private static void InstallStopMarkers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) return;

        bool missing = StopMarkers.Any(name => readWrapper.Find(name) == null);
        if (!missing) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return;
            bool changed = false;
            foreach (string name in StopMarkers)
            {
                if (wrapper.Find(name) != null) continue;
                new GameObject(name).transform.SetParent(wrapper, false);
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[RAFT V12] Disabled obsolete v7/v8/v9/v10/v11 automatic raft material passes.");
            }
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
        Texture2D raftTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (prefab == null || raftTexture == null || oarTexture == null)
        {
            Debug.LogError("[RAFT V12] Missing BaseBoat.prefab, Raft Texture.jpg, or Oar Texture.jpg.");
            return false;
        }

        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null)
        {
            Debug.LogError("[RAFT V12] Uploaded Raft Model wrapper is missing.");
            return false;
        }
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer target = FindExistingSplitRenderer(wrapper);
            Mesh mesh = GetMesh(target);
            if (target == null || mesh == null || mesh.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V12] Could not find the existing split raft renderer. No geometry/material was changed.");
                LogRendererInventory(wrapper);
                return false;
            }

            int oarSlot = mesh.subMeshCount - 1; // every v7+ split appends the oar slot last
            Material[] restored = new Material[mesh.subMeshCount];
            Renderer sourceRenderer = FindSourceRenderer(target);

            for (int slot = 0; slot < oarSlot; slot++)
            {
                restored[slot] = FindOriginalRaftMaterial(slot, raftTexture);
                if (restored[slot] == null)
                    restored[slot] = BuildFallbackRaftMaterial(sourceRenderer, slot, raftTexture);
                if (restored[slot] == null)
                {
                    Debug.LogError("[RAFT V12] Could not recover a raft material for source slot " + slot + ". No prefab change was saved.");
                    return false;
                }
                ForceTexture(restored[slot], raftTexture);
            }

            Material oarMaterial = BuildOarMaterial(oarTexture);
            if (oarMaterial == null)
            {
                Debug.LogError("[RAFT V12] Could not build the dedicated oar material.");
                return false;
            }
            restored[oarSlot] = oarMaterial;

            // MATERIALS ONLY. The currently-working split mesh and oar geometry are
            // intentionally left completely untouched.
            target.sharedMaterials = restored;
            EditorUtility.SetDirty(target);

            RemoveMarker(wrapper, MarkerName);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);
            foreach (string old in StopMarkers)
                if (wrapper.Find(old) == null) new GameObject(old).transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Verify what was actually serialized, not merely what we intended.
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform savedWrapper = saved != null ? saved.transform.Find(WrapperPath) : null;
            Renderer savedTarget = savedWrapper != null ? FindExistingSplitRenderer(savedWrapper) : null;
            Material[] check = savedTarget != null ? savedTarget.sharedMaterials : null;
            bool valid = check != null && check.Length >= mesh.subMeshCount;
            if (valid)
            {
                for (int i = 0; i < oarSlot; i++)
                    valid &= IsTexture(check[i], raftTexture);
                valid &= IsTexture(check[oarSlot], oarTexture);
            }

            string slotReport = string.Join(", ", restored.Select((m, i) =>
                i + "=" + (m != null ? m.name : "NULL") + "/" + TextureName(m)));

            if (!valid)
            {
                Debug.LogError("[RAFT V12] Save verification FAILED. Material slots after save: " + slotReport);
                return false;
            }

            Debug.Log(
                "[RAFT V12] SUCCESS — root cause removed and raft material restored WITHOUT changing the working oar split. " +
                "renderer=" + AnimationUtility.CalculateTransformPath(target.transform, root.transform) +
                " mesh=" + mesh.name + " submeshes=" + mesh.subMeshCount +
                " oarSlot=" + oarSlot + " slots: " + slotReport);

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft v12 Restored",
                    "The existing working oar mesh was kept exactly as-is. Original raft submesh slots now use Raft Texture.jpg, and only the final dedicated oar slot uses Oar Texture.jpg.",
                    "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V12] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindExistingSplitRenderer(Transform wrapper)
    {
        Renderer best = null;
        long bestScore = long.MinValue;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null || mesh.subMeshCount < 2) continue;
            bool splitName = mesh.name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0;
            Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
            bool hasOar = mats.Any(IsOarMaterial);
            long score = mesh.vertexCount + (splitName ? 10000000L : 0L) + (hasOar ? 5000000L : 0L);
            if (score > bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }
        return best;
    }

    private static Renderer FindSourceRenderer(Renderer target)
    {
        GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath);
        if (authored == null) return null;
        Renderer[] renderers = authored.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && GetMesh(r) != null).ToArray();
        if (renderers.Length == 0) return null;

        Renderer exact = renderers.FirstOrDefault(r => string.Equals(r.name, target.name, StringComparison.Ordinal));
        if (exact != null) return exact;
        int vertices = GetMesh(target) != null ? GetMesh(target).vertexCount : 0;
        return renderers.OrderBy(r => Mathf.Abs(GetMesh(r).vertexCount - vertices)).FirstOrDefault();
    }

    private static Material FindOriginalRaftMaterial(int slot, Texture2D raftTexture)
    {
        if (!AssetDatabase.IsValidFolder(AuthoredMaterialFolder)) return null;
        Material best = null;
        int bestScore = int.MinValue;
        string suffix = "_" + slot;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { AuthoredMaterialFolder }))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || IsOarMaterial(material)) continue;
            string name = material.name ?? string.Empty;
            if (!name.StartsWith("Raft_", StringComparison.OrdinalIgnoreCase)) continue;

            int score = 0;
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) score += 20;
            if (IsTexture(material, raftTexture)) score += 15;
            if (material.mainTexture != null && material.mainTexture.name.IndexOf("Raft", StringComparison.OrdinalIgnoreCase) >= 0) score += 10;
            if (score > bestScore) { bestScore = score; best = material; }
        }
        return best;
    }

    private static Material BuildFallbackRaftMaterial(Renderer sourceRenderer, int slot, Texture2D raftTexture)
    {
        Directory.CreateDirectory(RecoveryFolder);
        AssetDatabase.Refresh();
        string path = RecoveryFolder + "/RaftRestored_v12_slot" + slot + ".mat";
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material[] sourceMaterials = sourceRenderer != null ? sourceRenderer.sharedMaterials : null;
        Material template = sourceMaterials != null && sourceMaterials.Length > 0
            ? sourceMaterials[Mathf.Clamp(slot, 0, sourceMaterials.Length - 1)]
            : null;
        Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
        if (fallback == null) fallback = Shader.Find("Standard");

        if (result == null)
        {
            if (template != null && template.shader != null) result = new Material(template);
            else if (fallback != null) result = new Material(fallback);
            else return null;
            AssetDatabase.CreateAsset(result, path);
        }
        else if (template != null && template.shader != null)
        {
            EditorUtility.CopySerialized(template, result);
        }

        result.name = "RaftRestored_v12_slot" + slot;
        ForceTexture(result, raftTexture);
        return result;
    }

    private static Material BuildOarMaterial(Texture2D oarTexture)
    {
        Directory.CreateDirectory(RecoveryFolder);
        AssetDatabase.Refresh();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OarMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (material == null)
        {
            if (shader == null) return null;
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, OarMaterialPath);
        }
        else if (shader != null && material.shader == null)
        {
            material.shader = shader;
        }
        material.name = "OarUserTexture";
        ForceTexture(material, oarTexture);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .24f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ForceTexture(Material material, Texture2D texture)
    {
        if (material == null) return;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
    }

    private static bool IsOarMaterial(Material material)
    {
        if (material == null) return false;
        if ((material.name ?? string.Empty).IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        Texture texture = material.mainTexture;
        return texture != null && texture.name.IndexOf("Oar Texture", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTexture(Material material, Texture2D texture)
    {
        if (material == null || texture == null) return false;
        if (material.mainTexture == texture) return true;
        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == texture) return true;
        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == texture) return true;
        return false;
    }

    private static string TextureName(Material material)
    {
        if (material == null) return "NULL";
        Texture texture = material.mainTexture;
        return texture != null ? texture.name : "no-texture";
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static void RemoveMarker(Transform wrapper, string name)
    {
        Transform marker = wrapper.Find(name);
        if (marker != null) UnityEngine.Object.DestroyImmediate(marker.gameObject);
    }

    private static void LogRendererInventory(Transform wrapper)
    {
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null) continue;
            string mats = string.Join(",", (renderer.sharedMaterials ?? Array.Empty<Material>())
                .Select((m, i) => i + ":" + (m != null ? m.name : "NULL") + "/" + TextureName(m)));
            Debug.Log("[RAFT V12] renderer=" + AnimationUtility.CalculateTransformPath(renderer.transform, wrapper) +
                      " mesh=" + mesh.name + " vertices=" + mesh.vertexCount +
                      " submeshes=" + mesh.subMeshCount + " mats=[" + mats + "]");
        }
    }
}
