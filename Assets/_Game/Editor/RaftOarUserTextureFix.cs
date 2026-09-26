using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the user's supplied Oar Texture.jpg from a tracked repository asset,
/// independent of Raft.zip and Blender's imported material links.
///
/// The previous repair still depended on the locally extracted package texture and
/// on world-space elongated bounds. That was fragile for rotated paddles and could
/// leave the shaft on the raft material and small end-cap renderers on Unity grey.
/// This pass identifies the authored paddle assembly from animation curves first,
/// then names/materials, then rotation-safe local mesh geometry. It forces the same
/// dedicated oar material onto every submesh and nearby end-cap renderer in that
/// assembly. No deck/hull material is changed.
/// </summary>
public static class RaftOarUserTextureFix
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string TexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string MaterialFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string MaterialPath = MaterialFolder + "/OarUserTexture.mat";
    private const string MarkerName = "RaftOarUserTexture_v5";

    [InitializeOnLoadMethod]
    private static void QueueAutomaticRepair()
    {
        // Run after RaftModelImporter, RaftPaddleTextureFix and authored animation
        // hookup have had their delayed editor passes.
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
            EditorUtility.DisplayDialog("Raft Oar Texture", "No change was made. Check the Console diagnostic for the exact missing asset/renderer reason.", "OK");
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (prefab == null)
        {
            Debug.LogWarning("[OAR DIAG] BaseBoat.prefab is missing; oar texture pass skipped.");
            return false;
        }
        if (texture == null)
        {
            Debug.LogError("[OAR DIAG] Tracked user texture is missing at " + TexturePath + ". Pull the latest main branch; do not substitute the raft texture.");
            return false;
        }

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        if (prefabWrapper == null)
        {
            Debug.LogWarning("[OAR DIAG] Uploaded raft wrapper is missing. Apply the authored raft model first.");
            return false;
        }
        if (!force && prefabWrapper.Find(MarkerName) != null) return false;

        ConfigureTexture(TexturePath);
        Material oarMaterial = BuildMaterial(texture);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;
            Transform exactModel = FindAuthoredModelRoot(wrapper);
            if (exactModel == null)
            {
                Debug.LogWarning("[OAR DIAG] Authored model root could not be found inside " + WrapperPath + ".");
                return false;
            }

            Renderer[] all = exactModel.GetComponentsInChildren<Renderer>(true);
            if (all.Length == 0)
            {
                Debug.LogWarning("[OAR DIAG] Authored raft contains no renderers.");
                return false;
            }

            HashSet<Renderer> seeds = new HashSet<Renderer>();
            AddAnimationSeeds(exactModel, seeds);
            AddNamedSeeds(all, seeds);
            AddGeometrySeeds(all, seeds);

            if (seeds.Count == 0)
            {
                Debug.LogError("[OAR DIAG] No paddle/oar renderer could be identified. No raft renderer was modified.");
                LogRendererInventory(exactModel, all, null);
                return false;
            }

            HashSet<Renderer> targets = new HashSet<Renderer>(seeds);
            ExpandToAssembly(all, seeds, targets);

            foreach (Renderer renderer in targets)
                AssignEverySubmesh(renderer, oarMaterial);

            Transform oldMarker = wrapper.Find(MarkerName);
            if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            StringBuilder report = new StringBuilder();
            report.AppendLine("[OAR DIAG] User oar texture applied successfully.");
            report.AppendLine("Texture: " + TexturePath + "  " + texture.width + "x" + texture.height);
            report.AppendLine("Seed renderers: " + seeds.Count + "  Final textured renderers: " + targets.Count);
            foreach (Renderer renderer in targets.OrderBy(r => AnimationUtility.CalculateTransformPath(r.transform, exactModel)))
            {
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, exactModel);
                int submeshes = GetSubMeshCount(renderer);
                report.AppendLine("  OAR TARGET: " + path + " | " + renderer.GetType().Name + " | submeshes=" + submeshes + " | slots=" + renderer.sharedMaterials.Length);
            }
            Debug.Log(report.ToString());

            if (force)
                EditorUtility.DisplayDialog(
                    "Raft Oar Texture Fixed",
                    "The tracked Oar Texture.jpg is now forced onto every identified oar submesh and end-cap renderer. A detailed OAR DIAG renderer report was written to the Console.",
                    "OK");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AddAnimationSeeds(Transform exactModel, HashSet<Renderer> seeds)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath) == null) return;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(BlendPath);
        foreach (UnityEngine.Object asset in assets)
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                string property = binding.propertyName ?? string.Empty;
                if (property.IndexOf("Rotation", StringComparison.OrdinalIgnoreCase) < 0 &&
                    property.IndexOf("Euler", StringComparison.OrdinalIgnoreCase) < 0 &&
                    property.IndexOf("Position", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Transform animated = ResolvePath(exactModel, binding.path);
                if (animated == null) continue;
                Renderer[] descendants = animated.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in descendants)
                    if (LooksLikeOarByName(r) || LooksElongated(r)) seeds.Add(r);

                Renderer own = animated.GetComponent<Renderer>();
                if (own != null) seeds.Add(own);
            }
        }
    }

    private static Transform ResolvePath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path)) return root;
        Transform direct = root.Find(path);
        if (direct != null) return direct;

        string prefix = root.name + "/";
        if (path.StartsWith(prefix, StringComparison.Ordinal))
        {
            direct = root.Find(path.Substring(prefix.Length));
            if (direct != null) return direct;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            string relative = AnimationUtility.CalculateTransformPath(t, root);
            if (string.Equals(relative, path, StringComparison.Ordinal) ||
                path.EndsWith("/" + relative, StringComparison.Ordinal))
                return t;
        }
        return null;
    }

    private static void AddNamedSeeds(Renderer[] renderers, HashSet<Renderer> seeds)
    {
        foreach (Renderer renderer in renderers)
            if (LooksLikeOarByName(renderer)) seeds.Add(renderer);
    }

    private static bool LooksLikeOarByName(Renderer renderer)
    {
        if (renderer == null) return false;
        string name = renderer.name ?? string.Empty;
        if (ContainsOarWord(name)) return true;
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null) continue;
            if (ContainsOarWord(material.name)) return true;
            Texture main = material.mainTexture;
            if (main != null && ContainsOarWord(main.name)) return true;
        }
        return false;
    }

    private static bool ContainsOarWord(string value)
    {
        return !string.IsNullOrEmpty(value) &&
            (value.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
             value.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void AddGeometrySeeds(Renderer[] renderers, HashSet<Renderer> seeds)
    {
        // Geometry fallback is deliberately conservative: choose at most the two
        // strongest long/narrow authored meshes. Local mesh bounds make this safe
        // even though the visible paddles are diagonally rotated in the prefab.
        List<Tuple<Renderer, float>> candidates = new List<Tuple<Renderer, float>>();
        foreach (Renderer renderer in renderers)
        {
            if (!LooksElongated(renderer)) continue;
            Bounds local = LocalMeshBounds(renderer);
            Vector3 s = local.size;
            float largest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            float smallest = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            float middle = s.x + s.y + s.z - largest - smallest;
            float ratio = largest / Mathf.Max(.01f, middle);
            float score = ratio * 10f + largest - middle - smallest;
            candidates.Add(Tuple.Create(renderer, score));
        }

        foreach (Tuple<Renderer, float> candidate in candidates.OrderByDescending(c => c.Item2).Take(2))
            seeds.Add(candidate.Item1);
    }

    private static bool LooksElongated(Renderer renderer)
    {
        Bounds b = LocalMeshBounds(renderer);
        Vector3 s = b.size;
        float largest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        float smallest = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        float middle = s.x + s.y + s.z - largest - smallest;
        return largest >= 1.45f && largest <= 5.0f && middle <= 1.15f &&
               largest / Mathf.Max(.01f, middle) >= 3.0f && smallest <= .55f;
    }

    private static Bounds LocalMeshBounds(Renderer renderer)
    {
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null && skinned.sharedMesh != null) return skinned.sharedMesh.bounds;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null) return filter.sharedMesh.bounds;
        return new Bounds(Vector3.zero, renderer.bounds.size);
    }

    private static void ExpandToAssembly(Renderer[] all, HashSet<Renderer> seeds, HashSet<Renderer> targets)
    {
        foreach (Renderer seed in seeds)
        {
            if (seed == null) continue;
            Bounds expanded = seed.bounds;
            expanded.Expand(.34f);

            foreach (Renderer candidate in all)
            {
                if (candidate == null || targets.Contains(candidate)) continue;

                // Direct hierarchy relationships are the strongest evidence for a
                // separately-authored paddle cap/handle component.
                if (candidate.transform.IsChildOf(seed.transform) || seed.transform.IsChildOf(candidate.transform) ||
                    candidate.transform.parent == seed.transform.parent)
                {
                    if (IsSmallCompanion(candidate) || LooksLikeOarByName(candidate)) targets.Add(candidate);
                    continue;
                }

                // End caps can import as tiny sibling objects with generic names.
                // Only absorb small renderers physically touching an identified oar.
                if (IsSmallCompanion(candidate) && expanded.Intersects(candidate.bounds))
                    targets.Add(candidate);
            }
        }
    }

    private static bool IsSmallCompanion(Renderer renderer)
    {
        Vector3 s = renderer.bounds.size;
        float largest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        return largest <= .85f;
    }

    private static void AssignEverySubmesh(Renderer renderer, Material material)
    {
        int submeshes = GetSubMeshCount(renderer);
        int existing = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0;
        int count = Mathf.Max(1, Mathf.Max(submeshes, existing));
        Material[] slots = new Material[count];
        for (int i = 0; i < slots.Length; i++) slots[i] = material;
        renderer.sharedMaterials = slots;
    }

    private static int GetSubMeshCount(Renderer renderer)
    {
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null && skinned.sharedMesh != null) return skinned.sharedMesh.subMeshCount;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : 0;
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

    private static Material BuildMaterial(Texture2D texture)
    {
        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh();
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
        bool changed = importer.textureType != TextureImporterType.Default || !importer.sRGBTexture ||
                       importer.wrapMode != TextureWrapMode.Repeat || importer.maxTextureSize < 2048;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        if (changed) importer.SaveAndReimport();
    }

    private static void LogRendererInventory(Transform root, Renderer[] renderers, HashSet<Renderer> targets)
    {
        StringBuilder report = new StringBuilder("[OAR DIAG] Authored renderer inventory:\n");
        foreach (Renderer renderer in renderers)
        {
            string path = AnimationUtility.CalculateTransformPath(renderer.transform, root);
            Bounds local = LocalMeshBounds(renderer);
            report.Append("  ").Append(path)
                .Append(" | localSize=").Append(local.size.ToString("F3"))
                .Append(" | slots=").Append(renderer.sharedMaterials.Length)
                .Append(" | target=").Append(targets != null && targets.Contains(renderer))
                .AppendLine();
        }
        Debug.Log(report.ToString());
    }
}