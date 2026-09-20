using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class RedSnapperImporter
{
    private const string RootFolder =
        "Assets/_Game/Fishing/RedSnapper";

    private const string SourceFolder =
        RootFolder + "/Source";

    private const string FbxPath =
        SourceFolder + "/RedSnapper.fbx";

    private const string TexturePath =
        SourceFolder + "/RedSnapper_Texture.png";

    private const string MaterialPath =
        RootFolder + "/RedSnapper.mat";

    private const string ControllerPath =
        RootFolder + "/RedSnapper.controller";

    private const string PrefabPath =
        "Assets/Resources/Fishing/RedSnapper.prefab";

    private const string PreviewGrantKey =
        "OpenWorld.RedSnapperPreviewGrant.v1";

    // Reuse the Blender path already found by the Goatfish importer so the
    // normal workflow remains one click after choosing the Red Snapper ZIP.
    private const string BlenderEditorPrefsKey =
        "OpenWorld.Goatfish.BlenderExecutable";

    private const float TargetLength =
        0.92f;

    private static string sessionBlenderExecutable;

    [MenuItem("Tools/Open World/Import Red Snapper (One Click)...")]
    public static void ImportRedSnapper()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Red Snapper",
                "Exit Play Mode first.",
                "OK"
            );

            return;
        }

        string zipPath =
            EditorUtility.OpenFilePanel(
                "Select RedSnapper ZIP",
                string.Empty,
                "zip"
            );

        if (string.IsNullOrWhiteSpace(
                zipPath))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Cleaning previous generated Red Snapper...",
                0.05f
            );

            CleanOldGeneratedAssets();
            EnsureFolders();

            string libraryRoot =
                Path.GetFullPath(
                    "Library/RedSnapperImport"
                );

            if (Directory.Exists(
                    libraryRoot))
            {
                Directory.Delete(
                    libraryRoot,
                    true
                );
            }

            Directory.CreateDirectory(
                libraryRoot
            );

            string blendPath =
                Path.Combine(
                    libraryRoot,
                    "RedSnapper.blend"
                );

            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Extracting new authored model, animation, and texture...",
                0.16f
            );

            ExtractPackage(
                zipPath,
                blendPath
            );

            ConfigureTexture();

            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Exporting the new authored animation through Blender...",
                0.32f
            );

            string blenderExecutable =
                ResolveBlenderExecutable();

            ExportBlendToFbx(
                blenderExecutable,
                blendPath,
                Path.GetFullPath(
                    FbxPath
                )
            );

            AssetDatabase.Refresh();

            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Importing bones and new swim animation...",
                0.50f
            );

            ConfigureFbxImporter();

            GameObject sourceAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FbxPath
                );

            if (sourceAsset == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the Red Snapper FBX exported from Blender."
                );
            }

            AnimationClip swimClip =
                FindSwimClip();

            if (swimClip == null)
            {
                throw new InvalidOperationException(
                    "The Red Snapper FBX exported successfully, but Unity did not find its authored animation."
                );
            }

            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Building game-lit material and final gameplay prefab...",
                0.72f
            );

            Material material =
                BuildMaterial();

            AnimatorController controller =
                BuildController(
                    swimClip
                );

            BuildPrefab(
                sourceAsset,
                material,
                controller
            );

            PlayerPrefs.DeleteKey(
                PreviewGrantKey
            );

            PlayerPrefs.Save();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath
                );

            EditorUtility.DisplayDialog(
                "Red Snapper Imported",
                "Done. The Red Snapper uses the selected authored animation. Its front/head is locked, the path is guided from the head position, turns pivot around the head, and aquarium movement advances the head only along forward. Import validation rejects sideways or rolled orientation. The next Play Mode will grant a fresh test Red Snapper.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Red Snapper Import Failed",
                exception.Message +
                "\n\nThe full exception is in the Unity Console.",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/Open World/Rebuild Red Snapper From Imported Source")]
    public static void RebuildRedSnapper()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Red Snapper",
                "Exit Play Mode first.",
                "OK"
            );

            return;
        }

        if (!File.Exists(
                Path.GetFullPath(
                    FbxPath
                )) ||
            !File.Exists(
                Path.GetFullPath(
                    TexturePath
                )))
        {
            EditorUtility.DisplayDialog(
                "Red Snapper",
                "Imported source is missing. Run Import Red Snapper (One Click)... first.",
                "OK"
            );

            return;
        }

        try
        {
            ConfigureTexture();
            ConfigureFbxImporter();

            GameObject sourceAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FbxPath
                );

            AnimationClip swimClip =
                FindSwimClip();

            if (sourceAsset == null ||
                swimClip == null)
            {
                throw new InvalidOperationException(
                    "The imported Red Snapper source could not be rebuilt."
                );
            }

            Material material =
                BuildMaterial();

            AnimatorController controller =
                BuildController(
                    swimClip
                );

            BuildPrefab(
                sourceAsset,
                material,
                controller
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Red Snapper Rebuilt",
                "The Red Snapper prefab was rebuilt from its existing imported source.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Red Snapper Rebuild Failed",
                exception.Message,
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CleanOldGeneratedAssets()
    {
        // Delete the generated prefab first so Unity never observes a prefab
        // whose model source has disappeared during the rebuild.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(
                PrefabPath
            );
        }

        if (AssetDatabase.IsValidFolder(
                RootFolder))
        {
            AssetDatabase.DeleteAsset(
                RootFolder
            );
        }

        AssetDatabase.Refresh();
    }

    private static void EnsureFolders()
    {
        EnsureFolderRecursive(
            SourceFolder
        );

        EnsureFolderRecursive(
            "Assets/Resources/Fishing"
        );
    }

    private static void ExtractPackage(
        string zipPath,
        string blendPath)
    {
        using FileStream stream =
            File.OpenRead(
                zipPath
            );

        using ZipArchive archive =
            new ZipArchive(
                stream,
                ZipArchiveMode.Read
            );

        ZipArchiveEntry blend =
            archive.Entries
                .FirstOrDefault(
                    entry =>
                        entry.FullName
                            .EndsWith(
                                ".blend",
                                StringComparison.OrdinalIgnoreCase
                            )
                );

        ZipArchiveEntry texture =
            archive.Entries
                .FirstOrDefault(
                    entry =>
                    {
                        string extension =
                            Path.GetExtension(
                                entry.FullName
                            )
                            .ToLowerInvariant();

                        return
                            extension == ".png" ||
                            extension == ".jpg" ||
                            extension == ".jpeg";
                    }
                );

        if (blend == null)
        {
            throw new InvalidDataException(
                "No .blend file was found in the new Red Snapper ZIP."
            );
        }

        if (texture == null)
        {
            throw new InvalidDataException(
                "No texture image was found in the Red Snapper ZIP."
            );
        }

        WriteEntry(
            blend,
            blendPath
        );

        WriteEntry(
            texture,
            Path.GetFullPath(
                TexturePath
            )
        );

        AssetDatabase.Refresh();
    }

    private static void WriteEntry(
        ZipArchiveEntry entry,
        string targetPath)
    {
        string absolute =
            Path.GetFullPath(
                targetPath
            );

        string directory =
            Path.GetDirectoryName(
                absolute
            );

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory
            );
        }

        using Stream input =
            entry.Open();

        using FileStream output =
            File.Create(
                absolute
            );

        input.CopyTo(
            output
        );
    }

    private static string ResolveBlenderExecutable()
    {
        if (!string.IsNullOrWhiteSpace(
                sessionBlenderExecutable) &&
            File.Exists(
                sessionBlenderExecutable))
        {
            return sessionBlenderExecutable;
        }

        string saved =
            EditorPrefs.GetString(
                BlenderEditorPrefsKey,
                string.Empty
            );

        if (!string.IsNullOrWhiteSpace(
                saved) &&
            File.Exists(saved))
        {
            sessionBlenderExecutable =
                saved;

            return saved;
        }

        List<string> candidates =
            new List<string>();

        string programFiles =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles
            );

        string programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86
            );

        AddBlenderCandidates(
            candidates,
            Path.Combine(
                programFiles,
                "Blender Foundation"
            )
        );

        AddBlenderCandidates(
            candidates,
            Path.Combine(
                programFilesX86,
                "Blender Foundation"
            )
        );

        AddBlenderCandidates(
            candidates,
            Path.Combine(
                programFilesX86,
                "Steam",
                "steamapps",
                "common",
                "Blender"
            )
        );

        string localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );

        AddBlenderCandidates(
            candidates,
            Path.Combine(
                localAppData,
                "Programs",
                "Blender Foundation"
            )
        );

        string found =
            candidates
                .Where(
                    File.Exists
                )
                .OrderByDescending(
                    path =>
                        File.GetLastWriteTimeUtc(
                            path
                        )
                )
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(
                found))
        {
            sessionBlenderExecutable =
                found;

            EditorPrefs.SetString(
                BlenderEditorPrefsKey,
                found
            );

            return found;
        }

        string picked =
            EditorUtility.OpenFilePanel(
                "Locate Blender.exe (one time)",
                programFiles,
                "exe"
            );

        if (string.IsNullOrWhiteSpace(
                picked))
        {
            throw new InvalidOperationException(
                "Blender could not be found. Select blender.exe once, then the Red Snapper importer will remember it."
            );
        }

        if (!File.Exists(
                picked) ||
            !string.Equals(
                Path.GetFileName(
                    picked),
                "blender.exe",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new InvalidOperationException(
                "Please select Blender's blender.exe executable, not the RedSnapper.blend file."
            );
        }

        sessionBlenderExecutable =
            picked;

        EditorPrefs.SetString(
            BlenderEditorPrefsKey,
            picked
        );

        return picked;
    }

    private static void AddBlenderCandidates(
        List<string> candidates,
        string root)
    {
        if (string.IsNullOrWhiteSpace(
                root) ||
            !Directory.Exists(
                root))
        {
            return;
        }

        try
        {
            candidates.AddRange(
                Directory.GetFiles(
                    root,
                    "blender.exe",
                    SearchOption.AllDirectories
                )
            );
        }
        catch
        {
            // Ignore protected/unreadable folders.
        }
    }

    private static void ExportBlendToFbx(
        string blenderExecutable,
        string blendPath,
        string fbxPath)
    {
        if (!File.Exists(
                blendPath))
        {
            throw new FileNotFoundException(
                "The extracted Red Snapper Blend source is missing.",
                blendPath
            );
        }

        string scriptFolder =
            Path.GetFullPath(
                "Library/RedSnapperImport"
            );

        Directory.CreateDirectory(
            scriptFolder
        );

        string scriptPath =
            Path.Combine(
                scriptFolder,
                "ExportRedSnapper.py"
            );

        string python =
@"import bpy
import os
import sys

args = sys.argv
out_path = args[args.index('--') + 1]

armatures = [
    obj for obj in bpy.context.scene.objects
    if obj.type == 'ARMATURE'
]

meshes = [
    obj for obj in bpy.context.scene.objects
    if obj.type == 'MESH'
]

if not armatures:
    raise RuntimeError('No armature was found in the Red Snapper Blend file.')

if not meshes:
    raise RuntimeError('No mesh was found in the Red Snapper Blend file.')

# Prefer the armature actually used by the fish mesh.
armature = None
for mesh in meshes:
    for modifier in mesh.modifiers:
        if modifier.type == 'ARMATURE' and modifier.object is not None:
            armature = modifier.object
            break
    if armature is not None:
        break

if armature is None:
    armature = armatures[0]

if armature.animation_data is None:
    armature.animation_data_create()

swim = armature.animation_data.action

# The user's latest Red Snapper file names the authored armature animation
# ArmatureAction. Prefer it explicitly over mesh/object actions such as
# PlaneAction whenever the active action is not set.
if swim is None:
    for action in bpy.data.actions:
        if action.name.lower() == 'armatureaction':
            swim = action
            break

if swim is None:
    for action in bpy.data.actions:
        name = action.name.lower()
        if 'swim' in name or 'armatureaction' in name:
            swim = action
            break

if swim is None and len(bpy.data.actions) > 0:
    swim = bpy.data.actions[0]

if swim is None:
    raise RuntimeError('No authored Red Snapper animation action was found.')

armature.animation_data.action = swim

bpy.context.scene.frame_start = int(swim.frame_range[0])
bpy.context.scene.frame_end = int(swim.frame_range[1])
bpy.context.scene.frame_set(bpy.context.scene.frame_start)

# Avoid context-sensitive select_all calls in --background mode.
for obj in bpy.context.view_layer.objects:
    try:
        obj.select_set(False)
    except Exception:
        pass

for obj in meshes + [armature]:
    obj.hide_viewport = False
    obj.hide_render = False

    try:
        obj.hide_set(False)
    except Exception:
        pass

    try:
        obj.select_set(True)
    except Exception:
        pass

try:
    bpy.context.view_layer.objects.active = armature
except Exception:
    pass

os.makedirs(os.path.dirname(out_path), exist_ok=True)

bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    object_types={'MESH', 'ARMATURE'},
    apply_unit_scale=True,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    axis_forward='-Z',
    axis_up='Y',
    path_mode='AUTO'
)

