using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies ONLY the lighting added in the newer crankbait FBX.
///
/// The uploaded "w lighting" FBX has the exact same three lure meshes, texture,
/// and the exact same three authored retrieve animation stacks as the current
/// version. Its meaningful visual addition is two directional lights (Sun and
/// Sun.001). This tool copies only those lights onto the existing prefab, so the
/// current mesh sizing, hook placement, Animator/controller, LineAttach transform,
/// and runtime retrieve/facing movement are left completely untouched.
/// </summary>
public static class LiplessCrankbaitLightingUpdate
{
    private const string PrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string TempFolder = "Assets/_Game/Fishing/LiplessCrankbait/LightingImportTemp";
    private const string TempFbxPath = TempFolder + "/LiplessCrankbaitGreenStripedLighting.fbx";
    private const string LightingRigName = "AuthoredLightingRig";

    // This is the exact FBX from "Lipless Crankbait Green Striped w lighting.zip"
    // supplied for this update. Prefer it even when the older lure ZIP is still
    // sitting beside it in Downloads.
    private const string ExpectedLightingFbxSha256 =
        "6efa7134b83d180fd11d9bf0ed4ce3353cf6615d7f54f80ff14f39cc657ab968";

    // Layer 30 is unused in this project. The gameplay camera and URP renderers
    // already render all layers. Putting only the lure renderers on this layer lets
    // the two authored directional lights affect the lure without relighting the
    // entire island every time the lure turns toward the player.
    private const int LureLightingLayer = 30;

