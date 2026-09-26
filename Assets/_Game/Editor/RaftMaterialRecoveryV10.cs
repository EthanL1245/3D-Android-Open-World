using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Final raft/oar recovery pass.
///
/// The earlier diagnosis accidentally treated the first merged renderer as if it
/// were an oar renderer. That is why the oar texture could spill onto the raft.
/// v10 starts from the untouched Raft.blend mesh again, restores the exact original
/// generated Raft_* materials to every source submesh, then isolates only the two
/// diagonal paddle corridors at triangle level. It never trusts the currently
/// contaminated prefab material slots.
///
/// The old v7/v8/v9 automatic migrations are also marked complete before they get a
/// chance to run, so they cannot repaint the recovered raft on a later editor reload.
/// </summary>
public static class RaftMaterialRecoveryV10
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string RaftTexturePath = "Assets/_Game/Boats/Raft/Authored/Raft Texture.jpg";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string AuthoredMaterialFolder = "Assets/_Game/Boats/Raft/Authored/Materials";
    private const string RecoveryFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string SplitMeshPath = RecoveryFolder + "/RaftOarSplit_v10.asset";
    private const string OarMaterialPath = RecoveryFolder + "/OarUserTexture.mat";
    private const string MarkerName = "RaftMaterialRecovery_v10";

    private static readonly string[] LegacyStopMarkers =
    {
        "RaftOarUserTexture_v7",
        "RaftOarUserTexture_v8",
        "RaftMaterialRecovery_v9"
    };

    private sealed class Tri
    {
        public int submesh;
        public int ordinal;
        public int a, b, c;
        public Vector3 p0, p1, p2;
        public Vector3 center;
        public float area;
        public float genericDiagonal;
        public float radius;
    }

    private sealed class LineFit
    {
        public int side;
        public Vector2 mean;
        public Vector2 direction;
        public float yMedian;
        public float halfWidth;
        public float yTolerance;
        public float seedThreshold;
        public readonly List<Tri> seeds = new List<Tri>();
    }

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        // One early editor tick is enough to stop the older deeply-nested passes.
        EditorApplication.delayCall += InstallLegacyStopMarkers;

        // Run after the old queues would have fired. v10 is intentionally last.
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () =>
                            EditorApplication.delayCall += () =>
                                EditorApplication.delayCall += () =>
                                    EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Restore Raft EXACTLY + Keep Oar Texture (v10)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v10 Recovery", "Exit Play Mode first.", "OK");
            return;
        }

        InstallLegacyStopMarkers();
        if (!Apply(true))
            EditorUtility.DisplayDialog(
                "Raft v10 Recovery",
                "No recovery was saved. Check the Console for [RAFT V10] diagnostics.",
                "OK");
    }

    private static void InstallLegacyStopMarkers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null) return;

        bool missing = LegacyStopMarkers.Any(name => readWrapper.Find(name) == null);
        if (!missing) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return;
            bool changed = false;
            foreach (string markerName in LegacyStopMarkers)
            {
                if (wrapper.Find(markerName) != null) continue;
                GameObject marker = new GameObject(markerName);
                marker.transform.SetParent(wrapper, false);
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[RAFT V10] Disabled obsolete v7/v8/v9 raft material migrations so they cannot repaint the raft again.");
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
            Debug.LogError("[RAFT V10] Missing BaseBoat.prefab, Raft.blend, Raft Texture.jpg, or Oar Texture.jpg.");
            return false;
        }

        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null)
        {
            Debug.LogError("[RAFT V10] Uploaded Raft Model wrapper is missing.");
            return false;
        }
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer target = FindContaminatedMergedRenderer(wrapper);
            if (target == null)
            {
                Debug.LogError("[RAFT V10] Could not find the merged renderer that was contaminated by the old oar split.");
                LogRendererInventory(wrapper);
                return false;
            }

            Renderer sourceRenderer = FindSourceRenderer(authored.transform, target);
            Mesh sourceMesh = GetMesh(sourceRenderer);
            if (sourceRenderer == null || sourceMesh == null)
            {
                Debug.LogError("[RAFT V10] Could not map the contaminated renderer back to the untouched Raft.blend mesh.");
                return false;
            }

            Vector3[] vertices;
            try { vertices = sourceMesh.vertices; }
            catch (Exception e)
            {
                Debug.LogError("[RAFT V10] Raft.blend mesh is not readable: " + e.Message);
                return false;
            }
            if (vertices == null || vertices.Length == 0) return false;

            List<Tri> triangles = BuildTriangles(sourceMesh, vertices, target.transform, root.transform);
            if (triangles.Count < 20)
            {
                Debug.LogError("[RAFT V10] Source mesh did not expose enough triangles for safe oar isolation.");
                return false;
            }

            float centerX = Median(triangles.Select(t => t.center.x));
            float[] lateral = triangles.Select(t => Mathf.Abs(t.center.x - centerX)).OrderBy(v => v).ToArray();
            float coreHalf = PercentileSorted(lateral, 0.65f);
            float outerHalf = PercentileSorted(lateral, 0.995f);
            if (coreHalf < 0.01f || outerHalf < coreHalf * 1.10f)
            {
                Debug.LogError("[RAFT V10] Raft/oar lateral footprint is too compact to split safely. core=" + coreHalf + " outer=" + outerHalf);
                return false;
            }

            LineFit left = FitOarLine(triangles, -1, centerX, coreHalf, outerHalf);
            LineFit right = FitOarLine(triangles, +1, centerX, coreHalf, outerHalf);
            if (left == null || right == null)
            {
                Debug.LogError("[RAFT V10] Could not fit both diagonal oar corridors from the untouched source geometry.");
                LogTriangleStats(triangles, centerX, coreHalf, outerHalf);
                return false;
            }

            HashSet<Tri> selected = new HashSet<Tri>();
            SelectCorridorTriangles(triangles, left, centerX, coreHalf, selected);
            SelectCorridorTriangles(triangles, right, centerX, coreHalf, selected);
            ExpandSmallCorridorGaps(triangles, left, centerX, coreHalf, selected);
            ExpandSmallCorridorGaps(triangles, right, centerX, coreHalf, selected);

            int leftCount = selected.Count(t => t.center.x < centerX);
            int rightCount = selected.Count(t => t.center.x > centerX);
            float selectedFraction = selected.Count / (float)Mathf.Max(1, triangles.Count);

            // A pair of paddles should be a minority of this asset. Refuse any mask
            // broad enough to repaint the raft again.
            if (leftCount < 4 || rightCount < 4 || selectedFraction > 0.18f)
            {
                Debug.LogError(
                    "[RAFT V10] Oar mask failed safety limits. left=" + leftCount +
                    " right=" + rightCount + " selected=" + selected.Count + "/" + triangles.Count +
                    " (" + (selectedFraction * 100f).ToString("0.0") + "%). No prefab change was saved.");
                LogFit(left, "LEFT");
                LogFit(right, "RIGHT");
                return false;
            }

            Material[] raftMaterials = BuildOriginalRaftMaterials(sourceRenderer, sourceMesh.subMeshCount, raftTexture);
            if (raftMaterials == null || raftMaterials.Length == 0)
            {
                Debug.LogError("[RAFT V10] Could not restore the original Raft_* material(s).");
                return false;
            }
            Material oarMaterial = BuildOarMaterial(oarTexture);
            if (oarMaterial == null) return false;

            int moved = BuildRecoveredMesh(sourceMesh, triangles, selected, target, raftMaterials, oarMaterial);
            if (moved <= 0)
            {
                Debug.LogError("[RAFT V10] Oar mask was valid but no triangles were moved.");
                return false;
            }

            // Record final authority. Keep old stop markers as permanent guards.
            Transform old = wrapper.Find(MarkerName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject finalMarker = new GameObject(MarkerName);
            finalMarker.transform.SetParent(wrapper, false);
            foreach (string legacy in LegacyStopMarkers)
            {
                if (wrapper.Find(legacy) != null) continue;
                GameObject marker = new GameObject(legacy);
                marker.transform.SetParent(wrapper, false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[RAFT V10] SUCCESS — original raft mesh/materials restored from Raft.blend; only the two fitted diagonal oar corridors use Oar Texture.jpg. " +
                "renderer=" + AnimationUtility.CalculateTransformPath(target.transform, root.transform) +
                " sourceSubmeshes=" + sourceMesh.subMeshCount +
                " oarTriangles=" + moved + "/" + triangles.Count +
                " (" + (selectedFraction * 100f).ToString("0.0") + "%).");
            LogFit(left, "LEFT");
            LogFit(right, "RIGHT");

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft v10 Recovered",
                    "The raft is back on its original Raft Texture/materials. Only the two diagonal oar corridors use Oar Texture.jpg. Older v7/v8/v9 passes are disabled.",
                    "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V10] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindContaminatedMergedRenderer(Transform wrapper)
    {
        Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
        Renderer best = null;
        long bestScore = long.MinValue;
        foreach (Renderer renderer in renderers)
        {
            Mesh mesh = GetMesh(renderer);
            if (mesh == null) continue;
            bool split = mesh.name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0;
            bool oarMaterial = (renderer.sharedMaterials ?? Array.Empty<Material>()).Any(m =>
                m != null && (m.name.IndexOf("OarUserTexture", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              (m.mainTexture != null && m.mainTexture.name.IndexOf("Oar Texture", StringComparison.OrdinalIgnoreCase) >= 0)));
            bool knownRaftName = renderer.name.IndexOf("Plane.001", StringComparison.OrdinalIgnoreCase) >= 0;
            long score = mesh.vertexCount;
            if (split) score += 10000000L;
            if (oarMaterial) score += 5000000L;
            if (knownRaftName) score += 1000000L;
            if (score > bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }
        return best;
    }

    private static Renderer FindSourceRenderer(Transform authoredRoot, Renderer target)
    {
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

    private static List<Tri> BuildTriangles(Mesh mesh, Vector3[] vertices, Transform rendererTransform, Transform boatRoot)
    {
        List<Tri> result = new List<Tri>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
            int[] indices = mesh.GetIndices(s);
            for (int i = 0, ordinal = 0; i + 2 < indices.Length; i += 3, ordinal++)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                if (!Valid(ia, vertices.Length) || !Valid(ib, vertices.Length) || !Valid(ic, vertices.Length)) continue;

                Vector3 w0 = rendererTransform.TransformPoint(vertices[ia]);
                Vector3 w1 = rendererTransform.TransformPoint(vertices[ib]);
                Vector3 w2 = rendererTransform.TransformPoint(vertices[ic]);
                Vector3 p0 = boatRoot.InverseTransformPoint(w0);
                Vector3 p1 = boatRoot.InverseTransformPoint(w1);
                Vector3 p2 = boatRoot.InverseTransformPoint(w2);
                Vector3 center = (p0 + p1 + p2) / 3f;
                float area = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                float radius = Mathf.Max((p0 - center).magnitude, Mathf.Max((p1 - center).magnitude, (p2 - center).magnitude));

                Vector2 d01 = new Vector2(p1.x - p0.x, p1.z - p0.z);
                Vector2 d12 = new Vector2(p2.x - p1.x, p2.z - p1.z);
                Vector2 d20 = new Vector2(p0.x - p2.x, p0.z - p2.z);
                Vector2 edge = Longest(d01, d12, d20);
                float diagonal = 0f;
                if (edge.sqrMagnitude > 0.000001f)
                {
                    edge.Normalize();
                    diagonal = 2f * Mathf.Min(Mathf.Abs(edge.x), Mathf.Abs(edge.y));
                }

                result.Add(new Tri
                {
                    submesh = s,
                    ordinal = ordinal,
                    a = ia, b = ib, c = ic,
                    p0 = p0, p1 = p1, p2 = p2,
                    center = center,
                    area = Mathf.Max(area, 0.000001f),
                    genericDiagonal = diagonal,
                    radius = radius
                });
            }
        }
        return result;
    }

    private static LineFit FitOarLine(List<Tri> triangles, int side, float centerX, float coreHalf, float outerHalf)
    {
        float threshold = coreHalf + (outerHalf - coreHalf) * 0.28f;
        List<Tri> seeds = triangles.Where(t =>
            (t.center.x - centerX) * side >= threshold && t.genericDiagonal >= 0.20f).ToList();

        if (seeds.Count < 5)
            seeds = triangles.Where(t => (t.center.x - centerX) * side >= threshold).ToList();
        if (seeds.Count < 4) return null;

        // Weight meaningful faces more than tiny bevel noise, but cap the weight so a
        // single large deck face cannot dominate the fit.
        float medianArea = Median(seeds.Select(t => t.area));
        medianArea = Mathf.Max(medianArea, 0.000001f);
        float weightSum = 0f;
        Vector2 mean = Vector2.zero;
        foreach (Tri t in seeds)
        {
            float w = Mathf.Clamp(t.area / medianArea, 0.25f, 4f);
            mean += new Vector2(t.center.x, t.center.z) * w;
            weightSum += w;
        }
        mean /= Mathf.Max(0.0001f, weightSum);

        float xx = 0f, zz = 0f, xz = 0f;
        foreach (Tri t in seeds)
        {
            float w = Mathf.Clamp(t.area / medianArea, 0.25f, 4f);
            Vector2 d = new Vector2(t.center.x, t.center.z) - mean;
            xx += d.x * d.x * w;
            zz += d.y * d.y * w;
            xz += d.x * d.y * w;
        }
        float angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        if (direction.sqrMagnitude < 0.5f) return null;

        // Prefer the diagonal eigenvector. If PCA chose the perpendicular axis, swap.
        float diagA = 2f * Mathf.Min(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        Vector2 perpendicularCandidate = new Vector2(-direction.y, direction.x);
        float diagB = 2f * Mathf.Min(Mathf.Abs(perpendicularCandidate.x), Mathf.Abs(perpendicularCandidate.y));
        if (diagB > diagA + 0.15f) direction = perpendicularCandidate;

        List<float> distances = seeds.Select(t => PerpendicularDistance(new Vector2(t.center.x, t.center.z), mean, direction)).OrderBy(v => v).ToList();
        float spread = PercentileSorted(distances.ToArray(), 0.88f);
        float halfWidth = Mathf.Clamp(spread * 1.65f + 0.025f, 0.045f, Mathf.Max(0.07f, coreHalf * 0.28f));
        float yMedian = Median(seeds.Select(t => t.center.y));
        float[] yDev = seeds.Select(t => Mathf.Abs(t.center.y - yMedian)).OrderBy(v => v).ToArray();
        float yTol = Mathf.Clamp(PercentileSorted(yDev, 0.90f) * 2.2f + 0.05f, 0.08f, 0.32f);

        LineFit fit = new LineFit
        {
            side = side,
            mean = mean,
            direction = direction,
            yMedian = yMedian,
            halfWidth = halfWidth,
            yTolerance = yTol,
            seedThreshold = threshold
        };
        fit.seeds.AddRange(seeds);
        return fit;
    }

    private static void SelectCorridorTriangles(List<Tri> triangles, LineFit fit, float centerX, float coreHalf, HashSet<Tri> selected)
    {
        foreach (Tri tri in triangles)
        {
            float signedLateral = (tri.center.x - centerX) * fit.side;
            if (signedLateral < -coreHalf * 0.03f) continue; // never cross to the opposite half of the raft

            Vector2 p = new Vector2(tri.center.x, tri.center.z);
            float distance = PerpendicularDistance(p, fit.mean, fit.direction);
            float allowance = fit.halfWidth + Mathf.Min(tri.radius * 0.35f, fit.halfWidth * 0.55f);
            if (distance > allowance) continue;

            bool outsideCore = signedLateral >= coreHalf * 0.92f;
            float alignment = EdgeAlignment(tri, fit.direction);
            bool yNear = Mathf.Abs(tri.center.y - fit.yMedian) <= fit.yTolerance;

            // Outside the dense raft body, geometry in the fitted corridor is almost
            // certainly paddle geometry. Inside the body require line-aligned edges
            // and the paddle's vertical band so deck planks are not swept in.
            if (outsideCore || (yNear && alignment >= 0.68f))
                selected.Add(tri);
        }
    }

    private static void ExpandSmallCorridorGaps(List<Tri> triangles, LineFit fit, float centerX, float coreHalf, HashSet<Tri> selected)
    {
        // One non-chaining fill pass catches blade caps/bevels whose own longest edge
        // is perpendicular to the shaft. The strict corridor/y/size limits prevent a
        // walk into the deck (the exact failure mode of v7).
        List<Tri> existing = selected.Where(t => (t.center.x - centerX) * fit.side >= 0f).ToList();
        if (existing.Count == 0) return;

        foreach (Tri tri in triangles)
        {
            if (selected.Contains(tri)) continue;
            float signed = (tri.center.x - centerX) * fit.side;
            if (signed < 0f) continue;
            if (tri.radius > Mathf.Max(0.18f, fit.halfWidth * 2.2f)) continue;
            if (Mathf.Abs(tri.center.y - fit.yMedian) > fit.yTolerance * 1.25f) continue;

            float distance = PerpendicularDistance(new Vector2(tri.center.x, tri.center.z), fit.mean, fit.direction);
            if (distance > fit.halfWidth * 1.15f + tri.radius * 0.25f) continue;

            float near = float.PositiveInfinity;
            for (int i = 0; i < existing.Count; i++)
                near = Mathf.Min(near, (existing[i].center - tri.center).magnitude - existing[i].radius - tri.radius);
            if (near <= Mathf.Max(0.04f, fit.halfWidth * 0.55f))
                selected.Add(tri);
        }
    }

    private static Material[] BuildOriginalRaftMaterials(Renderer sourceRenderer, int submeshCount, Texture2D raftTexture)
    {
        Directory.CreateDirectory(RecoveryFolder);
        AssetDatabase.Refresh();
        Material[] sourceMaterials = sourceRenderer.sharedMaterials ?? Array.Empty<Material>();
        Material[] result = new Material[Mathf.Max(1, submeshCount)];

        for (int slot = 0; slot < result.Length; slot++)
        {
            Material source = slot < sourceMaterials.Length ? sourceMaterials[slot] : sourceMaterials.FirstOrDefault(m => m != null);
            string expectedKey = "Raft_" + SafeName(sourceRenderer.name) + "_" + SafeName(source != null ? source.name : "Material") + "_" + slot;
            string expectedPath = AuthoredMaterialFolder + "/" + expectedKey + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(expectedPath);

            if (material == null)
            {
                string recoveryPath = RecoveryFolder + "/RaftRestored_v10_" + SafeName(sourceRenderer.name) + "_" + slot + ".mat";
                material = AssetDatabase.LoadAssetAtPath<Material>(recoveryPath);
                Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
                if (fallback == null) fallback = Shader.Find("Standard");
                if (material == null)
                {
                    if (source != null && source.shader != null) material = new Material(source);
                    else if (fallback != null) material = new Material(fallback);
                    else return null;
                    AssetDatabase.CreateAsset(material, recoveryPath);
                }
                else if (source != null && source.shader != null)
                {
                    EditorUtility.CopySerialized(source, material);
                }
            }

            ForceTexture(material, raftTexture);
            result[slot] = material;
        }
        return result;
    }

    private static Material BuildOarMaterial(Texture2D oarTexture)
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
        else if (shader != null)
        {
            material.shader = shader;
        }
        material.name = "OarUserTexture";
        ForceTexture(material, oarTexture);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .24f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ForceTexture(Material material, Texture2D texture)
    {
        if (material == null) return;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        // Preserve the authored material's other visual properties; only undo the
        // accidental texture replacement. White is only forced if the color somehow
        // became nearly black from a broken import.
        if (material.HasProperty("_BaseColor"))
        {
            Color c = material.GetColor("_BaseColor");
            if (c.maxColorComponent < 0.08f) material.SetColor("_BaseColor", Color.white);
        }
        if (material.HasProperty("_Color"))
        {
            Color c = material.GetColor("_Color");
            if (c.maxColorComponent < 0.08f) material.SetColor("_Color", Color.white);
        }
        EditorUtility.SetDirty(material);
    }

    private static int BuildRecoveredMesh(Mesh source, List<Tri> triangles, HashSet<Tri> selected,
        Renderer target, Material[] raftMaterials, Material oarMaterial)
    {
        Dictionary<int, HashSet<int>> selectedOrdinals = new Dictionary<int, HashSet<int>>();
        foreach (Tri tri in selected)
        {
            if (!selectedOrdinals.TryGetValue(tri.submesh, out HashSet<int> set))
            {
                set = new HashSet<int>();
                selectedOrdinals.Add(tri.submesh, set);
            }
            set.Add(tri.ordinal);
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
            selectedOrdinals.TryGetValue(s, out HashSet<int> moveSet);
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

        Mesh recovered = UnityEngine.Object.Instantiate(source);
        recovered.name = source.name + "_RaftRecovered_v10";
        int originalSubmeshes = source.subMeshCount;
        recovered.subMeshCount = originalSubmeshes + 1;
        for (int s = 0; s < originalSubmeshes; s++)
        {
            MeshTopology topology = source.GetTopology(s);
            if (topology == MeshTopology.Triangles) recovered.SetTriangles(keep[s], s, false);
            else recovered.SetIndices(keep[s].ToArray(), topology, s, false);
        }
        recovered.SetTriangles(oar, originalSubmeshes, false);
        recovered.RecalculateBounds();

        if (AssetDatabase.LoadAssetAtPath<Mesh>(SplitMeshPath) != null)
            AssetDatabase.DeleteAsset(SplitMeshPath);
        AssetDatabase.CreateAsset(recovered, SplitMeshPath);
        recovered = AssetDatabase.LoadAssetAtPath<Mesh>(SplitMeshPath);

        MeshFilter filter = target.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinned = target as SkinnedMeshRenderer;
        if (filter != null) filter.sharedMesh = recovered;
        else if (skinned != null) skinned.sharedMesh = recovered;
        else return 0;

        Material[] materials = new Material[originalSubmeshes + 1];
        for (int i = 0; i < originalSubmeshes; i++)
            materials[i] = i < raftMaterials.Length ? raftMaterials[i] : raftMaterials[0];
        materials[originalSubmeshes] = oarMaterial;
        target.sharedMaterials = materials;

        EditorUtility.SetDirty(target);
        if (filter != null) EditorUtility.SetDirty(filter);
        if (skinned != null) EditorUtility.SetDirty(skinned);
        return moved;
    }

    private static float EdgeAlignment(Tri tri, Vector2 direction)
    {
        Vector2 e01 = new Vector2(tri.p1.x - tri.p0.x, tri.p1.z - tri.p0.z);
        Vector2 e12 = new Vector2(tri.p2.x - tri.p1.x, tri.p2.z - tri.p1.z);
        Vector2 e20 = new Vector2(tri.p0.x - tri.p2.x, tri.p0.z - tri.p2.z);
        float best = 0f;
        if (e01.sqrMagnitude > 0.000001f) best = Mathf.Max(best, Mathf.Abs(Vector2.Dot(e01.normalized, direction)));
        if (e12.sqrMagnitude > 0.000001f) best = Mathf.Max(best, Mathf.Abs(Vector2.Dot(e12.normalized, direction)));
        if (e20.sqrMagnitude > 0.000001f) best = Mathf.Max(best, Mathf.Abs(Vector2.Dot(e20.normalized, direction)));
        return best;
    }

    private static float PerpendicularDistance(Vector2 point, Vector2 linePoint, Vector2 direction)
    {
        Vector2 d = point - linePoint;
        return Mathf.Abs(d.x * direction.y - d.y * direction.x);
    }

    private static Vector2 Longest(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 best = a;
        if (b.sqrMagnitude > best.sqrMagnitude) best = b;
        if (c.sqrMagnitude > best.sqrMagnitude) best = c;
        return best;
    }

    private static float Median(IEnumerable<float> values)
    {
        float[] sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return 0f;
        int mid = sorted.Length / 2;
        return (sorted.Length & 1) == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
    }

    private static float PercentileSorted(float[] sorted, float percentile)
    {
        if (sorted == null || sorted.Length == 0) return 0f;
        percentile = Mathf.Clamp01(percentile);
        float index = percentile * (sorted.Length - 1);
        int lo = Mathf.FloorToInt(index);
        int hi = Mathf.CeilToInt(index);
        if (lo == hi) return sorted[lo];
        return Mathf.Lerp(sorted[lo], sorted[hi], index - lo);
    }

    private static bool Valid(int index, int count)
    {
        return index >= 0 && index < count;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

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

    private static void LogRendererInventory(Transform wrapper)
    {
        string report = "[RAFT V10] Renderer inventory:\n" + string.Join("\n", wrapper.GetComponentsInChildren<Renderer>(true).Select(r =>
        {
            Mesh m = GetMesh(r);
            string mats = string.Join(",", (r.sharedMaterials ?? Array.Empty<Material>()).Select(x => x != null ? x.name : "null"));
            return "  " + AnimationUtility.CalculateTransformPath(r.transform, wrapper) +
                   " mesh=" + (m != null ? m.name : "null") +
                   " verts=" + (m != null ? m.vertexCount : 0) +
                   " mats=" + mats;
        }));
        Debug.Log(report);
    }

    private static void LogTriangleStats(List<Tri> triangles, float centerX, float coreHalf, float outerHalf)
    {
        int leftOuter = triangles.Count(t => t.center.x - centerX <= -coreHalf);
        int rightOuter = triangles.Count(t => t.center.x - centerX >= coreHalf);
        int leftDiag = triangles.Count(t => t.center.x - centerX <= -coreHalf && t.genericDiagonal >= .20f);
        int rightDiag = triangles.Count(t => t.center.x - centerX >= coreHalf && t.genericDiagonal >= .20f);
        Debug.Log("[RAFT V10] Triangle stats total=" + triangles.Count +
                  " centerX=" + centerX.ToString("0.000") +
                  " coreHalf=" + coreHalf.ToString("0.000") +
                  " outerHalf=" + outerHalf.ToString("0.000") +
                  " outerL/R=" + leftOuter + "/" + rightOuter +
                  " diagonalOuterL/R=" + leftDiag + "/" + rightDiag);
    }

    private static void LogFit(LineFit fit, string label)
    {
        if (fit == null)
        {
            Debug.Log("[RAFT V10] " + label + " fit=null");
            return;
        }
        Debug.Log("[RAFT V10] " + label +
                  " seeds=" + fit.seeds.Count +
                  " mean=(" + fit.mean.x.ToString("0.000") + "," + fit.mean.y.ToString("0.000") + ")" +
                  " dir=(" + fit.direction.x.ToString("0.000") + "," + fit.direction.y.ToString("0.000") + ")" +
                  " halfWidth=" + fit.halfWidth.ToString("0.000") +
                  " y=" + fit.yMedian.ToString("0.000") +
                  " yTol=" + fit.yTolerance.ToString("0.000") +
                  " seedThreshold=" + fit.seedThreshold.ToString("0.000"));
    }
}
