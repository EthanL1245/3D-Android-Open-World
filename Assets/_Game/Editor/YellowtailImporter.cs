using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

public static class YellowtailImporter
{
    private const string Root =
        "Assets/_Game/Fishing/YellowtailImported";

    private const string Source =
        Root + "/Source";

    private const string FbxPath =
        Source + "/YellowTail.fbx";

    private const string TexturePath =
        Source + "/YellowTailTexture.png";

    private const string MaterialPath =
        Root + "/Yellowtail.mat";

    private const string ControllerPath =
        Root + "/Yellowtail.controller";

    // Keep the existing Resources path so FishVisualFactory automatically
    // replaces the old procedural Hero Yellowtail everywhere.
    private const string PrefabPath =
        "Assets/Resources/Fishing/HeroYellowtail.prefab";

    private const float TargetLength = 0.92f;

    [MenuItem("Tools/Open World/Install New Yellowtail (One Click)")]
    public static void InstallOneClick()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Yellowtail Import",
                "Exit Play Mode first.",
                "OK"
            );

            return;
        }

        string zipPath =
            FindYellowtailZip();

        if (string.IsNullOrWhiteSpace(
                zipPath))
        {
            string downloads =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile
                    ),
                    "Downloads"
                );

            zipPath =
                EditorUtility.OpenFilePanel(
                    "Choose Yellow Tail.zip",
                    Directory.Exists(downloads)
                        ? downloads
                        : string.Empty,
                    "zip"
                );
        }

        if (string.IsNullOrWhiteSpace(
                zipPath))
        {
            return;
        }

        try
        {
            ImportFromZip(
                zipPath
            );

            EditorUtility.DisplayDialog(
                "Yellowtail Installed",
                "Done. The new animated Yellowtail replaces the old Yellowtail prefab and uses the same head-led train-track aquarium turning system as Red Snapper.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Yellowtail Import Failed",
                exception.Message +
                "\n\nThe full error is in the Unity Console.",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static void ImportFromZip(
        string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException(
                "Yellow Tail.zip was not found.",
                zipPath
            );
        }

        EditorUtility.DisplayProgressBar(
            "Yellowtail",
            "Extracting model and texture...",
            0.10f
        );

        EnsureFolderRecursive(
            Source
        );

        EnsureFolderRecursive(
            "Assets/Resources/Fishing"
        );

        Extract(
            zipPath
        );

        EditorUtility.DisplayProgressBar(
            "Yellowtail",
            "Importing texture...",
            0.25f
        );

        ConfigureTexture();

        EditorUtility.DisplayProgressBar(
            "Yellowtail",
            "Importing five-bone animated fish...",
            0.42f
        );

        ConfigureFbx();

        EditorUtility.DisplayProgressBar(
            "Yellowtail",
            "Building replacement prefab...",
            0.68f
        );

        BuildPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath
            );
    }

    private static string FindYellowtailZip()
    {
        string user =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile
            );

        string desktop =
            Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory
            );

        string[] folders =
        {
            Path.Combine(
                user,
                "Downloads"
            ),
            desktop,
            Path.GetFullPath(".")
        };

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
                continue;

            string[] files =
                Directory.GetFiles(
                    folder,
                    "*.zip",
                    SearchOption.TopDirectoryOnly
                );

            string best =
                files
                    .Where(
                        file =>
                        {
                            string name =
                                Path.GetFileNameWithoutExtension(
                                    file
                                )
                                .Replace(
                                    " ",
                                    string.Empty
                                )
                                .Replace(
                                    "_",
                                    string.Empty
                                )
                                .Replace(
                                    "-",
                                    string.Empty
                                )
                                .ToLowerInvariant();

                            return
                                name.Contains(
                                    "yellowtail"
                                ) &&
                                !name.Contains(
                                    "goat"
                                );
                        }
                    )
                    .OrderByDescending(
                        File.GetLastWriteTimeUtc
                    )
                    .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(
                    best))
            {
                return best;
            }
        }

        return null;
    }

    private static void Extract(
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

        if (fbx == null)
        {
            throw new InvalidDataException(
                "Yellow Tail.zip does not contain an FBX."
            );
        }

        if (texture == null)
        {
            throw new InvalidDataException(
                "Yellow Tail.zip does not contain a texture image."
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
                "Unity could not import the Yellowtail texture."
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
                "Unity could not import YellowTail.fbx."
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

        if (clips == null ||
            clips.Length == 0)
        {
            throw new InvalidOperationException(
                "The new Yellowtail FBX does not contain an animation take."
            );
        }

        ModelImporterClipAnimation swim =
            clips
                .OrderByDescending(
                    clip =>
                        clip.lastFrame -
                        clip.firstFrame
                )
                .First();

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

    private static void BuildPrefab()
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
                "Yellowtail model or Swim animation could not be loaded."
            );
        }

        Material material =
            BuildMaterial();

        AnimatorController controller =
            BuildController(
                clip
            );

        GameObject root =
            new GameObject(
                "HeroYellowtail"
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

            model.name =
                "AuthoredModel";

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
                );

            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "No Yellowtail renderers were found."
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
                    slots[i] =
                        material;
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

            animator.applyRootMotion =
                false;

            AlignFromRig(
                visual.transform,
                model.transform
            );

            NormalizeAndCenter(
                root.transform,
                visual.transform
            );

            YellowtailPresentation presentation =
                root.AddComponent<YellowtailPresentation>();

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
            new Material(
                shader
            );

        material.name =
            "Yellowtail";

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
            0.36f
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
                .AddState(
                    "Swim"
                );

        state.motion =
            clip;

        controller.layers[0]
            .stateMachine
            .defaultState =
            state;

        return controller;
    }

    private static void AlignFromRig(
        Transform visual,
        Transform model)
    {
        Transform head =
            FindDeepChild(
                model,
                "Bone"
            );

        Transform tail =
            FindDeepChild(
                model,
                "Bone.004"
            );

        if (head == null ||
            tail == null)
        {
            throw new InvalidOperationException(
                "Yellowtail Bone through Bone.004 rig chain was not found."
            );
        }

        Vector3 headDirection =
            head.position -
            tail.position;

        if (headDirection.sqrMagnitude <
            0.0001f)
        {
            throw new InvalidOperationException(
                "Yellowtail rig has zero body direction."
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
            up =
                Vector3.up;
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

    private static Transform FindDeepChild(
        Transform root,
        string childName)
    {
        return
            root
                .GetComponentsInChildren<Transform>(
                    true
                )
                .FirstOrDefault(
                    item =>
                        item.name ==
                        childName
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

        if (length >
            0.0001f)
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

    private static void EnsureFolderRecursive(
        string path)
    {
        string[] parts =
            path.Split('/');

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

            current =
                next;
        }
    }
}
