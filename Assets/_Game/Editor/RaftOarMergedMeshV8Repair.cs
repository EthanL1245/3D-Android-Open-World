using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v8 repair for the user's authored raft.
///
/// The Blender model imports as one merged renderer containing the raft AND both
/// oars. v7 correctly proved that the oars can be isolated as disconnected triangle
/// components, but its endpoint-expansion pass could also absorb nearby deck pieces.
/// That made some/all raft geometry inherit the oar material.
///
/// v8 always rebuilds from the untouched mesh inside Raft.blend, preserves the
/// existing pre-oar raft material slots, then creates ONE extra oar submesh. Only a
/// strict mirrored diagonal oar pair plus small pieces directly aligned with those
/// two oar endpoints are allowed into the oar submesh. Companion detection does not
/// chain, so it cannot walk from an oar endpoint across neighboring deck planks.
/// </summary>
public static class RaftOarMergedMeshV8Repair
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string TexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string AssetFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string MaterialPath = AssetFolder + "/OarUserTexture.mat";
    private const string SplitMeshPath = AssetFolder + "/RaftOarSplit_v8.asset";
    private const string MarkerName = "RaftOarUserTexture_v8";

    private sealed class Part
    {
        public int id;
        public readonly HashSet<int> vertices = new HashSet<int>();
        public readonly Dictionary<int,List<int>> triangleOrdinals = new Dictionary<int,List<int>>();
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
            a = Find(a); b = Find(b);
            if (a == b) return;
            if (rank[a] < rank[b]) parent[a] = b;
            else if (rank[a] > rank[b]) parent[b] = a;
            else { parent[b] = a; rank[a]++; }
        }
    }

    [InitializeOnLoadMethod]
    private static void QueueRepair()
    {
        // v7 runs earlier in its own delayed queue. v8 intentionally runs after it,
        // then records a separate marker so subsequent editor reloads are no-ops.
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Restore Raft Material + Keep Oars Textured")]
    private static void ForceRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft Material Repair", "Exit Play Mode first.", "OK");
            return;
        }

        if (!Apply(true))
            EditorUtility.DisplayDialog(
                "Raft Material Repair",
                "No change was saved. Check the Console for [OAR V8] diagnostics.",
                "OK");
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        EnsureBlendReadable();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (prefab == null || authored == null || oarTexture == null)
        {
            Debug.LogError("[OAR V8] Missing BaseBoat.prefab, Raft.blend, or Oar Texture.jpg.");
            return false;
        }

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        if (prefabWrapper == null)
        {
            Debug.LogError("[OAR V8] Uploaded raft wrapper is missing.");
            return false;
        }
        if (!force && prefabWrapper.Find(MarkerName) != null) return false;

        Material oarMaterial = BuildOarMaterial(oarTexture);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            Transform model = wrapper != null ? FindAuthoredModelRoot(wrapper) : null;
            if (wrapper == null || model == null)
            {
                Debug.LogError("[OAR V8] Could not resolve authored raft model in BaseBoat.prefab.");
                return false;
            }

            Renderer target = FindMergedTargetRenderer(model);
            if (target == null)
            {
                Debug.LogError("[OAR V8] Could not find the merged raft renderer in the prefab.");
                return false;
            }

            Renderer sourceRenderer = FindMatchingSourceRenderer(authored.transform, model, target);
            Mesh sourceMesh = GetMesh(sourceRenderer);
            if (sourceRenderer == null || sourceMesh == null)
            {
                Debug.LogError("[OAR V8] Could not map the prefab renderer back to untouched Raft.blend geometry.");
                return false;
            }

            Material[] preservedRaftMaterials = CaptureRaftMaterials(target, sourceMesh.subMeshCount, oarTexture);
            if (preservedRaftMaterials == null)
            {
                Debug.LogError("[OAR V8] Could not recover the raft's pre-oar material. Nothing was saved.");
                return false;
            }

            List<Part> parts = BuildParts(sourceMesh, target.transform, root.transform);
            if (parts.Count < 2)
            {
                Debug.LogError("[OAR V8] Untouched Raft.blend mesh did not expose enough disconnected components.");
                return false;
            }

            Vector3 overallCenter = OverallCenter(parts);
            if (!SelectStrictOarPair(parts, root.transform, overallCenter, out Part left, out Part right))
            {
                LogParts(parts, overallCenter);
                Debug.LogError("[OAR V8] Strict oar pair detection failed. The existing prefab was left untouched.");
                return false;
            }

            HashSet<Part> selected = new HashSet<Part> { left, right };
            IncludeSafeEndpointPieces(parts, left, selected);
            IncludeSafeEndpointPieces(parts, right, selected);

            int moved = BuildSplitMesh(sourceMesh, selected, target, preservedRaftMaterials, oarMaterial);
            if (moved <= 0)
            {
                Debug.LogError("[OAR V8] Oars were identified but no triangles were moved. Nothing was saved.");
                return false;
            }

            Transform old = wrapper.Find(MarkerName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(wrapper, false);

            // Keep the v7 marker as well. That prevents the older migration from
            // rerunning on the now-correct v8 mesh during a later editor reload.
            if (wrapper.Find("RaftOarUserTexture_v7") == null)
            {
                GameObject v7Marker = new GameObject("RaftOarUserTexture_v7");
                v7Marker.transform.SetParent(wrapper, false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[OAR V8] SUCCESS. Restored untouched Raft.blend geometry, preserved " +
                sourceMesh.subMeshCount + " original raft material slot(s), moved " + moved +
                " oar triangles into one dedicated Oar Texture submesh, selected components=" + selected.Count + ".");

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft Restored",
                    "The raft has its original material again. Only the isolated oar triangles use Oar Texture.jpg.",
                    "OK");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[OAR V8] Exception: " + exception);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindMergedTargetRenderer(Transform model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        Renderer best = null;
        int bestVertices = -1;
        foreach (Renderer renderer in renderers)
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null) continue;
            bool alreadySplit = mesh.name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasOarMaterial = renderer.sharedMaterials != null && renderer.sharedMaterials.Any(m =>
                m != null && m.name.IndexOf("OarUserTexture", StringComparison.OrdinalIgnoreCase) >= 0);
            int score = mesh.vertexCount + (alreadySplit || hasOarMaterial ? 1000000 : 0);
            if (score > bestVertices)
            {
                bestVertices = score;
                best = renderer;
            }
        }
        return best;
    }

    private static Renderer FindMatchingSourceRenderer(Transform authoredRoot, Transform model, Renderer target)
    {
        string path = AnimationUtility.CalculateTransformPath(target.transform, model);
        Transform byPath = string.IsNullOrEmpty(path) ? authoredRoot : authoredRoot.Find(path);
        Renderer renderer = byPath != null ? byPath.GetComponent<Renderer>() : null;
        if (renderer != null && GetMesh(renderer) != null) return renderer;

        Renderer[] source = authoredRoot.GetComponentsInChildren<Renderer>(true);
        Renderer sameName = source.FirstOrDefault(r => r != null && r.name == target.name && GetMesh(r) != null);
        if (sameName != null) return sameName;
        return source.Where(r => GetMesh(r) != null).OrderByDescending(r => GetMesh(r).vertexCount).FirstOrDefault();
    }

    private static Material[] CaptureRaftMaterials(Renderer target, int count, Texture2D oarTexture)
    {
        Material[] current = target.sharedMaterials ?? Array.Empty<Material>();
        Material fallback = current.FirstOrDefault(m => IsRaftMaterial(m, oarTexture));
        if (fallback == null) fallback = FindGeneratedRaftMaterial(oarTexture);
        if (fallback == null) return null;

        Material[] result = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < result.Length; i++)
        {
            Material candidate = i < current.Length ? current[i] : null;
            result[i] = IsRaftMaterial(candidate, oarTexture) ? candidate : fallback;
        }
        return result;
    }

    private static bool IsRaftMaterial(Material material, Texture2D oarTexture)
    {
        if (material == null) return false;
        if (material.name.IndexOf("OarUserTexture", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        Texture texture = material.mainTexture;
        if (texture == oarTexture) return false;
        if (texture != null && texture.name.IndexOf("Oar Texture", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    private static Material FindGeneratedRaftMaterial(Texture2D oarTexture)
    {
        string[] roots = { "Assets/_Game/Boats/Raft/Authored/Materials" };
        foreach (string guid in AssetDatabase.FindAssets("t:Material", roots))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (!IsRaftMaterial(material, oarTexture)) continue;
            if (material.mainTexture != null && material.mainTexture.name.IndexOf("Raft", StringComparison.OrdinalIgnoreCase) >= 0)
                return material;
        }
        return null;
    }

    private static List<Part> BuildParts(Mesh mesh, Transform rendererTransform, Transform boatRoot)
    {
        Vector3[] vertices = mesh.vertices;
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

        Dictionary<int,Part> map = new Dictionary<int,Part>();
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

        foreach (Part part in map.Values) FinalizePart(part, vertices, rendererTransform, boatRoot);
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
            Vector3 p = rendererTransform.TransformPoint(vertices[index]);
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
        part.width = Mathf.Max(.015f, radius * 2f);
        part.ratio = part.length / part.width;

        Vector3 horizontal = Vector3.ProjectOnPlane(part.direction, boatRoot.up);
        if (horizontal.sqrMagnitude > .0001f)
        {
            horizontal.Normalize();
            float forward = Mathf.Abs(Vector3.Dot(horizontal, boatRoot.forward));
            float side = Mathf.Abs(Vector3.Dot(horizontal, boatRoot.right));
            part.diagonal = Mathf.Clamp01(2f * Mathf.Min(forward, side));
        }
    }

    private static bool SelectStrictOarPair(List<Part> parts, Transform boatRoot, Vector3 center, out Part bestA, out Part bestB)
    {
        bestA = null; bestB = null;
        float bestScore = float.NegativeInfinity;
        foreach (Part part in parts) part.lateral = Vector3.Dot(part.center - center, boatRoot.right);

        List<Part> candidates = parts.Where(p =>
            p.length >= .85f && p.length <= 5f && p.ratio >= 2.4f && p.diagonal >= .28f &&
            p.triangleCount >= 4 && Mathf.Abs(p.lateral) >= .10f).ToList();

        for (int i = 0; i < candidates.Count; i++)
        for (int j = i + 1; j < candidates.Count; j++)
        {
            Part a = candidates[i], b = candidates[j];
            if (a.lateral * b.lateral >= 0f) continue;
            float lenSimilarity = Mathf.Min(a.length, b.length) / Mathf.Max(a.length, b.length);
            float widthSimilarity = Mathf.Min(a.width, b.width) / Mathf.Max(a.width, b.width);
            if (lenSimilarity < .68f || widthSimilarity < .38f) continue;

            float score = (a.diagonal + b.diagonal) * 18f + (a.ratio + b.ratio) * 2f +
                          (a.length + b.length) * 4f + lenSimilarity * 15f + widthSimilarity * 5f;
            if (score > bestScore)
            {
                bestScore = score;
                bestA = a; bestB = b;
            }
        }
        return bestA != null && bestB != null;
    }

    private static void IncludeSafeEndpointPieces(List<Part> all, Part core, HashSet<Part> selected)
    {
        // Deliberately compare only against the original long oar core. Do not use
        // newly-added companions as new seeds: that was the v7 path that could walk
        // from a handle into nearby deck geometry.
        foreach (Part candidate in all)
        {
            if (candidate == core || selected.Contains(candidate)) continue;
            Vector3 size = candidate.bounds.size;
            float maxDimension = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxDimension > .95f || candidate.triangleCount > 2000) continue;

            float distanceA = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointA), core.endpointA);
            float distanceB = Vector3.Distance(candidate.bounds.ClosestPoint(core.endpointB), core.endpointB);
            if (Mathf.Min(distanceA, distanceB) > .28f) continue;

            Vector3 relative = candidate.center - core.center;
            float axial = Vector3.Dot(relative, core.direction);
            Vector3 perpendicularVector = relative - core.direction * axial;
            float allowedPerpendicular = Mathf.Max(.18f, core.width * .70f);
            if (perpendicularVector.magnitude > allowedPerpendicular) continue;

            // Endpoint pieces should live toward an end of the oar, not around its
            // middle where the shaft crosses the deck.
            if (Mathf.Abs(axial) < core.length * .30f) continue;
            selected.Add(candidate);
        }
    }

    private static int BuildSplitMesh(Mesh source, HashSet<Part> selected, Renderer target,
        Material[] raftMaterials, Material oarMaterial)
    {
        Dictionary<int,HashSet<int>> move = new Dictionary<int,HashSet<int>>();
        foreach (Part part in selected)
        foreach (KeyValuePair<int,List<int>> pair in part.triangleOrdinals)
        {
            if (!move.TryGetValue(pair.Key, out HashSet<int> ordinals))
            {
                ordinals = new HashSet<int>();
                move.Add(pair.Key, ordinals);
            }
            foreach (int ordinal in pair.Value) ordinals.Add(ordinal);
        }

        List<int>[] keep = new List<int>[source.subMeshCount];
        List<int> oar = new List<int>();
        int moved = 0;
        for (int s = 0; s < source.subMeshCount; s++)
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
        if (moved <= 0) return 0;

        Mesh split = UnityEngine.Object.Instantiate(source);
        split.name = source.name + "_OarSplit_v8";
        int originalSubmeshes = source.subMeshCount;
        split.subMeshCount = originalSubmeshes + 1;
        for (int s = 0; s < originalSubmeshes; s++)
        {
            MeshTopology topology = source.GetTopology(s);
            if (topology == MeshTopology.Triangles) split.SetTriangles(keep[s], s, false);
            else split.SetIndices(keep[s].ToArray(), topology, s, false);
        }
        split.SetTriangles(oar, originalSubmeshes, false);
        split.RecalculateBounds();

        if (AssetDatabase.LoadAssetAtPath<Mesh>(SplitMeshPath) != null) AssetDatabase.DeleteAsset(SplitMeshPath);
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

    private static Material BuildOarMaterial(Texture2D texture)
    {
        Directory.CreateDirectory(AssetFolder);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No supported lit shader exists for the oar material.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else material.shader = shader;

        material.name = "OarUserTexture";
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .24f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureBlendReadable()
    {
        ModelImporter importer = AssetImporter.GetAtPath(BlendPath) as ModelImporter;
        if (importer == null || importer.isReadable) return;
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static Transform FindAuthoredModelRoot(Transform wrapper)
    {
        for (int i = 0; i < wrapper.childCount; i++)
        {
            Transform child = wrapper.GetChild(i);
            if (child != null && child.GetComponentInChildren<Renderer>(true) != null) return child;
        }
        return null;
    }

    private static Vector3 OverallCenter(List<Part> parts)
    {
        bool have = false;
        Bounds bounds = default;
        foreach (Part part in parts)
        {
            if (!have) { bounds = part.bounds; have = true; }
            else bounds.Encapsulate(part.bounds);
        }
        return have ? bounds.center : Vector3.zero;
    }

    private static Vector3 Farthest(List<Vector3> points, Vector3 from)
    {
        Vector3 best = points[0];
        float bestDistance = -1f;
        foreach (Vector3 point in points)
        {
            float distance = (point - from).sqrMagnitude;
            if (distance > bestDistance) { bestDistance = distance; best = point; }
        }
        return best;
    }

    private static bool Valid(int index, int count)
    {
        return index >= 0 && index < count;
    }

    private static void LogParts(List<Part> parts, Vector3 center)
    {
        Debug.Log("[OAR V8] Component inventory:\n" + string.Join("\n", parts
            .OrderByDescending(p => p.diagonal * p.ratio)
            .Select(p => "  id=" + p.id + " tris=" + p.triangleCount +
                " center=(" + (p.center.x-center.x).ToString("0.00") + "," +
                              (p.center.y-center.y).ToString("0.00") + "," +
                              (p.center.z-center.z).ToString("0.00") + ")" +
                " len=" + p.length.ToString("0.00") +
                " width=" + p.width.ToString("0.00") +
                " ratio=" + p.ratio.ToString("0.00") +
                " diagonal=" + p.diagonal.ToString("0.00") +
                " lateral=" + p.lateral.ToString("0.00"))));
    }
}
