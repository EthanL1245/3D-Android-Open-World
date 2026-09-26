using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v16 is the final cleanup after the verified v15 split.
///
/// The user's close-up screenshots show that v15 correctly returned the main stationary
/// oar-rest faces to the raft material, but a few thin top/side/underside faces around
/// those rests still use Oar Texture. Those faces were not part of v15's compact-face
/// pairs because the imported Blender mesh fragments each box face independently.
///
/// This pass does NOT rediscover or repaint the whole raft. It:
///  1) compares the current v15 mesh with the older pre-v15 oar split to recover the
///     exact support geometry v15 already proved was stationary;
///  2) builds tight left/right support volumes from those verified triangles;
///  3) fits the actual moving oar centerline on each side using oar triangles OUTSIDE
///     the support volumes; and
///  4) returns only nearby leftover triangles that are outside the moving-oar shaft
///     corridor to the raft material.
///
/// This catches the little top lip, side slivers and underside faces while protecting
/// the actual moving shaft that passes through the rest. Geometry positions, UVs,
/// transforms, animation, colliders and boat gameplay are unchanged; only submesh
/// membership/material assignment changes.
/// </summary>
public static class RaftOarRestCleanupV16
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string RecoveryFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string MeshPath = RecoveryFolder + "/RaftOarRestsCleanup_v16.asset";
    private const string MarkerName = "RaftOarRestCleanup_v16";
    private const string V15Marker = "RaftOarRestMaterial_v15";

    private struct TriangleKey : IEquatable<TriangleKey>
    {
        public int a, b, c;
        public TriangleKey(int x, int y, int z)
        {
            if (x > y) { int t = x; x = y; y = t; }
            if (y > z) { int t = y; y = z; z = t; }
            if (x > y) { int t = x; x = y; y = t; }
            a = x; b = y; c = z;
        }
        public bool Equals(TriangleKey other) => a == other.a && b == other.b && c == other.c;
        public override bool Equals(object obj) => obj is TriangleKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((a * 397) ^ b) * 397 ^ c; }
        }
    }

    private sealed class TriInfo
    {
        public int ordinal;
        public Vector3 center;
        public Bounds bounds;
        public float maxEdge;
        public float lineDistance;
    }

    private struct LineFit
    {
        public bool valid;
        public Vector3 point;
        public Vector3 direction;
    }

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        // v15 queues four delayed editor turns. Run after it has had ample time to
        // finish and serialize its marker/mesh on a fresh pull.
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () =>
                            EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Finish Oar Rest Texture Cleanup (v16)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v16", "Exit Play Mode first.", "OK");
            return;
        }
        if (!Apply(true))
            EditorUtility.DisplayDialog("Raft v16", "No change was saved. Check the Console for [RAFT V16] diagnostics.", "OK");
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform readWrapper = prefab != null ? prefab.transform.Find(WrapperPath) : null;
        if (readWrapper == null) return false;
        if (readWrapper.Find(V15Marker) == null)
        {
            Debug.Log("[RAFT V16] Waiting for successful v15 material split first.");
            return false;
        }
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            Renderer renderer = FindRenderer(wrapper);
            Mesh current = GetMesh(renderer);
            if (renderer == null || current == null || current.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V16] Could not find the current raft/oar split renderer.");
                return false;
            }

            int oarSlot = current.subMeshCount - 1;
            if (current.GetTopology(0) != MeshTopology.Triangles || current.GetTopology(oarSlot) != MeshTopology.Triangles)
            {
                Debug.LogError("[RAFT V16] Expected triangle topology in raft and oar slots.");
                return false;
            }

            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            if (materials.Length < current.subMeshCount || materials[0] == null || materials[oarSlot] == null ||
                IsOar(materials[0]) || !IsOar(materials[oarSlot]))
            {
                Debug.LogError("[RAFT V16] Material slots are not in the expected post-v15 state.");
                return false;
            }

            Vector3[] vertices;
            try { vertices = current.vertices; }
            catch (Exception e)
            {
                Debug.LogError("[RAFT V16] Current raft mesh is not CPU-readable: " + e.Message);
                return false;
            }

            int[] currentOar = current.GetIndices(oarSlot);
            int[] currentRaft = current.GetIndices(0);
            int currentOarTriangles = currentOar.Length / 3;
            if (currentOarTriangles < 24)
            {
                Debug.LogError("[RAFT V16] Current oar slot is unexpectedly small; refusing cleanup.");
                return false;
            }

            Mesh preV15 = FindPreV15Mesh(current, oarSlot, currentOar);
            if (preV15 == null)
            {
                Debug.LogError("[RAFT V16] Could not locate the preserved pre-v15 split mesh needed to recover the verified support footprint. No prefab change was saved.");
                return false;
            }

            int[] oldOar = preV15.GetIndices(oarSlot);
            HashSet<TriangleKey> currentKeys = BuildTriangleKeySet(currentOar);
            List<int> verifiedSupportSeed = new List<int>();
            for (int i = 0; i + 2 < oldOar.Length; i += 3)
            {
                TriangleKey key = new TriangleKey(oldOar[i], oldOar[i + 1], oldOar[i + 2]);
                if (!currentKeys.Contains(key))
                {
                    verifiedSupportSeed.Add(oldOar[i]);
                    verifiedSupportSeed.Add(oldOar[i + 1]);
                    verifiedSupportSeed.Add(oldOar[i + 2]);
                }
            }

            int seedTriangles = verifiedSupportSeed.Count / 3;
            // v15's real diagnostic was 32 support triangles. Keep tolerance for a
            // slightly different local history, but only trust a substantial, bounded
            // previously-returned support set.
            if (seedTriangles < 16 || seedTriangles > 40)
            {
                Debug.LogError("[RAFT V16] Pre-v15 comparison produced an implausible support seed: " + seedTriangles + " triangles. No change saved.");
                return false;
            }

            if (!BuildSideBounds(verifiedSupportSeed, vertices, renderer.transform, root.transform,
                    out Bounds leftSupport, out bool haveLeft, out Bounds rightSupport, out bool haveRight) ||
                !haveLeft || !haveRight)
            {
                Debug.LogError("[RAFT V16] Could not build both verified support volumes from the v15 triangle difference.");
                return false;
            }

            List<TriInfo> triangles = BuildTriangleInfos(currentOar, vertices, renderer.transform, root.transform);
            LineFit leftLine = FitMovingOarLine(triangles, leftSupport, true);
            LineFit rightLine = FitMovingOarLine(triangles, rightSupport, false);
            if (!leftLine.valid || !rightLine.valid)
            {
                Debug.LogError("[RAFT V16] Could not fit both moving-oar centerlines outside the support volumes. No change saved.");
                return false;
            }

            List<TriInfo> rawCandidates = new List<TriInfo>();
            foreach (TriInfo tri in triangles)
            {
                bool left = tri.center.x < 0f;
                Bounds support = left ? leftSupport : rightSupport;
                LineFit line = left ? leftLine : rightLine;

                Bounds neighborhood = support;
                neighborhood.Expand(new Vector3(0.18f, 0.12f, 0.18f));
                if (!neighborhood.Intersects(tri.bounds)) continue;

                // Never eat a large paddle/blade face. The visual defects shown by
                // the user are small box-cap/sliver faces around the stationary rest.
                if (tri.maxEdge > 0.58f) continue;

                // The moving shaft passes directly through the rest. Protect a tight
                // 3D corridor around the shaft centerline, even if those shaft faces
                // overlap the support's AABB. The stationary top/side/bottom faces sit
                // outside this corridor and can safely return to the raft material.
                tri.lineDistance = DistanceToLine(tri.center, line);
                if (tri.lineDistance <= 0.050f) continue;

                // Leftover rest faces should be at roughly the same elevation as the
                // already-verified support box, not floating above it with the oar.
                if (tri.center.y > support.max.y + 0.075f) continue;
                if (tri.center.y < support.min.y - 0.075f) continue;

                rawCandidates.Add(tri);
            }

            // Require mirrored evidence before changing anything. This keeps a random
            // nearby oar fragment from being repainted just because it happens to pass
            // close to one support.
            HashSet<int> reclaimOrdinals = new HashSet<int>();
            List<TriInfo> leftCandidates = rawCandidates.Where(t => t.center.x < 0f).ToList();
            List<TriInfo> rightCandidates = rawCandidates.Where(t => t.center.x > 0f).ToList();
            foreach (TriInfo a in leftCandidates)
            foreach (TriInfo b in rightCandidates)
            {
                float mirrorX = Mathf.Abs(Mathf.Abs(a.center.x) - Mathf.Abs(b.center.x));
                float dy = Mathf.Abs(a.center.y - b.center.y);
                float dz = Mathf.Abs(a.center.z - b.center.z);
                float edge = Mathf.Abs(a.maxEdge - b.maxEdge);
                if (mirrorX <= 0.11f && dy <= 0.075f && dz <= 0.11f && edge <= 0.10f)
                {
                    reclaimOrdinals.Add(a.ordinal);
                    reclaimOrdinals.Add(b.ordinal);
                }
            }

            int extraReturned = reclaimOrdinals.Count;
            if (extraReturned < 2 || extraReturned > 18)
            {
                Debug.LogError("[RAFT V16] Safety stop: expected a small mirrored set of leftover rest faces, found " + extraReturned + ". No prefab change was saved.");
                LogDiagnostics(preV15, currentOarTriangles, seedTriangles, leftSupport, rightSupport, leftLine, rightLine, rawCandidates, reclaimOrdinals);
                return false;
            }

            List<int> keepOar = new List<int>(currentOar.Length);
            List<int> backToRaft = new List<int>(extraReturned * 3);
            for (int i = 0, ordinal = 0; i + 2 < currentOar.Length; i += 3, ordinal++)
            {
                List<int> destination = reclaimOrdinals.Contains(ordinal) ? backToRaft : keepOar;
                destination.Add(currentOar[i]);
                destination.Add(currentOar[i + 1]);
                destination.Add(currentOar[i + 2]);
            }

            int remainingMoving = keepOar.Count / 3;
            if (remainingMoving < 28 || extraReturned >= Mathf.CeilToInt(currentOarTriangles * 0.35f))
            {
                Debug.LogError("[RAFT V16] Final safety stop: currentOar=" + currentOarTriangles +
                    " extraReturned=" + extraReturned + " remainingMoving=" + remainingMoving + ".");
                return false;
            }

            Mesh refined = UnityEngine.Object.Instantiate(current);
            refined.name = "RaftOarRestsCleanup_v16";
            List<int> raftCombined = new List<int>(currentRaft.Length + backToRaft.Count);
            raftCombined.AddRange(currentRaft);
            raftCombined.AddRange(backToRaft);
            refined.SetTriangles(raftCombined, 0, false);
            refined.SetTriangles(keepOar, oarSlot, false);
            refined.RecalculateBounds();

            Directory.CreateDirectory(RecoveryFolder);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null) AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(refined, MeshPath);
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (saved == null)
            {
                Debug.LogError("[RAFT V16] Failed to save the cleaned mesh asset.");
                return false;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (filter != null) filter.sharedMesh = saved;
            else if (skinned != null) skinned.sharedMesh = saved;
            else return false;

            Transform oldMarker = wrapper.Find(MarkerName);
            if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);

            EditorUtility.SetDirty(renderer);
            if (filter != null) EditorUtility.SetDirty(filter);
            if (skinned != null) EditorUtility.SetDirty(skinned);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string reclaimed = string.Join(" | ", triangles.Where(t => reclaimOrdinals.Contains(t.ordinal))
                .OrderBy(t => t.center.x).ThenBy(t => t.center.z)
                .Select(t => "ord=" + t.ordinal + " center=" + t.center.ToString("F3") +
                    " edge=" + t.maxEdge.ToString("0.000") + " shaftDist=" + t.lineDistance.ToString("0.000")));

            Debug.Log("[RAFT V16] SUCCESS — cleaned the remaining stationary top/side/underside oar-rest faces. " +
                "Recovered " + extraReturned + " additional triangles to raft texture; " + remainingMoving +
                " moving-oar triangles remain on Oar Texture. Verified v15 support seed=" + seedTriangles +
                ". Reclaimed: " + reclaimed);

            if (force)
                EditorUtility.DisplayDialog("Raft v16 Complete",
                    "The remaining top/side/underside faces of the stationary oar rests now use the raft texture. The moving oars remain on the oar texture.", "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V16] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Mesh FindPreV15Mesh(Mesh current, int oarSlot, int[] currentOar)
    {
        HashSet<TriangleKey> currentSet = BuildTriangleKeySet(currentOar);
        string currentPath = AssetDatabase.GetAssetPath(current);
        Mesh best = null;
        float bestScore = float.PositiveInfinity;

        string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { RecoveryFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) continue;
            Mesh candidate = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (candidate == null || candidate.vertexCount != current.vertexCount || candidate.subMeshCount != current.subMeshCount) continue;
            if (candidate.GetTopology(oarSlot) != MeshTopology.Triangles) continue;

            int[] indices = candidate.GetIndices(oarSlot);
            int count = indices.Length / 3;
            int currentCount = currentOar.Length / 3;
            int difference = count - currentCount;
            if (difference < 8 || difference > 48) continue;

            HashSet<TriangleKey> candidateSet = BuildTriangleKeySet(indices);
            if (!currentSet.All(candidateSet.Contains)) continue;

            float score = Mathf.Abs(difference - 32) * 10f;
            string name = candidate.name ?? string.Empty;
            if (name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) score -= 15f;
            if (name.IndexOf("v15", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("v16", StringComparison.OrdinalIgnoreCase) >= 0) score += 100f;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private static HashSet<TriangleKey> BuildTriangleKeySet(int[] indices)
    {
        HashSet<TriangleKey> set = new HashSet<TriangleKey>();
        for (int i = 0; i + 2 < indices.Length; i += 3)
            set.Add(new TriangleKey(indices[i], indices[i + 1], indices[i + 2]));
        return set;
    }

    private static bool BuildSideBounds(List<int> indices, Vector3[] vertices, Transform rendererTransform, Transform root,
        out Bounds left, out bool haveLeft, out Bounds right, out bool haveRight)
    {
        left = default; right = default; haveLeft = false; haveRight = false;
        for (int i = 0; i + 2 < indices.Count; i += 3)
        {
            Vector3 a = ToBoat(vertices[indices[i]], rendererTransform, root);
            Vector3 b = ToBoat(vertices[indices[i + 1]], rendererTransform, root);
            Vector3 c = ToBoat(vertices[indices[i + 2]], rendererTransform, root);
            Vector3 center = (a + b + c) / 3f;
            bool isLeft = center.x < 0f;
            if (isLeft)
            {
                if (!haveLeft) { left = new Bounds(a, Vector3.zero); haveLeft = true; }
                left.Encapsulate(a); left.Encapsulate(b); left.Encapsulate(c);
            }
            else
            {
                if (!haveRight) { right = new Bounds(a, Vector3.zero); haveRight = true; }
                right.Encapsulate(a); right.Encapsulate(b); right.Encapsulate(c);
            }
        }
        return haveLeft || haveRight;
    }

    private static List<TriInfo> BuildTriangleInfos(int[] indices, Vector3[] vertices, Transform rendererTransform, Transform root)
    {
        List<TriInfo> result = new List<TriInfo>(indices.Length / 3);
        for (int i = 0, ordinal = 0; i + 2 < indices.Length; i += 3, ordinal++)
        {
            Vector3 a = ToBoat(vertices[indices[i]], rendererTransform, root);
            Vector3 b = ToBoat(vertices[indices[i + 1]], rendererTransform, root);
            Vector3 c = ToBoat(vertices[indices[i + 2]], rendererTransform, root);
            Bounds bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
            result.Add(new TriInfo
            {
                ordinal = ordinal,
                center = (a + b + c) / 3f,
                bounds = bounds,
                maxEdge = Mathf.Max(Vector3.Distance(a, b), Mathf.Max(Vector3.Distance(b, c), Vector3.Distance(c, a)))
            });
        }
        return result;
    }

    private static LineFit FitMovingOarLine(List<TriInfo> triangles, Bounds support, bool left)
    {
        Bounds exclusion = support;
        exclusion.Expand(new Vector3(0.30f, 0.20f, 0.30f));
        List<Vector3> points = triangles
            .Where(t => (left ? t.center.x < 0f : t.center.x > 0f) && !exclusion.Contains(t.center))
            .Select(t => t.center)
            .ToList();
        if (points.Count < 5) return default;

        Vector3 mean = Vector3.zero;
        foreach (Vector3 p in points) mean += p;
        mean /= points.Count;

        Vector3 direction = points.OrderByDescending(p => (p - mean).sqrMagnitude).First() - mean;
        if (direction.sqrMagnitude < 0.0001f) return default;
        direction.Normalize();

        // Principal-axis power iteration on the centroid covariance matrix.
        float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
        foreach (Vector3 p in points)
        {
            Vector3 d = p - mean;
            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
            yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
        }
        for (int iteration = 0; iteration < 12; iteration++)
        {
            Vector3 next = new Vector3(
                xx * direction.x + xy * direction.y + xz * direction.z,
                xy * direction.x + yy * direction.y + yz * direction.z,
                xz * direction.x + yz * direction.y + zz * direction.z);
            if (next.sqrMagnitude < 0.000001f) break;
            direction = next.normalized;
        }

        return new LineFit { valid = direction.sqrMagnitude > 0.5f, point = mean, direction = direction.normalized };
    }

    private static float DistanceToLine(Vector3 point, LineFit line)
    {
        Vector3 delta = point - line.point;
        Vector3 closest = line.point + line.direction * Vector3.Dot(delta, line.direction);
        return Vector3.Distance(point, closest);
    }

    private static Vector3 ToBoat(Vector3 vertex, Transform rendererTransform, Transform root)
    {
        return root.InverseTransformPoint(rendererTransform.TransformPoint(vertex));
    }

    private static void LogDiagnostics(Mesh pre, int currentCount, int seedCount, Bounds left, Bounds right,
        LineFit leftLine, LineFit rightLine, List<TriInfo> candidates, HashSet<int> reclaimed)
    {
        Debug.Log("[RAFT V16] diagnostic: preMesh=" + (pre != null ? pre.name : "NULL") +
            " currentOar=" + currentCount + " verifiedSeed=" + seedCount +
            " leftSupport=" + left.center.ToString("F3") + "/" + left.size.ToString("F3") +
            " rightSupport=" + right.center.ToString("F3") + "/" + right.size.ToString("F3") +
            " leftLine=" + leftLine.point.ToString("F3") + " dir=" + leftLine.direction.ToString("F3") +
            " rightLine=" + rightLine.point.ToString("F3") + " dir=" + rightLine.direction.ToString("F3") +
            " rawCandidates=" + candidates.Count + " paired=" + reclaimed.Count);
        if (candidates.Count > 0)
            Debug.Log("[RAFT V16] candidate faces: " + string.Join(" | ", candidates
                .OrderBy(t => t.center.x).ThenBy(t => t.center.z)
                .Select(t => "ord=" + t.ordinal + " c=" + t.center.ToString("F3") +
                    " edge=" + t.maxEdge.ToString("0.000") + " shaftDist=" + t.lineDistance.ToString("0.000"))));
    }

    private static Renderer FindRenderer(Transform wrapper)
    {
        Renderer best = null;
        long bestScore = long.MinValue;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null || mesh.subMeshCount < 2) continue;
            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            if (materials.Length < mesh.subMeshCount || !IsOar(materials[mesh.subMeshCount - 1])) continue;

            long score = mesh.vertexCount;
            string name = mesh.name ?? string.Empty;
            if (name.IndexOf("v15", StringComparison.OrdinalIgnoreCase) >= 0) score += 20000000L;
            if (name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) score += 10000000L;
            if ((renderer.name ?? string.Empty).IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0) score += 1000000L;
            if (score > bestScore) { bestScore = score; best = renderer; }
        }
        return best;
    }

    private static bool IsOar(Material material)
    {
        if (material == null) return false;
        string name = material.name ?? string.Empty;
        if (name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        Texture texture = material.mainTexture;
        return texture != null && (texture.name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   texture.name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0);
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
