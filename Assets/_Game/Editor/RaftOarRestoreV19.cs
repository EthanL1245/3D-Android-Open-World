using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// v19 is a recovery + diagnostic pass after v18 was too broad.
///
/// What went wrong in v18:
/// it protected only ONE "best" split renderer. The user's raft actually contains
/// more than one split renderer/material pair involved in the paddle assembly, so
/// v18 could repaint another genuine moving-oar final submesh with Raft Texture.
///
/// v19 does NOT touch geometry. It restores OarUserTexture only to the LAST submesh
/// of every renderer that is demonstrably part of the existing oar-split lineage.
/// All non-final slots are left exactly as v18/v16 currently have them. This should
/// restore the previously-good moving oars while preserving the newly-corrected
/// stationary wood underneath/around them.
///
/// It also dumps an exact renderer/submesh/material map to the Console so if even one
/// face is still wrong, the next fix can target the exact path + slot rather than
/// guessing from screenshots.
/// </summary>
public static class RaftOarRestoreV19
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string OarMaterialPath = "Assets/_Game/Boats/Raft/UserTextures/OarUserTexture.mat";
    private const string MarkerName = "RaftOarRestore_v19";

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Restore Moving Oars + Dump Exact Raft Map (v19)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v19", "Exit Play Mode first.", "OK");
            return;
        }
        Apply(true);
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[RAFT V19] BaseBoat.prefab is missing."); return false; }
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) { Debug.LogError("[RAFT V19] Uploaded Raft Model wrapper is missing."); return false; }
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        Material oarMaterial = LoadOrBuildOarMaterial();
        if (oarMaterial == null) { Debug.LogError("[RAFT V19] Could not load/build OarUserTexture material."); return false; }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            int restored = 0;
            var restoredReport = new List<string>();
            foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = GetMesh(renderer);
                if (mesh == null || mesh.subMeshCount < 2) continue;
                Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
                if (mats.Length < mesh.subMeshCount) continue;

                string path = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform);
                int last = mesh.subMeshCount - 1;
                bool splitLineage = IsSplitLineage(mesh, path, renderer.name);
                bool finalLooksLikeOar = LooksLikeOarSubmesh(renderer, mesh, last, root.transform);
                bool finalWasV18 = IsV18Material(mats[last]);

                // Recovery is intentionally conservative: restore only a FINAL slot
                // that belongs to known split lineage, or a v18-repainted final slot
                // whose own submesh geometry is long/slender like a paddle.
                if ((splitLineage || (finalWasV18 && finalLooksLikeOar)) && !UsesOarTexture(mats[last]))
                {
                    Material[] updated = (Material[])mats.Clone();
                    updated[last] = oarMaterial;
                    renderer.sharedMaterials = updated;
                    EditorUtility.SetDirty(renderer);
                    restored++;
                    restoredReport.Add(path + " slot " + last + " mesh=" + mesh.name +
                        " lineage=" + splitLineage + " oarShape=" + finalLooksLikeOar);
                }
            }

            Transform old = wrapper.Find(MarkerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RAFT V19] RECOVERY COMPLETE — restored Oar Texture to " + restored +
                " final moving-oar slot(s). Non-final/stationary slots were NOT changed. " +
                (restoredReport.Count > 0 ? string.Join(" | ", restoredReport) : "No slot required restoration."));

            DumpExactMap(root, wrapper);

            if (force)
                EditorUtility.DisplayDialog("Raft v19",
                    "Moving-oar material recovery ran. The exact renderer/submesh/material map was printed as [RAFT V19 MAP] lines in the Console.", "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V19] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void DumpExactMap(GameObject root, Transform wrapper)
    {
        int rendererIndex = 0;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
            string path = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform);
            if (mesh == null)
            {
                Debug.Log("[RAFT V19 MAP] R" + rendererIndex++ + " path=" + path + " mesh=NULL mats=" + mats.Length);
                continue;
            }

            Bounds whole = RendererMeshBounds(renderer, mesh, root.transform);
            Debug.Log("[RAFT V19 MAP] R" + rendererIndex + " path=" + path +
                " renderer=" + renderer.GetType().Name + " mesh=" + mesh.name +
                " verts=" + mesh.vertexCount + " submeshes=" + mesh.subMeshCount +
                " wholeCenter=" + whole.center.ToString("F3") + " wholeSize=" + whole.size.ToString("F3") +
                " splitLineage=" + IsSplitLineage(mesh, path, renderer.name));

            int slots = Mathf.Max(mesh.subMeshCount, mats.Length);
            for (int slot = 0; slot < slots; slot++)
            {
                Material mat = slot < mats.Length ? mats[slot] : null;
                string matPath = mat != null ? AssetDatabase.GetAssetPath(mat) : "NULL";
                string tex = TextureName(mat);
                int tri = 0;
                Bounds sb = default;
                bool haveBounds = false;
                if (slot < mesh.subMeshCount && mesh.GetTopology(slot) == MeshTopology.Triangles)
                {
                    tri = (int)(mesh.GetIndexCount(slot) / 3);
                    haveBounds = TrySubmeshBounds(renderer, mesh, slot, root.transform, out sb);
                }
                Debug.Log("[RAFT V19 MAP]   R" + rendererIndex + " S" + slot +
                    " tris=" + tri +
                    (haveBounds ? " center=" + sb.center.ToString("F3") + " size=" + sb.size.ToString("F3") : "") +
                    " mat=" + (mat != null ? mat.name : "NULL") +
                    " tex=" + tex + " asset=" + matPath +
                    " v18=" + IsV18Material(mat) + " oar=" + UsesOarTexture(mat));
            }
            rendererIndex++;
        }
    }

    private static bool IsSplitLineage(Mesh mesh, string path, string rendererName)
    {
        string n = ((mesh != null ? mesh.name : "") + " " + path + " " + rendererName).ToLowerInvariant();
        return n.Contains("oarsplit") || n.Contains("v15") || n.Contains("v16") ||
               n.Contains("oarrest") || n.Contains("movingoar");
    }

    private static bool LooksLikeOarSubmesh(Renderer renderer, Mesh mesh, int slot, Transform root)
    {
        if (!TrySubmeshBounds(renderer, mesh, slot, root, out Bounds b)) return false;
        float[] s = { Mathf.Abs(b.size.x), Mathf.Abs(b.size.y), Mathf.Abs(b.size.z) };
        Array.Sort(s);
        float longest = s[2], second = s[1], shortest = s[0];
        int tris = mesh.GetTopology(slot) == MeshTopology.Triangles ? (int)(mesh.GetIndexCount(slot) / 3) : 0;
        return tris >= 2 && longest >= .55f &&
               (second <= longest * .48f || shortest <= longest * .16f);
    }

    private static bool TrySubmeshBounds(Renderer renderer, Mesh mesh, int slot, Transform root, out Bounds bounds)
    {
        bounds = default;
        if (renderer == null || mesh == null || slot < 0 || slot >= mesh.subMeshCount) return false;
        Vector3[] verts;
        try { verts = mesh.vertices; } catch { return false; }
        int[] idx;
        try { idx = mesh.GetIndices(slot); } catch { return false; }
        bool have = false;
        foreach (int i in idx)
        {
            if (i < 0 || i >= verts.Length) continue;
            Vector3 p = root.InverseTransformPoint(renderer.transform.TransformPoint(verts[i]));
            if (!have) { bounds = new Bounds(p, Vector3.zero); have = true; }
            else bounds.Encapsulate(p);
        }
        return have;
    }

    private static Bounds RendererMeshBounds(Renderer renderer, Mesh mesh, Transform root)
    {
        Bounds b = default; bool have = false;
        Vector3[] verts;
        try { verts = mesh.vertices; }
        catch { return new Bounds(root.InverseTransformPoint(renderer.bounds.center), renderer.bounds.size); }
        foreach (Vector3 v in verts)
        {
            Vector3 p = root.InverseTransformPoint(renderer.transform.TransformPoint(v));
            if (!have) { b = new Bounds(p, Vector3.zero); have = true; }
            else b.Encapsulate(p);
        }
        return b;
    }

    private static Material LoadOrBuildOarMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(OarMaterialPath);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (tex == null) return null;
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            m = new Material(shader) { name = "OarUserTexture" };
            string folder = System.IO.Path.GetDirectoryName(OarMaterialPath).Replace('\\','/');
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            AssetDatabase.CreateAsset(m, OarMaterialPath);
        }
        m.name = "OarUserTexture";
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .24f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static bool IsV18Material(Material m)
    {
        if (m == null) return false;
        string path = AssetDatabase.GetAssetPath(m) ?? "";
        return path.IndexOf("/V18StationaryWood/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               (m.name ?? "").StartsWith("RaftWood_v18_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesOarTexture(Material m)
    {
        if (m == null) return false;
        if ((m.name ?? "").IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        Texture t = m.mainTexture;
        return t != null && (t.name ?? "").IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TextureName(Material m)
    {
        if (m == null) return "NULL";
        Texture t = m.mainTexture;
        if (t == null && m.HasProperty("_BaseMap")) t = m.GetTexture("_BaseMap");
        if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
        return t != null ? t.name : "none";
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
        MeshFilter mf = renderer.GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }
}
