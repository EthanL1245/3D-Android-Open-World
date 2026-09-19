using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

public static class YellowfinTunaImporter
{
    private const string RootFolder =
        "Assets/_Game/Fishing/YellowfinTuna";

    private const string SourceFolder =
        RootFolder + "/Source";

    private const string FbxPath =
        SourceFolder + "/YellowfinTuna.fbx";

    private const string TexturePath =
        SourceFolder + "/YellowfinTuna_Texture.jpg";

    private const string MaterialPath =
        RootFolder + "/YellowfinTuna.mat";

    private const string ControllerPath =
        RootFolder + "/YellowfinTuna.controller";

    private const string PrefabPath =
        "Assets/Resources/Fishing/YellowfinTuna.prefab";

    private const float TargetBodyLength = 1.35f;

    [MenuItem("Tools/Open World/Import Yellowfin Tuna Model...")]
    public static void ImportYellowfinTuna()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Yellowfin Tuna",
                "Exit Play Mode before importing the model.",
                "OK"
            );

            return;
        }

        string zipPath =
            EditorUtility.OpenFilePanel(
                "Select Yellowfin Tuna.zip",
                string.Empty,
                "zip"
            );

        if (string.IsNullOrWhiteSpace(zipPath))
            return;

        try
        {
            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Extracting your FBX and texture...",
                0.10f
            );

            EnsureFolders();
            ExtractSourceFiles(zipPath);

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Configuring the texture...",
                0.24f
            );

            ConfigureTexture();

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Importing rig and animation...",
                0.40f
            );

            ConfigureModelImporter();

            AnimationClip swimClip =
                FindSwimAnimation();

            if (swimClip == null)
            {
                EditorUtility.DisplayDialog(
                    "Yellowfin Tuna",
                    "The FBX imported, but I could not find its Armature animation clip. The source package contains an ArmatureAction, so send me the Unity Console/import details if this happens.",
                    "OK"
                );

                return;
            }

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Creating mobile material and animation controller...",
                0.58f
            );

            Material material =
                BuildMaterial();

            AnimatorController controller =
                BuildAnimatorController(
                    swimClip
                );

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Building normalized gameplay prefab...",
                0.76f
            );

            BuildGameplayPrefab(
                material,
                controller
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath
                );

            Selection.activeObject =
                prefab;

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Imported",
                "Your original FBX, photo texture, armature, and ArmatureAction animation are now wired into the game.\n\nThe model is normalized to gameplay size, cameras/lights from the Blender scene are excluded, root motion is disabled, and the animation loops automatically.\n\nA temporary one-time preview Yellowfin Tuna will appear in your caught-fish inventory the next time you enter Play Mode.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Import Failed",
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

    [MenuItem("Tools/Open World/Rebuild Yellowfin Tuna From Imported Source")]
    public static void RebuildFromImportedSource()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Yellowfin Tuna",
                "Exit Play Mode before rebuilding.",
                "OK"
            );

            return;
        }

        if (!File.Exists(
                Path.GetFullPath(FbxPath)) ||
            !File.Exists(
                Path.GetFullPath(TexturePath)))
        {
            EditorUtility.DisplayDialog(
                "Yellowfin Tuna",
                "Imported source files are missing. Run Tools > Open World > Import Yellowfin Tuna Model... first.",
                "OK"
            );

            return;
        }

        try
        {
            ConfigureTexture();
            ConfigureModelImporter();

            AnimationClip swimClip =
                FindSwimAnimation();

            if (swimClip == null)
            {
                throw new InvalidOperationException(
                    "Could not find the Yellowfin Tuna swim animation."
                );
            }

            Material material =
                BuildMaterial();

            AnimatorController controller =
                BuildAnimatorController(
                    swimClip
                );

            BuildGameplayPrefab(
                material,
                controller
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath
                );

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Rebuilt",
                "Rebuilt the gameplay prefab from the imported FBX and texture.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Rebuild Failed",
                exception.Message,
                "OK"
            );
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder(
            "Assets/_Game/Fishing",
            "YellowfinTuna"
        );

        EnsureFolder(
            RootFolder,
            "Source"
        );

        EnsureFolder(
            "Assets/Resources",
            "Fishing"
        );
    }

    private static void EnsureFolder(
        string parent,
        string child)
    {
        string path =
            parent + "/" + child;

        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] pieces =
            parent.Split('/');

        string current =
            pieces[0];

        for (int i = 1;
             i < pieces.Length;
             i++)
        {
            string next =
                current + "/" + pieces[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    pieces[i]
                );
            }

            current = next;
        }

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(
                parent,
                child
            );
        }
    }

    private static void ExtractSourceFiles(
        string zipPath)
    {
        using FileStream stream =
            File.OpenRead(zipPath);

        using ZipArchive archive =
            new ZipArchive(
                stream,
                ZipArchiveMode.Read
            );

        ZipArchiveEntry fbxEntry =
            archive.Entries
                .FirstOrDefault(
                    entry =>
                        entry.FullName
                            .EndsWith(
                                ".fbx",
                                StringComparison
                                    .OrdinalIgnoreCase
                            )
                );

        ZipArchiveEntry textureEntry =
            archive.Entries
                .FirstOrDefault(
                    entry =>
                        IsImageFile(
                            entry.FullName
                        ) &&
                        entry.FullName
                            .IndexOf(
                                "texture",
                                StringComparison
                                    .OrdinalIgnoreCase
                            ) >= 0
                ) ??
            archive.Entries
                .FirstOrDefault(
                    entry =>
                        IsImageFile(
                            entry.FullName
                        )
                );

        if (fbxEntry == null)
        {
            throw new InvalidDataException(
                "No .fbx file was found inside the selected ZIP."
            );
        }

        if (textureEntry == null)
        {
            throw new InvalidDataException(
                "No JPG/PNG texture was found inside the selected ZIP."
            );
        }

        WriteArchiveEntry(
            fbxEntry,
            FbxPath
        );

        WriteArchiveEntry(
            textureEntry,
            TexturePath
        );

        AssetDatabase.ImportAsset(
            TexturePath,
            ImportAssetOptions
                .ForceSynchronousImport |
            ImportAssetOptions
                .ForceUpdate
        );

        AssetDatabase.ImportAsset(
            FbxPath,
            ImportAssetOptions
                .ForceSynchronousImport |
            ImportAssetOptions
                .ForceUpdate
        );
    }

    private static bool IsImageFile(
        string path)
    {
        string extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return
            extension == ".jpg" ||
            extension == ".jpeg" ||
            extension == ".png";
    }

    private static void WriteArchiveEntry(
        ZipArchiveEntry entry,
        string assetPath)
    {
        string absolutePath =
            Path.GetFullPath(
                assetPath
            );

        string directory =
            Path.GetDirectoryName(
                absolutePath
            );

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(
                directory
            );
        }

        using Stream input =
            entry.Open();

        using FileStream output =
            File.Create(
                absolutePath
            );

        input.CopyTo(output);
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(
            TexturePath,
            ImportAssetOptions
                .ForceSynchronousImport |
            ImportAssetOptions
                .ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(
                TexturePath
            ) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Unity could not create a TextureImporter for the Yellowfin Tuna texture."
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
            TextureImporterCompression
                .CompressedHQ;

        importer.compressionQuality = 90;

        importer.SaveAndReimport();
    }

    private static void ConfigureModelImporter()
    {
        AssetDatabase.ImportAsset(
            FbxPath,
            ImportAssetOptions
                .ForceSynchronousImport |
            ImportAssetOptions
                .ForceUpdate
        );

        ModelImporter importer =
            AssetImporter.GetAtPath(
                FbxPath
            ) as ModelImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Unity could not create a ModelImporter for the Yellowfin Tuna FBX."
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

        importer.isReadable = false;

        importer.SaveAndReimport();

        ModelImporterClipAnimation[] defaults =
            importer.defaultClipAnimations;

        if (defaults == null ||
            defaults.Length == 0)
        {
            return;
        }

        ModelImporterClipAnimation selected =
            defaults.FirstOrDefault(
                clip =>
                    ContainsArmatureAction(
                        clip.name
                    ) ||
                    ContainsArmatureAction(
                        clip.takeName
                    )
            );

        if (selected == null)
        {
            selected =
                defaults.FirstOrDefault(
                    clip =>
                        ContainsArmature(
                            clip.name
                        ) ||
                        ContainsArmature(
                            clip.takeName
                        )
                );
        }

        if (selected == null)
            return;

        selected.name = "Swim";
        selected.loopTime = true;

        importer.clipAnimations =
            new[]
            {
                selected
            };

        importer.SaveAndReimport();
    }

    private static bool ContainsArmatureAction(
        string value)
    {
        return
            !string.IsNullOrEmpty(value) &&
            value.IndexOf(
                "ArmatureAction",
                StringComparison
                    .OrdinalIgnoreCase
            ) >= 0;
    }

    private static bool ContainsArmature(
        string value)
    {
        return
            !string.IsNullOrEmpty(value) &&
            value.IndexOf(
                "Armature",
                StringComparison
                    .OrdinalIgnoreCase
            ) >= 0 &&
            value.IndexOf(
                "Camera",
                StringComparison
                    .OrdinalIgnoreCase
            ) < 0 &&
            value.IndexOf(
                "Lamp",
                StringComparison
                    .OrdinalIgnoreCase
            ) < 0;
    }

    private static AnimationClip FindSwimAnimation()
    {
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath(
                FbxPath
            );

        AnimationClip[] clips =
            assets
                .OfType<AnimationClip>()
                .Where(
                    clip =>
                        !clip.name.StartsWith(
                            "__preview__",
                            StringComparison
                                .OrdinalIgnoreCase
                        )
                )
                .ToArray();

        AnimationClip swim =
            clips.FirstOrDefault(
                clip =>
                    string.Equals(
                        clip.name,
                        "Swim",
                        StringComparison
                            .OrdinalIgnoreCase
                    )
            );

        if (swim != null)
            return swim;

        swim =
            clips.FirstOrDefault(
                clip =>
                    ContainsArmatureAction(
                        clip.name
                    )
            );

        if (swim != null)
            return swim;

        return
            clips.FirstOrDefault(
                clip =>
                    ContainsArmature(
                        clip.name
                    )
            );
    }

    private static Material BuildMaterial()
    {
        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );

        if (shader == null)
        {
            throw new InvalidOperationException(
                "URP/Lit shader was not found."
            );
        }

        Material existing =
            AssetDatabase.LoadAssetAtPath<Material>(
                MaterialPath
            );

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(
                MaterialPath
            );
        }

        Material material =
            new Material(shader)
            {
                name =
                    "YellowfinTuna"
            };

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                TexturePath
            );

        material.SetTexture(
            "_BaseMap",
            texture
        );

        material.SetColor(
            "_BaseColor",
            Color.white
        );

        if (material.HasProperty(
                "_Smoothness"))
        {
            material.SetFloat(
                "_Smoothness",
                0.58f
            );
        }

        if (material.HasProperty(
                "_Metallic"))
        {
            material.SetFloat(
                "_Metallic",
                0.02f
            );
        }

        material.enableInstancing = true;

        AssetDatabase.CreateAsset(
            material,
            MaterialPath
        );

        return material;
    }

    private static AnimatorController BuildAnimatorController(
        AnimationClip swimClip)
    {
        AnimatorController existing =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPath
            );

        if (existing != null)
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

        AnimatorStateMachine machine =
            controller.layers[0]
                .stateMachine;

        AnimatorState state =
            machine.AddState(
                "Swim"
            );

        state.motion = swimClip;
        state.speed = 1f;

        machine.defaultState = state;

        EditorUtility.SetDirty(
            controller
        );

        return controller;
    }

    private static void BuildGameplayPrefab(
        Material material,
        AnimatorController controller)
    {
        GameObject sourceAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                FbxPath
            );

        if (sourceAsset == null)
        {
            throw new InvalidOperationException(
                "The imported Yellowfin Tuna FBX could not be loaded."
            );
        }

        GameObject root =
            new GameObject(
                "YellowfinTuna"
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
                PrefabUtility
                    .InstantiatePrefab(
                        sourceAsset
                    ) as GameObject;

            if (model == null)
            {
                model =
                    UnityEngine.Object
                        .Instantiate(
                            sourceAsset
                        );
            }

            model.name =
                "ImportedModel";

            model.transform.SetParent(
                visual.transform,
                false
            );

            RemoveSceneOnlyComponents(
                model
            );

            ApplyMaterial(
                model,
                material
            );

            Animator animator =
                model.GetComponent<Animator>();

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

            OrientFromArmature(
                model,
                visual.transform
            );

            NormalizeSizeAndCenter(
                root.transform,
                visual.transform
            );

            YellowfinTunaPresentation presentation =
                root.AddComponent<YellowfinTunaPresentation>();

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
        }
        finally
        {
            UnityEngine.Object
                .DestroyImmediate(
                    root
                );
        }
    }

    private static void RemoveSceneOnlyComponents(
        GameObject model)
    {
        foreach (Camera camera
                 in model.GetComponentsInChildren<Camera>(
                     true))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    camera
                );
        }

        foreach (Light light
                 in model.GetComponentsInChildren<Light>(
                     true))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    light
                );
        }

        foreach (AudioListener listener
                 in model.GetComponentsInChildren<AudioListener>(
                     true))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    listener
                );
        }
    }

    private static void ApplyMaterial(
        GameObject model,
        Material material)
    {
        Renderer[] renderers =
            model.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            int slotCount =
                Mathf.Max(
                    1,
                    renderer.sharedMaterials.Length
                );

            Material[] materials =
                new Material[slotCount];

            for (int i = 0;
                 i < slotCount;
                 i++)
            {
                materials[i] =
                    material;
            }

            renderer.sharedMaterials =
                materials;

            renderer.shadowCastingMode =
                ShadowCastingMode.On;

            renderer.receiveShadows = true;
        }
    }

    private static void OrientFromArmature(
        GameObject model,
        Transform visual)
    {
        Transform[] transforms =
            model.GetComponentsInChildren<Transform>(
                true
            );

        Transform first =
            transforms.FirstOrDefault(
                transform =>
                    string.Equals(
                        transform.name,
                        "Bone",
                        StringComparison
                            .OrdinalIgnoreCase
                    )
            );

        Transform last =
            transforms.FirstOrDefault(
                transform =>
                    string.Equals(
                        transform.name,
                        "Bone.003",
                        StringComparison
                            .OrdinalIgnoreCase
                    )
            );

        if (first == null ||
            last == null)
        {
            return;
        }

        Vector3 tailDirection =
            last.position -
            first.position;

        if (tailDirection.sqrMagnitude <
            0.000001f)
        {
            return;
        }

        Vector3 headDirection =
            -tailDirection.normalized;

        visual.rotation =
            Quaternion.FromToRotation(
                headDirection,
                Vector3.forward
            ) *
            visual.rotation;
    }

    private static void NormalizeSizeAndCenter(
        Transform root,
        Transform visual)
    {
        Bounds bounds =
            CalculateBounds(root);

        float length =
            bounds.size.z;

        if (length < 0.001f)
        {
            length =
                Mathf.Max(
                    bounds.size.x,
                    bounds.size.y,
                    bounds.size.z
                );
        }

        if (length > 0.001f)
        {
            float scale =
                TargetBodyLength /
                length;

            visual.localScale *=
                scale;
        }

        bounds =
            CalculateBounds(root);

        Vector3 offset =
            -bounds.center;

        visual.position +=
            offset;
    }

    private static Bounds CalculateBounds(
        Transform root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        bool hasBounds = false;
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

            if (!hasBounds)
            {
                bounds =
                    renderer.bounds;

                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds
                );
            }
        }

        if (!hasBounds)
        {
            bounds =
                new Bounds(
                    root.position,
                    Vector3.one
                );
        }

        return bounds;
    }
}