    [MenuItem("Tools/Open World/Apply Lipless Crankbait NEW LIGHTING (One Click)")]
    public static void ApplyOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Lipless Crankbait Lighting", "Exit Play Mode first.", "OK");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "Lipless Crankbait Lighting",
                "The existing lure prefab is missing. Reimport the working lure first, then apply this lighting-only update.",
                "OK");
            return;
        }

        string zipPath = FindLightingZip();
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            zipPath = EditorUtility.OpenFilePanel(
                "Choose Lipless Crankbait Green Striped w lighting.zip",
                Directory.Exists(downloads) ? downloads : string.Empty,
                "zip");
        }
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        try
        {
            EditorUtility.DisplayProgressBar("Crankbait lighting", "Verifying the new lighting FBX...", 0.12f);
            ExtractVerifiedLightingFbx(zipPath);

            EditorUtility.DisplayProgressBar("Crankbait lighting", "Importing the two authored lights...", 0.40f);
            ConfigureLightingSource();

            EditorUtility.DisplayProgressBar("Crankbait lighting", "Applying lighting without touching lure movement...", 0.72f);
            ApplyLightingToExistingPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            EditorUtility.DisplayDialog(
                "Crankbait Lighting Updated",
                "Applied the two lights from the new model only. The existing lure mesh/size, hooks, animation controller, LineAttach position, line movement, and retrieve-facing behavior were not rebuilt or changed.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Crankbait Lighting Failed",
                exception.Message + "\n\nThe full error is in the Unity Console.",
                "OK");
        }
        finally
        {
            // The imported FBX is only a source from which we copy light settings;
            // the finished prefab has no dependency on this temporary asset.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TempFbxPath) != null)
                AssetDatabase.DeleteAsset(TempFbxPath);
            EditorUtility.ClearProgressBar();
        }
    }

    private static void ExtractVerifiedLightingFbx(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("The lighting ZIP was not found.", zipPath);

        EnsureFolderRecursive(TempFolder);

        using FileStream stream = File.OpenRead(zipPath);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry fbx = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));

        if (fbx == null)
            throw new InvalidDataException("The selected ZIP does not contain a crankbait FBX.");

        string hash;
        using (Stream input = fbx.Open())
        using (SHA256 sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", string.Empty).ToLowerInvariant();

        if (!string.Equals(hash, ExpectedLightingFbxSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "That is not the supplied 'w lighting' crankbait FBX. Choose 'Lipless Crankbait Green Striped w lighting.zip' so the existing animation/line setup cannot accidentally be replaced by a different model revision.");

        string absolute = Path.GetFullPath(TempFbxPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        using Stream source = fbx.Open();
        using FileStream output = File.Create(absolute);
        source.CopyTo(output);
        AssetDatabase.Refresh();
    }

    private static void ConfigureLightingSource()
    {
        AssetDatabase.ImportAsset(
            TempFbxPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        ModelImporter importer = AssetImporter.GetAtPath(TempFbxPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Unity could not import the lighting FBX.");

        // We are deliberately NOT importing/replacing animation from this FBX.
        // Animation remains whatever is already proven to work in the live lure.
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.preserveHierarchy = true;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.SaveAndReimport();
    }

    private static void ApplyLightingToExistingPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TempFbxPath);
        if (source == null)
            throw new InvalidOperationException("The imported lighting FBX could not be loaded.");

        Light[] authoredLights = source.GetComponentsInChildren<Light>(true);
        if (authoredLights.Length != 2)
            throw new InvalidOperationException(
                "Expected exactly the two authored lights (Sun and Sun.001), but Unity imported " +
                authoredLights.Length + ". Nothing was changed on the working lure prefab.");

        if (authoredLights.Any(l => l == null || l.type != LightType.Directional))
            throw new InvalidOperationException(
                "The supplied lighting revision is expected to contain two directional Sun lights. Nothing was changed because the imported light setup did not match that revision.");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform authoredModel = FindDeepChild(root.transform, "AuthoredModel");
            if (authoredModel == null)
                throw new InvalidOperationException("The working lure prefab has no AuthoredModel child.");

            // Snapshot invariants before doing anything. These values are checked
            // again after the lighting rig is added so this tool cannot silently
            // change the exact pieces the user asked us to preserve.
            Transform lineAttach = FindDeepChild(root.transform, "LineAttach");
            if (lineAttach == null)
                throw new InvalidOperationException("The working lure prefab has no LineAttach transform.");

            Transform lineParent = lineAttach.parent;
            Vector3 linePosition = lineAttach.localPosition;
            Quaternion lineRotation = lineAttach.localRotation;
            Vector3 lineScale = lineAttach.localScale;

            Animator animator = root.GetComponentInChildren<Animator>(true);
            RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
            Vector3 rootScale = root.transform.localScale;

            Transform oldRig = FindDirectChild(authoredModel, LightingRigName);
            if (oldRig != null)
                UnityEngine.Object.DestroyImmediate(oldRig.gameObject);

            GameObject rigObject = new GameObject(LightingRigName);
            Transform rig = rigObject.transform;
            rig.SetParent(authoredModel, false);
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
            rig.localScale = Vector3.one;

            // Isolate the Blender-authored Sun lights to the lure itself. The world
            // lights can still illuminate the lure normally because their default
            // culling mask includes this otherwise-unused layer.
            Renderer[] lureRenderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < lureRenderers.Length; i++)
            {
                Renderer renderer = lureRenderers[i];
                if (renderer != null)
                    renderer.gameObject.layer = LureLightingLayer;
            }

            for (int i = 0; i < authoredLights.Length; i++)
            {
                Light sourceLight = authoredLights[i];
                GameObject lightObject = new GameObject(sourceLight.name);
                lightObject.layer = LureLightingLayer;
                lightObject.transform.SetParent(rig, false);

                // Directional-light position is irrelevant. Copy only its exact
                // authored orientation relative to the FBX root.
                lightObject.transform.localPosition = Vector3.zero;
                lightObject.transform.localRotation =
                    Quaternion.Inverse(source.transform.rotation) * sourceLight.transform.rotation;
                lightObject.transform.localScale = Vector3.one;

                Light target = lightObject.AddComponent<Light>();
                target.type = LightType.Directional;
                target.color = sourceLight.color;
                target.intensity = sourceLight.intensity;
                target.bounceIntensity = sourceLight.bounceIntensity;
                target.colorTemperature = sourceLight.colorTemperature;
                target.useColorTemperature = sourceLight.useColorTemperature;
                target.shadowStrength = sourceLight.shadowStrength;
                target.shadows = LightShadows.None;
                target.cullingMask = 1 << LureLightingLayer;
                target.renderMode = LightRenderMode.ForcePixel;
            }

            // Hard guarantees: line attachment, root scale and Animator/controller
            // must be byte-for-byte-equivalent values after this lighting-only edit.
            if (lineAttach.parent != lineParent ||
                lineAttach.localPosition != linePosition ||
                lineAttach.localRotation != lineRotation ||
                lineAttach.localScale != lineScale)
                throw new InvalidOperationException("Lighting update tried to alter LineAttach; prefab save was aborted.");

            Animator animatorAfter = root.GetComponentInChildren<Animator>(true);
            if (animatorAfter == null || animatorAfter.runtimeAnimatorController != controller)
                throw new InvalidOperationException("Lighting update tried to alter the lure Animator/controller; prefab save was aborted.");

            if (root.transform.localScale != rootScale)
                throw new InvalidOperationException("Lighting update tried to alter lure scale; prefab save was aborted.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string FindLightingZip()
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

            string[] candidates = Directory.GetFiles(folder, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(file => NormalizeName(Path.GetFileNameWithoutExtension(file))
                    .Contains("liplesscrankbaitgreenstriped"))
                .OrderByDescending(file =>
                    NormalizeName(Path.GetFileNameWithoutExtension(file)).Contains("wlighting") ? 1 : 0)
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            // Prefer the exact uploaded FBX by content hash, not merely by filename.
            for (int i = 0; i < candidates.Length; i++)
                if (ZipContainsExpectedLightingFbx(candidates[i]))
                    return candidates[i];
        }

        return null;
    }

    private static bool ZipContainsExpectedLightingFbx(string zipPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(zipPath);
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
            ZipArchiveEntry fbx = archive.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
            if (fbx == null) return false;

            using Stream input = fbx.Open();
            using SHA256 sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", string.Empty).ToLowerInvariant();
            return string.Equals(hash, ExpectedLightingFbxSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeName(string value)
    {
        return (value ?? string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name)
                return all[i];
        return null;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == name)
                return child;
        }
        return null;
    }

    private static void EnsureFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
