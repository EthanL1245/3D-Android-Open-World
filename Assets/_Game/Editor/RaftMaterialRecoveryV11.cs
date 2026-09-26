using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v11 raft recovery.
///
/// The v10 diagnostics showed that using the median of triangle centres as the raft
/// centre was wrong for this particular authored mesh (9 outer triangles on one side
/// vs 95 on the other). That made the line-fitting approach fail before it changed
/// anything.
///
/// v11 goes back to the untouched Raft.blend mesh and uses connected geometry, which
/// is the same strategy that successfully found the paddles in v7, but WITHOUT v7's
/// unsafe chaining endpoint expansion. The raft centre comes from the full geometry
/// bounds, not triangle density. Only the two mirrored long diagonal components plus
/// tiny pieces directly touching their endpoints are moved to an oar submesh. Every
/// other source triangle keeps its original submesh and gets the original raft texture.
/// </summary>
public static class RaftMaterialRecoveryV11
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string RaftTexturePath = "Assets/_Game/Boats/Raft/Authored/Raft Texture.jpg";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string RecoveryFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string SplitMeshPath = RecoveryFolder + "/RaftOarSplit_v11.asset";
    private const string OarMaterialPath = RecoveryFolder + "/OarUserTexture.mat";
    private const string MarkerName = "RaftMaterialRecovery_v11";

    private static readonly string[] StopMarkers =
    {
        "RaftOarUserTexture_v7",
        "RaftOarUserTexture_v8",
        "RaftMaterialRecovery_v9",
        "RaftMaterialRecovery_v10"
    };

    private sealed class Part
    {
        public int id;
        public readonly HashSet<int> vertices = new HashSet<int>();
        public readonly Dictionary<int, List<int>> triangleOrdinals = new Dictionary<int, List<int>>();
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
            parent = new int[count];
            rank = new byte[count];
            for (int i = 0; i < count; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a == b) return;
            if (rank[a] < rank[b]) parent[a] = b;
            else if (rank[a] > rank[b]) parent[b] = a;
            else { parent[b] = a; rank[a]++; }
        }
    }

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        // Stop every older repair before its deeper delayed queue can fire.
        EditorApplication.delayCall += InstallStopMarkers;

        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () =>
                            EditorApplication.delayCall += () =>
                                EditorApplication.delayCall += () =>
                                    EditorApplication.delayCall += () =>
                                        EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Restore Original Raft Texture + Oars (v11)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v11 Recovery", "Exit Play Mode first.", "OK");
            return;
        }

        InstallStopMarkers();
        if (!Apply(true))
            EditorUtility.DisplayDialog(
                "Raft v11 Recovery",
                "No recovery was saved. Check the Console for [RAFT V11] diagnostics.",
                "OK");
    }

    private static void InstallStopMarkers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) return;
        if (StopMarkers.All(name => readWrapper.Find(name) != null)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return;
            bool changed = false;
            foreach (string name in StopMarkers)
            {
                if (wrapper.Find(name) != null) continue;
                GameObject marker = new GameObject(name);
                marker.transform.SetParent(wrapper, false);
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[RAFT V11] Disabled obsolete v7/v8/v9/v10 raft material passes.");
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
        EnsureReadable();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath);
        Texture2D raftTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (prefab == null || authored == null || raftTexture == null || oarTexture == null)
        {
            Debug.LogError("[RAFT V11] Missing BaseBoat.prefab, Raft.blend, Raft Texture.jpg, or Oar Texture.jpg.");
            return false;
        }

        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null)
        {
            Debug.LogError("[RAFT V11] Uploaded Raft Model wrapper is missing.");
            return false;
        }
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer target = FindTargetRenderer(wrapper);
            Renderer sourceRenderer = FindSourceRenderer(authored.transform, target);
            Mesh sourceMesh = GetMesh(sourceRenderer);
            if (target == null || sourceRenderer == null || sourceMesh == null)
            {
                Debug.LogError("[RAFT V11] Could not map the boat renderer to untouched Raft.blend geometry.");
                return false;
            }

            Vector3[] vertices;
            try { vertices = sourceMesh.vertices; }
            catch (Exception e)
            {
                Debug.LogError("[RAFT V11] Source raft mesh is not readable: " + e.Message);
                return false;
            }

            List<Part> parts = BuildParts(sourceMesh, vertices, target.transform, root.transform);
            if (parts.Count < 2)
            {
                Debug.LogError("[RAFT V11] Source mesh did not expose enough connected components.");
                return false;
            }

            Bounds overall = OverallBounds(parts);
            Vector3 overallCenter = overall.center;
            if (!SelectOarPair(parts, root.transform, overallCenter, out Part left, out Part right))
            {
                Debug.LogError("[RAFT V11] Could not identify the mirrored diagonal oar pair. No prefab change was saved.");
                LogParts(parts, overallCenter);
                return false;
            }

            HashSet<Part> selected = new HashSet<Part> { left, right };
            IncludeSafeEndpointPieces(parts, left, selected);
            IncludeSafeEndpointPieces(parts, right, selected);

            int selectedTriangles = selected.Sum(p => p.triangleCount);
            int totalTriangles = parts.Sum(p => p.triangleCount);
            float fraction = selectedTriangles / (float)Mathf.Max(1, totalTriangles);
            if (fraction > 0.18f)
            {
                Debug.LogError("[RAFT V11] Safety stop: proposed oar geometry is " +
                    (fraction * 100f).ToString("0.0") + "% of the raft mesh. Nothing was changed.");
                LogSelected(left, right, selected, overallCenter);
                return false;
            }

            Material[] raftMaterials = BuildRaftMaterials(sourceRenderer, sourceMesh.subMeshCount, raftTexture);
            Material oarMaterial = BuildOarMaterial(oarTexture);
            if (raftMaterials == null || raftMaterials.Length == 0 || oarMaterial == null)
            {
                Debug.LogError("[RAFT V11] Could not rebuild raft/oar materials.");
                return false;
            }

            int moved = BuildRecoveredMesh(sourceMesh, selected, target, raftMaterials, oarMaterial);
            if (moved <= 0)
            {
                Debug.LogError("[RAFT V11] Oars were identified but no triangles were moved.");
                return false;
            }

            Transform old = wrapper.Find(MarkerName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject finalMarker = new GameObject(MarkerName);
            finalMarker.transform.SetParent(wrapper, false);
            foreach (string stop in StopMarkers)
            {
                if (wrapper.Find(stop) != null) continue;
                GameObject marker = new GameObject(stop);
                marker.transform.SetParent(wrapper, false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RAFT V11] SUCCESS — raft rebuilt from untouched Raft.blend. " +
                "Raft Texture.jpg restored to all non-oar source triangles; Oar Texture.jpg applied only to " +
                moved + "/" + totalTriangles + " triangles (" + (fraction * 100f).ToString("0.0") + "%). " +
                "geometryCenter=" + Vec(overallCenter) +
                " left=" + PartSummary(left, overallCenter) +
                " right=" + PartSummary(right, overallCenter));

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft v11 Recovered",
                    "The original raft material has been restored from the untouched source mesh. Only the two detected oars and tiny endpoint caps use the oar texture.",
                    "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V11] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindTargetRenderer(Transform wrapper)
    {
        Renderer best = null;
        long bestScore = long.MinValue;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null) continue;
            long score = mesh.vertexCount;
            if (mesh.name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) score += 10000000L;
            if (renderer.name.IndexOf("Plane.001", StringComparison.OrdinalIgnoreCase) >= 0) score += 1000000L;
            if ((renderer.sharedMaterials ?? Array.Empty<Material>()).Any(m => m != null &&
                (m.name.IndexOf("Oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (m.mainTexture != null && m.mainTexture.name.IndexOf("Oar", StringComparison.OrdinalIgnoreCase) >= 0))))
                score += 5000000L;
            if (score > bestScore) { bestScore = score; best = renderer; }
        }
        return best;
    }

    private static Renderer FindSourceRenderer(Transform authoredRoot, Renderer target)
    {
        if (target == null) return null;
        Mesh targetMesh = GetMesh(target);
        int targetVertices = targetMesh != null ? targetMesh.vertexCount : 0;
        Renderer[] source = authoredRoot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && GetMesh(r) != null).ToArray();
        if (source.Length == 0) return null;

        Renderer sameName = source
            .Where(r => string.Equals(r.name, target.name, StringComparison.Ordinal))
            .OrderBy(r => Mathf.Abs(GetMesh(r).vertexCount - targetVertices))
            .FirstOrDefault();
        if (sameName != null) return sameName;
        return source.OrderBy(r => Mathf.Abs(GetMesh(r).vertexCount - targetVertices)).FirstOrDefault();
    }

    private static List<Part> BuildParts(Mesh mesh, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        UnionFind uf = new UnionFind(vertices.Length);
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
            int[] indices = mesh.GetIndices(s);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
                uf.Union(a, b); uf.Union(b, c); uf.Union(c, a);
            }
        }

        Dictionary<int, Part> map = new Dictionary<int, Part>();
        int nextId = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
            int[] indices = mesh.GetIndices(s);
            for (int i = 0, ordinal = 0; i + 2 < indices.Length; i += 3, ordinal++)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
                int key = uf.Find(a);
                if (!map.TryGetValue(key, out Part part))
                {
                    part = new Part { id = nextId++ };
                    map.Add(key, part);
                }
                part.vertices.Add(a); part.vertices.Add(b); part.vertices.Add(c);
                part.triangleCount++;
                if (!part.triangleOrdinals.TryGetValue(s, out List<int> ordinals))
                {
                    ordinals = new List<int>();
                    part.triangleOrdinals.Add(s, ordinals);
                }
                ordinals.Add(ordinal);
            }
        }

        foreach (Part part in map.Values)
            FinalizePart(part, vertices, rendererTransform, boatRoot);
        return map.Values.ToList();
    }

    private static void FinalizePart(Part part, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        List<Vector3> points = new List<Vector3>(part.vertices.Count);
        Vector3 sum = Vector3.zero;
        bool have = false;
        Bounds bounds = default;
        foreach (int index in part.vertices)
        {
            Vector3 world = rendererTransform.TransformPoint(vertices[index]);
            Vector3 p = boatRoot.InverseTransformPoint(world);
            points.Add(p);
            sum += p;
            if (!have) { bounds = new Bounds(p, Vector3.zero); have = true; }
            else bounds.Encapsulate(p);
        }

        part.bounds = bounds;
        part.center = points.Count > 0 ? sum / points.Count : bounds.center;
        if (points.Count < 2) return;

        Vector3 a = Farthest(points, part.center);
        Vector3 b = Farthest(points, a);
        Vector3 delta = b - a;
        part.length = delta.magnitude;
        if (part.length < 0.0001f) return;
        part.endpointA = a;
        part.endpointB = b;
        part.direction = delta / part.length;

        float radius = 0f;
        foreach (Vector3 p in points)
        {
            Vector3 d = p - part.center;
            Vector3 perpendicular = d - part.direction * Vector3.Dot(d, part.direction);
            radius = Mathf.Max(radius, perpendicular.magnitude);
        }
        part.width = Mathf.Max(0.015f, radius * 2f);
        part.ratio = part.length / part.width;

        Vector3 horizontal = Vector3.ProjectOnPlane(part.direction, Vector3.up);
        if (horizontal.sqrMagnitude > 0.0001f)
        {
            horizontal.Normalize();
            float x = Mathf.Abs(horizontal.x);
            float z = Mathf.Abs(horizontal.z);
            part.diagonal = Mathf.Clamp01(2f * Mathf.Min(x, z));
        }
    }

    private static bool SelectOarPair(List<Part> parts, Transform boatRoot, Vector3 center, out Part bestA, out Part bestB)
    {
        bestA = null;
        bestB = null;
        float bestScore = float.NegativeInfinity;

        foreach (Part part in parts)
            part.lateral = part.center.x - center.x;

        List<Part> candidates = parts.Where(p =>
            p.length >= 0.55f && p.length <= 5.0f &&
            p.ratio >= 2.0f && p.diagonal >= 0.28f &&
            p.triangleCount >= 2 && Mathf.Abs(p.lateral) >= 0.08f).ToList();

        for (int i = 0; i < candidates.Count; i++)
        for (int j = i + 1; j < candidates.Count; j++)
        {
            Part a = candidates[i], b = candidates[j];
            if (a.lateral * b.lateral >= 0f) continue;

            float lenSimilarity = Mathf.Min(a.length, b.length) / Mathf.Max(0.001f, Mathf.Max(a.length, b.length));
            float widthSimilarity = Mathf.Min(a.width, b.width) / Mathf.Max(0.001f, Mathf.Max(a.width, b.width));
            if (lenSimilarity < 0.50f || widthSimilarity < 0.20f) continue;

            float mirrorBalance = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Abs(a.lateral) - Mathf.Abs(b.lateral)) /
                                                      Mathf.Max(0.05f, Mathf.Max(Mathf.Abs(a.lateral), Mathf.Abs(b.lateral))));
            float score =
                (a.diagonal + b.diagonal) * 25f +
                (Mathf.Min(a.ratio, 20f) + Mathf.Min(b.ratio, 20f)) * 2.5f +
                (a.length + b.length) * 5f +
                lenSimilarity * 18f + widthSimilarity * 6f + mirrorBalance * 8f;

            if (score > bestScore)
            {
                bestScore = score;
                bestA = a;
                bestB = b;
            }
        }

        if (bestA != null && bestB != null && bestA.lateral > bestB.lateral)
        {
            Part temp = bestA; bestA = bestB; bestB = temp;
        }
        return bestA != null && bestB != null;
    }

    private static void IncludeSafeEndpointPieces(List<Part> all, Part core, HashSet<Part> selected)
    {
        // Non-chaining on purpose. Only compare to the original long oar core.
        foreach (Part candidate in all)
        {
            if (candidate == core || selected.Contains(candidate)) continue;
            Vector3 size = candidate.bounds.size;
            float maxDimension = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDimension > 0.42f || candidate.triangleCount > 300) continue;

            float da = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointA), core.endpointA);
            float db = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointB), core.endpointB);
            if (Mathf.Min(da, db) > 0.15f) continue;

            Vector3 relative = candidate.center - core.center;
            float axial = Vector3.Dot(relative, core.direction);
            Vector3 perpendicular = relative - core.direction * axial;
            if (perpendicular.magnitude > Mathf.Max(0.12f, core.width * 0.60f)) continue;
            if (Mathf.Abs(axial) < core.length * 0.34f) continue;

            selected.Add(candidate);
        }
    }

    private static Material[] BuildRaftMaterials(Renderer sourceRenderer, int submeshCount, Texture2D raftTexture)
    {
        Directory.CreateDirectory(RecoveryFolder);
        AssetDatabase.Refresh();
        Material[] source = sourceRenderer.sharedMaterials ?? Array.Empty<Material>();
        Material sourceFallback = source.FirstOrDefault(m => m != null);
        Material[] result = new Material[Mathf.Max(1, submeshCount)];

        for (int slot = 0; slot < result.Length; slot++)
        {
            Material template = slot < source.Length && source[slot] != null ? source[slot] : sourceFallback;
            string path = RecoveryFolder + "/RaftRestored_v11_" + SafeName(sourceRenderer.name) + "_" + slot + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
            if (fallback == null) fallback = Shader.Find("Standard");

            if (material == null)
            {
                if (template != null && template.shader != null) material = new Material(template);
                else if (fallback != null) material = new Material(fallback);
                else return null;
                AssetDatabase.CreateAsset(material, path);
            }
            else if (template != null && template.shader != null)
            {
                EditorUtility.CopySerialized(template, material);
            }
            else if (fallback != null)
            {
                material.shader = fallback;
            }

            material.name = "RaftRestored_v11_" + slot;
            ForceTexture(material, raftTexture);
            result[slot] = material;
        }
        return result;
    }

    private static Material BuildOarMaterial(Texture2D texture)
    {
        Directory.CreateDirectory(RecoveryFolder);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OarMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (material == null)
        {
            if (shader == null) return null;
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, OarMaterialPath);
        }
        else if (shader != null) material.shader = shader;

        material.name = "OarUserTexture";
        ForceTexture(material, texture);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.24f);
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

    private static int BuildRecoveredMesh(Mesh source, HashSet<Part> selected, Renderer target,
        Material[] raftMaterials, Material oarMaterial)
    {
        Dictionary<int, HashSet<int>> move = new Dictionary<int, HashSet<int>>();
        foreach (Part part in selected)
        foreach (KeyValuePair<int, List<int>> pair in part.triangleOrdinals)
        {
            if (!move.TryGetValue(pair.Key, out HashSet<int> set))
            {
                set = new HashSet<int>();
                move.Add(pair.Key, set);
            }
            foreach (int ordinal in pair.Value) set.Add(ordinal);
        }

        int originalSubmeshes = source.subMeshCount;
        List<int>[] keep = new List<int>[originalSubmeshes];
        List<int> oar = new List<int>();
        int moved = 0;

        for (int s = 0; s < originalSubmeshes; s++)
        {
            int[] indices = source.GetIndices(s);
            keep[s] = new List<int>(indices.Length);
            if (source.GetTopology(s) != MeshTopology.Triangles)
            {
                keep[s].AddRange(indices);
                continue;
            }

            move.TryGetValue(s, out HashSet<int> moveSet);
            for (int i = 0, ordinal = 0; i + 2 < indices.Length; i += 3, ordinal++)
            {
                if (moveSet != null && moveSet.Contains(ordinal))
                {
                    oar.Add(indices[i]); oar.Add(indices[i + 1]); oar.Add(indices[i + 2]);
                    moved++;
                }
                else
                {
                    keep[s].Add(indices[i]); keep[s].Add(indices[i + 1]); keep[s].Add(indices[i + 2]);
                }
            }
        }
        if (moved == 0) return 0;

        Mesh split = UnityEngine.Object.Instantiate(source);
        split.name = source.name + "_OarSplit_v11";
        split.subMeshCount = originalSubmeshes + 1;
        for (int s = 0; s < originalSubmeshes; s++)
        {
            MeshTopology topology = source.GetTopology(s);
            if (topology == MeshTopology.Triangles) split.SetTriangles(keep[s], s, false);
            else split.SetIndices(keep[s].ToArray(), topology, s, false);
        }
        split.SetTriangles(oar, originalSubmeshes, false);
        split.RecalculateBounds();

        if (AssetDatabase.LoadAssetAtPath<Mesh>(SplitMeshPath) != null)
            AssetDatabase.DeleteAsset(SplitMeshPath);
        AssetDatabase.CreateAsset(split, SplitMeshPath);
        split = AssetDatabase.LoadAssetAtPath<Mesh>(SplitMeshPath);

        MeshFilter filter = target.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinned = target as SkinnedMeshRenderer;
        if (filter != null) filter.sharedMesh = split;
        else if (skinned != null) skinned.sharedMesh = split;
        else return 0;

        Material[] materials = new Material[originalSubmeshes + 1];
        for (int s = 0; s < originalSubmeshes; s++)
            materials[s] = s < raftMaterials.Length ? raftMaterials[s] : raftMaterials[0];
        materials[originalSubmeshes] = oarMaterial;
        target.sharedMaterials = materials;

        EditorUtility.SetDirty(target);
        if (filter != null) EditorUtility.SetDirty(filter);
        if (skinned != null) EditorUtility.SetDirty(skinned);
        return moved;
    }

    private static Bounds OverallBounds(List<Part> parts)
    {
        bool have = false;
        Bounds result = default;
        foreach (Part part in parts)
        {
            if (!have) { result = part.bounds; have = true; }
            else result.Encapsulate(part.bounds);
        }
        return result;
    }

    private static Vector3 Farthest(List<Vector3> points, Vector3 from)
    {
        Vector3 best = points[0];
        float bestDistance = -1f;
        foreach (Vector3 point in points)
        {
            float d = (point - from).sqrMagnitude;
            if (d > bestDistance) { bestDistance = d; best = point; }
        }
        return best;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static bool Valid(int index, int count) => index >= 0 && index < count;

    private static void EnsureReadable()
    {
        ModelImporter importer = AssetImporter.GetAtPath(BlendPath) as ModelImporter;
        if (importer == null || importer.isReadable) return;
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Material";
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_').Replace(':', '_').Replace(' ', '_');
    }

    private static void LogParts(List<Part> parts, Vector3 center)
    {
        string text = string.Join("\n", parts
            .OrderByDescending(p => p.diagonal * Mathf.Min(p.ratio, 20f) * p.length)
            .Take(40)
            .Select(p => "  " + PartSummary(p, center)));
        Debug.Log("[RAFT V11] Connected-component inventory (top 40):\n" + text);
    }

    private static void LogSelected(Part left, Part right, HashSet<Part> selected, Vector3 center)
    {
        Debug.Log("[RAFT V11] LEFT " + PartSummary(left, center));
        Debug.Log("[RAFT V11] RIGHT " + PartSummary(right, center));
        Debug.Log("[RAFT V11] Selected components: " + string.Join(",", selected.Select(p => p.id.ToString()).ToArray()));
    }

    private static string PartSummary(Part p, Vector3 center)
    {
        return "id=" + p.id +
               " tris=" + p.triangleCount +
               " center=" + Vec(p.center - center) +
               " len=" + p.length.ToString("0.000") +
               " width=" + p.width.ToString("0.000") +
               " ratio=" + p.ratio.ToString("0.00") +
               " diagonal=" + p.diagonal.ToString("0.00") +
               " lateral=" + p.lateral.ToString("0.000");
    }

    private static string Vec(Vector3 v)
    {
        return "(" + v.x.ToString("0.000") + "," + v.y.ToString("0.000") + "," + v.z.ToString("0.000") + ")";
    }
}