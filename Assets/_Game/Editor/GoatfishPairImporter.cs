using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

public static class GoatfishPairImporter
{
    private const string RootFolder =
        "Assets/_Game/Fishing/Goatfish";

    private const string ResourcesFolder =
        "Assets/Resources/Fishing";

    private const string PreviewGrantKey =
        "OpenWorld.GoatfishPreviewGrant.v1";

    private const string BlenderEditorPrefsKey =
        "OpenWorld.Goatfish.BlenderExecutable";

    private static string sessionBlenderExecutable;

    private const float TargetLength =
        0.92f;

    private class FishImportDefinition
    {
        public string displayName;
        public string folderName;
        public string zipMatchA;
        public string zipMatchB;
        public string sourceBaseName;
        public string prefabName;
        public string materialName;
        public string controllerName;
    }

    private static readonly FishImportDefinition Yellow =
        new FishImportDefinition
        {
            displayName = "Yellow Goatfish",
            folderName = "Yellow",
            zipMatchA = "yellow",
            zipMatchB = "goat",
            sourceBaseName = "YellowGoatfish",
            prefabName = "YellowGoatfish",
            materialName = "YellowGoatfish",
            controllerName = "YellowGoatfish"
        };

    private static readonly FishImportDefinition BlackSpot =
        new FishImportDefinition
        {
            displayName = "Black Spot Goatfish",
            folderName = "BlackSpot",
            zipMatchA = "black",
            zipMatchB = "goat",
            sourceBaseName = "BlackSpotGoatfish",
            prefabName = "BlackSpotGoatfish",
            materialName = "BlackSpotGoatfish",
            controllerName = "BlackSpotGoatfish"
        };

