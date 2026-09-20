using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

public static class MackerelImporter
{
    private const string Root =
        "Assets/_Game/Fishing/Mackerel";

    private const string Source =
        Root + "/Source";

    private const string FbxPath =
        Source + "/Mackerel.fbx";

    private const string TexturePath =
        Source + "/Mackerel_Texture.png";

    private const string MaterialPath =
        Root + "/Mackerel.mat";

    private const string ControllerPath =
        Root + "/Mackerel.controller";

    private const string PrefabPath =
        "Assets/Resources/Fishing/Mackerel.prefab";

    private const float TargetLength = 0.92f;

    public static void ImportFromZip(
        string zipPath,
        bool showDialog)
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before importing Mackerel."
            );
        }

        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException(
                "Mackerel ZIP was not found.",
                zipPath
            );
        }

        EnsureFolderRecursive(Source);
        EnsureFolderRecursive(
            "Assets/Resources/Fishing"
        );

        Extract(zipPath);
        ConfigureTexture();
        ConfigureFbx();
        Build();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath
            );

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Mackerel Imported",
                "The new animated Mackerel model and texture are installed.",
                "OK"
            );
        }
    }

    private static void Extract(
        string zipPath)
    {
        using FileStream stream =
            File.OpenRead(zipPath);

        using ZipArchive archive =
            new ZipArchive(
                stream,
                ZipArchiveMode.Read
            );

        ZipArchiveEntry fbx =
            archive.Entries
                .FirstOrDefault(
                    entry =>
                        entry.FullName.EndsWith(
                            ".fbx",
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
                            ext == ".png" ||
                            ext == ".jpg" ||
                            ext == ".jpeg";
                    }
                );

        if (fbx == null ||
            texture == null)
        {
            throw new InvalidDataException(
                "Mackerel.zip must contain an FBX and texture."
            );
        }

        WriteEntry(fbx, FbxPath);
        WriteEntry(texture, TexturePath);

        AssetDatabase.Refresh();
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
                "Unity could not import the Mackerel texture."
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

        importer.SaveAndReimport();
    }

    private static void ConfigureFbx()
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
                "Unity could not import Mackerel.fbx."
            );
        }

        importer.importAnimation = true;
        importer.animationType =
            ModelImporterAnimationType.Generic;

        importer.importCameras = false;
        importer.importLights = false;

        importer.materialImportMode =
            ModelImporterMaterialImportMode.None;

        importer.importNormals =
            ModelImporterNormals.Import;

        importer.importTangents =
            ModelImporterTangents.CalculateMikk;

        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips =
            importer.defaultClipAnimations;

        ModelImporterClipAnimation swim =
            clips.FirstOrDefault();

        if (swim == null)
        {
            throw new InvalidOperationException(
                "The Mackerel FBX does not contain an animation take."
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

    private static void Build()
    {
        GameObject source =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                FbxPath
            );

        AnimationClip clip =
            AssetDatabase.LoadAllAssetsAtPath(
                    FbxPath
                )
                .OfType<AnimationClip>()
                .FirstOrDefault(
                    item =>
                        item.name == "Swim"
                );

        if (source == null ||
            clip == null)
        {
            throw new InvalidOperationException(
                "Mackerel model or Swim animation could not be loaded."
            );
        }

        Material material =
            BuildMaterial();

        AnimatorController controller =
            BuildController(clip);

        GameObject root =
            new GameObject("Mackerel");

        try
        {
            GameObject visual =
                new GameObject("Visual");

            visual.transform.SetParent(
                root.transform,
                false
            );

            GameObject model =
                PrefabUtility.InstantiatePrefab(
                    source
                ) as GameObject;

            if (model == null)
            {
                model =
                    UnityEngine.Object.Instantiate(
                        source
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

            model.name = "AuthoredModel";

            model.transform.SetParent(
                visual.transform,
                false
            );

            foreach (Camera camera in
                     model.GetComponentsInChildren<Camera>(
                         true
                     ))
            {
                UnityEngine.Object.DestroyImmediate(
                    camera
                );
            }

            foreach (Light light in
                     model.GetComponentsInChildren<Light>(
                         true
                     ))
            {
                UnityEngine.Object.DestroyImmediate(
                    light
                );
            }

            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(
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
                    "No Mackerel renderers were found."
                );
            }

            foreach (Renderer renderer in renderers)
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

                renderer.sharedMaterials = slots;

                renderer.shadowCastingMode =
                    ShadowCastingMode.On;

                renderer.receiveShadows = true;
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

            AlignFromRig(
                visual.transform,
                model.transform
            );

            NormalizeAndCenter(
                root.transform,
                visual.transform
            );

            MackerelPresentation presentation =
                root.AddComponent<MackerelPresentation>();

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

    private static Material BuildMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(
                MaterialPath) != null)
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
                "URP/Lit shader was not found."
            );
        }

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                TexturePath
            );

        Material material =
            new Material(shader);

        material.name = "Mackerel";

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
            0.32f
        );

        AssetDatabase.CreateAsset(
            material,
            MaterialPath
        );

        return material;
    }

    private static AnimatorController BuildController(
        AnimationClip clip)
    {
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath) != null)
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
                .AddState("Swim");

        state.motion = clip;

        controller.layers[0]
            .stateMachine
            .defaultState = state;

        return controller;
    }

    private static void AlignFromRig(
        Transform visual,
        Transform model)
    {
        Transform head =
            Find(
                model,
                "Bone"
            );

        Transform tail =
            Find(
                model,
                "Bone.004"
            );

        if (head == null ||
            tail == null)
        {
            throw new InvalidOperationException(
                "Mackerel Bone/Bone.004 rig chain was not found."
            );
        }

        Vector3 headDirection =
            head.position -
            tail.position;

        if (headDirection.sqrMagnitude <
            0.0001f)
        {
            throw new InvalidOperationException(
                "Mackerel rig has zero body direction."
            );
        }

        headDirection.Normalize();

        Vector3 up =
            Vector3.ProjectOnPlane(
                model.up,
                headDirection
            );

        if (up.sqrMagnitude <
            0.0001f)
        {
            up = Vector3.up;
        }

        up.Normalize();

        visual.rotation =
            Quaternion.LookRotation(
                Vector3.forward,
                Vector3.up
            ) *
            Quaternion.Inverse(
                Quaternion.LookRotation(
                    headDirection,
                    up
                )
            ) *
            visual.rotation;
    }

    private static Transform Find(
        Transform root,
        string name)
    {
        return root
            .GetComponentsInChildren<Transform>(
                true
            )
            .FirstOrDefault(
                item =>
                    item.name == name
            );
    }

    private static void NormalizeAndCenter(
        Transform root,
        Transform visual)
    {
        Bounds bounds =
            CalculateBounds(root);

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
            CalculateBounds(root);

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

        if (renderers.Length == 0)
        {
            return
                new Bounds(
                    root.position,
                    Vector3.one
                );
        }

        Bounds bounds =
            renderers[0].bounds;

        for (int i = 1;
             i < renderers.Length;
             i++)
        {
            bounds.Encapsulate(
                renderers[i].bounds
            );
        }

        return bounds;
    }

    private static void WriteEntry(
        ZipArchiveEntry entry,
        string assetPath)
    {
        string absolute =
            Path.GetFullPath(assetPath);

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                absolute
            )
        );

        using Stream input =
            entry.Open();

        using FileStream output =
            File.Create(absolute);

        input.CopyTo(output);
    }

    private static void EnsureFolderRecursive(
        string path)
    {
        string[] parts =
            path.Split('/');

        string current = "Assets";

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
