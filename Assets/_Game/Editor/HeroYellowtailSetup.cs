using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class HeroYellowtailSetup
{
    private const string RootFolder =
        "Assets/_Game/Fishing/HeroFish";

    private const string BodyTexturePath =
        RootFolder + "/YellowtailBody.png";

    private const string NormalTexturePath =
        RootFolder + "/YellowtailNormal.png";

    private const string BodyMeshPath =
        RootFolder + "/YellowtailBodyV2.asset";

    private const string PectoralMeshPath =
        RootFolder + "/YellowtailPectoralV2.asset";

    private const string GillMeshPath =
        RootFolder + "/YellowtailGillV2.asset";

    private const string EyeMeshPath =
        RootFolder + "/YellowtailEyeV2.asset";

    private const string DiscMeshPath =
        RootFolder + "/YellowtailEyeDiscV2.asset";

    private const string MouthMeshPath =
        RootFolder + "/YellowtailMouthV2.asset";

    private const string BodyMaterialPath =
        RootFolder + "/YellowtailBodyV2.mat";

    private const string FinMaterialPath =
        RootFolder + "/YellowtailFinsV2.mat";

    private const string DetailMaterialPath =
        RootFolder + "/YellowtailDetailsV2.mat";

    private const string EyeMaterialPath =
        RootFolder + "/YellowtailEyeV2.mat";

    private const string PupilMaterialPath =
        RootFolder + "/YellowtailPupilV2.mat";

    private const string PrefabPath =
        "Assets/Resources/Fishing/HeroYellowtail.prefab";

    private const int Rings = 26;
    private const int Segments = 20;

    [MenuItem("Tools/Open World/Rebuild Hero Yellowtail V2")]
    public static void BuildHeroYellowtailV2()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Hero Yellowtail V2",
                "Exit Play Mode before rebuilding the hero fish.",
                "OK"
            );

            return;
        }

        EnsureFolders();

        try
        {
            EditorUtility.DisplayProgressBar(
                "Hero Yellowtail V2",
                "Painting realistic skin and scale relief...",
                0.08f
            );

            Texture2D bodyTexture =
                CreateBodyTexture();

            Texture2D normalTexture =
                CreateNormalTexture();

            EditorUtility.DisplayProgressBar(
                "Hero Yellowtail V2",
                "Sculpting anatomical body and attached fins...",
                0.24f
            );

            Mesh bodyMesh =
                CreateBodyAndIntegratedFinMesh();

            Mesh pectoralMesh =
                CreatePectoralMesh();

            Mesh gillMesh =
                CreateGillMesh();

            Mesh eyeMesh =
                CreateEyeMesh();

            Mesh eyeDiscMesh =
                CreateDiscMesh();

            Mesh mouthMesh =
                CreateMouthMesh();

            Shader heroShader =
                Shader.Find(
                    "OpenWorld/HeroFish"
                );

            Shader finShader =
                Shader.Find(
                    "OpenWorld/HeroFishFin"
                );

            Shader simpleShader =
                Shader.Find(
                    "OpenWorld/FishingLit"
                );

            if (heroShader == null ||
                finShader == null ||
                simpleShader == null)
            {
                EditorUtility.DisplayDialog(
                    "Hero Yellowtail V2",
                    "One or more fish shaders have not imported yet. Wait for Unity's import/compile spinner to finish, then run this command again.",
                    "OK"
                );

                return;
            }

            EditorUtility.DisplayProgressBar(
                "Hero Yellowtail V2",
                "Creating fish materials...",
                0.48f
            );

            Material bodyMaterial =
                CreateMaterial(
                    BodyMaterialPath,
                    heroShader
                );

            bodyMaterial.SetTexture(
                "_BaseMap",
                bodyTexture
            );

            bodyMaterial.SetTexture(
                "_NormalMap",
                normalTexture
            );

            bodyMaterial.SetColor(
                "_BaseColor",
                Color.white
            );

            bodyMaterial.SetFloat(
                "_Smoothness",
                0.80f
            );

            bodyMaterial.SetFloat(
                "_NormalStrength",
                0.68f
            );

            bodyMaterial.SetFloat(
                "_FresnelStrength",
                0.30f
            );

            Material finMaterial =
                CreateMaterial(
                    FinMaterialPath,
                    finShader
                );

            finMaterial.SetColor(
                "_BaseColor",
                new Color(
                    0.98f,
                    0.72f,
                    0.06f,
                    0.90f
                )
            );

            finMaterial.SetFloat(
                "_Smoothness",
                0.44f
            );

            Material detailMaterial =
                CreateMaterial(
                    DetailMaterialPath,
                    finShader
                );

            detailMaterial.SetColor(
                "_BaseColor",
                new Color(
                    0.055f,
                    0.095f,
                    0.105f,
                    0.74f
                )
            );

            Material eyeMaterial =
                CreateMaterial(
                    EyeMaterialPath,
                    simpleShader
                );

            eyeMaterial.SetColor(
                "_BaseColor",
                new Color(
                    0.56f,
                    0.43f,
                    0.14f,
                    1f
                )
            );

            eyeMaterial.SetFloat(
                "_Smoothness",
                0.94f
            );

            Material pupilMaterial =
                CreateMaterial(
                    PupilMaterialPath,
                    simpleShader
                );

            pupilMaterial.SetColor(
                "_BaseColor",
                new Color(
                    0.005f,
                    0.007f,
                    0.007f,
                    1f
                )
            );

            pupilMaterial.SetFloat(
                "_Smoothness",
                0.96f
            );

            EditorUtility.DisplayProgressBar(
                "Hero Yellowtail V2",
                "Rigging and assembling hero prefab...",
                0.72f
            );

            GameObject root =
                BuildPrefabObject(
                    bodyMesh,
                    pectoralMesh,
                    gillMesh,
                    eyeMesh,
                    eyeDiscMesh,
                    mouthMesh,
                    bodyMaterial,
                    finMaterial,
                    detailMaterial,
                    eyeMaterial,
                    pupilMaterial
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

            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath
                );

            Selection.activeObject =
                prefab;

            int bodyTriangles =
                bodyMesh != null
                    ? bodyMesh.triangles.Length / 3
                    : 0;

            EditorUtility.DisplayDialog(
                "Hero Yellowtail V2 Ready",
                "Rebuilt the Yellowtail with a sculpted head/body profile, narrow tail stem, integrated dorsal/anal/tail fins, embedded pectoral fin roots, gill plates, mouth, proper eyes, scale normal mapping, and a five-bone swim rig.\n\nMain skinned mesh: about " +
                bodyTriangles +
                " triangles. Ambient ocean fish still use the lightweight model for mobile performance.",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/Open World/Give Trophy Yellowtail (Play Mode)")]
    public static void GiveTrophyYellowtail()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Trophy Yellowtail",
                "Enter Play Mode first, then run this command.",
                "OK"
            );

            return;
        }

        FishingInventory inventory =
            Object.FindFirstObjectByType<FishingInventory>();

        if (inventory == null)
        {
            EditorUtility.DisplayDialog(
                "Trophy Yellowtail",
                "FishingInventory was not found.",
                "OK"
            );

            return;
        }

        inventory.AddFish(
            3,
            6.25f
        );

        EditorUtility.DisplayDialog(
            "Trophy Yellowtail",
            "Added a 6.25 kg Yellowtail. Open FISH and tap it to hold the V2 model.",
            "OK"
        );
    }

    private static void EnsureFolders()
    {
        EnsureFolder(
            "Assets/_Game/Fishing",
            "HeroFish"
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

    private static Texture2D CreateBodyTexture()
    {
        const int width = 1024;
        const int height = 512;

        Texture2D texture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                true,
                false
            );

        Color[] pixels =
            new Color[
                width * height
            ];

        for (int y = 0;
             y < height;
             y++)
        {
            float v =
                (float)y /
                (height - 1);

            for (int x = 0;
                 x < width;
                 x++)
            {
                float u =
                    (float)x /
                    (width - 1);

                float angle =
                    u *
                    Mathf.PI *
                    2f;

                float vertical =
                    Mathf.Sin(angle);

                float topMask =
                    Mathf.Clamp01(
                        vertical * 1.1f
                    );

                float bellyMask =
                    Mathf.Clamp01(
                        -vertical * 1.15f
                    );

                Color silver =
                    new Color(
                        0.48f,
                        0.61f,
                        0.65f,
                        0.84f
                    );

                Color dorsal =
                    new Color(
                        0.035f,
                        0.115f,
                        0.19f,
                        0.92f
                    );

                Color belly =
                    new Color(
                        0.82f,
                        0.86f,
                        0.80f,
                        0.74f
                    );

                Color color =
                    Color.Lerp(
                        silver,
                        dorsal,
                        Mathf.Pow(
                            topMask,
                            0.72f
                        )
                    );

                color =
                    Color.Lerp(
                        color,
                        belly,
                        bellyMask * 0.90f
                    );

                float sideBand =
                    1f -
                    Mathf.SmoothStep(
                        0.02f,
                        0.22f,
                        Mathf.Abs(
                            Mathf.Sin(angle)
                        )
                    );

                float lengthMask =
                    Mathf.SmoothStep(
                        0.10f,
                        0.19f,
                        v
                    ) *
                    (
                        1f -
                        Mathf.SmoothStep(
                            0.84f,
                            0.96f,
                            v
                        )
                    );

                float stripe =
                    sideBand *
                    lengthMask;

                color =
                    Color.Lerp(
                        color,
                        new Color(
                            0.96f,
                            0.72f,
                            0.075f,
                            0.90f
                        ),
                        stripe * 0.82f
                    );

                float scalePattern =
                    ScaleHeight(
                        u,
                        v
                    );

                float sideScales =
                    Mathf.Pow(
                        1f -
                        Mathf.Abs(vertical),
                        0.55f
                    );

                float shimmer =
                    (
                        scalePattern -
                        0.5f
                    ) *
                    0.085f *
                    sideScales;

                color.r += shimmer;
                color.g += shimmer;
                color.b += shimmer;

                float headDarkening =
                    1f -
                    Mathf.SmoothStep(
                        0.04f,
                        0.24f,
                        v
                    );

                color =
                    Color.Lerp(
                        color,
                        color *
                        new Color(
                            0.78f,
                            0.86f,
                            0.90f,
                            1f
                        ),
                        headDarkening *
                        0.22f
                    );

                color.a =
                    Mathf.Clamp01(
                        color.a +
                        scalePattern *
                        0.12f
                    );

                pixels[
                    y * width + x
                ] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);

        WriteTexture(
            texture,
            BodyTexturePath,
            false
        );

        Object.DestroyImmediate(
            texture
        );

        return AssetDatabase.LoadAssetAtPath<Texture2D>(
            BodyTexturePath
        );
    }

    private static Texture2D CreateNormalTexture()
    {
        const int width = 1024;
        const int height = 512;

        Texture2D texture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                true,
                true
            );

        Color[] pixels =
            new Color[
                width * height
            ];

        float du =
            1f / width;

        float dv =
            1f / height;

        for (int y = 0;
             y < height;
             y++)
        {
            float v =
                (float)y /
                (height - 1);

            for (int x = 0;
                 x < width;
                 x++)
            {
                float u =
                    (float)x /
                    (width - 1);

                float left =
                    ScaleHeight(
                        u - du,
                        v
                    );

                float right =
                    ScaleHeight(
                        u + du,
                        v
                    );

                float down =
                    ScaleHeight(
                        u,
                        v - dv
                    );

                float up =
                    ScaleHeight(
                        u,
                        v + dv
                    );

                Vector3 normal =
                    new Vector3(
                        (left - right) * 2.2f,
                        (down - up) * 2.2f,
                        1f
                    ).normalized;

                pixels[
                    y * width + x
                ] =
                    new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1f
                    );
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);

        WriteTexture(
            texture,
            NormalTexturePath,
            true
        );

        Object.DestroyImmediate(
            texture
        );

        return AssetDatabase.LoadAssetAtPath<Texture2D>(
            NormalTexturePath
        );
    }

    private static float ScaleHeight(
        float u,
        float v)
    {
        u = Mathf.Repeat(u, 1f);
        v = Mathf.Clamp01(v);

        float row =
            Mathf.Sin(
                v *
                Mathf.PI *
                2f *
                48f
            );

        float stagger =
            Mathf.Sin(
                u *
                Mathf.PI *
                2f *
                28f +
                row * 0.78f
            );

        float vertical =
            Mathf.Sin(
                v *
                Mathf.PI *
                2f *
                50f
            );

        return
            Mathf.Clamp01(
                0.5f +
                stagger *
                vertical *
                0.5f
            );
    }

    private static void WriteTexture(
        Texture2D texture,
        string assetPath,
        bool normalMap)
    {
        string absolute =
            Path.GetFullPath(
                assetPath
            );

        File.WriteAllBytes(
            absolute,
            texture.EncodeToPNG()
        );

        AssetDatabase.ImportAsset(
            assetPath,
            ImportAssetOptions.ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(
                assetPath
            ) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType =
            normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

        importer.sRGBTexture =
            !normalMap;

        importer.mipmapEnabled = true;

        importer.wrapMode =
            TextureWrapMode.Repeat;

        importer.filterMode =
            FilterMode.Bilinear;

        importer.anisoLevel = 2;
        importer.maxTextureSize = 1024;

        importer.textureCompression =
            TextureImporterCompression
                .CompressedHQ;

        importer.SaveAndReimport();
    }

    private static Mesh CreateBodyAndIntegratedFinMesh()
    {
        List<Vector3> vertices =
            new List<Vector3>();

        List<Vector2> uvs =
            new List<Vector2>();

        List<BoneWeight> weights =
            new List<BoneWeight>();

        List<int> bodyTriangles =
            new List<int>();

        List<int> finTriangles =
            new List<int>();

        for (int ring = 0;
             ring < Rings;
             ring++)
        {
            float t =
                (float)ring /
                (Rings - 1);

            float z =
                ZFromT(t);

            float width =
                BodyWidth(t);

            float top =
                BodyTop(t);

            float bottom =
                BodyBottom(t);

            float center =
                BodyCenterY(t);

            for (int segment = 0;
                 segment <= Segments;
                 segment++)
            {
                float u =
                    (float)segment /
                    Segments;

                float angle =
                    u *
                    Mathf.PI *
                    2f;

                float sin =
                    Mathf.Sin(angle);

                float radiusY =
                    sin >= 0f
                        ? top
                        : bottom;

                float x =
                    Mathf.Cos(angle) *
                    width;

                float y =
                    center +
                    sin * radiusY;

                float sideFlatten =
                    Mathf.Lerp(
                        0.94f,
                        1f,
                        Mathf.Abs(
                            Mathf.Sin(angle)
                        )
                    );

                x *= sideFlatten;

                vertices.Add(
                    new Vector3(
                        x,
                        y,
                        z
                    )
                );

                uvs.Add(
                    new Vector2(
                        u,
                        t
                    )
                );

                weights.Add(
                    WeightForT(t)
                );
            }
        }

        int row =
            Segments + 1;

        for (int ring = 0;
             ring < Rings - 1;
             ring++)
        {
            for (int segment = 0;
                 segment < Segments;
                 segment++)
            {
                int a =
                    ring * row +
                    segment;

                int b = a + 1;
                int c = a + row;
                int d = c + 1;

                bodyTriangles.Add(a);
                bodyTriangles.Add(c);
                bodyTriangles.Add(b);

                bodyTriangles.Add(b);
                bodyTriangles.Add(c);
                bodyTriangles.Add(d);
            }
        }

        AddNoseCap(
            vertices,
            uvs,
            weights,
            bodyTriangles
        );

        AddDorsalFin(
            vertices,
            uvs,
            weights,
            finTriangles
        );

        AddSecondDorsalFin(
            vertices,
            uvs,
            weights,
            finTriangles
        );

        AddAnalFin(
            vertices,
            uvs,
            weights,
            finTriangles
        );

        AddCaudalFin(
            vertices,
            uvs,
            weights,
            finTriangles
        );

        Mesh mesh =
            new Mesh
            {
                name =
                    "HeroYellowtailBodyV2"
            };

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.boneWeights =
            weights.ToArray();

        mesh.subMeshCount = 2;

        mesh.SetTriangles(
            bodyTriangles,
            0
        );

        mesh.SetTriangles(
            finTriangles,
            1
        );

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        ReplaceMeshAsset(
            BodyMeshPath,
            mesh
        );

        return AssetDatabase
            .LoadAssetAtPath<Mesh>(
                BodyMeshPath
            );
    }

    private static void AddNoseCap(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<BoneWeight> weights,
        List<int> triangles)
    {
        int centerIndex =
            vertices.Count;

        vertices.Add(
            new Vector3(
                0f,
                -0.018f,
                0.715f
            )
        );

        uvs.Add(
            new Vector2(
                0.5f,
                0f
            )
        );

        weights.Add(
            FullBoneWeight(0)
        );

        for (int segment = 0;
             segment < Segments;
             segment++)
        {
            triangles.Add(
                centerIndex
            );

            triangles.Add(
                segment + 1
            );

            triangles.Add(
                segment
            );
        }
    }

    private static void AddDorsalFin(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<BoneWeight> weights,
        List<int> triangles)
    {
        float[] t =
        {
            0.30f,
            0.35f,
            0.40f,
            0.45f,
            0.50f
        };

        float[] height =
        {
            0.035f,
            0.16f,
            0.19f,
            0.135f,
            0.025f
        };

        AddFinRibbon(
            vertices,
            uvs,
            weights,
            triangles,
            t,
            height,
            true
        );
    }

    private static void AddSecondDorsalFin(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<BoneWeight> weights,
        List<int> triangles)
    {
        float[] t =
        {
            0.51f,
            0.57f,
            0.64f,
            0.71f,
            0.78f,
            0.84f
        };

        float[] height =
        {
            0.03f,
            0.12f,
            0.13f,
            0.11f,
            0.075f,
            0.018f
        };

        AddFinRibbon(
            vertices,
            uvs,
            weights,
            triangles,
            t,
            height,
            true
        );
    }

    private static void AddAnalFin(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<BoneWeight> weights,
        List<int> triangles)
    {
        float[] t =
        {
            0.57f,
            0.63f,
            0.70f,
            0.77f,
            0.83f
        };

        float[] height =
        {
            0.02f,
            0.105f,
            0.11f,
            0.075f,
            0.015f
        };

        AddFinRibbon(
            vertices,
            uvs,
            weights,
            triangles,
            t,
            height,
            false
        );
    }

    private static void AddFinRibbon(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<BoneWeight> weights,
        List<int> triangles,
        float[] tValues,
        float[] heights,
        bool top)
    {
        int start =
            vertices.Count;

        for (int i = 0;
             i < tValues.Length;
             i++)
        {
            float t =
                tValues[i];

            float z =
                ZFromT(t);

            float rootY =
                BodyCenterY(t) +
                (
                    top
                        ? BodyTop(t)
                        : -BodyBottom(t)
                );

            float tipY =
                rootY +
                (
                    top
                        ? heights[i]
                        : -heights[i]
                );

            vertices.Add(
                new Vector3(
                    0f,
                    rootY - (
                        top ? 0.008f : -0.008f
                    ),
                    z
                )
            );

            vertices.Add(
                new Vector3(
                    0f,
                    tipY,
                    z -
                    heights[i] *
                    0.12f
                )
            );

            uvs.Add(
                new Vector2(
                    0f,
                    (float)i /
                    (tValues.Length - 1)
                )
            );

            uvs.Add(
                new Vector2(
                    1f,
                    (float)i /
                    (tValues.Length - 1)
                )
            );

            BoneWeight weight =
                WeightForT(t);

            weights.Add(weight);
            weights.Add(weight);
        }

        for (int i = 0;
             i < tValues.Length - 1;
             i++)
        {
            int a =
                start + i * 2;

            int b = a + 1;
            int c = a + 2;
            int d = a + 3;

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);

            triangles.Add(b);
            triangles.Add(d);
            triangles.Add(c);
        }
    }

    private static void AddCaudalFin(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<BoneWeight> weights,
        List<int> triangles)
    {
        int start =
            vertices.Count;

        Vector3[] points =
        {
            new Vector3(0f, 0.055f, -0.565f),
            new Vector3(0f, 0.22f, -0.72f),
            new Vector3(0f, 0.38f, -0.95f),
            new Vector3(0f, 0.10f, -0.86f),
            new Vector3(0f, 0f, -0.73f),
            new Vector3(0f, -0.10f, -0.86f),
            new Vector3(0f, -0.38f, -0.95f),
            new Vector3(0f, -0.22f, -0.72f),
            new Vector3(0f, -0.055f, -0.565f)
        };

        Vector2[] tailUv =
        {
            new Vector2(0.50f, 0.56f),
            new Vector2(0.62f, 0.70f),
            new Vector2(0.78f, 1.00f),
            new Vector2(0.68f, 0.52f),
            new Vector2(0.50f, 0.40f),
            new Vector2(0.32f, 0.52f),
            new Vector2(0.22f, 1.00f),
            new Vector2(0.38f, 0.70f),
            new Vector2(0.50f, 0.56f)
        };

        for (int i = 0;
             i < points.Length;
             i++)
        {
            vertices.Add(points[i]);
            uvs.Add(tailUv[i]);
            weights.Add(
                FullBoneWeight(4)
            );
        }

        int[] indices =
        {
            0, 1, 4,
            1, 3, 4,
            1, 2, 3,
            4, 5, 8,
            5, 7, 8,
            5, 6, 7
        };

        for (int i = 0;
             i < indices.Length;
             i++)
        {
            triangles.Add(
                start +
                indices[i]
            );
        }
    }

    private static Mesh CreatePectoralMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-0.025f, 0f, 0.10f),
            new Vector3(0.025f, 0f, 0.08f),
            new Vector3(0.16f, -0.015f, -0.12f),
            new Vector3(0.27f, -0.035f, -0.31f),
            new Vector3(0.08f, -0.012f, -0.23f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 4,
            4, 2, 3
        };

        return CreateStaticMesh(
            "HeroYellowtailPectoralV2",
            vertices,
            triangles,
            PectoralMeshPath
        );
    }

    private static Mesh CreateGillMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(0f, 0.13f, 0.055f),
            new Vector3(0f, 0.145f, 0.025f),
            new Vector3(0f, 0.07f, -0.055f),
            new Vector3(0f, -0.105f, -0.075f),
            new Vector3(0f, -0.115f, -0.045f),
            new Vector3(0f, 0.055f, -0.018f)
        };

        int[] triangles =
        {
            0, 1, 5,
            1, 2, 5,
            5, 2, 4,
            2, 3, 4
        };

        return CreateStaticMesh(
            "HeroYellowtailGillV2",
            vertices,
            triangles,
            GillMeshPath
        );
    }

    private static Mesh CreateEyeMesh()
    {
        const int latitude = 5;
        const int longitude = 10;

        List<Vector3> vertices =
            new List<Vector3>();

        List<int> triangles =
            new List<int>();

        for (int lat = 0;
             lat <= latitude;
             lat++)
        {
            float v =
                (float)lat /
                latitude;

            float phi =
                v * Mathf.PI;

            float y =
                Mathf.Cos(phi) *
                0.038f;

            float ring =
                Mathf.Sin(phi) *
                0.038f;

            for (int lon = 0;
                 lon <= longitude;
                 lon++)
            {
                float u =
                    (float)lon /
                    longitude;

                float theta =
                    u *
                    Mathf.PI *
                    2f;

                vertices.Add(
                    new Vector3(
                        Mathf.Cos(theta) *
                        ring,
                        y,
                        Mathf.Sin(theta) *
                        ring
                    )
                );
            }
        }

        int row =
            longitude + 1;

        for (int lat = 0;
             lat < latitude;
             lat++)
        {
            for (int lon = 0;
                 lon < longitude;
                 lon++)
            {
                int a =
                    lat * row + lon;

                int b = a + 1;
                int c = a + row;
                int d = c + 1;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        return CreateStaticMesh(
            "HeroYellowtailEyeV2",
            vertices.ToArray(),
            triangles.ToArray(),
            EyeMeshPath
        );
    }

    private static Mesh CreateDiscMesh()
    {
        const int segments = 12;

        List<Vector3> vertices =
            new List<Vector3>();

        List<int> triangles =
            new List<int>();

        vertices.Add(Vector3.zero);

        for (int i = 0;
             i <= segments;
             i++)
        {
            float angle =
                (float)i /
                segments *
                Mathf.PI *
                2f;

            vertices.Add(
                new Vector3(
                    0f,
                    Mathf.Cos(angle) *
                    0.021f,
                    Mathf.Sin(angle) *
                    0.021f
                )
            );
        }

        for (int i = 0;
             i < segments;
             i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }

        return CreateStaticMesh(
            "HeroYellowtailEyeDiscV2",
            vertices.ToArray(),
            triangles.ToArray(),
            DiscMeshPath
        );
    }

    private static Mesh CreateMouthMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-0.060f, -0.010f, 0f),
            new Vector3(0.060f, -0.010f, 0f),
            new Vector3(0.052f, 0.010f, 0f),
            new Vector3(-0.052f, 0.010f, 0f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3
        };

        return CreateStaticMesh(
            "HeroYellowtailMouthV2",
            vertices,
            triangles,
            MouthMeshPath
        );
    }

    private static Mesh CreateStaticMesh(
        string name,
        Vector3[] vertices,
        int[] triangles,
        string path)
    {
        Mesh mesh =
            new Mesh
            {
                name = name,
                vertices = vertices,
                triangles = triangles
            };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        ReplaceMeshAsset(
            path,
            mesh
        );

        return AssetDatabase
            .LoadAssetAtPath<Mesh>(
                path
            );
    }

    private static void ReplaceMeshAsset(
        string path,
        Mesh mesh)
    {
        Object existing =
            AssetDatabase
                .LoadAssetAtPath<Object>(
                    path
                );

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(
                path
            );
        }

        AssetDatabase.CreateAsset(
            mesh,
            path
        );
    }

    private static Material CreateMaterial(
        string path,
        Shader shader)
    {
        Material existing =
            AssetDatabase
                .LoadAssetAtPath<Material>(
                    path
                );

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(
                path
            );
        }

        Material material =
            new Material(shader);

        AssetDatabase.CreateAsset(
            material,
            path
        );

        return material;
    }

    private static GameObject BuildPrefabObject(
        Mesh bodyMesh,
        Mesh pectoralMesh,
        Mesh gillMesh,
        Mesh eyeMesh,
        Mesh eyeDiscMesh,
        Mesh mouthMesh,
        Material bodyMaterial,
        Material finMaterial,
        Material detailMaterial,
        Material eyeMaterial,
        Material pupilMaterial)
    {
        GameObject root =
            new GameObject(
                "HeroYellowtail"
            );

        HeroFishAnimator animator =
            root.AddComponent<HeroFishAnimator>();

        GameObject rig =
            new GameObject("Rig");

        rig.transform.SetParent(
            root.transform,
            false
        );

        Transform[] bones =
            new Transform[5];

        float[] boneZ =
        {
            0.51f,
            0.18f,
            -0.10f,
            -0.34f,
            -0.56f
        };

        Transform parent =
            rig.transform;

        for (int i = 0;
             i < bones.Length;
             i++)
        {
            GameObject bone =
                new GameObject(
                    "Spine" + i
                );

            bone.transform.SetParent(
                parent,
                false
            );

            bone.transform.localPosition =
                i == 0
                    ? new Vector3(
                        0f,
                        0f,
                        boneZ[i]
                    )
                    : new Vector3(
                        0f,
                        0f,
                        boneZ[i] -
                        boneZ[i - 1]
                    );

            bones[i] =
                bone.transform;

            parent =
                bone.transform;
        }

        GameObject body =
            new GameObject(
                "Body",
                typeof(SkinnedMeshRenderer)
            );

        body.transform.SetParent(
            root.transform,
            false
        );

        SkinnedMeshRenderer renderer =
            body.GetComponent<SkinnedMeshRenderer>();

        renderer.sharedMesh =
            bodyMesh;

        renderer.sharedMaterials =
            new Material[]
            {
                bodyMaterial,
                finMaterial
            };

        renderer.rootBone =
            bones[0];

        renderer.bones =
            bones;

        renderer.localBounds =
            new Bounds(
                new Vector3(
                    0f,
                    0f,
                    -0.12f
                ),
                new Vector3(
                    0.75f,
                    0.95f,
                    1.95f
                )
            );

        Matrix4x4[] bindposes =
            new Matrix4x4[
                bones.Length
            ];

        for (int i = 0;
             i < bones.Length;
             i++)
        {
            bindposes[i] =
                bones[i]
                    .worldToLocalMatrix *
                body.transform
                    .localToWorldMatrix;
        }

        bodyMesh.bindposes =
            bindposes;

        EditorUtility.SetDirty(
            bodyMesh
        );

        GameObject leftPectoral =
            CreateMeshObject(
                "LeftPectoral",
                bones[0],
                pectoralMesh,
                finMaterial
            );

        leftPectoral.transform.localPosition =
            new Vector3(
                0.205f,
                -0.015f,
                -0.11f
            );

        leftPectoral.transform.localRotation =
            Quaternion.Euler(
                2f,
                -8f,
                -12f
            );

        GameObject rightPectoral =
            CreateMeshObject(
                "RightPectoral",
                bones[0],
                pectoralMesh,
                finMaterial
            );

        rightPectoral.transform.localPosition =
            new Vector3(
                -0.205f,
                -0.015f,
                -0.11f
            );

        rightPectoral.transform.localScale =
            new Vector3(
                -1f,
                1f,
                1f
            );

        rightPectoral.transform.localRotation =
            Quaternion.Euler(
                2f,
                8f,
                12f
            );

        CreateGillDetail(
            "LeftGill",
            bones[0],
            gillMesh,
            detailMaterial,
            new Vector3(
                0.238f,
                0.008f,
                -0.10f
            ),
            Vector3.one
        );

        CreateGillDetail(
            "RightGill",
            bones[0],
            gillMesh,
            detailMaterial,
            new Vector3(
                -0.238f,
                0.008f,
                -0.10f
            ),
            new Vector3(
                -1f,
                1f,
                1f
            )
        );

        CreateEyeAssembly(
            "LeftEye",
            bones[0],
            eyeMesh,
            eyeDiscMesh,
            eyeMaterial,
            pupilMaterial,
            new Vector3(
                0.224f,
                0.067f,
                0.015f
            ),
            1f
        );

        CreateEyeAssembly(
            "RightEye",
            bones[0],
            eyeMesh,
            eyeDiscMesh,
            eyeMaterial,
            pupilMaterial,
            new Vector3(
                -0.224f,
                0.067f,
                0.015f
            ),
            -1f
        );

        GameObject mouth =
            CreateMeshObject(
                "Mouth",
                bones[0],
                mouthMesh,
                detailMaterial
            );

        mouth.transform.localPosition =
            new Vector3(
                0f,
                -0.055f,
                0.201f
            );

        animator.Configure(
            bones[1],
            bones[2],
            bones[3],
            bones[4],
            leftPectoral.transform,
            rightPectoral.transform
        );

        return root;
    }

    private static void CreateGillDetail(
        string name,
        Transform parent,
        Mesh mesh,
        Material material,
        Vector3 localPosition,
        Vector3 localScale)
    {
        GameObject gill =
            CreateMeshObject(
                name,
                parent,
                mesh,
                material
            );

        gill.transform.localPosition =
            localPosition;

        gill.transform.localScale =
            localScale;
    }

    private static void CreateEyeAssembly(
        string name,
        Transform parent,
        Mesh eyeMesh,
        Mesh discMesh,
        Material eyeMaterial,
        Material pupilMaterial,
        Vector3 localPosition,
        float side)
    {
        GameObject eye =
            CreateMeshObject(
                name,
                parent,
                eyeMesh,
                eyeMaterial
            );

        eye.transform.localPosition =
            localPosition;

        GameObject pupil =
            CreateMeshObject(
                "Pupil",
                eye.transform,
                discMesh,
                pupilMaterial
            );

        pupil.transform.localPosition =
            new Vector3(
                0.039f * side,
                0f,
                0f
            );
    }

    private static GameObject CreateMeshObject(
        string name,
        Transform parent,
        Mesh mesh,
        Material material)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );

        gameObject.transform.SetParent(
            parent,
            false
        );

        gameObject.GetComponent<MeshFilter>()
            .sharedMesh =
            mesh;

        gameObject.GetComponent<MeshRenderer>()
            .sharedMaterial =
            material;

        return gameObject;
    }

    private static float ZFromT(float t)
    {
        return Mathf.Lerp(
            0.70f,
            -0.58f,
            t
        );
    }

    private static float BodyWidth(float t)
    {
        if (t < 0.05f)
        {
            return Mathf.Lerp(
                0.060f,
                0.155f,
                t / 0.05f
            );
        }

        if (t < 0.18f)
        {
            return Mathf.Lerp(
                0.155f,
                0.235f,
                (t - 0.05f) /
                0.13f
            );
        }

        if (t < 0.40f)
        {
            return Mathf.Lerp(
                0.235f,
                0.265f,
                (t - 0.18f) /
                0.22f
            );
        }

        if (t < 0.62f)
        {
            return Mathf.Lerp(
                0.265f,
                0.238f,
                (t - 0.40f) /
                0.22f
            );
        }

        if (t < 0.82f)
        {
            return Mathf.Lerp(
                0.238f,
                0.125f,
                (t - 0.62f) /
                0.20f
            );
        }

        return Mathf.Lerp(
            0.125f,
            0.052f,
            (t - 0.82f) /
            0.18f
        );
    }

    private static float BodyTop(float t)
    {
        if (t < 0.08f)
        {
            return Mathf.Lerp(
                0.060f,
                0.180f,
                t / 0.08f
            );
        }

        if (t < 0.30f)
        {
            return Mathf.Lerp(
                0.180f,
                0.285f,
                (t - 0.08f) /
                0.22f
            );
        }

        if (t < 0.56f)
        {
            return Mathf.Lerp(
                0.285f,
                0.265f,
                (t - 0.30f) /
                0.26f
            );
        }

        if (t < 0.82f)
        {
            return Mathf.Lerp(
                0.265f,
                0.125f,
                (t - 0.56f) /
                0.26f
            );
        }

        return Mathf.Lerp(
            0.125f,
            0.060f,
            (t - 0.82f) /
            0.18f
        );
    }

    private static float BodyBottom(float t)
    {
        if (t < 0.08f)
        {
            return Mathf.Lerp(
                0.045f,
                0.145f,
                t / 0.08f
            );
        }

        if (t < 0.34f)
        {
            return Mathf.Lerp(
                0.145f,
                0.245f,
                (t - 0.08f) /
                0.26f
            );
        }

        if (t < 0.58f)
        {
            return Mathf.Lerp(
                0.245f,
                0.230f,
                (t - 0.34f) /
                0.24f
            );
        }

        if (t < 0.82f)
        {
            return Mathf.Lerp(
                0.230f,
                0.110f,
                (t - 0.58f) /
                0.24f
            );
        }

        return Mathf.Lerp(
            0.110f,
            0.050f,
            (t - 0.82f) /
            0.18f
        );
    }

    private static float BodyCenterY(float t)
    {
        return
            Mathf.Lerp(
                -0.012f,
                0.006f,
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                )
            );
    }

    private static BoneWeight WeightForT(float t)
    {
        float position =
            Mathf.Clamp01(t) *
            4f;

        int first =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    position
                ),
                0,
                4
            );

        int second =
            Mathf.Min(
                first + 1,
                4
            );

        float blend =
            Mathf.Clamp01(
                position -
                first
            );

        return new BoneWeight
        {
            boneIndex0 = first,
            weight0 = 1f - blend,
            boneIndex1 = second,
            weight1 = blend
        };
    }

    private static BoneWeight FullBoneWeight(
        int bone)
    {
        return new BoneWeight
        {
            boneIndex0 = bone,
            weight0 = 1f
        };
    }
}