    [MenuItem("Tools/Open World/Import Goatfish Pair (One Folder)...")]
    public static void ImportPair()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Goatfish Import",
                "Exit Play Mode first.",
                "OK"
            );

            return;
        }

        string folder =
            EditorUtility.OpenFolderPanel(
                "Choose the folder containing both Goatfish ZIP files",
                string.Empty,
                string.Empty
            );

        if (string.IsNullOrWhiteSpace(folder))
            return;

        string[] zipFiles =
            Directory.GetFiles(
                folder,
                "*.zip",
                SearchOption.TopDirectoryOnly
            );

        string yellowZip =
            FindZip(
                zipFiles,
                Yellow
            );

        string blackZip =
            FindZip(
                zipFiles,
                BlackSpot
            );

        if (yellowZip == null ||
            blackZip == null)
        {
            EditorUtility.DisplayDialog(
                "Goatfish Import",
                "I could not find both ZIP files in that folder.\n\nExpected filenames containing:\n• Yellow + Goat\n• Black + Goat",
                "OK"
            );

            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Goatfish Pair",
                "Preparing folders...",
                0.05f
            );

            EnsureFolderRecursive(
                RootFolder
            );

            EnsureFolderRecursive(
                ResourcesFolder
            );

            ImportOne(
                Yellow,
                yellowZip,
                0.10f,
                0.46f
            );

            ImportOne(
                BlackSpot,
                blackZip,
                0.52f,
                0.88f
            );

            PlayerPrefs.DeleteKey(
                PreviewGrantKey
            );

            PlayerPrefs.Save();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    ResourcesFolder +
                    "/YellowGoatfish.prefab"
                );

            EditorUtility.DisplayDialog(
                "Goatfish Pair Imported",
                "Done. Both fish were exported from their Blend armature into clean animated FBXs and use their authored swimming action, while their material uses the game's URP lighting. Cameras/lights from the source are excluded.\n\nThe next Play Mode will grant one Yellow Goatfish and one Black Spot Goatfish to your inventory for immediate testing.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Goatfish Import Failed",
                exception.Message +
                "\n\nThe importer now exports the Blend source through blender.exe into an animated FBX before Unity imports it.",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/Open World/Rebuild Goatfish Pair From Imported Source")]
    public static void RebuildPair()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Goatfish Rebuild",
                "Exit Play Mode first.",
                "OK"
            );

            return;
        }

        try
        {
            BuildFromExistingSource(
                Yellow,
                0.12f,
                0.48f
            );

            BuildFromExistingSource(
                BlackSpot,
                0.54f,
                0.90f
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Goatfish Rebuilt",
                "Both Goatfish prefabs were rebuilt from the already-imported Blend sources.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Goatfish Rebuild Failed",
                exception.Message,
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static string FindZip(
        string[] zipFiles,
        FishImportDefinition definition)
    {
        return zipFiles
            .FirstOrDefault(
                path =>
                {
                    string name =
                        Path.GetFileNameWithoutExtension(
                            path
                        )
                        .ToLowerInvariant();

                    return
                        name.Contains(
                            definition.zipMatchA
                        ) &&
                        name.Contains(
                            definition.zipMatchB
                        );
                }
            );
    }

    private static void ImportOne(
        FishImportDefinition definition,
        string zipPath,
        float progressStart,
        float progressEnd)
    {
        string variantRoot =
            RootFolder +
            "/" +
            definition.folderName;

        if (AssetDatabase.IsValidFolder(
                variantRoot))
        {
            AssetDatabase.DeleteAsset(
                variantRoot
            );
        }

        string prefabPath =
            ResourcesFolder +
            "/" +
            definition.prefabName +
            ".prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(
                prefabPath
            );
        }

        EnsureFolderRecursive(
            variantRoot +
            "/Source"
        );

        string libraryRoot =
            Path.GetFullPath(
                "Library/GoatfishImport/" +
                definition.folderName
            );

        Directory.CreateDirectory(
            libraryRoot
        );

        string blendPath =
            Path.Combine(
                libraryRoot,
                definition.sourceBaseName +
                ".blend"
            );

        string fbxPath =
            variantRoot +
            "/Source/" +
            definition.sourceBaseName +
            ".fbx";

        string texturePath =
            variantRoot +
            "/Source/" +
            definition.sourceBaseName +
            "_Texture.jpg";

        string stlPath =
            variantRoot +
            "/Source/" +
            definition.sourceBaseName +
            ".stl";

        EditorUtility.DisplayProgressBar(
            "Goatfish Pair",
            "Extracting " +
            definition.displayName +
            "...",
            Mathf.Lerp(
                progressStart,
                progressEnd,
                0.08f
            )
        );

        ExtractPackage(
            zipPath,
            blendPath,
            texturePath,
            stlPath
        );

        ConfigureTexture(
            texturePath
        );

        EditorUtility.DisplayProgressBar(
            "Goatfish Pair",
            "Exporting bones + authored swim animation from Blender for " +
            definition.displayName +
            "...",
            Mathf.Lerp(
                progressStart,
                progressEnd,
                0.24f
            )
        );

        string blenderExecutable =
            ResolveBlenderExecutable();

        ExportBlendToFbx(
            blenderExecutable,
            blendPath,
            Path.GetFullPath(
                fbxPath
            )
        );

        AssetDatabase.Refresh();

        EditorUtility.DisplayProgressBar(
            "Goatfish Pair",
            "Importing animated FBX for " +
            definition.displayName +
            "...",
            Mathf.Lerp(
                progressStart,
                progressEnd,
                0.46f
            )
        );

        ConfigureFbxImporter(
            fbxPath
        );

        BuildVariant(
            definition,
            fbxPath,
            texturePath
        );
    }

    private static void BuildFromExistingSource(
        FishImportDefinition definition,
        float progressStart,
        float progressEnd)
    {
        string variantRoot =
            RootFolder +
            "/" +
            definition.folderName;

        string fbxPath =
            variantRoot +
            "/Source/" +
            definition.sourceBaseName +
            ".fbx";

        string texturePath =
            variantRoot +
            "/Source/" +
            definition.sourceBaseName +
            "_Texture.jpg";

        if (!File.Exists(
                Path.GetFullPath(
                    fbxPath
                )) ||
            !File.Exists(
                Path.GetFullPath(
                    texturePath
                )))
        {
            throw new FileNotFoundException(
                definition.displayName +
                " imported FBX source is missing. Run Import Goatfish Pair (One Folder)... first."
            );
        }

        EditorUtility.DisplayProgressBar(
            "Goatfish Pair",
            "Rebuilding " +
            definition.displayName +
            "...",
            Mathf.Lerp(
                progressStart,
                progressEnd,
                0.45f
            )
        );

        ConfigureTexture(
            texturePath
        );

        ConfigureFbxImporter(
            fbxPath
        );

        BuildVariant(
            definition,
            fbxPath,
            texturePath
        );
    }

    private static void ExtractPackage(
        string zipPath,
        string blendPath,
        string texturePath,
        string stlPath)
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
                        string ext =
                            Path.GetExtension(
                                entry.FullName
                            )
                            .ToLowerInvariant();

                        return
                            ext == ".jpg" ||
                            ext == ".jpeg" ||
                            ext == ".png";
                    }
                );

        ZipArchiveEntry stl =
            archive.Entries
                .FirstOrDefault(
                    entry =>
                        entry.FullName
                            .EndsWith(
                                ".stl",
                                StringComparison.OrdinalIgnoreCase
                            )
                );

        if (blend == null)
        {
            throw new InvalidDataException(
                "No .blend file was found in " +
                Path.GetFileName(
                    zipPath
                )
            );
        }

        if (texture == null)
        {
            throw new InvalidDataException(
                "No texture image was found in " +
                Path.GetFileName(
                    zipPath
                )
            );
        }

        WriteEntry(
            blend,
            blendPath
        );

        WriteEntry(
            texture,
            texturePath
        );

        if (stl != null)
        {
            WriteEntry(
                stl,
                stlPath
            );
        }

        AssetDatabase.Refresh();
    }

    private static void WriteEntry(
        ZipArchiveEntry entry,
        string assetPath)
    {
        string absolute =
            Path.GetFullPath(
                assetPath
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                absolute
            )
        );

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

    private static void ConfigureTexture(
        string texturePath)
    {
        AssetDatabase.ImportAsset(
            texturePath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(
                texturePath
            ) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Unity could not create the texture importer for " +
                texturePath
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
                "The Goatfish ZIPs are correct, but the Blender application could not be found. Install Blender or select blender.exe when prompted, then run the importer again."
            );
        }

        if (!File.Exists(picked) ||
            !string.Equals(
                Path.GetFileName(picked),
                "blender.exe",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new InvalidOperationException(
                "Please select Blender's blender.exe executable, not a .blend project file."
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
            !Directory.Exists(root))
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
                "The extracted Blend source is missing.",
                blendPath
            );
        }

        string scriptFolder =
            Path.GetFullPath(
                "Library/GoatfishImport"
            );

        Directory.CreateDirectory(
            scriptFolder
        );

        string scriptPath =
            Path.Combine(
                scriptFolder,
                "ExportGoatfish.py"
            );

        string python =
