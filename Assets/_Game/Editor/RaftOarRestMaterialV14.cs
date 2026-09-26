using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v14 narrows the existing mixed oar submesh so ONLY the moving paddle geometry
/// keeps Oar Texture.jpg. The stationary rests/brackets around the middle of each
/// paddle shaft are moved back to the raft-material submesh.
///
/// Earlier v14 assumed the final split slot itself was tiny. The device diagnostic
/// proved that assumption wrong: the final slot currently contains 76 triangles over
/// ~2.675 m, so it contains the moving oars AND the small rests together. This pass
/// therefore works INSIDE that already-working oar slot instead of repainting the
/// whole slot. It finds long/narrow paddle components, preserves them and their
/// endpoint pieces, then moves only small components sitting around the middle of a
/// paddle shaft back to raft slot 0.
///
/// No vertices, UVs, transforms, animation curves, colliders or gameplay components
/// are changed. Only triangle-to-submesh membership changes.
/// </summary>
public static class RaftOarRestMaterialV14
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string RecoveryFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string MeshPath = RecoveryFolder + "/RaftOarRestsBackToRaft_v14.asset";
    private const string MarkerName = "RaftOarRestMaterial_v14";
    private const string V12Marker = "RaftMaterialRecovery_v12";
    private const string V13Marker = "RaftMovingOarsOnly_v13";

    private sealed class Part
    {
        public int id;
        public readonly HashSet<int> vertices = new HashSet<int>();
        public readonly List<int> triangleOrdinals = new List<int>();
        public int triangleCount;
        public Vector3 center;
        public Vector3 endpointA;
        public Vector3 endpointB;
        public float length;
        public float width;
        public float ratio;
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
            a = Find(a);
            b = Find(b);
            if (a == b) return;
            if (rank[a] < rank[b]) parent[a] = b;
            else if (rank[a] > rank[b]) parent[b] = a;
            else
            {
                parent[b] = a;
                rank[a]++;
            }
        }
    }

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall += BlockV13Retry;
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Make ONLY Moving Oars Use Oar Texture (v14)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v14", "Exit Play Mode first.", "OK");
            return;
        }

        BlockV13Retry();
        if (!Apply(true))
            EditorUtility.DisplayDialog("Raft v14", "No change was saved. Check the Console for [RAFT V14] diagnostics.", "OK");
    }

    private static void BlockV13Retry()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null || readWrapper.Find(V13Marker) != null) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null || wrapper.Find(V13Marker) != null) return;
            new GameObject(V13Marker).transform.SetParent(wrapper, false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[RAFT V14] Disabled obsolete v13 retry. v14 now separates stationary rests from moving oars inside the existing mixed oar slot.");
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
        if (prefab == null) return false;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) return false;
        if (!force && readWrapper.Find(MarkerName) != null) return false;
        if (readWrapper.Find(V12Marker) == null)
        {
            Debug.LogWarning("[RAFT V14] Waiting for v12 raft-material recovery first.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer merged = FindMergedSplitRenderer(wrapper);
            Mesh source = GetMesh(merged);
            if (merged == null || source == null || source.subMeshCount < 2)
            {
                Debug.LogError("[RAFT V14] Could not find the v12 split raft renderer.");
                return false;
            }

            int oarSlot = source.subMeshCount - 1;
            if (source.GetTopology(oarSlot) != MeshTopology.Triangles ||
                source.GetTopology(0) != MeshTopology.Triangles)
            {
                Debug.LogError("[RAFT V14] Raft/oar slots are not triangle geometry; refusing to change the mesh.");
                return false;
            }

            Vector3[] vertices;
            try { vertices = source.vertices; }
            catch (Exception e)
            {
                Debug.LogError("[RAFT V14] Current raft mesh is not CPU-readable: " + e.Message);
                return false;
            }

            int[] oarIndices = source.GetIndices(oarSlot);
            int totalOarTriangles = oarIndices.Length / 3;
            if (totalOarTriangles < 8)
            {
                Debug.LogError("[RAFT V14] Existing oar slot has too little geometry to safely separate rests.");
                return false;
            }

            Material[] materials = merged.sharedMaterials ?? Array.Empty<Material>();
            if (materials.Length < source.subMeshCount ||
                materials[0] == null || materials[oarSlot] == null ||
                IsOarMaterial(materials[0]) || !IsOarMaterial(materials[oarSlot]))
            {
                Debug.LogError("[RAFT V14] Expected raft material in slot 0 and oar material in final slot; refusing to modify geometry.");
                return false;
            }

            List<Part> parts = BuildParts(oarIndices, vertices, merged.transform, root.transform);
            if (parts.Count < 3)
            {
                Debug.LogError("[RAFT V14] Mixed oar slot did not contain enough disconnected pieces to separate rests safely.");
                LogInventory(parts);
                return false;
            }

            List<Part> cores = parts
                .Where(p => p.triangleCount >= 4 && p.length >= 0.72f && p.ratio >= 2.0f)
                .OrderByDescending(p => p.length)
                .ToList();

            // If an import slightly changes scale/topology, keep the two longest
            // clearly elongated components rather than failing on a fixed threshold.
            if (cores.Count < 2)
            {
                cores = parts
                    .Where(p => p.triangleCount >= 4 && p.length >= 0.50f && p.ratio >= 1.8f)
                    .OrderByDescending(p => p.length)
                    .Take(4)
                    .ToList();
            }

            if (cores.Count < 2)
            {
                Debug.LogError("[RAFT V14] Could not identify at least two elongated moving-oar components.");
                LogInventory(parts);
                return false;
            }

            HashSet<Part> stationaryRests = new HashSet<Part>();
            foreach (Part part in parts)
            {
                if (cores.Contains(part)) continue;

                // Stationary oarlocks/rests are compact pieces sitting around the
                // middle of a long shaft. Disconnected blade/end-cap pieces live
                // near an endpoint and are deliberately NOT returned to the raft.
                if (part.triangleCount > 16 || part.length > 0.62f) continue;

                foreach (Part core in cores)
                {
                    if (IsMiddleShaftSupport(part, core))
                    {
                        stationaryRests.Add(part);
                        break;
                    }
                }
            }

            if (stationaryRests.Count == 0)
            {
                Debug.LogError("[RAFT V14] No compact middle-of-shaft rest/support components were found. No prefab change was saved.");
                LogInventory(parts);
                return false;
            }

            HashSet<int> restOrdinals = new HashSet<int>();
            foreach (Part part in stationaryRests)
                foreach (int ordinal in part.triangleOrdinals)
                    restOrdinals.Add(ordinal);

            List<int> movingOarIndices = new List<int>(oarIndices.Length);
            List<int> restIndices = new List<int>();
            for (int i = 0, ordinal = 0; i + 2 < oarIndices.Length; i += 3, ordinal++)
            {
                List<int> destination = restOrdinals.Contains(ordinal) ? restIndices : movingOarIndices;
                destination.Add(oarIndices[i]);
                destination.Add(oarIndices[i + 1]);
                destination.Add(oarIndices[i + 2]);
            }

            int returnedTriangles = restIndices.Count / 3;
            int movingTriangles = movingOarIndices.Count / 3;
            if (returnedTriangles <= 0 || movingTriangles < 8 ||
                returnedTriangles > 32 || returnedTriangles >= Mathf.CeilToInt(totalOarTriangles * 0.40f))
            {
                Debug.LogError(
                    "[RAFT V14] Safety stop after classification. total=" + totalOarTriangles +
                    " moving=" + movingTriangles + " returnedToRaft=" + returnedTriangles +
                    ". No prefab change was saved.");
                LogInventory(parts);
                return false;
            }

            Mesh refined = UnityEngine.Object.Instantiate(source);
            refined.name = "RaftOarRestsBackToRaft_v14";

            int[] raft0 = source.GetIndices(0);
            List<int> raftCombined = new List<int>(raft0.Length + restIndices.Count);
            raftCombined.AddRange(raft0);
            raftCombined.AddRange(restIndices);

            refined.SetTriangles(raftCombined, 0, false);
            refined.SetTriangles(movingOarIndices, oarSlot, false);
            refined.RecalculateBounds();

            Directory.CreateDirectory(RecoveryFolder);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
                AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(refined, MeshPath);
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (savedMesh == null)
            {
                Debug.LogError("[RAFT V14] Could not save refined raft/oar mesh asset.");
                return false;
            }

            MeshFilter filter = merged.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = merged as SkinnedMeshRenderer;
            if (filter != null) filter.sharedMesh = savedMesh;
            else if (skinned != null) skinned.sharedMesh = savedMesh;
            else
            {
                Debug.LogError("[RAFT V14] Target renderer has no assignable mesh component.");
                return false;
            }

            RemoveMarker(wrapper, MarkerName);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);
            if (wrapper.Find(V13Marker) == null)
                new GameObject(V13Marker).transform.SetParent(wrapper, false);

            EditorUtility.SetDirty(merged);
            if (filter != null) EditorUtility.SetDirty(filter);
            if (skinned != null) EditorUtility.SetDirty(skinned);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string restReport = string.Join(" | ", stationaryRests.OrderBy(p => p.center.x).Select(p =>
                "id=" + p.id + " tris=" + p.triangleCount +
                " len=" + p.length.ToString("0.000") +
                " center=" + p.center.ToString("F3")));

            Debug.Log(
                "[RAFT V14] SUCCESS — only stationary middle-shaft oar rests/supports were returned to raft texture. " +
                "Returned " + returnedTriangles + "/" + totalOarTriangles + " triangles; kept " +
                movingTriangles + " moving-oar triangles on Oar Texture. Rests: " + restReport);

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft v14 Complete",
                    "Only the moving oars keep the oar texture. The stationary rests/brackets they sit on now use the raft texture.",
                    "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V14] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool IsMiddleShaftSupport(Part small, Part core)
    {
        Vector3 a = core.endpointA;
        Vector3 b = core.endpointB;
        Vector3 ab = b - a;
        float sqr = ab.sqrMagnitude;
        if (sqr < 0.0001f) return false;

        float t = Vector3.Dot(small.center - a, ab) / sqr;
        float clampedT = Mathf.Clamp01(t);
        Vector3 closest = a + ab * clampedT;
        float distance = Vector3.Distance(small.center, closest);

        // The oar rest belongs near the pivot/middle of the shaft. End pieces at
        // either blade/handle are kept on the oar even when they are tiny.
        bool middle = t >= 0.20f && t <= 0.80f;
        float tolerance = Mathf.Clamp(core.width * 2.2f + 0.06f, 0.12f, 0.34f);
        return middle && distance <= tolerance;
    }

    private static List<Part> BuildParts(int[] indices, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        UnionFind uf = new UnionFind(vertices.Length);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            if (!Valid(a, vertices.Length) || !Valid(b, vertices.Length) || !Valid(c, vertices.Length)) continue;
            uf.Union(a, b);
            uf.Union(b, c);
            uf.Union(c, a);
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
            part.vertices.Add(a);
            part.vertices.Add(b);
            part.vertices.Add(c);
            part.triangleOrdinals.Add(ordinal);
            part.triangleCount++;
        }

        foreach (Part part in map.Values)
            FinalizePart(part, vertices, rendererTransform, boatRoot);

        return map.Values.ToList();
    }

    private static void FinalizePart(Part part, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        List<Vector3> points = new List<Vector3>(part.vertices.Count);
        Vector3 sum = Vector3.zero;
        foreach (int index in part.vertices)
        {
            Vector3 world = rendererTransform.TransformPoint(vertices[index]);
            Vector3 p = boatRoot.InverseTransformPoint(world);
            points.Add(p);
            sum += p;
        }

        if (points.Count == 0) return;
        part.center = sum / points.Count;
        if (points.Count < 2) return;

        Vector3 a = Farthest(points, part.center);
        Vector3 b = Farthest(points, a);
        Vector3 delta = b - a;
        part.length = delta.magnitude;
        part.endpointA = a;
        part.endpointB = b;
        if (part.length < 0.0001f)
        {
            part.width = 0.01f;
            part.ratio = 0f;
            return;
        }

        Vector3 direction = delta / part.length;
        float radius = 0f;
        foreach (Vector3 p in points)
        {
            Vector3 d = p - part.center;
            Vector3 perpendicular = d - direction * Vector3.Dot(d, direction);
            radius = Mathf.Max(radius, perpendicular.magnitude);
        }

        part.width = Mathf.Max(0.012f, radius * 2f);
        part.ratio = part.length / part.width;
    }

    private static Vector3 Farthest(List<Vector3> points, Vector3 origin)
    {
        Vector3 best = points[0];
        float bestDistance = -1f;
        foreach (Vector3 point in points)
        {
            float distance = (point - origin).sqrMagnitude;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }
        return best;
    }

    private static bool Valid(int index, int count)
    {
        return index >= 0 && index < count;
    }

    private static void LogInventory(List<Part> parts)
    {
        string report = string.Join(" | ", parts.OrderByDescending(p => p.length).Select(p =>
            "id=" + p.id + " tris=" + p.triangleCount +
            " len=" + p.length.ToString("0.000") +
            " width=" + p.width.ToString("0.000") +
            " ratio=" + p.ratio.ToString("0.00") +
            " center=" + p.center.ToString("F3")));
        Debug.Log("[RAFT V14] component inventory: " + report);
    }

    private static Renderer FindMergedSplitRenderer(Transform wrapper)
    {
        Renderer best = null;
        long bestScore = long.MinValue;
        foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null || mesh.subMeshCount < 2) continue;

            Material[] materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            bool finalIsOar = materials.Length >= mesh.subMeshCount && IsOarMaterial(materials[mesh.subMeshCount - 1]);
            if (!finalIsOar) continue;

            long score = mesh.vertexCount;
            string meshName = mesh.name ?? string.Empty;
            if (meshName.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0) score += 10000000L;
            if (meshName.IndexOf("RaftMovingOars", StringComparison.OrdinalIgnoreCase) >= 0) score += 9000000L;
            if ((renderer.name ?? string.Empty).IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0) score += 1000000L;

            if (score > bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }
        return best;
    }

    private static bool IsOarMaterial(Material material)
    {
        if (material == null) return false;
        string name = material.name ?? string.Empty;
        if (name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        Texture texture = material.mainTexture;
        return texture != null && (texture.name.IndexOf("Oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   texture.name.IndexOf("Paddle", StringComparison.OrdinalIgnoreCase) >= 0);
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
}
