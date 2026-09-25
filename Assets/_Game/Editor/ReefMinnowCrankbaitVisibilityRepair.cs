using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds Reef Minnow from the already-proven Neon Breach prefab instead of
/// baking a second mesh through FBX transform space. The uploaded Reef Minnow
/// STL is byte-identical in geometry/topology to the approved crankbait model,
/// so the safest faithful import is to keep the known-visible mesh/UV/hook/
/// animation/lighting hierarchy and replace only the authored black/blue texture.
/// This avoids the bad mesh-space bake that could make the previous Reef Minnow
/// body disappear through culling/bounds/transform issues.
/// </summary>
public static class ReefMinnowCrankbaitVisibilityRepair
{
    private const string ZipStem = "Lipless Crankbait Black with Blue Stripes";
    private const string BasePrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string OutputPrefabPath = "Assets/Resources/Fishing/ReefMinnowCrankbait.prefab";
    private const string Folder = "Assets/_Game/Fishing/ReefMinnow";
    private const string TexturePath = Folder + "/Source/BodyTexture.jpg";
    private const string MaterialPath = Folder + "/ReefMinnowVisibleBody.mat";
    private const string AutoSessionKey = "OpenWorld.AutoRepair.ReefMinnowVisible.20260925.v2";

    private const string ExpectedStlSha256 = "ecb16deecf91e3b7a67f239a379a2f6de72a10540e97e5b40280bae5f8d9a499";
    private const string ExpectedBlendSha256 = "bd13ce359bd110ed2b0b9ba5a6ba14b5c5dc8e8e4dd85fdf847402ebd58f5f0f";
    private const string ExpectedTextureSha256 = "9b57567f44a8c8000ebf332ca4d9f3be920c3ebb9df299dec8f55dc58a3b885e";
    private const int ExpectedTriangleCount = 1720;

