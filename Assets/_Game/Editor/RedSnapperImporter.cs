using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

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

    private const float TargetLength =
        0.92f;

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
                "Select RedSnapper.zip",
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

            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Extracting animated FBX and texture...",
                0.18f
            );

            ExtractPackage(
                zipPath
            );

            ConfigureTexture();

            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Importing authored bones and swim animation...",
                0.36f
            );

            ConfigureFbxImporter();

            GameObject sourceAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FbxPath
                );

            if (sourceAsset == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the Red Snapper FBX."
                );
            }

            AnimationClip swimClip =
                FindSwimClip();

            if (swimClip == null)
            {
                throw new InvalidOperationException(
                    "The Red Snapper FBX imported, but no swim animation clip was found."
                );
            }

            EditorUtility.DisplayProgressBar(
                "Red Snapper",
                "Building game-lit material and final prefab...",
                0.64f
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
                "Done. Species Red Snapper now uses your rigged FBX, supplied texture, authored swim animation, and the game's URP lighting. The next Play Mode will grant one temporary Red Snapper catch for immediate testing.",
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
        string zipPath)
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

        ZipArchiveEntry fbx =
            archive.Entries
                .FirstOrDefault(
                    entry =>
                        entry.FullName
                            .EndsWith(
                                ".fbx",
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

        if (fbx == null)
        {
            throw new InvalidDataException(
                "No FBX file was found in the Red Snapper ZIP."
            );
        }

        if (texture == null)
        {
            throw new InvalidDataException(
                "No texture image was found in the Red Snapper ZIP."
            );
        }

        WriteEntry(
            fbx,
            FbxPath
        );

        WriteEntry(
            texture,
            TexturePath
        );

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
                "No animation clip was imported from RedSnapper.fbx."
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

        string lower =
            value.ToLowerInvariant();

        return
            lower.Contains(
                "armatureaction"
            ) ||
            lower.Contains(
                "swim"
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

            OrientFromRig(
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

    private static void OrientFromRig(
        Transform visual,
        Transform model)
    {
        Transform first =
            FindBone(
                model,
                "Bone.001"
            );

        Transform last =
            FindBone(
                model,
                "Bone.004"
            );

        if (first == null ||
            last == null)
        {
            Debug.LogWarning(
                "Red Snapper rig endpoints Bone.001/Bone.004 were not found. Keeping the authored source orientation."
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

        visual.rotation =
            Quaternion.Inverse(
                sourceBasis
            ) *
            visual.rotation;
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
