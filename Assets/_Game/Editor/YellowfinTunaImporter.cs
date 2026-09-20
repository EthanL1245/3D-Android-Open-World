using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
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

    private const string PrefabPath =
        "Assets/Resources/Fishing/YellowfinTuna.prefab";

    private const string InventorySaveKey =
        "OpenWorld.FishingInventory.v1";

    private const string PreviewGrantKeyV1 =
        "OpenWorld.YellowfinTunaPreviewGrant.v1";

    private const string PreviewGrantKeyV2 =
        "OpenWorld.YellowfinTunaPreviewGrant.v2";

    private const float TargetBodyLength =
        0.82f;

    [Serializable]
    private class PreviewFishRecord
    {
        public int speciesId;
        public float weightKg;
        public long caughtUtcTicks;
    }

    [Serializable]
    private class PreviewInventoryData
    {
        public List<PreviewFishRecord> fish =
            new List<PreviewFishRecord>();
    }

    [MenuItem("Tools/Open World/Rebuild Yellowfin Tuna Movement")]
    public static void RebuildYellowfinTunaMovement()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Yellowfin Tuna",
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
                "Yellowfin Tuna",
                "The imported Yellowfin Tuna source is missing. Run Replace Yellowfin Tuna (One Click)... once first.",
                "OK"
            );

            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Rebuilding natural tuna movement...",
                0.20f
            );

            ConfigureTexture();
            ConfigureModelImporter(
                true
            );

            GameObject sourceAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FbxPath
                );

            MeshFilter sourceFilter =
                sourceAsset != null
                    ? sourceAsset
                        .GetComponentsInChildren<MeshFilter>(
                            true
                        )
                        .FirstOrDefault(
                            filter =>
                                filter.sharedMesh != null
                        )
                    : null;

            if (sourceAsset == null ||
                sourceFilter == null)
            {
                throw new InvalidOperationException(
                    "The imported Yellowfin Tuna mesh could not be loaded."
                );
            }

            MeshAnalysis analysis =
                AnalyzeMesh(
                    sourceFilter.sharedMesh,
                    sourceFilter.transform
                );

            Material material =
                BuildMaterial(
                    analysis
                );

            BuildGameplayPrefab(
                sourceAsset,
                material,
                analysis
            );

            ConfigureModelImporter(
                false
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath
                );

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Movement Fixed",
                "Rebuilt the existing Tuna without re-selecting the ZIP. Aquarium bend direction is corrected so the body curves into the turn. Held Tuna now uses a stronger alternating C-bend and tail kick, with less whole-object yaw, so it visibly tries to swim instead of moving like a rigid prop.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Rebuild Failed",
                exception.Message,
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/Open World/Replace Yellowfin Tuna (One Click)...")]
    public static void ReplaceYellowfinTuna()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Yellowfin Tuna",
                "Exit Play Mode first.",
                "OK"
            );

            return;
        }

        string zipPath =
            EditorUtility.OpenFilePanel(
                "Select Unanimated Untextured Yellowfin Tuna.zip",
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
                "Yellowfin Tuna",
                "Cleaning the old imported tuna...",
                0.05f
            );

            CleanOldImportedTuna();
            EnsureFolders();

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Extracting the clean mesh and texture...",
                0.16f
            );

            ExtractSourceFiles(
                zipPath
            );

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Importing the authored mesh without old animation data...",
                0.31f
            );

            ConfigureTexture();
            ConfigureModelImporter(
                true
            );

            GameObject sourceAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    FbxPath
                );

            if (sourceAsset == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the imported Yellowfin Tuna FBX."
                );
            }

            MeshFilter sourceFilter =
                sourceAsset
                    .GetComponentsInChildren<MeshFilter>(
                        true
                    )
                    .FirstOrDefault(
                        filter =>
                            filter.sharedMesh != null
                    );

            if (sourceFilter == null)
            {
                throw new InvalidOperationException(
                    "The FBX does not contain a usable mesh."
                );
            }

            Mesh mesh =
                sourceFilter.sharedMesh;

            MeshAnalysis analysis =
                AnalyzeMesh(
                    mesh,
                    sourceFilter.transform
                );

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Creating the natural fish material and subtle attached swim...",
                0.51f
            );

            Material material =
                BuildMaterial(
                    analysis
                );

            EditorUtility.DisplayProgressBar(
                "Yellowfin Tuna",
                "Building the gameplay prefab...",
                0.70f
            );

            BuildGameplayPrefab(
                sourceAsset,
                material,
                analysis
            );

            ConfigureModelImporter(
                false
            );

            ResetPreviewFish();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath
                );

            Selection.activeObject =
                prefab;

            int triangles =
                CountTriangles(mesh);

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Replaced",
                "Done. The old rig/animation pipeline was removed.\n\nThis version preserves the FBX's original upright orientation and only applies yaw if needed for gameplay forward. It uses your clean authored fish as ONE intact mesh, your supplied texture, and a very subtle GPU body/tail swim so fins stay attached. The material has also been made much less glossy/plastic.\n\nMesh: " +
                mesh.vertexCount +
                " vertices, about " +
                triangles +
                " triangles.\n\nI also reset the temporary preview catch, so the next Play Mode starts with exactly one fresh 14.50 kg Yellowfin Tuna for inspection.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Yellowfin Tuna Import Failed",
                exception.Message +
                "\n\nThe complete exception is in the Unity Console.",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CleanOldImportedTuna()
    {
        if (AssetDatabase.IsValidFolder(
                RootFolder))
        {
            AssetDatabase.DeleteAsset(
                RootFolder
            );
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath) != null)
        {
            AssetDatabase.DeleteAsset(
                PrefabPath
            );
        }

        AssetDatabase.Refresh();
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

        if (AssetDatabase.IsValidFolder(
                path))
        {
            return;
        }

        string[] pieces =
            parent.Split('/');

        string current =
            pieces[0];

        for (int i = 1;
             i < pieces.Length;
             i++)
        {
            string next =
                current + "/" +
                pieces[i];

            if (!AssetDatabase.IsValidFolder(
                    next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    pieces[i]
                );
            }

            current = next;
        }

        if (!AssetDatabase.IsValidFolder(
                path))
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
            File.OpenRead(
                zipPath
            );

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
                        )
                );

        if (fbxEntry == null)
        {
            throw new InvalidDataException(
                "No FBX file was found inside the ZIP."
            );
        }

        if (textureEntry == null)
        {
            throw new InvalidDataException(
                "No JPG/PNG texture was found inside the ZIP."
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

        AssetDatabase.Refresh();
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
        string absolute =
            Path.GetFullPath(
                assetPath
            );

        string directory =
            Path.GetDirectoryName(
                absolute
            );

        if (!Directory.Exists(
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
                "Unity could not create the texture importer."
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

    private static void ConfigureModelImporter(
        bool readable)
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
                "Unity could not create the FBX model importer."
            );
        }

        importer.importAnimation = false;

        importer.animationType =
            ModelImporterAnimationType.None;

        importer.importCameras = false;
        importer.importLights = false;

        importer.importBlendShapes = false;

        importer.materialImportMode =
            ModelImporterMaterialImportMode.None;

        importer.meshCompression =
            ModelImporterMeshCompression.Off;

        importer.importNormals =
            ModelImporterNormals.Import;

        importer.importTangents =
            ModelImporterTangents.CalculateMikk;

        importer.isReadable =
            readable;

        importer.SaveAndReimport();
    }

    private struct MeshAnalysis
    {
        public Vector3 bodyAxis;
        public Vector3 sideAxis;
        public Vector3 upAxis;

        public float bodyMin;
        public float bodyMax;

        public bool tailAtMin;
    }

    private static MeshAnalysis AnalyzeMesh(
        Mesh mesh,
        Transform meshTransform)
    {
        Bounds bounds =
            mesh.bounds;

        // The source FBX has intentionally non-uniform object scaling.
        // Raw mesh bounds alone therefore misidentify Y/Z.
        // Determine anatomical axes after accounting for the authored scale.
        Matrix4x4 matrix =
            meshTransform.localToWorldMatrix;

        Vector3 authoredScale =
            new Vector3(
                matrix.GetColumn(0).magnitude,
                matrix.GetColumn(1).magnitude,
                matrix.GetColumn(2).magnitude
            );

        authoredScale =
            new Vector3(
                Mathf.Max(
                    authoredScale.x,
                    0.0001f
                ),
                Mathf.Max(
                    authoredScale.y,
                    0.0001f
                ),
                Mathf.Max(
                    authoredScale.z,
                    0.0001f
                )
            );

        Vector3 authoredSize =
            Vector3.Scale(
                bounds.size,
                authoredScale
            );

        int lengthIndex =
            LargestAxisIndex(
                authoredSize
            );

        int sideIndex =
            SmallestAxisIndex(
                authoredSize
            );

        int upIndex =
            3 -
            lengthIndex -
            sideIndex;

        Vector3 bodyAxis =
            AxisForIndex(
                lengthIndex
            );

        Vector3 sideAxis =
            AxisForIndex(
                sideIndex
            );

        Vector3 upAxis =
            AxisForIndex(
                upIndex
            );

        float bodyMin =
            Component(
                bounds.min,
                lengthIndex
            );

        float bodyMax =
            Component(
                bounds.max,
                lengthIndex
            );

        bool tailAtMin =
            InferTailAtMin(
                mesh.vertices,
                lengthIndex,
                upIndex,
                bodyMin,
                bodyMax
            );

        Debug.Log(
            "Yellowfin Tuna anatomy: body=" +
            AxisName(lengthIndex) +
            ", up=" +
            AxisName(upIndex) +
            ", side=" +
            AxisName(sideIndex) +
            ", tail=" +
            (
                tailAtMin
                    ? "negative body end"
                    : "positive body end"
            ) +
            ", authored size=" +
            authoredSize
        );

        return new MeshAnalysis
        {
            bodyAxis = bodyAxis,
            sideAxis = sideAxis,
            upAxis = upAxis,
            bodyMin = bodyMin,
            bodyMax = bodyMax,
            tailAtMin = tailAtMin
        };
    }

    private static string AxisName(
        int index)
    {
        switch (index)
        {
            case 0:
                return "X";

            case 1:
                return "Y";

            default:
                return "Z";
        }
    }

    private static int LargestAxisIndex(
        Vector3 value)
    {
        if (value.x >= value.y &&
            value.x >= value.z)
        {
            return 0;
        }

        if (value.y >= value.x &&
            value.y >= value.z)
        {
            return 1;
        }

        return 2;
    }

    private static int SmallestAxisIndex(
        Vector3 value)
    {
        if (value.x <= value.y &&
            value.x <= value.z)
        {
            return 0;
        }

        if (value.y <= value.x &&
            value.y <= value.z)
        {
            return 1;
        }

        return 2;
    }

    private static Vector3 AxisForIndex(
        int index)
    {
        switch (index)
        {
            case 0:
                return Vector3.right;

            case 1:
                return Vector3.up;

            default:
                return Vector3.forward;
        }
    }

    private static float Component(
        Vector3 value,
        int index)
    {
        switch (index)
        {
            case 0:
                return value.x;

            case 1:
                return value.y;

            default:
                return value.z;
        }
    }

    private static bool InferTailAtMin(
        Vector3[] vertices,
        int lengthIndex,
        int upIndex,
        float min,
        float max)
    {
        float range =
            Mathf.Max(
                0.0001f,
                max - min
            );

        float band =
            range * 0.13f;

        float minLow =
            float.PositiveInfinity;

        float minHigh =
            float.NegativeInfinity;

        float maxLow =
            float.PositiveInfinity;

        float maxHigh =
            float.NegativeInfinity;

        foreach (Vector3 vertex
                 in vertices)
        {
            float along =
                Component(
                    vertex,
                    lengthIndex
                );

            float height =
                Component(
                    vertex,
                    upIndex
                );

            if (along <=
                min + band)
            {
                minLow =
                    Mathf.Min(
                        minLow,
                        height
                    );

                minHigh =
                    Mathf.Max(
                        minHigh,
                        height
                    );
            }

            if (along >=
                max - band)
            {
                maxLow =
                    Mathf.Min(
                        maxLow,
                        height
                    );

                maxHigh =
                    Mathf.Max(
                        maxHigh,
                        height
                    );
            }
        }

        float minSpan =
            minHigh - minLow;

        float maxSpan =
            maxHigh - maxLow;

        if (!float.IsFinite(
                minSpan) ||
            !float.IsFinite(
                maxSpan))
        {
            return true;
        }

        // A tuna tail is much taller at its extreme than the pointed snout.
        return minSpan >
               maxSpan;
    }

    private static Material BuildMaterial(
        MeshAnalysis analysis)
    {
        Shader shader =
            Shader.Find(
                "OpenWorld/YellowfinTuna"
            );

        if (shader == null)
        {
            throw new InvalidOperationException(
                "OpenWorld/YellowfinTuna shader has not imported yet. Wait for Unity to finish compiling and run the command again."
            );
        }

        Material existing =
            AssetDatabase
                .LoadAssetAtPath<Material>(
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
            AssetDatabase
                .LoadAssetAtPath<Texture2D>(
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

        material.SetFloat(
            "_Smoothness",
            0.34f
        );

        material.SetFloat(
            "_FresnelStrength",
            0.055f
        );

        material.SetVector(
            "_BodyAxis",
            analysis.bodyAxis
        );

        material.SetVector(
            "_SideAxis",
            analysis.sideAxis
        );

        material.SetFloat(
            "_BodyMin",
            analysis.bodyMin
        );

        material.SetFloat(
            "_BodyMax",
            analysis.bodyMax
        );

        material.SetFloat(
            "_TailAtMin",
            analysis.tailAtMin
                ? 1f
                : 0f
        );

        material.SetFloat(
            "_SwimStrength",
            0.115f
        );

        material.SetFloat(
            "_SwimSpeed",
            5.6f
        );

        material.enableInstancing = true;

        AssetDatabase.CreateAsset(
            material,
            MaterialPath
        );

        return material;
    }

    private static void BuildGameplayPrefab(
        GameObject sourceAsset,
        Material material,
        MeshAnalysis analysis)
    {
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
                "AuthoredYellowfinModel";

            model.transform.SetParent(
                visual.transform,
                false
            );

            StripNonFishComponents(
                model
            );

            Renderer[] renderers =
                model
                    .GetComponentsInChildren<Renderer>(
                        true
                    )
                    .Where(
                        renderer =>
                            !(renderer
                                is ParticleSystemRenderer)
                    )
                    .ToArray();

            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "No fish renderer was found in the FBX."
                );
            }

            foreach (Renderer renderer
                     in renderers)
            {
                Material[] assigned =
                    new Material[
                        Mathf.Max(
                            1,
                            renderer
                                .sharedMaterials
                                .Length
                        )
                    ];

                for (int i = 0;
                     i < assigned.Length;
                     i++)
                {
                    assigned[i] =
                        material;
                }

                renderer.sharedMaterials =
                    assigned;

                renderer.shadowCastingMode =
                    ShadowCastingMode.On;

                renderer.receiveShadows =
                    true;
            }

            OrientFish(
                visual.transform,
                model.transform,
                analysis
            );

            NormalizeAndCenter(
                root.transform,
                visual.transform
            );

            YellowfinTunaPresentation presentation =
                root.AddComponent<YellowfinTunaPresentation>();

            presentation.Configure(
                renderers
            );

            if (AssetDatabase
                .LoadAssetAtPath<GameObject>(
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

    private static void StripNonFishComponents(
        GameObject model)
    {
        foreach (Camera camera
                 in model
                     .GetComponentsInChildren<Camera>(
                         true
                     ))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    camera
                );
        }

        foreach (Light light
                 in model
                     .GetComponentsInChildren<Light>(
                         true
                     ))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    light
                );
        }

        foreach (AudioListener listener
                 in model
                     .GetComponentsInChildren<AudioListener>(
                         true
                     ))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    listener
                );
        }

        foreach (Animator animator
                 in model
                     .GetComponentsInChildren<Animator>(
                         true
                     ))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    animator
                );
        }

        foreach (Animation animation
                 in model
                     .GetComponentsInChildren<Animation>(
                         true
                     ))
        {
            UnityEngine.Object
                .DestroyImmediate(
                    animation
                );
        }
    }

    private static void OrientFish(
        Transform visual,
        Transform model,
        MeshAnalysis analysis)
    {
        MeshFilter meshFilter =
            model
                .GetComponentsInChildren<MeshFilter>(
                    true
                )
                .FirstOrDefault(
                    filter =>
                        filter.sharedMesh != null
                );

        if (meshFilter == null)
            return;

        // Preserve the FBX's authored upright orientation.
        // Only rotate around world Y so the head faces gameplay +Z.
        // Never infer or overwrite roll/pitch from mesh bounds.
        Vector3 headAxisLocal =
            analysis.tailAtMin
                ? analysis.bodyAxis
                : -analysis.bodyAxis;

        Vector3 headWorld =
            meshFilter.transform
                .TransformDirection(
                    headAxisLocal
                );

        Vector3 horizontalHead =
            Vector3.ProjectOnPlane(
                headWorld,
                Vector3.up
            );

        if (horizontalHead.sqrMagnitude <
            0.0001f)
        {
            Debug.LogWarning(
                "Yellowfin Tuna forward axis could not be resolved horizontally. Keeping the FBX's original orientation unchanged."
            );

            return;
        }

        horizontalHead.Normalize();

        float yaw =
            Vector3.SignedAngle(
                horizontalHead,
                Vector3.forward,
                Vector3.up
            );

        visual.rotation =
            Quaternion.AngleAxis(
                yaw,
                Vector3.up
            ) *
            visual.rotation;
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
            bounds.size.z;

        if (length > 0.0001f)
        {
            visual.localScale *=
                TargetBodyLength /
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
            root
                .GetComponentsInChildren<Renderer>(
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

    private static int CountTriangles(
        Mesh mesh)
    {
        int count = 0;

        for (int subMesh = 0;
             subMesh < mesh.subMeshCount;
             subMesh++)
        {
            count +=
                (int)mesh
                    .GetIndexCount(
                        subMesh
                    ) /
                3;
        }

        return count;
    }

    private static void ResetPreviewFish()
    {
        if (PlayerPrefs.HasKey(
                InventorySaveKey))
        {
            string json =
                PlayerPrefs.GetString(
                    InventorySaveKey,
                    string.Empty
                );

            if (!string.IsNullOrWhiteSpace(
                    json))
            {
                try
                {
                    PreviewInventoryData data =
                        JsonUtility
                            .FromJson<PreviewInventoryData>(
                                json
                            );

                    if (data != null &&
                        data.fish != null)
                    {
                        data.fish.RemoveAll(
                            record =>
                                record.speciesId ==
                                FishCatalog
                                    .YellowfinTunaId
                        );

                        PlayerPrefs.SetString(
                            InventorySaveKey,
                            JsonUtility.ToJson(
                                data
                            )
                        );
                    }
                }
                catch
                {
                    // Keep unrelated inventory untouched if an old save cannot be parsed.
                }
            }
        }

        PlayerPrefs.DeleteKey(
            PreviewGrantKeyV1
        );

        PlayerPrefs.DeleteKey(
            PreviewGrantKeyV2
        );

        PlayerPrefs.Save();
    }
}