@"import bpy
import os
import sys

args = sys.argv
out_path = args[args.index('--') + 1]

swim = None
for action in bpy.data.actions:
    if 'swim' in action.name.lower():
        swim = action
        break

armatures = [
    obj for obj in bpy.context.scene.objects
    if obj.type == 'ARMATURE'
]
meshes = [
    obj for obj in bpy.context.scene.objects
    if obj.type == 'MESH'
]

if not armatures:
    raise RuntimeError('No armature was found in the Blend file.')

if not meshes:
    raise RuntimeError('No mesh was found in the Blend file.')

armature = armatures[0]

if armature.animation_data is None:
    armature.animation_data_create()

if swim is not None:
    armature.animation_data.action = swim
    bpy.context.scene.frame_start = int(swim.frame_range[0])
    bpy.context.scene.frame_end = int(swim.frame_range[1])

bpy.ops.object.select_all(action='DESELECT')

for obj in meshes + [armature]:
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.hide_render = False
    obj.select_set(True)

bpy.context.view_layer.objects.active = armature

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
                "Blender took more than 2 minutes to export the Goatfish."
            );
        }

        if (process.ExitCode != 0 ||
            !File.Exists(
                fbxPath))
        {
            throw new InvalidOperationException(
                "Blender could not export the animated Goatfish FBX.\n\nBlender output:\n" +
                standardOutput +
                "\n\nBlender errors:\n" +
                standardError
            );
        }
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

    private static void ConfigureFbxImporter(
        string fbxPath)
    {
        AssetDatabase.ImportAsset(
            fbxPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate
        );

        ModelImporter importer =
            AssetImporter.GetAtPath(
                fbxPath
            ) as ModelImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Unity could not create an FBX importer for " +
                Path.GetFileName(
                    fbxPath
                )
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
                "The FBX exported successfully, but Unity did not import an animation clip from " +
                Path.GetFileName(
                    fbxPath
                )
            );
        }

        swim.name = "Swim";
        swim.loopTime = true;
        swim.loopPose = true;

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

        string lowered =
            value.ToLowerInvariant();

        return
            lowered.Contains(
                "yellowgoatfish swimming"
            ) ||
            lowered.Contains(
                "swimming"
            ) ||
            lowered.Contains(
                "swim"
            );
    }

    private static void BuildVariant(
        FishImportDefinition definition,
        string blendPath,
        string texturePath)
    {
        string variantRoot =
            RootFolder +
            "/" +
            definition.folderName;

        string materialPath =
            variantRoot +
            "/" +
            definition.materialName +
            ".mat";

        string controllerPath =
            variantRoot +
            "/" +
            definition.controllerName +
            ".controller";

        string prefabPath =
            ResourcesFolder +
            "/" +
            definition.prefabName +
            ".prefab";

        GameObject sourceAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                blendPath
            );

        if (sourceAsset == null)
        {
            throw new InvalidOperationException(
                "The imported model asset is missing for " +
                definition.displayName
            );
        }

        AnimationClip swimClip =
            FindSwimClip(
                blendPath
            );

        if (swimClip == null)
        {
            throw new InvalidOperationException(
                "The Swim clip could not be found for " +
                definition.displayName
            );
        }

        Material material =
            BuildMaterial(
                materialPath,
                texturePath
            );

        AnimatorController controller =
            BuildController(
                controllerPath,
                swimClip
            );

        BuildPrefab(
            sourceAsset,
            material,
            controller,
            prefabPath,
            definition.displayName
        );
    }

    private static AnimationClip FindSwimClip(
        string modelPath)
    {
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(
                modelPath
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

        clip =
            assets
                .OfType<AnimationClip>()
                .FirstOrDefault(
                    candidate =>
                        !candidate.name
                            .StartsWith(
                                "__preview__",
                                StringComparison.OrdinalIgnoreCase
                            ) &&
                        ContainsSwimName(
                            candidate.name
                        )
                );

        return clip;
    }

    private static Material BuildMaterial(
        string materialPath,
        string texturePath)
    {
        Material old =
            AssetDatabase.LoadAssetAtPath<Material>(
                materialPath
            );

        if (old != null)
        {
            AssetDatabase.DeleteAsset(
                materialPath
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
                texturePath
            );

        Material material =
            new Material(
                shader
            );

        material.name =
            Path.GetFileNameWithoutExtension(
                materialPath
            );

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
            0.0f
        );

        material.SetFloat(
            "_Smoothness",
            0.30f
        );

        material.enableInstancing = true;

        AssetDatabase.CreateAsset(
            material,
            materialPath
        );

        return material;
    }

    private static AnimatorController BuildController(
        string controllerPath,
        AnimationClip clip)
    {
        RuntimeAnimatorController old =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                controllerPath
            );

        if (old != null)
        {
            AssetDatabase.DeleteAsset(
                controllerPath
            );
        }

        AnimatorController controller =
            AnimatorController
                .CreateAnimatorControllerAtPath(
                    controllerPath
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
        AnimatorController controller,
        string prefabPath,
        string displayName)
    {
        GameObject root =
            new GameObject(
                displayName.Replace(
                    " ",
                    string.Empty
                )
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
                    "No renderers were found in " +
                    displayName
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

            OrientFromRig(
                visual.transform,
                model.transform
            );

            NormalizeAndCenter(
                root.transform,
                visual.transform
            );

            GoatfishPresentation presentation =
                root.AddComponent<GoatfishPresentation>();

            presentation.Configure(
                animator
            );

            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(
                    prefabPath
                );
            }

            PrefabUtility.SaveAsPrefabAsset(
                root,
                prefabPath
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

    private static void OrientFromRig(
        Transform visual,
        Transform model)
    {
        Transform first =
            FindDeepChild(
                model,
                "Bone"
            );

        Transform last =
            FindDeepChild(
                model,
                "Bone.003"
            );

        if (first == null ||
            last == null)
        {
            Debug.LogWarning(
                "Goatfish rig bones Bone/Bone.003 were not found. Keeping the authored source orientation."
            );

            return;
        }

        Vector3 tailDirection =
            last.position -
            first.position;

        if (tailDirection.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        Vector3 headDirection =
            -tailDirection.normalized;

        Vector3 authoredUp =
            model.transform.up;

        authoredUp =
            Vector3.ProjectOnPlane(
                authoredUp,
                headDirection
            );

        if (authoredUp.sqrMagnitude <
            0.0001f)
        {
            authoredUp =
                Vector3.up;
        }

        authoredUp.Normalize();

        Quaternion sourceBasis =
            Quaternion.LookRotation(
                headDirection,
                authoredUp
            );

        Quaternion targetBasis =
            Quaternion.LookRotation(
                Vector3.forward,
                Vector3.up
            );

        visual.rotation =
            targetBasis *
            Quaternion.Inverse(
                sourceBasis
            ) *
            visual.rotation;
    }

    private static Transform FindDeepChild(
        Transform root,
        string targetName)
    {
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(
                true
            );

        return transforms
            .FirstOrDefault(
                item =>
                    item.name ==
                    targetName
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
