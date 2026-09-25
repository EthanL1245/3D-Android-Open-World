using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public static class LiplessCrankbaitImporter
{
    private const string Root = "Assets/_Game/Fishing/LiplessCrankbait";
    private const string Source = Root + "/Source";
    private const string TexturePath = Source + "/LiplessCrankbaitGreenStriped.jpg";
    private const string FbxPath = Source + "/LiplessCrankbaitGreenStriped.fbx";
    private const string BodyMaterialPath = Root + "/LiplessCrankbaitGreenStriped.mat";
    private const string HookMaterialPath = Root + "/LiplessCrankbaitHooks.mat";
    private const string ControllerPath = Root + "/LiplessCrankbaitGreenStriped.controller";
    private const string PrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";

    // The old import normalized the authored model to 0.16 m and the game later
    // doubled it. Keep that intended visible size, but apply it once to the whole
    // lure (body + both treble hooks) so it can never become person-sized again.
    private const float TargetOverallLengthMetres = 0.32f;

    [MenuItem("Tools/Open World/Reimport Lipless Crankbait FIXED (One Click)")]
    public static void InstallOneClick()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Lipless Crankbait", "Exit Play Mode first.", "OK");
            return;
        }

        string zipPath = FindZip();
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            zipPath = EditorUtility.OpenFilePanel(
                "Choose Lipless Crankbait Green Striped FBX ZIP",
                Directory.Exists(downloads) ? downloads : string.Empty,
                "zip");
        }
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        try
        {
            Import(zipPath);
            EditorUtility.DisplayDialog(
                "Lipless Crankbait Reimported",
                "Imported the supplied FBX directly (no Blender re-export), fixed the body/hook animation pairing, normalized the complete lure to 32 cm end-to-end, kept the authored orientation, attached the line to the body nose, and made the shell render correctly from both sides.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Crankbait Import Failed",
                exception.Message + "\n\nThe full error is in the Unity Console.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void Import(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("The crankbait ZIP was not found.", zipPath);

        EnsureFolderRecursive(Source);
        EnsureFolderRecursive("Assets/Resources/Fishing");

        EditorUtility.DisplayProgressBar("Lipless Crankbait", "Extracting the supplied FBX directly...", 0.12f);
        ExtractDirectFbx(zipPath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayProgressBar("Lipless Crankbait", "Importing authored mesh and animations...", 0.38f);
        ConfigureTexture();
        ConfigureFbx();

        EditorUtility.DisplayProgressBar("Lipless Crankbait", "Rebuilding lure with correct hooks and scale...", 0.70f);
        BuildPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void ExtractDirectFbx(string zipPath)
    {
        using FileStream stream = File.OpenRead(zipPath);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);

        ZipArchiveEntry fbx = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry texture = archive.Entries.FirstOrDefault(e =>
        {
            string ext = Path.GetExtension(e.FullName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
        });

        if (fbx == null)
            throw new InvalidDataException(
                "Use the new FBX ZIP. It must contain Lipless Crankbait Green Striped.fbx. The importer intentionally no longer re-exports the Blend file because that was changing the model hierarchy/transforms.");
        if (texture == null)
            throw new InvalidDataException("The crankbait ZIP must also contain its texture image.");

        WriteEntry(fbx, FbxPath);
        WriteEntry(texture, TexturePath);
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(TexturePath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not import the crankbait texture.");

        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ConfigureFbx()
    {
        AssetDatabase.ImportAsset(FbxPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not import the supplied crankbait FBX.");

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.preserveHierarchy = true;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.SaveAndReimport();

        // The supplied FBX contains three simultaneously-authored object actions
        // (body, belly hook, tail hook). Keep all of them loopable; BuildController
        // combines the three matching clips instead of incorrectly choosing only one.
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = true;
                clips[i].loopPose = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static void BuildPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (source == null)
            throw new InvalidOperationException("The supplied crankbait FBX could not be loaded after import.");

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        RuntimeAnimatorController controller = BuildController(clips);
        BuildMaterials(out Material bodyMaterial, out Material hookMaterial);

        GameObject root = new GameObject("LiplessCrankbaitGreenStriped");
        try
        {
            GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (visual == null) visual = UnityEngine.Object.Instantiate(source);
            if (visual == null)
                throw new InvalidOperationException("Could not instantiate the supplied crankbait FBX.");

            if (PrefabUtility.IsPartOfPrefabInstance(visual))
                PrefabUtility.UnpackPrefabInstance(
                    visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Keep every authored child position/rotation/scale exactly as it exists
            // in the supplied FBX. Only the outer wrapper is uniformly size-normalized.
            visual.name = "AuthoredModel";
            visual.transform.SetParent(root.transform, false);

            foreach (Camera camera in visual.GetComponentsInChildren<Camera>(true))
                UnityEngine.Object.DestroyImmediate(camera);
            foreach (Light light in visual.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(light);

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("The supplied FBX contains no renderable lure mesh.");

            Renderer bodyRenderer = FindBodyRenderer(renderers);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                Material selected = renderer == bodyRenderer ? bodyMaterial : hookMaterial;
                Material[] slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = selected;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            // Line tie follows the actual BODY mesh nose, not the total bounds of
            // the lure (which previously let a treble hook become the attachment).
            GameObject attach = new GameObject("LineAttach");
            attach.transform.SetParent(bodyRenderer.transform, false);
            attach.transform.localPosition = FindBodyNose(bodyRenderer);
            attach.transform.localRotation = Quaternion.identity;
            attach.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (controller != null) animator.runtimeAnimatorController = controller;

            // The source FBX is roughly several metres across because of its Blender
            // object scales. Uniformly scale the OUTER wrapper only. This preserves
            // hook/body placement, animation, proportions, and orientation exactly.
            NormalizeOverallSize(root.transform, renderers, TargetOverallLengthMetres);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Renderer FindBodyRenderer(Renderer[] renderers)
    {
        Renderer best = renderers[0];
        float bestScore = -1f;
        for (int i = 0; i < renderers.Length; i++)
        {
            Vector3 size = renderers[i].bounds.size;
            float score = Mathf.Max(size.x, size.y, size.z) *
                          Mathf.Max(0.000001f, size.x * size.y * size.z);
            if (score > bestScore)
            {
                bestScore = score;
                best = renderers[i];
            }
        }
        return best;
    }

    private static Vector3 FindBodyNose(Renderer bodyRenderer)
    {
        Mesh mesh = null;
        MeshFilter filter = bodyRenderer.GetComponent<MeshFilter>();
        if (filter != null) mesh = filter.sharedMesh;
        if (mesh == null && bodyRenderer is SkinnedMeshRenderer skinned)
            mesh = skinned.sharedMesh;

        if (mesh == null || mesh.vertexCount == 0)
        {
            Bounds fallback = bodyRenderer.localBounds;
            return new Vector3(fallback.center.x, fallback.min.y, fallback.center.z);
        }

        Vector3[] vertices = mesh.vertices;
        Bounds bounds = mesh.bounds;
        float threshold = bounds.min.y + Mathf.Max(0.00001f, bounds.size.y * 0.04f);
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i].y <= threshold)
            {
                sum += vertices[i];
                count++;
            }
        }

        return count > 0
            ? sum / count
            : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private static void NormalizeOverallSize(
        Transform root,
        Renderer[] renderers,
        float targetMetres)
    {
        bool found = false;
        Bounds total = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!found)
            {
                total = renderers[i].bounds;
                found = true;
            }
            else total.Encapsulate(renderers[i].bounds);
        }

        if (!found) return;
        Vector3 size = total.size;
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (longest <= 0.00001f || float.IsNaN(longest) || float.IsInfinity(longest)) return;

        float scale = targetMetres / longest;
        root.localScale = Vector3.one * scale;
    }

    private static void BuildMaterials(out Material body, out Material hooks)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath) != null)
            AssetDatabase.DeleteAsset(BodyMaterialPath);
        if (AssetDatabase.LoadAssetAtPath<Material>(HookMaterialPath) != null)
            AssetDatabase.DeleteAsset(HookMaterialPath);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP/Lit shader was not found.");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        body = new Material(shader) { name = "Lipless Crankbait Green Striped" };
        body.SetTexture("_BaseMap", texture);
        body.SetColor("_BaseColor", Color.white);
        SetIfPresent(body, "_Metallic", 0.08f);
        SetIfPresent(body, "_Smoothness", 0.42f);
        MakeOpaqueDoubleSided(body);
        AssetDatabase.CreateAsset(body, BodyMaterialPath);

        hooks = new Material(shader) { name = "Lipless Crankbait Treble Hooks" };
        hooks.SetColor("_BaseColor", new Color(0.42f, 0.44f, 0.46f, 1f));
        SetIfPresent(hooks, "_Metallic", 0.82f);
        SetIfPresent(hooks, "_Smoothness", 0.58f);
        MakeOpaqueDoubleSided(hooks);
        AssetDatabase.CreateAsset(hooks, HookMaterialPath);
    }

    private static void MakeOpaqueDoubleSided(Material material)
    {
        SetIfPresent(material, "_Surface", 0f);
        SetIfPresent(material, "_AlphaClip", 0f);
        SetIfPresent(material, "_Cull", 0f);
        material.doubleSidedGI = true;
        material.renderQueue = -1;
    }

    private static void SetIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static RuntimeAnimatorController BuildController(AnimationClip[] clips)
    {
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        if (clips == null || clips.Length == 0) return null;

        // The FBX has 9 stacks because Blender exported every action against every
        // object. The three diagonal pairs are the authored simultaneous animation:
        // hook A -> its own action, body -> its own action, hook B -> its own action.
        AnimationClip hookA = FindClip(clips, "BézierCurve|BézierCurveAction");
        AnimationClip body = FindClip(clips, "BézierCurve.001|BézierCurve.001Action");
        AnimationClip hookB = FindClip(clips, "BézierCurve.002|BézierCurve.002Action");

        List<AnimationClip> selected = new List<AnimationClip>();
        if (hookA != null) selected.Add(hookA);
        if (body != null) selected.Add(body);
        if (hookB != null) selected.Add(hookB);

        // Defensive fallback for a future FBX exporter that names clips differently.
        if (selected.Count == 0) selected.Add(clips.OrderByDescending(c => c.length).First());

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        while (controller.layers.Length < selected.Count)
            controller.AddLayer("Retrieve Part " + (controller.layers.Length + 1));

        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < selected.Count; i++)
        {
            layers[i].name = i == 0 ? "Retrieve Base" : "Retrieve Part " + (i + 1);
            layers[i].defaultWeight = 1f;
            layers[i].blendingMode = AnimatorLayerBlendingMode.Override;
            AnimatorState state = layers[i].stateMachine.AddState("Retrieve");
            state.motion = selected[i];
            layers[i].stateMachine.defaultState = state;
        }
        controller.layers = layers;
        return controller;
    }

    private static AnimationClip FindClip(AnimationClip[] clips, string exactName)
    {
        AnimationClip exact = clips.FirstOrDefault(c =>
            string.Equals(c.name, exactName, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        string compact = exactName.Replace(" ", string.Empty);
        return clips.FirstOrDefault(c =>
            c.name.Replace(" ", string.Empty)
                .IndexOf(compact, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string FindZip()
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
            string match = Directory.GetFiles(folder, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(file =>
                {
                    string name = Path.GetFileNameWithoutExtension(file)
                        .Replace(" ", string.Empty)
                        .Replace("_", string.Empty)
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                    return name.Contains("liplesscrankbaitgreenstriped");
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(match)) return match;
        }
        return null;
    }

    private static void WriteEntry(ZipArchiveEntry entry, string assetPath)
    {
        string absolute = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        using Stream input = entry.Open();
        using FileStream output = File.Create(absolute);
        input.CopyTo(output);
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