    [InitializeOnLoadMethod]
    private static void AutoRepairAfterPull()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoSessionKey, false)) return;
        SessionState.SetBool(AutoSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) == null) return;
            string package = FindPackage();
            if (string.IsNullOrWhiteSpace(package)) return;

            try
            {
                Repair(package, false);
                Debug.Log("Reef Minnow was automatically rebuilt from the proven visible crankbait template.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        };
    }

    [MenuItem("Tools/Open World/Reimport Reef Minnow Crankbait FIXED (One Click)")]
    public static void RepairOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Reef Minnow", "Exit Play Mode first.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Reef Minnow", "Neon Breach is missing, so the proven visible crankbait template is unavailable.", "OK");
            return;
        }

        string package = FindPackage();
        if (string.IsNullOrWhiteSpace(package))
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            package = EditorUtility.OpenFilePanel(
                "Choose Lipless Crankbait Black with Blue Stripes.zip",
                Directory.Exists(downloads) ? downloads : string.Empty,
                "zip");
        }
        if (string.IsNullOrWhiteSpace(package)) return;

        try
        {
            Repair(package, true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Reef Minnow Reimport Failed", exception.Message + "\n\nSee Console for the full error.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void Repair(string package, bool showDialog)
    {
        EditorUtility.DisplayProgressBar("Reef Minnow", "Validating exact black/blue package...", 0.12f);
        byte[] textureBytes = ValidateAndReadTexture(package);

        EnsureFolderRecursive(Folder + "/Source");
        EnsureFolderRecursive("Assets/Resources/Fishing");
        string absoluteTexture = Path.GetFullPath(TexturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteTexture));
        if (!File.Exists(absoluteTexture) || !File.ReadAllBytes(absoluteTexture).SequenceEqual(textureBytes))
            File.WriteAllBytes(absoluteTexture, textureBytes);

        EditorUtility.DisplayProgressBar("Reef Minnow", "Importing authored black/blue texture...", 0.34f);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ConfigureTexture();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null) throw new InvalidOperationException("Unity could not load the Reef Minnow texture.");

        EditorUtility.DisplayProgressBar("Reef Minnow", "Rebuilding from the proven visible crankbait mesh...", 0.62f);
        GameObject root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
        try
        {
            Transform authoredModel = FindDeepChild(root.transform, "AuthoredModel");
            Transform lineAttach = FindDeepChild(root.transform, "LineAttach");
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (authoredModel == null || lineAttach == null || animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("Neon Breach is missing AuthoredModel, LineAttach or its Animator/controller.");

            MeshFilter[] filters = authoredModel.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f != null && f.sharedMesh != null && !IsUnderLightingRig(f.transform, authoredModel)).ToArray();
            if (filters.Length != 3)
                throw new InvalidOperationException("Expected exactly the visible body and two hook meshes in Neon Breach, found " + filters.Length + ".");

            MeshFilter body = LargestMesh(filters);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer == null || bodyRenderer.sharedMaterial == null)
                throw new InvalidOperationException("Could not identify the visible Neon Breach body renderer/material.");

            // Validate the template body itself before cloning it. This makes a
            // broken/invisible mesh impossible to silently become Reef Minnow.
            int bodyTriangles = CountTriangles(body.sharedMesh);
            if (bodyTriangles <= 0 || body.sharedMesh.vertexCount <= 0 || body.sharedMesh.bounds.size.sqrMagnitude <= 0.000001f)
                throw new InvalidOperationException("The approved crankbait template body mesh is invalid or empty.");

            Transform lineParent = lineAttach.parent;
            Vector3 linePosition = lineAttach.localPosition;
            Quaternion lineRotation = lineAttach.localRotation;
            Vector3 lineScale = lineAttach.localScale;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Vector3 rootPosition = root.transform.localPosition;
            Quaternion rootRotation = root.transform.localRotation;
            Vector3 rootScale = root.transform.localScale;
            Dictionary<Transform, TransformSnapshot> transforms = filters.ToDictionary(f => f.transform, f => new TransformSnapshot(f.transform));

            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                AssetDatabase.DeleteAsset(MaterialPath);
            Material material = new Material(bodyRenderer.sharedMaterial) { name = "Reef Minnow Black Blue Body" };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            else material.mainTexture = texture;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, MaterialPath);

            Material[] slots = new Material[Mathf.Max(1, bodyRenderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            bodyRenderer.sharedMaterials = slots;

            // Explicit visibility checks: keep the exact template mesh and make
            // sure the body renderer was not inherited in a disabled state.
            authoredModel.gameObject.SetActive(true);
            body.gameObject.SetActive(true);
            bodyRenderer.enabled = true;
            root.name = "ReefMinnowCrankbait";

            if (lineAttach.parent != lineParent ||
                !Approximately(lineAttach.localPosition, linePosition) ||
                Quaternion.Angle(lineAttach.localRotation, lineRotation) > 0.0001f ||
                !Approximately(lineAttach.localScale, lineScale))
                throw new InvalidOperationException("Reef Minnow rebuild altered LineAttach. Save aborted.");
            if (root.GetComponentInChildren<Animator>(true).runtimeAnimatorController != controller)
                throw new InvalidOperationException("Reef Minnow rebuild altered the animation controller. Save aborted.");
            if (!Approximately(root.transform.localPosition, rootPosition) ||
                Quaternion.Angle(root.transform.localRotation, rootRotation) > 0.0001f ||
                !Approximately(root.transform.localScale, rootScale))
                throw new InvalidOperationException("Reef Minnow rebuild altered lure size/orientation. Save aborted.");
            foreach (var pair in transforms)
                if (!pair.Value.Matches(pair.Key))
                    throw new InvalidOperationException("Reef Minnow rebuild altered animated transform '" + pair.Key.name + "'. Save aborted.");

            PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
        if (saved == null) throw new InvalidOperationException("Reef Minnow prefab was not saved.");
        Renderer[] savedRenderers = saved.GetComponentsInChildren<Renderer>(true);
        if (!savedRenderers.Any(r => r != null && r.enabled))
            throw new InvalidOperationException("Reef Minnow saved without an enabled renderer.");

        Selection.activeObject = saved;
        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Reef Minnow Reimported",
                "Reef Minnow was rebuilt from the known-visible Neon Breach mesh/hierarchy and the supplied black/blue texture. Hooks, animation, LineAttach, lighting, size, orientation and fishing mechanics remain unchanged.",
                "OK");
        }
    }

    private static byte[] ValidateAndReadTexture(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Reef Minnow ZIP was not found.", path);
        using FileStream stream = File.OpenRead(path);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry stl = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry blend = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".blend", StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry texture = archive.Entries.FirstOrDefault(e =>
        {
            string ext = Path.GetExtension(e.FullName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
        });
        if (stl == null || blend == null || texture == null)
            throw new InvalidDataException("The Reef Minnow package must contain its STL, Blend file and texture.");

        byte[] stlBytes = ReadAll(stl);
        byte[] blendBytes = ReadAll(blend);
        byte[] textureBytes = ReadAll(texture);
        if (!HashEquals(stlBytes, ExpectedStlSha256) ||
            !HashEquals(blendBytes, ExpectedBlendSha256) ||
            !HashEquals(textureBytes, ExpectedTextureSha256))
            throw new InvalidDataException("Choose the exact supplied '" + ZipStem + ".zip'.");
        if (stlBytes.Length < 84 || BitConverter.ToUInt32(stlBytes, 80) != ExpectedTriangleCount)
            throw new InvalidDataException("The Reef Minnow STL topology does not match the supplied revision.");
        return textureBytes;
    }

    private static void ConfigureTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Could not configure the Reef Minnow texture importer.");
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static MeshFilter LargestMesh(MeshFilter[] filters)
    {
        MeshFilter best = filters[0];
        float score = -1f;
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            Bounds bounds = mesh.bounds;
            Vector3 size = bounds.size;
            float candidate = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * Mathf.Max(0.000001f, size.x * size.y * size.z);
            if (candidate > score) { score = candidate; best = filters[i]; }
        }
        return best;
    }

    private static int CountTriangles(Mesh mesh)
    {
        int count = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles) count += (int)mesh.GetIndexCount(i) / 3;
        return count;
    }

    private static bool IsUnderLightingRig(Transform transform, Transform authoredModel)
    {
        for (Transform current = transform; current != null && current != authoredModel; current = current.parent)
            if (current.name == "AuthoredLightingRig") return true;
        return false;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static string FindPackage()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] folders =
        {
            Path.Combine(user, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.GetFullPath(".")
        };
        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] matches = Directory.GetFiles(folder, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(f => Normalize(Path.GetFileNameWithoutExtension(f)).Contains(Normalize(ZipStem)))
                .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
            for (int i = 0; i < matches.Length; i++)
            {
                try { ValidateAndReadTexture(matches[i]); return matches[i]; }
                catch { }
            }
        }
        return null;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using Stream input = entry.Open();
        using MemoryStream memory = new MemoryStream();
        input.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool HashEquals(byte[] bytes, string expected)
    {
        using SHA256 sha = SHA256.Create();
        string actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) => (value ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 0.0000000001f;

    private static void EnsureFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;
        public TransformSnapshot(Transform t) { position = t.localPosition; rotation = t.localRotation; scale = t.localScale; }
        public bool Matches(Transform t) => Approximately(t.localPosition, position) && Quaternion.Angle(t.localRotation, rotation) <= 0.0001f && Approximately(t.localScale, scale);
    }
}
