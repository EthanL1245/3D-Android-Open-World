using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v9 recovery for the authored raft.
///
/// Earlier oar repairs proved the two oars can be isolated, but the prefab's current
/// material slots are no longer trustworthy: a contaminated split can make raft/deck
/// geometry inherit Oar Texture.jpg. This pass does NOT preserve current raft
/// materials. It rebuilds from the untouched Raft.blend mesh/materials, explicitly
/// restores Raft Texture.jpg to every original source submesh, and uses the CURRENT
/// oar split only as a hint for which connected components are really the two oars.
/// Any non-oar triangles that were accidentally moved into the oar slot are therefore
/// returned to their original source submesh/material.
/// </summary>
public static class RaftMaterialRecoveryV9
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string RaftTexturePath = "Assets/_Game/Boats/Raft/Authored/Raft Texture.jpg";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string AssetFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string SplitMeshPath = AssetFolder + "/RaftOarSplit_v9.asset";
    private const string OarMaterialPath = AssetFolder + "/OarUserTexture.mat";
    private const string MarkerName = "RaftMaterialRecovery_v9";

    private struct TriKey : IEquatable<TriKey>
    {
        public int a, b, c;
        public TriKey(int x, int y, int z)
        {
            if (x > y) Swap(ref x, ref y);
            if (y > z) Swap(ref y, ref z);
            if (x > y) Swap(ref x, ref y);
            a = x; b = y; c = z;
        }
        private static void Swap(ref int x, ref int y) { int t = x; x = y; y = t; }
        public bool Equals(TriKey other) { return a == other.a && b == other.b && c == other.c; }
        public override bool Equals(object obj) { return obj is TriKey && Equals((TriKey)obj); }
        public override int GetHashCode()
        {
            unchecked { return ((a * 397) ^ b) * 397 ^ c; }
        }
    }

    private sealed class Part
    {
        public int id;
        public readonly HashSet<int> vertices = new HashSet<int>();
        public readonly HashSet<TriKey> triangles = new HashSet<TriKey>();
        public int triangleCount;
        public Bounds bounds;
        public Vector3 center;
        public Vector3 endpointA;
        public Vector3 endpointB;
        public Vector3 direction;
        public float length;
        public float width;
        public float ratio;
        public float diagonal;
        public float lateral;
    }

    private sealed class UnionFind
    {
        private readonly int[] parent;
        private readonly byte[] rank;
        public UnionFind(int count)
        {
            parent = new int[count]; rank = new byte[count];
            for (int i = 0; i < count; i++) parent[i] = i;
        }
        public int Find(int x)
        {
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }
        public void Union(int a, int b)
        {
            a = Find(a); b = Find(b); if (a == b) return;
            if (rank[a] < rank[b]) parent[a] = b;
            else if (rank[a] > rank[b]) parent[b] = a;
            else { parent[b] = a; rank[a]++; }
        }
    }

    [InitializeOnLoadMethod]
    private static void QueueAutomaticRecovery()
    {
        // Run after the v6/v7/v8 migration queues. v9 is the final authority for the
        // raft material and records all newer markers so old passes stay dormant.
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () =>
                            EditorApplication.delayCall += () =>
                                EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Restore Original Raft Texture (v9)")]
    private static void ForceRecovery()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft Recovery", "Exit Play Mode first.", "OK");
            return;
        }
        if (!Apply(true))
            EditorUtility.DisplayDialog("Raft Recovery", "No change was saved. Check the Console for [RAFT V9] diagnostics.", "OK");
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        EnsureReadable(BlendPath);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath);
        Texture2D raftTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (prefab == null || authored == null || raftTexture == null || oarTexture == null)
        {
            Debug.LogError("[RAFT V9] Missing BaseBoat.prefab, Raft.blend, Raft Texture.jpg, or Oar Texture.jpg.");
            return false;
        }

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        if (prefabWrapper == null)
        {
            Debug.LogError("[RAFT V9] Uploaded raft wrapper is missing.");
            return false;
        }
        if (!force && prefabWrapper.Find(MarkerName) != null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            Transform model = wrapper != null ? FindAuthoredModelRoot(wrapper) : null;
            if (wrapper == null || model == null)
            {
                Debug.LogError("[RAFT V9] Could not resolve the authored raft model in BaseBoat.prefab.");
                return false;
            }

            Renderer target = FindSplitRenderer(model);
            Mesh currentMesh = GetMesh(target);
            if (target == null || currentMesh == null || currentMesh.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V9] Could not find the current split raft/oar renderer.");
                return false;
            }

            Renderer sourceRenderer = FindMatchingSourceRenderer(authored.transform, model, target);
            Mesh sourceMesh = GetMesh(sourceRenderer);
            if (sourceRenderer == null || sourceMesh == null)
            {
                Debug.LogError("[RAFT V9] Could not map the prefab renderer back to untouched Raft.blend.");
                return false;
            }

            int currentOarSubmesh = FindCurrentOarSubmesh(target, currentMesh);
            if (currentOarSubmesh < 0)
            {
                Debug.LogError("[RAFT V9] Could not locate the current dedicated oar submesh.");
                return false;
            }

            List<Part> parts = BuildPartsFromSubmesh(currentMesh, currentOarSubmesh, target.transform, root.transform);
            if (parts.Count < 2)
            {
                Debug.LogError("[RAFT V9] Current oar submesh does not contain enough connected components.");
                return false;
            }

            Vector3 overallCenter = CalculateRendererCenter(target);
            if (!SelectTrueOarPair(parts, root.transform, overallCenter, out Part left, out Part right))
            {
                LogParts(parts, overallCenter);
                Debug.LogError("[RAFT V9] Could not isolate the two true oars inside the current oar submesh.");
                return false;
            }

            HashSet<Part> selected = new HashSet<Part> { left, right };
            IncludeEndpointPieces(parts, left, selected);
            IncludeEndpointPieces(parts, right, selected);

            HashSet<TriKey> oarTriangles = new HashSet<TriKey>();
            foreach (Part part in selected)
                foreach (TriKey key in part.triangles) oarTriangles.Add(key);

            Material[] raftMaterials = BuildSourceRaftMaterials(sourceRenderer, sourceMesh.subMeshCount, raftTexture);
            Material oarMaterial = BuildOarMaterial(oarTexture);
            int moved = BuildCleanSplit(sourceMesh, target, oarTriangles, raftMaterials, oarMaterial);
            if (moved <= 0)
            {
                Debug.LogError("[RAFT V9] Oar components were selected but did not map back to untouched source triangles.");
                return false;
            }

            EnsureMarker(wrapper, "RaftOarUserTexture_v6");
            EnsureMarker(wrapper, "RaftOarUserTexture_v7");
            EnsureMarker(wrapper, "RaftOarUserTexture_v8");
            EnsureMarker(wrapper, MarkerName);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int currentOarTriangles = currentMesh.GetTopology(currentOarSubmesh) == MeshTopology.Triangles
                ? currentMesh.GetIndices(currentOarSubmesh).Length / 3 : 0;
            Debug.Log("[RAFT V9] SUCCESS. Rebuilt from untouched Raft.blend. Original source submeshes=" +
                sourceMesh.subMeshCount + ", prior oar-slot triangles=" + currentOarTriangles +
                ", true oar triangles kept=" + moved + ", selected components=" + selected.Count +
                ". Every original source submesh now explicitly uses Raft Texture.jpg; only the final oar submesh uses Oar Texture.jpg.");

            if (force)
                EditorUtility.DisplayDialog("Raft Recovered", "Original raft texture restored from Raft.blend/Raft Texture.jpg. Oars keep their separate oar texture.", "OK");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[RAFT V9] Exception: " + exception);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindSplitRenderer(Transform model)
    {
        Renderer best = null; int bestScore = int.MinValue;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer); if (mesh == null) continue;
            int score = mesh.vertexCount;
            if (mesh.name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) score += 1000000;
            Material[] materials = renderer.sharedMaterials;
            if (materials != null && materials.Any(IsOarMaterial)) score += 500000;
            if (score > bestScore) { bestScore = score; best = renderer; }
        }
        return best;
    }

    private static int FindCurrentOarSubmesh(Renderer renderer, Mesh mesh)
    {
        Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
        for (int i = 0; i < materials.Length && i < mesh.subMeshCount; i++)
            if (IsOarMaterial(materials[i])) return i;
        if (mesh.name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0 && mesh.subMeshCount >= 2)
            return mesh.subMeshCount - 1;
        return -1;
    }

    private static bool IsOarMaterial(Material material)
    {
        if (material == null) return false;
        if (material.name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        Texture texture = material.mainTexture;
        return texture != null && texture.name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Renderer FindMatchingSourceRenderer(Transform authoredRoot, Transform model, Renderer target)
    {
        string path = AnimationUtility.CalculateTransformPath(target.transform, model);
        Transform byPath = string.IsNullOrEmpty(path) ? authoredRoot : authoredRoot.Find(path);
        Renderer renderer = byPath != null ? byPath.GetComponent<Renderer>() : null;
        if (renderer != null && GetMesh(renderer) != null) return renderer;

        Renderer[] all = authoredRoot.GetComponentsInChildren<Renderer>(true);
        Renderer sameName = all.FirstOrDefault(r => r != null && r.name == target.name && GetMesh(r) != null);
        if (sameName != null) return sameName;
        return all.Where(r => GetMesh(r) != null).OrderByDescending(r => GetMesh(r).vertexCount).FirstOrDefault();
    }

    private static List<Part> BuildPartsFromSubmesh(Mesh mesh, int submesh, Transform rendererTransform, Transform boatRoot)
    {
        List<Part> result = new List<Part>();
        if (mesh.GetTopology(submesh) != MeshTopology.Triangles) return result;
        int[] indices = mesh.GetIndices(submesh);
        Vector3[] vertices = mesh.vertices;
        UnionFind uf = new UnionFind(vertices.Length);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
            uf.Union(a, b); uf.Union(b, c); uf.Union(c, a);
        }

        Dictionary<int,Part> map = new Dictionary<int,Part>(); int nextId = 0;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
            int key = uf.Find(a);
            if (!map.TryGetValue(key, out Part part))
            {
                part = new Part { id = nextId++ }; map.Add(key, part);
            }
            part.vertices.Add(a); part.vertices.Add(b); part.vertices.Add(c);
            part.triangles.Add(new TriKey(a, b, c)); part.triangleCount++;
        }
        foreach (Part part in map.Values) FinalizePart(part, vertices, rendererTransform, boatRoot);
        result.AddRange(map.Values); return result;
    }

    private static void FinalizePart(Part part, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        List<Vector3> points = new List<Vector3>(part.vertices.Count); Vector3 sum = Vector3.zero;
        bool have = false; Bounds bounds = default;
        foreach (int index in part.vertices)
        {
            Vector3 p = rendererTransform.TransformPoint(vertices[index]); points.Add(p); sum += p;
            if (!have) { bounds = new Bounds(p, Vector3.zero); have = true; } else bounds.Encapsulate(p);
        }
        part.bounds = bounds; part.center = points.Count > 0 ? sum / points.Count : bounds.center;
        if (points.Count < 2) return;
        Vector3 a = Farthest(points, part.center); Vector3 b = Farthest(points, a); Vector3 delta = b - a;
        part.length = delta.magnitude; if (part.length < .0001f) return;
        part.direction = delta / part.length; part.endpointA = a; part.endpointB = b;
        float radius = 0f;
        foreach (Vector3 p in points)
        {
            Vector3 d = p - part.center; Vector3 perpendicular = d - part.direction * Vector3.Dot(d, part.direction);
            radius = Mathf.Max(radius, perpendicular.magnitude);
        }
        part.width = Mathf.Max(.015f, radius * 2f); part.ratio = part.length / part.width;
        Vector3 horizontal = Vector3.ProjectOnPlane(part.direction, boatRoot.up);
        if (horizontal.sqrMagnitude > .0001f)
        {
            horizontal.Normalize(); float f = Mathf.Abs(Vector3.Dot(horizontal, boatRoot.forward));
            float s = Mathf.Abs(Vector3.Dot(horizontal, boatRoot.right)); part.diagonal = Mathf.Clamp01(2f * Mathf.Min(f, s));
        }
    }

    private static bool SelectTrueOarPair(List<Part> parts, Transform boatRoot, Vector3 center, out Part bestA, out Part bestB)
    {
        bestA = null; bestB = null; float bestScore = float.NegativeInfinity;
        foreach (Part p in parts) p.lateral = Vector3.Dot(p.center - center, boatRoot.right);
        List<Part> candidates = parts.Where(p => p.length >= .75f && p.length <= 5f && p.ratio >= 2.2f &&
            p.diagonal >= .22f && p.triangleCount >= 4 && Mathf.Abs(p.lateral) >= .08f).ToList();
        for (int i = 0; i < candidates.Count; i++)
        for (int j = i + 1; j < candidates.Count; j++)
        {
            Part a = candidates[i], b = candidates[j]; if (a.lateral * b.lateral >= 0f) continue;
            float lenSim = Mathf.Min(a.length, b.length) / Mathf.Max(a.length, b.length);
            float widSim = Mathf.Min(a.width, b.width) / Mathf.Max(a.width, b.width);
            if (lenSim < .62f || widSim < .30f) continue;
            float score = (a.diagonal + b.diagonal) * 20f + (a.ratio + b.ratio) * 2.5f +
                (a.length + b.length) * 4f + lenSim * 16f + widSim * 5f +
                (Mathf.Abs(a.lateral) + Mathf.Abs(b.lateral)) * 3f;
            if (score > bestScore) { bestScore = score; bestA = a; bestB = b; }
        }
        return bestA != null && bestB != null;
    }

    private static void IncludeEndpointPieces(List<Part> all, Part core, HashSet<Part> selected)
    {
        foreach (Part candidate in all)
        {
            if (candidate == core || selected.Contains(candidate)) continue;
            Vector3 size = candidate.bounds.size; float maxDimension = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDimension > .90f || candidate.triangleCount > 1800) continue;
            float da = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointA), core.endpointA);
            float db = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointB), core.endpointB);
            if (Mathf.Min(da, db) > .26f) continue;
            Vector3 relative = candidate.center - core.center; float axial = Vector3.Dot(relative, core.direction);
            Vector3 perpendicular = relative - core.direction * axial;
            if (perpendicular.magnitude > Mathf.Max(.16f, core.width * .65f)) continue;
            if (Mathf.Abs(axial) < core.length * .32f) continue;
            selected.Add(candidate);
        }
    }

    private static int BuildCleanSplit(Mesh source, Renderer target, HashSet<TriKey> selected,
        Material[] raftMaterials, Material oarMaterial)
    {
        List<int>[] keep = new List<int>[source.subMeshCount]; List<int> oar = new List<int>(); int moved = 0;
        for (int s = 0; s < source.subMeshCount; s++)
        {
            int[] indices = source.GetIndices(s); keep[s] = new List<int>(indices.Length);
            MeshTopology topology = source.GetTopology(s);
            if (topology != MeshTopology.Triangles) { keep[s].AddRange(indices); continue; }
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (selected.Contains(new TriKey(a, b, c))) { oar.Add(a); oar.Add(b); oar.Add(c); moved++; }
                else { keep[s].Add(a); keep[s].Add(b); keep[s].Add(c); }
            }
        }
        if (moved <= 0) return 0;

        Mesh split = UnityEngine.Object.Instantiate(source); split.name = source.name + "_OarSplit_v9";
        int original = source.subMeshCount; split.subMeshCount = original + 1;
        for (int s = 0; s < original; s++)
        {
            MeshTopology topology = source.GetTopology(s);
            if (topology == MeshTopology.Triangles) split.SetTriangles(keep[s], s, false);
            else split.SetIndices(keep[s].ToArray(), topology, s, false);
        }
        split.SetTriangles(oar, original, false); split.RecalculateBounds();
        if (AssetDatabase.LoadAssetAtPath<Mesh>(SplitMeshPath) != null) AssetDatabase.DeleteAsset(SplitMeshPath);
        AssetDatabase.CreateAsset(split, SplitMeshPath); split = AssetDatabase.LoadAssetAtPath<Mesh>(SplitMeshPath);

        MeshFilter filter = target.GetComponent<MeshFilter>(); SkinnedMeshRenderer skinned = target as SkinnedMeshRenderer;
        if (filter != null) filter.sharedMesh = split; else if (skinned != null) skinned.sharedMesh = split; else return 0;
        Material[] materials = new Material[original + 1];
        for (int s = 0; s < original; s++) materials[s] = s < raftMaterials.Length ? raftMaterials[s] : raftMaterials[0];
        materials[original] = oarMaterial; target.sharedMaterials = materials;
        EditorUtility.SetDirty(target); if (filter != null) EditorUtility.SetDirty(filter); if (skinned != null) EditorUtility.SetDirty(skinned);
        return moved;
    }

    private static Material[] BuildSourceRaftMaterials(Renderer sourceRenderer, int count, Texture2D raftTexture)
    {
        Directory.CreateDirectory(AssetFolder); Material[] source = sourceRenderer.sharedMaterials ?? Array.Empty<Material>();
        Material fallbackSource = source.FirstOrDefault(m => m != null);
        Material[] result = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < result.Length; i++)
        {
            Material template = i < source.Length && source[i] != null ? source[i] : fallbackSource;
            string path = AssetFolder + "/RaftRestored_v9_" + i + ".mat";
            result[i] = BuildTexturedMaterial(path, template, raftTexture, "RaftRestored_v9_" + i);
        }
        return result;
    }

    private static Material BuildOarMaterial(Texture2D oarTexture)
    {
        return BuildTexturedMaterial(OarMaterialPath, AssetDatabase.LoadAssetAtPath<Material>(OarMaterialPath), oarTexture, "OarUserTexture");
    }

    private static Material BuildTexturedMaterial(string path, Material template, Texture2D texture, string assetName)
    {
        Shader fallback = Shader.Find("Universal Render Pipeline/Lit"); if (fallback == null) fallback = Shader.Find("Standard");
        if (fallback == null) throw new InvalidOperationException("No supported lit shader exists for raft recovery.");
        bool validTemplate = template != null && template.shader != null &&
            !template.shader.name.Contains("InternalError") && !template.shader.name.Contains("Hidden/Internal");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = validTemplate ? new Material(template) : new Material(fallback); AssetDatabase.CreateAsset(material, path);
        }
        else if (validTemplate && material != template) EditorUtility.CopySerialized(template, material);
        else if (!validTemplate) material.shader = fallback;
        material.name = assetName; material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material); return material;
    }

    private static void EnsureReadable(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null && !importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null; SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh; MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static Transform FindAuthoredModelRoot(Transform wrapper)
    {
        for (int i = 0; i < wrapper.childCount; i++)
        {
            Transform child = wrapper.GetChild(i); if (child != null && child.GetComponentInChildren<Renderer>(true) != null) return child;
        }
        return null;
    }

    private static void EnsureMarker(Transform wrapper, string name)
    {
        if (wrapper.Find(name) != null) return; GameObject marker = new GameObject(name); marker.transform.SetParent(wrapper, false);
    }

    private static Vector3 CalculateRendererCenter(Renderer renderer) { return renderer != null ? renderer.bounds.center : Vector3.zero; }
    private static bool Valid(int index, int count) { return index >= 0 && index < count; }
    private static Vector3 Farthest(List<Vector3> points, Vector3 from)
    {
        Vector3 best = points[0]; float bestDistance = -1f;
        foreach (Vector3 point in points) { float d = (point - from).sqrMagnitude; if (d > bestDistance) { bestDistance = d; best = point; } }
        return best;
    }

    private static void LogParts(List<Part> parts, Vector3 center)
    {
        Debug.Log("[RAFT V9] Current oar-slot component inventory:\n" + string.Join("\n", parts.OrderByDescending(p => p.diagonal * p.ratio)
            .Select(p => "  id=" + p.id + " tris=" + p.triangleCount +
                " center=(" + (p.center.x-center.x).ToString("0.00") + "," + (p.center.y-center.y).ToString("0.00") + "," + (p.center.z-center.z).ToString("0.00") + ")" +
                " len=" + p.length.ToString("0.00") + " width=" + p.width.ToString("0.00") +
                " ratio=" + p.ratio.ToString("0.00") + " diagonal=" + p.diagonal.ToString("0.00") + " lateral=" + p.lateral.ToString("0.00"))));
    }
}
