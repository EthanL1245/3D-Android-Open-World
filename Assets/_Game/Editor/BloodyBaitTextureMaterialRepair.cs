using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs Bloody Bait's body material with a fresh supported URP material and
/// the exact supplied blue/red texture. The mesh, hooks, animation, LineAttach,
/// size, orientation, lighting and fishing mechanics are left unchanged.
/// </summary>
public static class BloodyBaitTextureMaterialRepair
{
    private const string PrefabPath = "Assets/Resources/Fishing/BloodyBaitCrankbait.prefab";
    private const string TexturePath = "Assets/_Game/Fishing/BloodyBait/Source/BodyTexture.jpg";
    private const string MaterialPath = "Assets/_Game/Fishing/BloodyBait/BloodyBaitURPBody.mat";
    private const string PackageFileName = "Lipless Crankbait Blue with Red Head.zip";
    private const string ExpectedTextureSha256 = "9359cd871e246d400048582eb44447fccefa10687e3b399b1eea72352803bc57";
    private const string AutoSessionKey = "OpenWorld.AutoRepair.BloodyBaitTextureMaterial.20260925.v1";

    [InitializeOnLoadMethod]
    private static void AutoRepairAfterPull()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoSessionKey, false)) return;
        SessionState.SetBool(AutoSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            // Wait one extra editor tick so any Bloody Bait importer that just ran
            // has finished writing/reimporting its prefab before we enforce material.
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;
                try
                {
                    Repair(false);
                    Debug.Log("Bloody Bait blue/red texture material was automatically repaired.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        };
    }

    [MenuItem("Tools/Open World/Fix Bloody Bait Texture Material (One Click)")]
    public static void RepairOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bloody Bait", "Exit Play Mode first.", "OK");
            return;
        }

        try
        {
            Repair(true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Bloody Bait Material Repair Failed",
                exception.Message + "\n\nSee Console for the full error.",
                "OK");
        }
    }

    private static void Repair(bool showDialog)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            throw new FileNotFoundException("Bloody Bait prefab has not been imported yet.", PrefabPath);

        EnsureTextureAsset();
        ConfigureTexture();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
            throw new InvalidOperationException("Bloody Bait blue/red texture is still missing after import.");

        // Never clone another lure's material here. A copied material can carry a
        // stale/broken shader reference and display as pink or untextured. Build
        // this material explicitly from a shader known to work in this URP project.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Resources.Load<Shader>("Fishing/FishingLit");
        if (shader == null)
            throw new InvalidOperationException("No supported URP fishing shader is available.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.name = "Bloody Bait Blue Red Body";
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.12f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.48f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform authoredModel = FindDeepChild(root.transform, "AuthoredModel");
            if (authoredModel == null)
                throw new InvalidOperationException("Bloody Bait prefab has no AuthoredModel.");

            MeshFilter[] filters = authoredModel.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f != null && f.sharedMesh != null && !IsUnderLightingRig(f.transform, authoredModel))
                .ToArray();
            if (filters.Length == 0)
                throw new InvalidOperationException("Bloody Bait prefab has no visible mesh filters.");

            MeshFilter body = LargestMesh(filters);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer == null)
                throw new InvalidOperationException("Bloody Bait body renderer is missing.");

            Material[] slots = new Material[Mathf.Max(1, bodyRenderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            bodyRenderer.sharedMaterials = slots;
            bodyRenderer.enabled = true;
            body.gameObject.SetActive(true);
            authoredModel.gameObject.SetActive(true);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MaterialPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Bloody Bait Fixed",
                "The supplied blue/red texture is now assigned through a fresh supported URP material. Hooks, animation, LineAttach, size, orientation and fishing mechanics were not changed.",
                "OK");
        }
    }

    private static void EnsureTextureAsset()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null) return;

        string package = FindPackage();
        if (string.IsNullOrWhiteSpace(package))
            throw new FileNotFoundException(
                "Could not find '" + PackageFileName + "' in Downloads/Desktop. Keep that ZIP there once so the authored texture can be restored.");

        using FileStream stream = File.OpenRead(package);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry texture = archive.Entries.FirstOrDefault(e =>
        {
            string ext = Path.GetExtension(e.FullName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
        });
        if (texture == null)
            throw new InvalidDataException("Bloody Bait ZIP has no texture image.");

        byte[] bytes = ReadAll(texture);
        if (!HashEquals(bytes, ExpectedTextureSha256))
            throw new InvalidDataException("The Bloody Bait texture does not match the supplied blue/red revision.");

        EnsureFolderRecursive("Assets/_Game/Fishing/BloodyBait/Source");
        string absolute = Path.GetFullPath(TexturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllBytes(absolute, bytes);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not configure Bloody Bait texture importer.");
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
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

            string exact = Path.Combine(folder, PackageFileName);
            if (File.Exists(exact)) return exact;

            string normalized = Normalize(Path.GetFileNameWithoutExtension(PackageFileName));
            string match = Directory.GetFiles(folder, "*.zip", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => Normalize(Path.GetFileNameWithoutExtension(f)).Contains(normalized));
            if (!string.IsNullOrWhiteSpace(match)) return match;
        }
        return null;
    }

    private static MeshFilter LargestMesh(MeshFilter[] filters)
    {
        MeshFilter best = filters[0];
        float score = -1f;
        foreach (MeshFilter filter in filters)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 size = bounds.size;
            float candidate = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) *
                              Mathf.Max(0.000001f, size.x * size.y * size.z);
            if (candidate > score)
            {
                score = candidate;
                best = filter;
            }
        }
        return best;
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
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
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

    private static string Normalize(string value) =>
        (value ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

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
}