if not os.path.exists(out_path):
    raise RuntimeError('FBX export did not produce an output file.')

print('RED_SNAPPER_ACTION=' + swim.name)
print('RED_SNAPPER_FRAMES=' + str(int(swim.frame_range[0])) + ':' + str(int(swim.frame_range[1])))
";

        File.WriteAllText(
            scriptPath,
            python
        );

        ProcessStartInfo startInfo =
            new ProcessStartInfo();

        startInfo.FileName =
            blenderExecutable;

        startInfo.Arguments =
            "--background " +
            QuoteArgument(
                blendPath
            ) +
            " --python " +
            QuoteArgument(
                scriptPath
            ) +
            " -- " +
            QuoteArgument(
                fbxPath
            );

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using Process process =
            Process.Start(
                startInfo
            );

        if (process == null)
        {
            throw new InvalidOperationException(
                "Blender could not be started."
            );
        }

        string standardOutput =
            process.StandardOutput.ReadToEnd();

        string standardError =
            process.StandardError.ReadToEnd();

        if (!process.WaitForExit(
                120000))
        {
            try
            {
                process.Kill();
            }
            catch
            {
            }

            throw new TimeoutException(
                "Blender took more than 2 minutes to export the Red Snapper."
            );
        }

        if (process.ExitCode != 0 ||
            !File.Exists(
                fbxPath))
        {
            Debug.LogError(
                "Full Blender Red Snapper export output:\n" +
                standardOutput +
                "\n\nFull Blender errors:\n" +
                standardError
            );

            throw new InvalidOperationException(
                "Blender opened the Red Snapper source but could not finish the animated FBX export. The full Blender traceback was written to the Unity Console."
            );
        }

        Debug.Log(
            "Red Snapper Blender export completed.\n" +
            standardOutput
        );
    }

    private static string QuoteArgument(
        string value)
    {
        return
            "\"" +
            value.Replace(
                "\"",
                "\\\""
            ) +
            "\"";
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(
            TexturePath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(
                TexturePath
            ) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Unity could not create the Red Snapper texture importer."
            );
        }

        importer.textureType =
            TextureImporterType.Default;

        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;

        importer.wrapMode =
            TextureWrapMode.Clamp;

        importer.filterMode =
            FilterMode.Trilinear;

        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;

        importer.textureCompression =
            TextureImporterCompression.CompressedHQ;

        importer.compressionQuality = 90;

        importer.SaveAndReimport();
    }

    private static void ConfigureFbxImporter()
    {
        AssetDatabase.ImportAsset(
            FbxPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate
        );

        ModelImporter importer =
            AssetImporter.GetAtPath(
                FbxPath
            ) as ModelImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Unity could not create the Red Snapper FBX importer."
            );
        }

        importer.importAnimation = true;

        importer.animationType =
            ModelImporterAnimationType.Generic;

        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = true;

        importer.materialImportMode =
            ModelImporterMaterialImportMode.None;

        importer.meshCompression =
            ModelImporterMeshCompression.Low;

        importer.importNormals =
            ModelImporterNormals.Import;

        importer.importTangents =
            ModelImporterTangents.CalculateMikk;

        importer.isReadable = false;

        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips =
            importer.defaultClipAnimations;

        ModelImporterClipAnimation swim =
            clips.FirstOrDefault(
                clip =>
                    ContainsSwimName(
                        clip.name
                    ) ||
                    ContainsSwimName(
                        clip.takeName
                    )
            );

        if (swim == null)
        {
            swim =
                clips.FirstOrDefault();
        }

        if (swim == null)
        {
            throw new InvalidOperationException(
                "The FBX exported successfully, but Unity did not import an animation clip from RedSnapper.fbx."
            );
        }

        swim.name = "Swim";
        swim.loopTime = true;
        swim.loopPose = true;

        Debug.Log(
            "Using authored Red Snapper animation take: " +
            swim.takeName +
            " at its imported timing."
        );

        importer.clipAnimations =
            new[]
            {
                swim
            };

        importer.SaveAndReimport();
    }

    private static bool ContainsSwimName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        string lower =
            value.ToLowerInvariant();

        return
            lower.Contains(
                "armatureaction"
            ) ||
            lower.Contains(
                "swim"
            ) ||
            lower.Contains(
                "action"
            );
    }

    private static AnimationClip FindSwimClip()
    {
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(
                FbxPath
            );

        AnimationClip clip =
            assets
                .OfType<AnimationClip>()
                .FirstOrDefault(
                    candidate =>
                        candidate.name ==
                        "Swim"
                );

        if (clip != null)
            return clip;

        return
            assets
                .OfType<AnimationClip>()
                .FirstOrDefault(
                    candidate =>
                        !candidate.name
                            .StartsWith(
                                "__preview__",
                                StringComparison.OrdinalIgnoreCase
                            )
                );
    }

    private static Material BuildMaterial()
    {
        Material old =
            AssetDatabase.LoadAssetAtPath<Material>(
                MaterialPath
            );

        if (old != null)
        {
            AssetDatabase.DeleteAsset(
                MaterialPath
            );
        }

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );

        if (shader == null)
        {
            throw new InvalidOperationException(
                "URP/Lit shader could not be found."
            );
        }

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                TexturePath
            );

        Material material =
            new Material(
                shader
            );

        material.name =
            "RedSnapper";

        material.SetTexture(
            "_BaseMap",
            texture
        );

        material.SetColor(
            "_BaseColor",
            Color.white
        );

        material.SetFloat(
            "_Metallic",
            0f
        );

        material.SetFloat(
            "_Smoothness",
            0.28f
        );

        material.enableInstancing = true;

        AssetDatabase.CreateAsset(
            material,
            MaterialPath
        );

        return material;
    }

    private static AnimatorController BuildController(
        AnimationClip clip)
    {
        RuntimeAnimatorController old =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath
            );

        if (old != null)
        {
            AssetDatabase.DeleteAsset(
                ControllerPath
            );
        }

        AnimatorController controller =
            AnimatorController
                .CreateAnimatorControllerAtPath(
                    ControllerPath
                );

        AnimatorState state =
            controller.layers[0]
                .stateMachine
                .AddState(
                    "Swim"
                );

        state.motion = clip;

        controller.layers[0]
            .stateMachine
            .defaultState = state;

        EditorUtility.SetDirty(
            controller
        );

        return controller;
    }

    private static void BuildPrefab(
        GameObject sourceAsset,
        Material material,
        AnimatorController controller)
    {
        GameObject root =
            new GameObject(
                "RedSnapper"
            );

        try
        {
            GameObject visual =
                new GameObject(
                    "Visual"
                );

            visual.transform.SetParent(
                root.transform,
                false
            );

            GameObject model =
                PrefabUtility.InstantiatePrefab(
                    sourceAsset
                ) as GameObject;

            if (model == null)
            {
                model =
                    UnityEngine.Object.Instantiate(
                        sourceAsset
                    );
            }

            if (PrefabUtility.IsPartOfPrefabInstance(
                    model))
            {
                PrefabUtility.UnpackPrefabInstance(
                    model,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction
                );
            }

            model.name =
                "AuthoredModel";

            model.transform.SetParent(
                visual.transform,
                false
            );

            StripNonFishObjects(
                model
            );

            Renderer[] renderers =
                model
                    .GetComponentsInChildren<Renderer>(
                        true
                    )
                    .Where(
                        renderer =>
                            !(renderer is ParticleSystemRenderer)
                    )
                    .ToArray();

            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "No renderers were found in the Red Snapper FBX."
                );
            }

            foreach (Renderer renderer
                     in renderers)
            {
                Material[] slots =
                    new Material[
                        Mathf.Max(
                            1,
                            renderer.sharedMaterials.Length
                        )
                    ];

                for (int i = 0;
                     i < slots.Length;
                     i++)
                {
                    slots[i] = material;
                }

                renderer.sharedMaterials =
                    slots;

                renderer.shadowCastingMode =
                    ShadowCastingMode.On;

                renderer.receiveShadows =
                    true;
            }

            Animator animator =
                model.GetComponent<Animator>();

            if (animator == null)
            {
                animator =
                    model.GetComponentInChildren<Animator>(
                        true
                    );
            }

            if (animator == null)
            {
                animator =
                    model.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController =
                controller;

            animator.applyRootMotion = false;

            animator.updateMode =
                AnimatorUpdateMode.Normal;

            animator.cullingMode =
                AnimatorCullingMode.CullUpdateTransforms;

            AlignHeadToGameplayForward(
                visual.transform,
                model.transform
            );

            NormalizeAndCenter(
                root.transform,
                visual.transform
            );

            RedSnapperPresentation presentation =
                root.AddComponent<RedSnapperPresentation>();

            presentation.Configure(
                animator
            );

            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(
                    PrefabPath
                );
            }

            PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath
            );

            AssetDatabase.ImportAsset(
                PrefabPath,
                ImportAssetOptions.ForceUpdate
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(
                root
            );
        }
    }

    private static void StripNonFishObjects(
        GameObject model)
    {
        foreach (Camera camera
                 in model.GetComponentsInChildren<Camera>(
                     true
                 ))
        {
            UnityEngine.Object.DestroyImmediate(
                camera
            );
        }

        foreach (Light light
                 in model.GetComponentsInChildren<Light>(
                     true
                 ))
        {
            UnityEngine.Object.DestroyImmediate(
                light
            );
        }

        foreach (AudioListener listener
                 in model.GetComponentsInChildren<AudioListener>(
                     true
                 ))
        {
            UnityEngine.Object.DestroyImmediate(
                listener
            );
        }
    }

    private static void AlignHeadToGameplayForward(
        Transform visual,
        Transform model)
    {
        // The authored rig is front-to-back: Bone is the head anchor and
        // Bone.001 is the next body segment. Use THAT front-only vector,
        // not the animated tail/body chain, to define gameplay forward.
        Transform head =
            FindBone(
                model,
                "Bone"
            );

        Transform next =
            FindBone(
                model,
                "Bone.001"
            );

        if (head == null ||
            next == null)
        {
            // Defensive fallback for alternate exports.
            head =
                FindBone(
                    model,
                    "Bone.001"
                );

            next =
                FindBone(
                    model,
                    "Bone.002"
                );
        }

        if (head == null ||
            next == null)
        {
            throw new InvalidOperationException(
                "Red Snapper head bones could not be found. The importer will not guess a forward axis."
            );
        }

        Vector3 headForward =
            head.position -
            next.position;

        if (headForward.sqrMagnitude <
            0.000001f)
        {
            throw new InvalidOperationException(
                "Red Snapper head-bone vector had zero length."
            );
        }

        headForward.Normalize();

        Vector3 authoredUp =
            model.transform.up;

        authoredUp =
            Vector3.ProjectOnPlane(
                authoredUp,
                headForward
            );

        if (authoredUp.sqrMagnitude <
            0.000001f)
        {
            authoredUp =
                Vector3.up;
        }

        authoredUp.Normalize();

        Quaternion authoredBasis =
            Quaternion.LookRotation(
                headForward,
                authoredUp
            );

        Quaternion gameplayBasis =
            Quaternion.LookRotation(
                Vector3.forward,
                Vector3.up
            );

        visual.rotation =
            gameplayBasis *
            Quaternion.Inverse(
                authoredBasis
            ) *
            visual.rotation;

        // Verify after correction. A value near 1 means the physical head
        // vector and root-forward vector are aligned.
        Vector3 verified =
            head.position -
            next.position;

        verified.Normalize();

        float forwardDot =
            Vector3.Dot(
                verified,
                Vector3.forward
            );

        float upDot =
            Vector3.Dot(
                visual.up,
                Vector3.up
            );

        Debug.Log(
            "Red Snapper import alignment verified. Head forward dot=" +
            forwardDot.ToString("0.000") +
            ", upright dot=" +
            upDot.ToString("0.000")
        );

        if (forwardDot < 0.985f)
        {
            throw new InvalidOperationException(
                "Red Snapper head axis did not align to gameplay forward. Import stopped instead of allowing lateral swimming."
            );
        }

        if (upDot < 0.90f)
        {
            throw new InvalidOperationException(
                "Red Snapper imported rolled onto its side. Import stopped instead of accepting an unrealistic swimming orientation."
            );
        }
    }

    private static Transform FindBone(
        Transform root,
        string boneName)
    {
        return
            root.GetComponentsInChildren<Transform>(
                    true
                )
                .FirstOrDefault(
                    item =>
                        item.name ==
                        boneName
                );
    }

    private static void NormalizeAndCenter(
        Transform root,
        Transform visual)
    {
        Bounds bounds =
            CalculateBounds(
                root
            );

        float length =
            Mathf.Max(
                bounds.size.z,
                Mathf.Max(
                    bounds.size.x,
                    bounds.size.y
                )
            );

        if (length > 0.0001f)
        {
            visual.localScale *=
                TargetLength /
                length;
        }

        bounds =
            CalculateBounds(
                root
            );

        visual.position +=
            root.position -
            bounds.center;
    }

    private static Bounds CalculateBounds(
        Transform root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        bool found = false;

        Bounds bounds =
            new Bounds(
                root.position,
                Vector3.zero
            );

        foreach (Renderer renderer
                 in renderers)
        {
            if (!renderer.enabled)
                continue;

            if (!found)
            {
                bounds =
                    renderer.bounds;

                found = true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds
                );
            }
        }

        if (!found)
        {
            bounds =
                new Bounds(
                    root.position,
                    Vector3.one
                );
        }

        return bounds;
    }

    private static void EnsureFolderRecursive(
        string path)
    {
        string[] parts =
            path.Split('/');

        if (parts.Length == 0 ||
            parts[0] != "Assets")
        {
            throw new ArgumentException(
                "Asset folder path must start with Assets."
            );
        }

        string current =
            "Assets";

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (!AssetDatabase.IsValidFolder(
                    next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]
                );
            }

            current = next;
        }
    }
}
