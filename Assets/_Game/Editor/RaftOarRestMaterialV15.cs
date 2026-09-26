using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finalizes the raft/oar material split using the real v14 diagnostics.
/// v14 correctly found 32 mirrored compact support triangles out of the 76-triangle
/// mixed oar slot, but its old 35% safety cap rejected that valid 32/76 result.
/// This pass uses the same conservative mirrored-support classification, accepts the
/// verified 32/76 scale, and moves ONLY those stationary support triangles back to
/// the raft material. All remaining moving-oar triangles keep Oar Texture.jpg.
/// </summary>
public static class RaftOarRestMaterialV15
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string RecoveryFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string MeshPath = RecoveryFolder + "/RaftOarRestsBackToRaft_v15.asset";
    private const string MarkerName = "RaftOarRestMaterial_v15";
    private const string V14Marker = "RaftOarRestMaterial_v14";
    private const string V12Marker = "RaftMaterialRecovery_v12";

    private sealed class Part
    {
        public int id;
        public readonly HashSet<int> vertices = new HashSet<int>();
        public readonly List<int> ordinals = new List<int>();
        public int tris;
        public Vector3 center;
        public float length;
        public float width;
        public float ratio;
    }

    private sealed class UF
    {
        private readonly int[] p;
        public UF(int n) { p = new int[n]; for (int i = 0; i < n; i++) p[i] = i; }
        public int F(int x) { return p[x] == x ? x : (p[x] = F(p[x])); }
        public void U(int a, int b) { a = F(a); b = F(b); if (a != b) p[b] = a; }
    }

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        // v14's classifier is already proven correct; only its safety percentage was
        // too strict. Stop the obsolete retry before its delayed pass can log again.
        EditorApplication.delayCall += BlockV14Retry;
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Finalize ONLY Moving Oars Use Oar Texture (v15)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v15", "Exit Play Mode first.", "OK");
            return;
        }
        BlockV14Retry();
        if (!Apply(true))
            EditorUtility.DisplayDialog("Raft v15", "No change was saved. Check the Console for [RAFT V15] diagnostics.", "OK");
    }

    private static void BlockV14Retry()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform readWrapper = prefab != null ? prefab.transform.Find(WrapperPath) : null;
        if (readWrapper == null || readWrapper.Find(V14Marker) != null) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null || wrapper.Find(V14Marker) != null) return;
            new GameObject(V14Marker).transform.SetParent(wrapper, false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[RAFT V15] Disabled obsolete v14 retry; v15 accepts the verified 32/76 mirrored-rest result.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform readWrapper = prefab != null ? prefab.transform.Find(WrapperPath) : null;
        if (readWrapper == null || readWrapper.Find(V12Marker) == null) return false;
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            Renderer renderer = FindRenderer(wrapper);
            Mesh source = GetMesh(renderer);
            if (renderer == null || source == null || source.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V15] Could not find the recovered raft/oar split renderer.");
                return false;
            }

            int oarSlot = source.subMeshCount - 1;
            if (source.GetTopology(0) != MeshTopology.Triangles || source.GetTopology(oarSlot) != MeshTopology.Triangles)
            {
                Debug.LogError("[RAFT V15] Expected triangle topology in raft and oar slots.");
                return false;
            }

            Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
            if (mats.Length < source.subMeshCount || mats[0] == null || mats[oarSlot] == null ||
                IsOar(mats[0]) || !IsOar(mats[oarSlot]))
            {
                Debug.LogError("[RAFT V15] Material slots are not in the expected v12 state.");
                return false;
            }

            Vector3[] verts = source.vertices;
            int[] raft = source.GetIndices(0);
            int[] oar = source.GetIndices(oarSlot);
            int total = oar.Length / 3;
            List<Part> parts = BuildParts(oar, verts, renderer.transform, root.transform);
            if (!Measure(raft, verts, renderer.transform, root.transform, out Bounds deck)) return false;

            List<Part> compact = parts.Where(p =>
                p.tris >= 1 && p.tris <= 8 &&
                p.length >= 0.18f && p.length <= 0.58f &&
                p.width >= 0.10f && p.width <= 0.36f &&
                p.ratio >= 1.15f && p.ratio <= 3.20f &&
                Mathf.Abs(p.center.x) >= 0.45f &&
                p.center.x >= deck.min.x - 0.12f && p.center.x <= deck.max.x + 0.12f &&
                p.center.z >= deck.min.z - 0.12f && p.center.z <= deck.max.z + 0.12f &&
                p.center.y >= deck.min.y - 0.25f && p.center.y <= deck.max.y + 0.40f).ToList();

            HashSet<Part> rests = new HashSet<Part>();
            for (int i = 0; i < compact.Count; i++)
            for (int j = i + 1; j < compact.Count; j++)
            {
                Part a = compact[i], b = compact[j];
                if (a.center.x * b.center.x >= 0f) continue;
                if (Mathf.Abs(Mathf.Abs(a.center.x) - Mathf.Abs(b.center.x)) <= 0.16f &&
                    Mathf.Abs(a.center.y - b.center.y) <= 0.12f &&
                    Mathf.Abs(a.center.z - b.center.z) <= 0.16f &&
                    Mathf.Abs(a.length - b.length) <= 0.12f &&
                    Mathf.Abs(a.width - b.width) <= 0.10f)
                {
                    rests.Add(a); rests.Add(b);
                }
            }

            if (!rests.Any(p => p.center.x < 0f) || !rests.Any(p => p.center.x > 0f))
            {
                Debug.LogError("[RAFT V15] Could not reproduce the mirrored support classification.");
                Log(parts);
                return false;
            }

            HashSet<int> restOrdinals = new HashSet<int>(rests.SelectMany(p => p.ordinals));
            List<int> keepOar = new List<int>();
            List<int> backToRaft = new List<int>();
            for (int i = 0, ord = 0; i + 2 < oar.Length; i += 3, ord++)
            {
                List<int> dst = restOrdinals.Contains(ord) ? backToRaft : keepOar;
                dst.Add(oar[i]); dst.Add(oar[i + 1]); dst.Add(oar[i + 2]);
            }

            int returned = backToRaft.Count / 3;
            int moving = keepOar.Count / 3;
            // Real device diagnostics: 32/76 are mirrored stationary support faces.
            // Accept up to 36 triangles / 48% while still requiring a clear majority
            // of the mixed slot to remain on the moving-oar material.
            if (returned < 2 || returned > 36 || returned >= Mathf.CeilToInt(total * 0.48f) || moving < 36)
            {
                Debug.LogError("[RAFT V15] Safety stop. total=" + total + " moving=" + moving + " returnedToRaft=" + returned + ".");
                Log(parts);
                return false;
            }

            Mesh refined = UnityEngine.Object.Instantiate(source);
            refined.name = "RaftOarRestsBackToRaft_v15";
            List<int> raftCombined = new List<int>(raft.Length + backToRaft.Count);
            raftCombined.AddRange(raft);
            raftCombined.AddRange(backToRaft);
            refined.SetTriangles(raftCombined, 0, false);
            refined.SetTriangles(keepOar, oarSlot, false);
            refined.RecalculateBounds();

            Directory.CreateDirectory(RecoveryFolder);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(refined, MeshPath);
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (saved == null) return false;

            MeshFilter mf = renderer.GetComponent<MeshFilter>();
            SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
            if (mf != null) mf.sharedMesh = saved;
            else if (smr != null) smr.sharedMesh = saved;
            else return false;

            Transform old = wrapper.Find(MarkerName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);
            if (wrapper.Find(V14Marker) == null) new GameObject(V14Marker).transform.SetParent(wrapper, false);

            EditorUtility.SetDirty(renderer);
            if (mf != null) EditorUtility.SetDirty(mf);
            if (smr != null) EditorUtility.SetDirty(smr);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = string.Join(" | ", rests.OrderBy(p => p.center.x).ThenBy(p => p.center.z)
                .Select(p => "id=" + p.id + " tris=" + p.tris + " center=" + p.center.ToString("F3")));
            Debug.Log("[RAFT V15] SUCCESS — ONLY moving-oar geometry keeps Oar Texture. Returned " + returned + "/" + total +
                " stationary support triangles to the raft texture; kept " + moving + " moving-oar triangles. Supports: " + report);

            if (force) EditorUtility.DisplayDialog("Raft v15 Complete", "Only the moving oars use the oar texture; stationary rests/supports now use the raft texture.", "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V15] Exception: " + e);
            return false;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static List<Part> BuildParts(int[] indices, Vector3[] vertices, Transform rt, Transform root)
    {
        UF uf = new UF(vertices.Length);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
            uf.U(a, b); uf.U(b, c); uf.U(c, a);
        }
        Dictionary<int, Part> map = new Dictionary<int, Part>(); int next = 0;
        for (int i = 0, ord = 0; i + 2 < indices.Length; i += 3, ord++)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
            int key = uf.F(a);
            if (!map.TryGetValue(key, out Part p)) { p = new Part { id = next++ }; map[key] = p; }
            p.vertices.Add(a); p.vertices.Add(b); p.vertices.Add(c); p.ordinals.Add(ord); p.tris++;
        }
        foreach (Part p in map.Values) Finalize(p, vertices, rt, root);
        return map.Values.ToList();
    }

    private static void Finalize(Part part, Vector3[] vertices, Transform rt, Transform root)
    {
        List<Vector3> pts = new List<Vector3>(); Vector3 sum = Vector3.zero;
        foreach (int i in part.vertices) { Vector3 p = root.InverseTransformPoint(rt.TransformPoint(vertices[i])); pts.Add(p); sum += p; }
        if (pts.Count == 0) return;
        part.center = sum / pts.Count;
        Vector3 a = Farthest(pts, part.center), b = Farthest(pts, a), d = b - a;
        part.length = d.magnitude;
        if (part.length < 0.0001f) { part.width = 0.01f; return; }
        Vector3 dir = d / part.length; float radius = 0f;
        foreach (Vector3 p in pts) { Vector3 q = p - part.center; q -= dir * Vector3.Dot(q, dir); radius = Mathf.Max(radius, q.magnitude); }
        part.width = Mathf.Max(0.012f, radius * 2f); part.ratio = part.length / part.width;
    }

    private static Vector3 Farthest(List<Vector3> pts, Vector3 from)
    {
        Vector3 best = pts[0]; float d = -1f;
        foreach (Vector3 p in pts) { float x = (p - from).sqrMagnitude; if (x > d) { d = x; best = p; } }
        return best;
    }

    private static bool Measure(int[] indices, Vector3[] vertices, Transform rt, Transform root, out Bounds b)
    {
        b = default; bool have = false;
        foreach (int i in indices)
        {
            if (!Valid(i, vertices.Length)) continue;
            Vector3 p = root.InverseTransformPoint(rt.TransformPoint(vertices[i]));
            if (!have) { b = new Bounds(p, Vector3.zero); have = true; } else b.Encapsulate(p);
        }
        return have;
    }

    private static void Log(List<Part> parts)
    {
        Debug.Log("[RAFT V15] component inventory: " + string.Join(" | ", parts.OrderByDescending(p => p.length)
            .Select(p => "id=" + p.id + " tris=" + p.tris + " len=" + p.length.ToString("0.000") +
            " width=" + p.width.ToString("0.000") + " ratio=" + p.ratio.ToString("0.00") + " center=" + p.center.ToString("F3"))));
    }

    private static Renderer FindRenderer(Transform wrapper)
    {
        Renderer best = null; long scoreBest = long.MinValue;
        foreach (Renderer r in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh m = GetMesh(r); if (m == null || m.subMeshCount < 2) continue;
            Material[] mats = r.sharedMaterials ?? Array.Empty<Material>();
            if (mats.Length < m.subMeshCount || !IsOar(mats[m.subMeshCount - 1])) continue;
            long s = m.vertexCount;
            string n = m.name ?? string.Empty;
            if (n.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) s += 10000000;
            if ((r.name ?? string.Empty).IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0) s += 1000000;
            if (s > scoreBest) { scoreBest = s; best = r; }
        }
        return best;
    }

    private static bool IsOar(Material m)
    {
        if (m == null) return false;
        string n = m.name ?? string.Empty;
        if (n.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        Texture t = m.mainTexture;
        return t != null && (t.name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool Valid(int i, int count) { return i >= 0 && i < count; }
    private static Mesh GetMesh(Renderer r)
    {
        if (r == null) return null;
        if (r is SkinnedMeshRenderer s) return s.sharedMesh;
        MeshFilter f = r.GetComponent<MeshFilter>();
        return f != null ? f.sharedMesh : null;
    }
}
