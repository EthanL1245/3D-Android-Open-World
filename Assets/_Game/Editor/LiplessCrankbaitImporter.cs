using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private const string MaterialPath = Root + "/LiplessCrankbaitGreenStriped.mat";
    private const string ControllerPath = Root + "/LiplessCrankbaitGreenStriped.controller";
    private const string PrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";
    private const float TargetLengthMetres = 0.16f;

    [MenuItem("Tools/Open World/Install Lipless Crankbait (One Click)")]
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
                "Choose Lipless Crankbait Green Striped.zip",
                Directory.Exists(downloads) ? downloads : string.Empty,
                "zip");
        }
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        try
        {
            Import(zipPath);
            EditorUtility.DisplayDialog(
                "Lipless Crankbait Installed",
                "Installed the animated crankbait. Its nose/line tie is the fishing-line attachment, its rear trails during retrieve, and the authored animation plays while reeling.",
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
            throw new FileNotFoundException("Lipless Crankbait Green Striped.zip was not found.", zipPath);

        EnsureFolderRecursive(Source);
        EnsureFolderRecursive("Assets/Resources/Fishing");

        string library = Path.GetFullPath("Library/LiplessCrankbaitImport");
        Directory.CreateDirectory(library);
        string blendPath = Path.Combine(library, "Lipless Crankbait Green Striped.blend");

        EditorUtility.DisplayProgressBar("Lipless Crankbait", "Extracting authored model...", 0.10f);
        Extract(zipPath, blendPath);

        EditorUtility.DisplayProgressBar("Lipless Crankbait", "Exporting Blender animation to FBX...", 0.28f);
        ExportBlend(ResolveBlenderExecutable(), blendPath, Path.GetFullPath(FbxPath));
        AssetDatabase.Refresh();

        EditorUtility.DisplayProgressBar("Lipless Crankbait", "Importing texture and animation...", 0.52f);
        ConfigureTexture();
        ConfigureFbx();

        EditorUtility.DisplayProgressBar("Lipless Crankbait", "Building realistic lure prefab...", 0.78f);
        BuildPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void Extract(string zipPath, string blendPath)
    {
        using FileStream stream = File.OpenRead(zipPath);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);

        ZipArchiveEntry blend = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".blend", StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry texture = archive.Entries.FirstOrDefault(e =>
        {
            string ext = Path.GetExtension(e.FullName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
        });

        if (blend == null || texture == null)
            throw new InvalidDataException("The crankbait ZIP must contain its Blender file and texture image.");

        using (Stream input = blend.Open())
        using (FileStream output = File.Create(blendPath))
            input.CopyTo(output);

        WriteEntry(texture, TexturePath);
    }

    private static void ExportBlend(string blender, string blendPath, string destinationFbx)
    {
        string scriptFolder = Path.GetFullPath("Library/LiplessCrankbaitImport");
        Directory.CreateDirectory(scriptFolder);
        string scriptPath = Path.Combine(scriptFolder, "ExportLiplessCrankbait.py");

        string python =
@"import bpy
import os
import sys

args = sys.argv
out_path = args[args.index('--') + 1]
allowed = {'MESH', 'CURVE', 'ARMATURE', 'EMPTY'}

for obj in bpy.context.view_layer.objects:
    try:
        obj.hide_viewport = False
        obj.hide_render = False
        obj.hide_set(False)
        obj.select_set(obj.type in allowed)
    except Exception:
        pass

selected = [o for o in bpy.context.scene.objects if o.type in allowed]
if not selected:
    raise RuntimeError('No exportable crankbait objects were found in the Blend file.')

try:
    bpy.context.view_layer.objects.active = next((o for o in selected if o.type == 'MESH'), selected[0])
except Exception:
    pass

os.makedirs(os.path.dirname(out_path), exist_ok=True)
if os.path.exists(out_path):
    os.remove(out_path)

bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    apply_unit_scale=True,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    axis_forward='-Z',
    axis_up='Y',
    path_mode='AUTO'
)

if not os.path.exists(out_path):
    raise RuntimeError('FBX export did not produce an output file.')
";
        File.WriteAllText(scriptPath, python);

        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = blender,
            Arguments = "--background " + Quote(blendPath) + " --python " + Quote(scriptPath) + " -- " + Quote(destinationFbx),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(info);
        if (process == null) throw new InvalidOperationException("Blender could not be started.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120000))
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException("Blender took more than 2 minutes to export the crankbait.");
        }
        if (process.ExitCode != 0 || !File.Exists(destinationFbx))
        {
            Debug.LogError("Crankbait Blender output:\n" + output + "\n\nCrankbait Blender errors:\n" + error);
            throw new InvalidOperationException("Blender opened the crankbait source but could not export the FBX.");
        }
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Could not import the crankbait texture.");
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
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Could not import the crankbait FBX.");

        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.SaveAndReimport();

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
        if (source == null) throw new InvalidOperationException("Crankbait FBX could not be loaded.");

        Material material = BuildMaterial();
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.length)
            .ToArray();
        RuntimeAnimatorController controller = clips.Length > 0 ? BuildController(clips[0]) : null;

        GameObject root = new GameObject("LiplessCrankbaitGreenStriped");
        try
        {
            GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (visual == null) visual = UnityEngine.Object.Instantiate(source);
            if (PrefabUtility.IsPartOfPrefabInstance(visual))
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            visual.name = "AuthoredModel";
            visual.transform.SetParent(root.transform, false);

            foreach (Camera camera in visual.GetComponentsInChildren<Camera>(true))
                UnityEngine.Object.DestroyImmediate(camera);
            foreach (Light light in visual.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(light);

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            // Inspection of the supplied STL/Blend shows the nose/line eye at
            // source -Y and the rear hook/tail at +Y. -90 degrees around X maps
            // source -Y to Unity +Z, so +Z is always the lure's forward/nose.
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            Bounds bounds = CalculateBounds(root.transform);
            float length = Mathf.Max(0.0001f, bounds.size.z);
            visual.transform.localScale *= TargetLengthMetres / length;

            bounds = CalculateBounds(root.transform);
            Vector3 nose = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
            visual.transform.position += root.transform.position - nose;

            GameObject attach = new GameObject("LineAttach");
            attach.transform.SetParent(root.transform, false);
            attach.transform.localPosition = Vector3.zero;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.applyRootMotion = false;
            if (controller != null) animator.runtimeAnimatorController = controller;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Material BuildMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            AssetDatabase.DeleteAsset(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP/Lit shader was not found.");
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        Material material = new Material(shader) { name = "Lipless Crankbait Green Striped" };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0.12f);
        material.SetFloat("_Smoothness", 0.48f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static RuntimeAnimatorController BuildController(AnimationClip clip)
    {
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorState state = controller.layers[0].stateMachine.AddState("Retrieve");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        return controller;
    }

    private static Bounds CalculateBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
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
                        .Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty)
                        .ToLowerInvariant();
                    return name.Contains("liplesscrankbaitgreenstriped");
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(match)) return match;
        }
        return null;
    }

    private static string ResolveBlenderExecutable()
    {
        string saved = EditorPrefs.GetString(BlenderPrefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return saved;

        List<string> candidates = new List<string>();
        foreach (string baseFolder in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        })
        {
            string blenderRoot = Path.Combine(baseFolder, "Blender Foundation");
            if (!Directory.Exists(blenderRoot)) continue;
            candidates.AddRange(Directory.GetFiles(blenderRoot, "blender.exe", SearchOption.AllDirectories));
        }

        string found = candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(found))
        {
            EditorPrefs.SetString(BlenderPrefsKey, found);
            return found;
        }

        string selected = EditorUtility.OpenFilePanel("Locate Blender", string.Empty, "exe");
        if (string.IsNullOrWhiteSpace(selected) || !File.Exists(selected))
            throw new FileNotFoundException("Blender.exe is required to import the authored crankbait animation.");
        EditorPrefs.SetString(BlenderPrefsKey, selected);
        return selected;
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
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
