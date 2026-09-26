using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v13 narrows the already-working v12 oar submesh so ONLY the moving paddle pieces
/// keep Oar Texture.jpg. The small stationary oar rests / brackets that the paddles
/// sit on are returned to the raft material.
///
/// Important: this does NOT rediscover the whole raft. It starts from the known-good
/// v12 split, inspects only the FINAL oar submesh, finds the mirrored long/narrow
/// paddle cores with the permissive geometry thresholds that originally succeeded,
/// keeps only aligned moving pieces and tiny endpoint caps/blades, and moves every
/// other component in that oar slot back to raft submesh 0. UVs, vertices, animation,
/// transforms, colliders and gameplay are unchanged.
/// </summary>
public static class RaftMovingOarsOnlyV13
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string RecoveryFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string MeshPath = RecoveryFolder + "/RaftMovingOarsOnly_v13.asset";
    private const string MarkerName = "RaftMovingOarsOnly_v13";
    private const string RequiredV12Marker = "RaftMaterialRecovery_v12";

    private sealed class Part
    {
        public int id;
        public readonly HashSet<int> vertices = new HashSet<int>();
        public readonly List<int> triangleOrdinals = new List<int>();
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

        public int Find(int value)
        {
            if (parent[value] != value) parent[value] = Find(parent[value]);
            return parent[value];
        }

        public void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            if (rank[a] < rank[b]) parent[a] = b;
            else if (rank[a] > rank[b]) parent[b] = a;
            else { parent[b] = a; rank[a]++; }
        }
    }

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        // v12 already owns the material slots. Run one tick later and only refine
        // which triangles belong in its final oar slot.
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Keep ONLY Moving Oars On Oar Texture (v13)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v13", "Exit Play Mode first.", "OK");
            return;
        }

        if (!Apply(true))
            EditorUtility.DisplayDialog(
                "Raft v13",
                "No change was saved. Check the Console for [RAFT V13] diagnostics.",
                "OK");
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return false;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) return false;
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        // Never run before v12 has restored the correct material slots.
        if (readWrapper.Find(RequiredV12Marker) == null)
        {
            if (force) Debug.LogWarning("[RAFT V13] Waiting for v12 material recovery first.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer target = FindSplitRenderer(wrapper);
            Mesh source = GetMesh(target);
            if (target == null || source == null || source.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V13] Could not find the known-good split raft renderer.");
                return false;
            }

            int oarSlot = source.subMeshCount - 1;
            if (source.GetTopology(oarSlot) != MeshTopology.Triangles)
            {
                Debug.LogError("[RAFT V13] Final oar slot is not triangle geometry.");
                return false;
            }

            Vector3[] vertices;
            try { vertices = source.vertices; }
            catch (Exception e)
            {
                Debug.LogError("[RAFT V13] Split mesh is not CPU-readable: " + e.Message);
                return false;
            }

            int[] oarIndices = source.GetIndices(oarSlot);
            if (oarIndices.Length < 6)
            {
                Debug.LogError("[RAFT V13] Existing oar slot does not contain enough geometry.");
                return false;
            }

            Bounds allBounds = CalculateMeshBoundsInBoat(source, vertices, target.transform, root.transform);
            List<Part> parts = BuildOarParts(oarIndices, vertices, target.transform, root.transform, allBounds.center.x);
            if (parts.Count < 2)
            {
                Debug.LogError("[RAFT V13] Existing oar slot has fewer than two disconnected components.");
                return false;
            }

            if (!SelectCorePair(parts, out Part left, out Part right))
            {
                LogInventory(parts, "PAIR-FAIL");
                Debug.LogError("[RAFT V13] Could not identify the two mirrored moving oar cores. No prefab change was saved.");
                return false;
            }

            HashSet<Part> moving = new HashSet<Part> { left, right };
            IncludeMovingPieces(parts, left, moving);
            IncludeMovingPieces(parts, right, moving);

            HashSet<int> keepOrdinals = new HashSet<int>();
            foreach (Part part in moving)
                foreach (int ordinal in part.triangleOrdinals)
                    keepOrdinals.Add(ordinal);

            List<int> movingOar = new List<int>();
            List<int> returnToRaft = new List<int>();
            for (int i = 0, ordinal = 0; i + 2 < oarIndices.Length; i += 3, ordinal++)
            {
                List<int> destination = keepOrdinals.Contains(ordinal) ? movingOar : returnToRaft;
                destination.Add(oarIndices[i]);
                destination.Add(oarIndices[i + 1]);
                destination.Add(oarIndices[i + 2]);
            }

            int movingTriangles = movingOar.Count / 3;
            int returnedTriangles = returnToRaft.Count / 3;
            int totalOarTriangles = oarIndices.Length / 3;
            if (movingTriangles < 8 || returnedTriangles <= 0)
            {
                LogInventory(parts, "NO-CHANGE");
                Debug.LogWarning(
                    "[RAFT V13] Safety stop: moving=" + movingTriangles +
                    " returned=" + returnedTriangles + " total=" + totalOarTriangles +
                    ". Existing v12 split was left untouched.");
                return false;
            }

            // Keep every existing source submesh untouched except:
            //   slot 0: append stationary rests/brackets so they use raft material
            //   final slot: keep only moving paddle geometry.
            Mesh refined = UnityEngine.Object.Instantiate(source);
            refined.name = "RaftMovingOarsOnly_v13";

            int[] raft0 = source.GetIndices(0);
            List<int> raftCombined = new List<int>(raft0.Length + returnToRaft.Count);
            raftCombined.AddRange(raft0);
            raftCombined.AddRange(returnToRaft);
            if (source.GetTopology(0) == MeshTopology.Triangles)
                refined.SetTriangles(raftCombined, 0, false);
            else
            {
                Debug.LogError("[RAFT V13] Raft material slot 0 is not triangle geometry. No change was saved.");
                UnityEngine.Object.DestroyImmediate(refined);
                return false;
            }

            refined.SetTriangles(movingOar, oarSlot, false);
            refined.RecalculateBounds();

            Directory.CreateDirectory(RecoveryFolder);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
                AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(refined, MeshPath);
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (savedMesh == null)
            {
                Debug.LogError("[RAFT V13] Could not save refined raft mesh asset.");
                return false;
            }

            MeshFilter filter = target.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = target as SkinnedMeshRenderer;
            if (filter != null) filter.sharedMesh = savedMesh;
            else if (skinned != null) skinned.sharedMesh = savedMesh;
            else
            {
                Debug.LogError("[RAFT V13] Target renderer has no assignable mesh component.");
                return false;
            }

            RemoveMarker(wrapper, MarkerName);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);

            EditorUtility.SetDirty(target);
            if (filter != null) EditorUtility.SetDirty(filter);
            if (skinned != null) EditorUtility.SetDirty(skinned);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string kept = string.Join(", ", moving.OrderBy(p => p.center.x).Select(p =>
                "id=" + p.id + " len=" + p.length.ToString("0.000") +
                " ratio=" + p.ratio.ToString("0.0") +
                " lateral=" + p.lateral.ToString("0.000")));

            Debug.Log(
                "[RAFT V13] SUCCESS — ONLY moving oar geometry remains on Oar Texture. " +
                "Returned " + returnedTriangles + "/" + totalOarTriangles +
                " stationary oar-rest/bracket triangles to raft material; kept " + movingTriangles +
                " moving-oar triangles. Components kept: " + kept);

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft v13 Complete",
                    "Only the moving oars use the oar texture now. Stationary rests/brackets that the oars sit on were returned to the raft material.",
                    "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V13] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static List<Part> BuildOarParts(int[] indices, Vector3[] vertices, Transform rendererTransform, Transform boatRoot, float centerX)
    {
        UnionFind uf = new UnionFind(vertices.Length);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
            uf.Union(a, b); uf.Union(b, c); uf.Union(c, a);
        }

        Dictionary<int, Part> map = new Dictionary<int, Part>();
        int nextId = 0;
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
            part.triangleOrdinals.Add(ordinal);
            part.triangleCount++;
        }

        foreach (Part part in map.Values)
        {
            FinalizePart(part, vertices, rendererTransform, boatRoot);
            part.lateral = part.center.x - centerX;
        }
        return map.Values.ToList();
    }

    private static void FinalizePart(Part part, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        List<Vector3> points = new List<Vector3>(part.vertices.Count);
        bool have = false;
        Bounds bounds = default;
        Vector3 sum = Vector3.zero;
        foreach (int index in part.vertices)
        {
            Vector3 world = rendererTransform.TransformPoint(vertices[index]);
            Vector3 p = boatRoot.InverseTransformPoint(world);
            points.Add(p); sum += p;
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
        if (part.length < .0001f) return;
        part.direction = delta / part.length;
        part.endpointA = a; part.endpointB = b;

        float radius = 0f;
        foreach (Vector3 p in points)
        {
            Vector3 d = p - part.center;
            Vector3 perpendicular = d - part.direction * Vector3.Dot(d, part.direction);
            radius = Mathf.Max(radius, perpendicular.magnitude);
        }
        part.width = Mathf.Max(.012f, radius * 2f);
        part.ratio = part.length / part.width;

        Vector2 horizontal = new Vector2(part.direction.x, part.direction.z);
        if (horizontal.sqrMagnitude > .0001f)
        {
            horizontal.Normalize();
            part.diagonal = Mathf.Clamp01(2f * Mathf.Min(Mathf.Abs(horizontal.x), Mathf.Abs(horizontal.y)));
        }
    }

    private static bool SelectCorePair(List<Part> parts, out Part bestA, out Part bestB)
    {
        bestA = null; bestB = null;
        float bestScore = float.NegativeInfinity;

        // Deliberately use the permissive v7-style thresholds. The real authored
        // paddle core measured ~0.83 m in the user's diagnostics, so the stricter
        // >=0.85 m v11 threshold incorrectly rejected it.
        List<Part> candidates = parts.Where(p =>
            p.length >= .50f && p.length <= 4.5f &&
            p.ratio >= 2.0f && p.diagonal >= .10f &&
            p.triangleCount >= 4 && Mathf.Abs(p.lateral) >= .05f).ToList();

        for (int i = 0; i < candidates.Count; i++)
        for (int j = i + 1; j < candidates.Count; j++)
        {
            Part a = candidates[i], b = candidates[j];
            if (a.lateral * b.lateral >= 0f) continue;

            float lenSimilarity = Mathf.Min(a.length, b.length) / Mathf.Max(.001f, Mathf.Max(a.length, b.length));
            float widthSimilarity = Mathf.Min(a.width, b.width) / Mathf.Max(.001f, Mathf.Max(a.width, b.width));
            if (lenSimilarity < .48f || widthSimilarity < .18f) continue;

            float score = CoreScore(a) + CoreScore(b) + lenSimilarity * 12f + widthSimilarity * 4f;
            if (score > bestScore)
            {
                bestScore = score;
                bestA = a;
                bestB = b;
            }
        }
        return bestA != null && bestB != null;
    }

    private static float CoreScore(Part p)
    {
        return p.length * 5f + Mathf.Min(p.ratio, 20f) * 2.4f + p.diagonal * 14f + Mathf.Abs(p.lateral) * 3f;
    }

    private static void IncludeMovingPieces(List<Part> all, Part core, HashSet<Part> moving)
    {
        foreach (Part candidate in all)
        {
            if (candidate == core || moving.Contains(candidate)) continue;

            Vector3 relative = candidate.center - core.center;
            float axial = Vector3.Dot(relative, core.direction);
            Vector3 perpendicularVector = relative - core.direction * axial;
            float perpendicular = perpendicularVector.magnitude;
            float alignment = Mathf.Abs(Vector3.Dot(candidate.direction, core.direction));

            // Other long/slender pieces that continue along the paddle axis are
            // moving paddle geometry, even if Blender exported them disconnected.
            bool alignedOarPiece =
                candidate.length >= .20f && candidate.ratio >= 2.25f &&
                alignment >= .68f &&
                perpendicular <= Mathf.Max(.12f, core.width * 1.6f) &&
                Mathf.Abs(axial) <= core.length * .72f + .55f &&
                Mathf.Abs(candidate.center.y - core.center.y) <= Mathf.Max(.22f, core.width * 2.0f);

            if (alignedOarPiece)
            {
                moving.Add(candidate);
                continue;
            }

            // Blade tips / handle caps may be compact and have an orientation unlike
            // the shaft. Include them ONLY at a true end of the core. A stationary
            // oar rest lives around the middle/pivot and therefore fails this test.
            Vector3 size = candidate.bounds.size;
            float maxDimension = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDimension > .75f || candidate.triangleCount > 2000) continue;

            float distanceA = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointA), core.endpointA);
            float distanceB = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointB), core.endpointB);
            float endpointDistance = Mathf.Min(distanceA, distanceB);
            float endpointLimit = Mathf.Max(.14f, Mathf.Min(.30f, core.width * 2.0f + .08f));
            if (endpointDistance > endpointLimit) continue;

            if (Mathf.Abs(axial) < core.length * .30f) continue;
            if (perpendicular > Mathf.Max(.14f, core.width * 1.8f)) continue;

            moving.Add(candidate);
        }
    }

    private static Renderer FindSplitRenderer(Transform wrapper)
    {
        Renderer best = null;
        long bestScore = long.MinValue;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null || mesh.subMeshCount < 2) continue;
            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            bool hasOar = materials.Any(m => m != null &&
                ((m.name ?? string.Empty).IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (m.mainTexture != null && m.mainTexture.name.IndexOf("Oar Texture", StringComparison.OrdinalIgnoreCase) >= 0)));
            bool split = mesh.name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         mesh.name.IndexOf("MovingOars", StringComparison.OrdinalIgnoreCase) >= 0;
            long score = mesh.vertexCount + (split ? 10000000L : 0L) + (hasOar ? 5000000L : 0L);
            if (score > bestScore) { bestScore = score; best = renderer; }
        }
        return best;
    }

    private static Bounds CalculateMeshBoundsInBoat(Mesh mesh, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        bool have = false;
        Bounds bounds = default;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = rendererTransform.TransformPoint(vertices[i]);
            Vector3 p = boatRoot.InverseTransformPoint(world);
            if (!have) { bounds = new Bounds(p, Vector3.zero); have = true; }
            else bounds.Encapsulate(p);
        }
        return bounds;
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

    private static void LogInventory(List<Part> parts, string reason)
    {
        string lines = string.Join("\n", parts.OrderByDescending(CoreScore).Take(50).Select(p =>
            "  id=" + p.id + " tris=" + p.triangleCount +
            " center=(" + p.center.x.ToString("0.000") + "," + p.center.y.ToString("0.000") + "," + p.center.z.ToString("0.000") + ")" +
            " len=" + p.length.ToString("0.000") + " width=" + p.width.ToString("0.000") +
            " ratio=" + p.ratio.ToString("0.00") + " diagonal=" + p.diagonal.ToString("0.00") +
            " lateral=" + p.lateral.ToString("0.000")));
        Debug.Log("[RAFT V13] " + reason + " component inventory (oar slot only):\n" + lines);
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

    private static bool Valid(int index, int count)
    {
        return index >= 0 && index < count;
    }
}
