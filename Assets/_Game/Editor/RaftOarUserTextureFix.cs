using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs the authored raft's oar materials without repainting the deck.
///
/// Important: the user's current Blender import is ONE MeshRenderer
/// (Empty.002/Plane.001) containing the deck and both oars. Older fixes searched for
/// separate long oar Renderers, so they could never succeed on this actual asset.
///
/// v7 works below Renderer level. It finds disconnected mesh components, identifies
/// the mirrored diagonal oar pair from geometry, includes small blade/end-cap pieces,
/// moves only those triangles into a dedicated submesh, and assigns Oar Texture.jpg
/// only to that new submesh. The deck keeps its existing raft material/UVs.
/// </summary>
public static class RaftOarUserTextureFix
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string TexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string AssetFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string MaterialPath = AssetFolder + "/OarUserTexture.mat";
    private const string MarkerName = "RaftOarUserTexture_v7";

    private sealed class ComponentInfo
    {
        public int id;
        public int rootVertex;
        public Renderer renderer;
        public Mesh mesh;
        public readonly HashSet<int> vertices = new HashSet<int>();
        public readonly Dictionary<int,List<int>> triangleOrdinals = new Dictionary<int,List<int>>();
        public int triangleCount;
        public Bounds worldBounds;
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
            int p = parent[value];
            if (p != value) parent[value] = Find(p);
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
    private static void QueueAutomaticRepair()
    {
        // Run after the older raft import/material passes. The v7 marker guarantees
        // this migration executes once even on projects that already ran v5/v6.
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Diagnose + Fix Raft Oar Texture")]
    private static void ForceRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft Oar Texture", "Exit Play Mode first.", "OK");
            return;
        }

        bool changed = Apply(true);
        if (!changed)
            EditorUtility.DisplayDialog(
                "Raft Oar Texture",
                "No change was made. The Console now contains a component-level [OAR DIAG] report.",
                "OK");
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        GameObject portablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Transform portableWrapper = portablePrefab != null ? portablePrefab.transform.Find(WrapperPath) : null;
        if (portableWrapper != null && portableWrapper.Find("RaftPortable_v23") != null) return false;

        Directory.CreateDirectory(AssetFolder);
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        EnsureBlendReadable();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (texture == null)
        {
            Debug.LogError("[OAR DIAG] Oar Texture.jpg did not import as Texture2D: " + TexturePath);
            return false;
        }
        if (prefab == null)
        {
            Debug.LogError("[OAR DIAG] BaseBoat.prefab is missing.");
            return false;
        }

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        if (prefabWrapper == null)
        {
            Debug.LogError("[OAR DIAG] Authored raft wrapper is missing: " + WrapperPath);
            return false;
        }
        if (!force && prefabWrapper.Find(MarkerName) != null) return false;

        ConfigureTexture(TexturePath);
        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Material oarMaterial = BuildMaterial(texture);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            Transform model = wrapper != null ? FindAuthoredModelRoot(wrapper) : null;
            if (wrapper == null || model == null)
            {
                Debug.LogError("[OAR DIAG] Could not resolve the authored model inside the boat prefab.");
                return false;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogError("[OAR DIAG] Authored raft has no renderers.");
                return false;
            }

            List<ComponentInfo> components = BuildComponents(renderers, root.transform);
            if (components.Count == 0)
            {
                Debug.LogError("[OAR DIAG] No readable triangle components were found in the authored raft mesh.");
                return false;
            }

            Vector3 overallCenter = CalculateOverallCenter(renderers);
            SelectOarPair(components, root.transform, overallCenter, out ComponentInfo left, out ComponentInfo right);
            if (left == null || right == null)
            {
                Debug.LogError("[OAR DIAG] Could not identify a mirrored diagonal oar pair. No raft material was changed.");
                LogComponentInventory(model, components, overallCenter);
                return false;
            }

            HashSet<ComponentInfo> selected = new HashSet<ComponentInfo> { left, right };
            IncludeEndpointCompanions(components, selected);

            int splitRendererCount = 0;
            int movedTriangles = 0;
            foreach (IGrouping<Renderer,ComponentInfo> group in selected.GroupBy(c => c.renderer))
            {
                int moved = SplitRendererOarTriangles(group.Key, group.ToList(), oarMaterial, splitRendererCount);
                if (moved > 0)
                {
                    movedTriangles += moved;
                    splitRendererCount++;
                }
            }

            if (movedTriangles <= 0)
            {
                Debug.LogError("[OAR DIAG] Oar components were identified, but no triangles could be moved to the oar material submesh.");
                LogComponentInventory(model, components, overallCenter);
                return false;
            }

            RemoveOldMarkers(wrapper);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StringBuilder report = new StringBuilder();
            report.AppendLine("[OAR DIAG] v7 SUCCESS — merged raft mesh was split by connected geometry.");
            report.AppendLine("Texture: " + TexturePath + "  " + texture.width + "x" + texture.height);
            report.AppendLine("Oar material: " + MaterialPath);
            report.AppendLine("Selected components: " + selected.Count + "  renderers split: " + splitRendererCount + "  triangles moved: " + movedTriangles);
            foreach (ComponentInfo c in selected.OrderBy(c => c.center.x))
                report.AppendLine(ComponentLine(c, overallCenter, "OAR"));
            Debug.Log(report.ToString());

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft Oars Fixed",
                    "The actual merged raft mesh was separated at triangle/component level. Only the two oars and their endpoint pieces now use Oar Texture.jpg; the deck retains its raft material.",
                    "OK");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[OAR DIAG] v7 exception: " + exception);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureBlendReadable()
    {
        ModelImporter importer = AssetImporter.GetAtPath(BlendPath) as ModelImporter;
        if (importer == null || importer.isReadable) return;
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private static List<ComponentInfo> BuildComponents(Renderer[] renderers, Transform boatRoot)
    {
        List<ComponentInfo> result = new List<ComponentInfo>();
        int nextId = 0;

        foreach (Renderer renderer in renderers)
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null || mesh.vertexCount == 0 || mesh.subMeshCount == 0) continue;

            Vector3[] vertices;
            try { vertices = mesh.vertices; }
            catch (Exception e)
            {
                Debug.LogWarning("[OAR DIAG] Mesh is not CPU-readable: " + renderer.name + " / " + e.Message);
                continue;
            }
            if (vertices == null || vertices.Length == 0) continue;

            UnionFind uf = new UnionFind(vertices.Length);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                int[] indices = mesh.GetIndices(s);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    if (!ValidVertex(a, vertices.Length) || !ValidVertex(b, vertices.Length) || !ValidVertex(c, vertices.Length)) continue;
                    uf.Union(a, b); uf.Union(b, c); uf.Union(c, a);
                }
            }

            Dictionary<int,ComponentInfo> byRoot = new Dictionary<int,ComponentInfo>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                int[] indices = mesh.GetIndices(s);
                for (int i = 0, ordinal = 0; i + 2 < indices.Length; i += 3, ordinal++)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    if (!ValidVertex(a, vertices.Length) || !ValidVertex(b, vertices.Length) || !ValidVertex(c, vertices.Length)) continue;
                    int rootVertex = uf.Find(a);
                    if (!byRoot.TryGetValue(rootVertex, out ComponentInfo info))
                    {
                        info = new ComponentInfo { id = nextId++, rootVertex = rootVertex, renderer = renderer, mesh = mesh };
                        byRoot.Add(rootVertex, info);
                    }
                    info.vertices.Add(a); info.vertices.Add(b); info.vertices.Add(c);
                    info.triangleCount++;
                    if (!info.triangleOrdinals.TryGetValue(s, out List<int> ordinals))
                    {
                        ordinals = new List<int>();
                        info.triangleOrdinals.Add(s, ordinals);
                    }
                    ordinals.Add(ordinal);
                }
            }

            foreach (ComponentInfo info in byRoot.Values)
            {
                FinalizeComponent(info, vertices, boatRoot);
                result.Add(info);
            }
        }
        return result;
    }

    private static void FinalizeComponent(ComponentInfo info, Vector3[] vertices, Transform boatRoot)
    {
        List<Vector3> points = new List<Vector3>(info.vertices.Count);
        bool haveBounds = false;
        Bounds bounds = default;
        Vector3 sum = Vector3.zero;

        foreach (int index in info.vertices)
        {
            Vector3 p = info.renderer.transform.TransformPoint(vertices[index]);
            points.Add(p);
            sum += p;
            if (!haveBounds) { bounds = new Bounds(p, Vector3.zero); haveBounds = true; }
            else bounds.Encapsulate(p);
        }

        info.worldBounds = bounds;
        info.center = points.Count > 0 ? sum / points.Count : bounds.center;
        if (points.Count < 2) return;

        Vector3 a = Farthest(points, info.center);
        Vector3 b = Farthest(points, a);
        Vector3 direction = b - a;
        info.length = direction.magnitude;
        if (info.length < .0001f) return;
        info.direction = direction / info.length;
        info.endpointA = a;
        info.endpointB = b;

        float radius = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 delta = points[i] - info.center;
            Vector3 perpendicular = delta - info.direction * Vector3.Dot(delta, info.direction);
            radius = Mathf.Max(radius, perpendicular.magnitude);
        }
        info.width = Mathf.Max(.015f, radius * 2f);
        info.ratio = info.length / info.width;

        Vector3 horizontal = Vector3.ProjectOnPlane(info.direction, boatRoot.up);
        if (horizontal.sqrMagnitude > .0001f)
        {
            horizontal.Normalize();
            float forward = Mathf.Abs(Vector3.Dot(horizontal, boatRoot.forward));
            float side = Mathf.Abs(Vector3.Dot(horizontal, boatRoot.right));
            info.diagonal = Mathf.Clamp01(2f * Mathf.Min(forward, side));
        }
    }

    private static Vector3 Farthest(List<Vector3> points, Vector3 from)
    {
        Vector3 best = points[0];
        float bestDistance = -1f;
        for (int i = 0; i < points.Count; i++)
        {
            float d = (points[i] - from).sqrMagnitude;
            if (d > bestDistance) { bestDistance = d; best = points[i]; }
        }
        return best;
    }

    private static void SelectOarPair(List<ComponentInfo> components, Transform boatRoot, Vector3 overallCenter,
        out ComponentInfo bestA, out ComponentInfo bestB)
    {
        bestA = null; bestB = null;
        float bestScore = float.NegativeInfinity;

        List<ComponentInfo> candidates = new List<ComponentInfo>();
        foreach (ComponentInfo c in components)
        {
            c.lateral = Vector3.Dot(c.center - overallCenter, boatRoot.right);
            if (c.length < .55f || c.ratio < 2.15f || c.diagonal < .12f || c.triangleCount < 4) continue;
            candidates.Add(c);
        }

        for (int i = 0; i < candidates.Count; i++)
        for (int j = i + 1; j < candidates.Count; j++)
        {
            ComponentInfo a = candidates[i], b = candidates[j];
            if (a.lateral * b.lateral >= 0f) continue;
            if (Mathf.Abs(a.lateral) < .10f || Mathf.Abs(b.lateral) < .10f) continue;

            float lengthSimilarity = Mathf.Min(a.length, b.length) / Mathf.Max(.001f, Mathf.Max(a.length, b.length));
            float widthSimilarity = Mathf.Min(a.width, b.width) / Mathf.Max(.001f, Mathf.Max(a.width, b.width));
            if (lengthSimilarity < .55f || widthSimilarity < .28f) continue;

            float score = PairScore(a) + PairScore(b) + lengthSimilarity * 12f + widthSimilarity * 4f;
            if (score > bestScore)
            {
                bestScore = score;
                bestA = a;
                bestB = b;
            }
        }
    }

    private static float PairScore(ComponentInfo c)
    {
        return c.length * 5f + Mathf.Min(c.ratio, 15f) * 2.4f + c.diagonal * 14f + Mathf.Abs(c.lateral) * 3f;
    }

    private static void IncludeEndpointCompanions(List<ComponentInfo> all, HashSet<ComponentInfo> selected)
    {
        // Blade tips / handle caps may be disconnected mesh islands. Add only small
        // components touching the OUTER endpoints of a selected oar. Repeating this
        // twice catches a blade plus a tiny cap without swallowing deck planks at the
        // inner pivot where an oar crosses the raft.
        for (int pass = 0; pass < 2; pass++)
        {
            List<ComponentInfo> add = new List<ComponentInfo>();
            foreach (ComponentInfo candidate in all)
            {
                if (selected.Contains(candidate)) continue;
                float largest = candidate.worldBounds.size.magnitude;
                if (largest > .85f || candidate.triangleCount > 2500) continue;

                float best = float.PositiveInfinity;
                foreach (ComponentInfo core in selected)
                {
                    Vector3 outer = Mathf.Abs(core.lateral) > .001f
                        ? (Vector3.Dot(core.endpointA - core.center, core.center) >= Vector3.Dot(core.endpointB - core.center, core.center) ? core.endpointA : core.endpointB)
                        : core.endpointA;

                    // Use both endpoints but require the companion to be small; this
                    // is safer across Blender coordinate/origin differences.
                    float da = DistanceToBounds(candidate.worldBounds, core.endpointA);
                    float db = DistanceToBounds(candidate.worldBounds, core.endpointB);
                    best = Mathf.Min(best, Mathf.Min(da, db));
                }
                if (best <= .24f) add.Add(candidate);
            }
            if (add.Count == 0) break;
            foreach (ComponentInfo c in add) selected.Add(c);
        }
    }

    private static float DistanceToBounds(Bounds bounds, Vector3 point)
    {
        return Vector3.Distance(bounds.ClosestPoint(point), point);
    }

    private static int SplitRendererOarTriangles(Renderer renderer, List<ComponentInfo> selected, Material oarMaterial, int rendererOrdinal)
    {
        Mesh source = GetMesh(renderer);
        if (source == null || selected == null || selected.Count == 0) return 0;

        Dictionary<int,HashSet<int>> selectedOrdinals = new Dictionary<int,HashSet<int>>();
        foreach (ComponentInfo component in selected)
        {
            foreach (KeyValuePair<int,List<int>> pair in component.triangleOrdinals)
            {
                if (!selectedOrdinals.TryGetValue(pair.Key, out HashSet<int> set))
                {
                    set = new HashSet<int>();
                    selectedOrdinals.Add(pair.Key, set);
                }
                for (int i = 0; i < pair.Value.Count; i++) set.Add(pair.Value[i]);
            }
        }

        List<int>[] keepBySubmesh = new List<int>[source.subMeshCount];
        List<int> oarTriangles = new List<int>();
        int moved = 0;

        for (int s = 0; s < source.subMeshCount; s++)
        {
            int[] indices = source.GetIndices(s);
            List<int> keep = new List<int>(indices.Length);
            keepBySubmesh[s] = keep;
            selectedOrdinals.TryGetValue(s, out HashSet<int> moveSet);

            if (source.GetTopology(s) != MeshTopology.Triangles)
            {
                keep.AddRange(indices);
                continue;
            }

            for (int i = 0, ordinal = 0; i + 2 < indices.Length; i += 3, ordinal++)
            {
                if (moveSet != null && moveSet.Contains(ordinal))
                {
                    oarTriangles.Add(indices[i]);
                    oarTriangles.Add(indices[i + 1]);
                    oarTriangles.Add(indices[i + 2]);
                    moved++;
                }
                else
                {
                    keep.Add(indices[i]);
                    keep.Add(indices[i + 1]);
                    keep.Add(indices[i + 2]);
                }
            }
        }

        if (moved == 0) return 0;

        Mesh split = UnityEngine.Object.Instantiate(source);
        split.name = source.name + "_OarSplit_v7";
        int originalSubmeshes = source.subMeshCount;
        split.subMeshCount = originalSubmeshes + 1;
        for (int s = 0; s < originalSubmeshes; s++)
            split.SetTriangles(keepBySubmesh[s], s, false);
        split.SetTriangles(oarTriangles, originalSubmeshes, false);
        split.RecalculateBounds();

        string meshPath = AssetFolder + "/RaftOarSplit_" + SafeName(renderer.name) + "_" + rendererOrdinal + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(split, meshPath);
        split = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (filter != null) filter.sharedMesh = split;
        else if (skinned != null) skinned.sharedMesh = split;
        else return 0;

        Material[] current = renderer.sharedMaterials ?? Array.Empty<Material>();
        Material fallback = current.FirstOrDefault(m => m != null);
        Material[] materials = new Material[originalSubmeshes + 1];
        for (int s = 0; s < originalSubmeshes; s++)
            materials[s] = s < current.Length && current[s] != null ? current[s] : fallback;
        materials[originalSubmeshes] = oarMaterial;
        renderer.sharedMaterials = materials;

        EditorUtility.SetDirty(renderer);
        if (filter != null) EditorUtility.SetDirty(filter);
        if (skinned != null) EditorUtility.SetDirty(skinned);
        return moved;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    private static bool ValidVertex(int index, int count)
    {
        return index >= 0 && index < count;
    }

    private static Vector3 CalculateOverallCenter(Renderer[] renderers)
    {
        bool have = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (!have) { bounds = renderer.bounds; have = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return have ? bounds.center : Vector3.zero;
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

    private static void RemoveOldMarkers(Transform wrapper)
    {
        for (int i = wrapper.childCount - 1; i >= 0; i--)
        {
            Transform child = wrapper.GetChild(i);
            if (child != null && child.name.StartsWith("RaftOarUserTexture_v", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Material BuildMaterial(Texture2D texture)
    {
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
        material.mainTextureScale = Vector2.one;
        material.mainTextureOffset = Vector2.zero;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .24f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Renderer";
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
    }

    private static void LogComponentInventory(Transform model, List<ComponentInfo> components, Vector3 overallCenter)
    {
        StringBuilder report = new StringBuilder("[OAR DIAG] Component inventory (the old renderer-level diagnosis was insufficient):\n");
        foreach (ComponentInfo c in components.OrderByDescending(PairScore))
            report.AppendLine(ComponentLine(c, overallCenter, "COMP"));
        Debug.Log(report.ToString());
    }

    private static string ComponentLine(ComponentInfo c, Vector3 overallCenter, string prefix)
    {
        string path = AnimationUtility.CalculateTransformPath(c.renderer.transform, c.renderer.transform.root);
        string submeshes = string.Join(",", c.triangleOrdinals.Keys.OrderBy(v => v));
        return "  " + prefix + " id=" + c.id + " path=" + path +
               " tris=" + c.triangleCount + " submeshes=[" + submeshes + "]" +
               " center=" + Vec(c.center - overallCenter) +
               " len=" + c.length.ToString("0.000") +
               " width=" + c.width.ToString("0.000") +
               " ratio=" + c.ratio.ToString("0.00") +
               " diagonal=" + c.diagonal.ToString("0.00") +
               " lateral=" + c.lateral.ToString("0.000");
    }

    private static string Vec(Vector3 v)
    {
        return "(" + v.x.ToString("0.00") + "," + v.y.ToString("0.00") + "," + v.z.ToString("0.00") + ")";
    }
}
