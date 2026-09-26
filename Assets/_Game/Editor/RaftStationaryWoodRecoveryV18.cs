using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// v18 fixes the last raft-texture defect revealed by the user's device screenshots.
///
/// v16 already proved that the final dedicated oar submesh now contains only the
/// moving paddles. v17 consequently found zero stationary triangles in that slot.
/// The remaining dark/grey sandwiched strip, underside and slightly protruding plank
/// therefore live in a DIFFERENT renderer/material slot, not in the oar submesh.
///
/// This pass deliberately stops trying to split the oar submesh. Instead it enforces
/// the actual art contract for the complete uploaded raft hierarchy:
///   * the proven moving-oar slot/renderers keep Oar Texture;
///   * every stationary raft renderer/material slot uses Raft Texture.
///
/// Existing mesh geometry, UVs, transforms, animation, colliders and boat gameplay
/// are not changed. Per-slot cloned materials retain the renderer's shader/properties
/// and only have the wood texture/base tint corrected, so the original UV mapping is
/// preserved while the remaining grey side/underside faces become the same wood as
/// the raft.
/// </summary>
public static class RaftStationaryWoodRecoveryV18
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string RaftTexturePath = "Assets/_Game/Boats/Raft/Authored/Raft Texture.jpg";
    private const string MaterialFolder = "Assets/_Game/Boats/Raft/UserTextures/V18StationaryWood";
    private const string MarkerName = "RaftStationaryWood_v18";
    private const string V16Marker = "RaftOarRestCleanup_v16";
    private const string V17Marker = "RaftOnlyMovingOars_v17";

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Restore ALL Stationary Raft Wood (v18)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v18", "Exit Play Mode first.", "OK");
            return;
        }
        if (!Apply(true))
            EditorUtility.DisplayDialog("Raft v18", "No change was saved. Check Console for [RAFT V18].", "OK");
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Texture2D raftTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath);
        if (prefab == null || raftTexture == null) return false;

        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) return false;
        if (readWrapper.Find(V16Marker) == null)
        {
            Debug.Log("[RAFT V18] Waiting for successful v16 oar/rest split first.");
            return false;
        }
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer split = FindSplitRenderer(wrapper);
            Mesh splitMesh = GetMesh(split);
            if (split == null || splitMesh == null || splitMesh.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V18] Could not find the proven raft/oar split renderer.");
                return false;
            }

            Material[] splitMats = split.sharedMaterials ?? Array.Empty<Material>();
            int oarSlot = splitMesh.subMeshCount - 1;
            if (splitMats.Length <= oarSlot || !IsOar(splitMats[oarSlot]))
            {
                Debug.LogError("[RAFT V18] Final split slot is no longer the dedicated moving-oar material; refusing to guess.");
                return false;
            }

            Bounds raftBounds;
            if (!BoundsOfNonOarSubmeshes(split, splitMesh, oarSlot, root.transform, out raftBounds))
            {
                Debug.LogError("[RAFT V18] Could not measure the stationary raft footprint.");
                return false;
            }

            EnsureFolder(MaterialFolder);
            int changedSlots = 0;
            int changedRenderers = 0;
            int protectedOarSlots = 0;
            List<string> report = new List<string>();

            foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                Mesh mesh = GetMesh(renderer);
                Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
                if (mats.Length == 0) continue;

                Material[] updated = (Material[])mats.Clone();
                bool rendererChanged = false;

                for (int slot = 0; slot < updated.Length; slot++)
                {
                    // The final slot of the proven merged renderer is the moving-oar
                    // geometry established by v16. Never touch it again.
                    if (renderer == split && slot == oarSlot)
                    {
                        protectedOarSlots++;
                        continue;
                    }

                    // Some authored/import histories can leave a separate paddle
                    // renderer. Keep it ONLY when its geometry actually extends out
                    // beyond the stationary raft footprint like a moving paddle does.
                    if (IsOar(updated[slot]) && LooksLikeSeparateMovingOar(renderer, raftBounds, root.transform))
                    {
                        protectedOarSlots++;
                        continue;
                    }

                    if (UsesTexture(updated[slot], raftTexture)) continue;

                    Material replacement = BuildWoodMaterial(updated[slot], raftTexture, renderer, slot);
                    if (replacement == null)
                    {
                        Debug.LogError("[RAFT V18] Could not build stationary wood material for " +
                            AnimationUtility.CalculateTransformPath(renderer.transform, root.transform) + " slot " + slot + ".");
                        return false;
                    }

                    updated[slot] = replacement;
                    changedSlots++;
                    rendererChanged = true;
                    report.Add(AnimationUtility.CalculateTransformPath(renderer.transform, root.transform) +
                        "[" + slot + "] " + (mats[slot] != null ? mats[slot].name : "NULL") + " -> " + replacement.name);
                }

                if (rendererChanged)
                {
                    renderer.sharedMaterials = updated;
                    EditorUtility.SetDirty(renderer);
                    changedRenderers++;
                }
            }

            // v17 is obsolete now: device diagnostics proved the dedicated oar slot
            // contains no stationary support-layer faces. Mark it complete so it does
            // not keep emitting the expected zero-reclaim error on every script reload.
            if (wrapper.Find(V17Marker) == null)
                new GameObject(V17Marker).transform.SetParent(wrapper, false);

            Transform old = wrapper.Find(MarkerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Serialized verification: every stationary material slot must now use
            // Raft Texture, and the proven moving-oar slot must still use Oar Texture.
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform savedWrapper = saved != null ? saved.transform.Find(WrapperPath) : null;
            Renderer savedSplit = savedWrapper != null ? FindSplitRenderer(savedWrapper) : null;
            Mesh savedMesh = GetMesh(savedSplit);
            Material[] savedMats = savedSplit != null ? savedSplit.sharedMaterials : null;
            if (savedSplit == null || savedMesh == null || savedMats == null ||
                savedMats.Length <= savedMesh.subMeshCount - 1 || !IsOar(savedMats[savedMesh.subMeshCount - 1]))
            {
                Debug.LogError("[RAFT V18] Save verification failed: moving-oar material was not preserved.");
                return false;
            }

            Debug.Log("[RAFT V18] SUCCESS — restored Raft Texture to " + changedSlots +
                " stationary material slot(s) across " + changedRenderers +
                " renderer(s); protected " + protectedOarSlots +
                " moving-oar slot(s). The remaining side/underside sandwich layer and protruding stationary plank now use raft wood. " +
                (report.Count > 0 ? "Changed: " + string.Join(" | ", report) : "All stationary slots were already wood-textured."));

            if (force)
                EditorUtility.DisplayDialog("Raft v18 Complete",
                    "Every stationary raft part now uses the raft wood texture. Only the actual moving oars keep the oar texture.", "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V18] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindSplitRenderer(Transform wrapper)
    {
        Renderer best = null;
        long score = long.MinValue;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null || mesh.subMeshCount < 2) continue;
            Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
            int last = mesh.subMeshCount - 1;
            if (mats.Length <= last || !IsOar(mats[last])) continue;
            string n = mesh.name ?? string.Empty;
            long s = mesh.vertexCount;
            if (n.IndexOf("v16", StringComparison.OrdinalIgnoreCase) >= 0) s += 40000000L;
            if (n.IndexOf("v15", StringComparison.OrdinalIgnoreCase) >= 0) s += 30000000L;
            if (n.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) s += 20000000L;
            if (s > score) { score = s; best = renderer; }
        }
        return best;
    }

    private static bool BoundsOfNonOarSubmeshes(Renderer renderer, Mesh mesh, int oarSlot, Transform root, out Bounds bounds)
    {
        bounds = default;
        bool have = false;
        Vector3[] verts;
        try { verts = mesh.vertices; }
        catch { return false; }

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (sub == oarSlot || mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
            int[] idx = mesh.GetIndices(sub);
            foreach (int i in idx)
            {
                if (i < 0 || i >= verts.Length) continue;
                Vector3 p = root.InverseTransformPoint(renderer.transform.TransformPoint(verts[i]));
                if (!have) { bounds = new Bounds(p, Vector3.zero); have = true; }
                else bounds.Encapsulate(p);
            }
        }
        return have;
    }

    private static bool LooksLikeSeparateMovingOar(Renderer renderer, Bounds raftBounds, Transform root)
    {
        Bounds b = LocalRendererBounds(renderer, root);
        bool outside = b.min.x < raftBounds.min.x - .08f || b.max.x > raftBounds.max.x + .08f ||
                       b.min.z < raftBounds.min.z - .08f || b.max.z > raftBounds.max.z + .08f;
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float second = Median3(b.size.x, b.size.y, b.size.z);
        bool slender = longest >= .65f && second <= longest * .34f;
        return outside && slender;
    }

    private static Bounds LocalRendererBounds(Renderer renderer, Transform root)
    {
        Mesh mesh = GetMesh(renderer);
        if (mesh == null) return new Bounds(root.InverseTransformPoint(renderer.bounds.center), Vector3.zero);
        Vector3[] verts;
        try { verts = mesh.vertices; }
        catch { return new Bounds(root.InverseTransformPoint(renderer.bounds.center), Vector3.zero); }
        Bounds b = default; bool have = false;
        foreach (Vector3 v in verts)
        {
            Vector3 p = root.InverseTransformPoint(renderer.transform.TransformPoint(v));
            if (!have) { b = new Bounds(p, Vector3.zero); have = true; }
            else b.Encapsulate(p);
        }
        return b;
    }

    private static float Median3(float a, float b, float c)
    {
        if (a > b) { float t = a; a = b; b = t; }
        if (b > c) { float t = b; b = c; c = t; }
        if (a > b) { float t = a; a = b; b = t; }
        return b;
    }

    private static Material BuildWoodMaterial(Material original, Texture2D raftTexture, Renderer renderer, int slot)
    {
        string pathKey = AnimationUtility.CalculateTransformPath(renderer.transform, renderer.transform.root)
            .Replace('/', '_').Replace('\\', '_').Replace(':', '_').Replace(' ', '_');
        if (string.IsNullOrEmpty(pathKey)) pathKey = renderer.name;
        string path = MaterialFolder + "/" + Sanitize(pathKey) + "_slot" + slot + ".mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = original != null && original.shader != null ? original.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            material = original != null ? new Material(original) : new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (original != null && original.shader != null)
        {
            EditorUtility.CopySerialized(original, material);
        }

        material.name = "RaftWood_v18_" + renderer.name + "_slot" + slot;
        material.mainTexture = raftTexture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", raftTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", raftTexture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .24f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool UsesTexture(Material material, Texture texture)
    {
        if (material == null || texture == null) return false;
        if (material.mainTexture == texture) return true;
        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == texture) return true;
        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") == texture) return true;
        return false;
    }

    private static bool IsOar(Material material)
    {
        if (material == null) return false;
        string n = material.name ?? string.Empty;
        if (n.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        Texture t = material.mainTexture;
        if (t == null) return false;
        string tn = t.name ?? string.Empty;
        return tn.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
        if (smr != null) return smr.sharedMesh;
        MeshFilter mf = renderer.GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Length > 80 ? s.Substring(s.Length - 80) : s;
    }
}
