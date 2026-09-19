using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class HeroYellowtailSetup
{
    private const string RootFolder =
        "Assets/_Game/Fishing/HeroFish";

    private const string TexturePath =
        RootFolder + "/YellowtailBody.png";

    private const string BodyMeshPath =
        RootFolder + "/YellowtailBody.asset";

    private const string TailMeshPath =
        RootFolder + "/YellowtailTail.asset";

    private const string FinMeshPath =
        RootFolder + "/YellowtailFin.asset";

    private const string EyeMeshPath =
        RootFolder + "/YellowtailEye.asset";

    private const string BodyMaterialPath =
        RootFolder + "/YellowtailBody.mat";

    private const string FinMaterialPath =
        RootFolder + "/YellowtailFins.mat";

    private const string EyeMaterialPath =
        RootFolder + "/YellowtailEye.mat";

    private const string PrefabPath =
        "Assets/Resources/Fishing/HeroYellowtail.prefab";

    [MenuItem("Tools/Open World/Build Hero Yellowtail")]
    public static void BuildHeroYellowtail()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Hero Yellowtail",
                "Exit Play Mode before building the hero fish.",
                "OK"
            );
            return;
        }

        EnsureFolders();

        EditorUtility.DisplayProgressBar(
            "Hero Yellowtail",
            "Generating skin texture...",
            0.08f
        );

        Texture2D bodyTexture =
            CreateBodyTexture();

        EditorUtility.DisplayProgressBar(
            "Hero Yellowtail",
            "Building optimized fish mesh...",
            0.24f
        );

        Mesh bodyMesh =
            CreateBodyMesh();

        Mesh tailMesh =
            CreateTailMesh();

        Mesh finMesh =
            CreateFinMesh();

        Mesh eyeMesh =
            CreateEyeMesh();

        Shader heroShader =
            Shader.Find("OpenWorld/HeroFish");

        Shader simpleShader =
            Shader.Find(
                "OpenWorld/FishingLit"
            );

        if (heroShader == null ||
            simpleShader == null)
        {
            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog(
                "Hero Yellowtail",
                "The hero fish shaders have not imported yet. Wait for Unity compilation/import to finish, then run this menu command again.",
                "OK"
            );
            return;
        }

        EditorUtility.DisplayProgressBar(
            "Hero Yellowtail",
            "Creating materials...",
            0.48f
        );

        Material bodyMaterial =
            CreateOrReplaceMaterial(
                BodyMaterialPath,
                heroShader
            );

        bodyMaterial.SetTexture(
            "_BaseMap",
            bodyTexture
        );

        bodyMaterial.SetColor(
            "_BaseColor",
            Color.white
        );

        bodyMaterial.SetFloat(
            "_Smoothness",
            0.78f
        );

        bodyMaterial.SetFloat(
            "_FresnelStrength",
            0.38f
        );

        Material finMaterial =
            CreateOrReplaceMaterial(
                FinMaterialPath,
                simpleShader
            );

        finMaterial.SetColor(
            "_BaseColor",
            new Color(
                0.94f,
                0.72f,
                0.07f,
                1f
            )
        );

        finMaterial.SetFloat(
            "_Smoothness",
            0.48f
        );

        Material eyeMaterial =
            CreateOrReplaceMaterial(
                EyeMaterialPath,
                simpleShader
            );

        eyeMaterial.SetColor(
            "_BaseColor",
            new Color(
                0.012f,
                0.014f,
                0.012f,
                1f
            )
        );

        eyeMaterial.SetFloat(
            "_Smoothness",
            0.92f
        );

        EditorUtility.DisplayProgressBar(
            "Hero Yellowtail",
            "Building animated prefab...",
            0.68f
        );

        GameObject root =
            BuildPrefabObject(
                bodyMesh,
                tailMesh,
                finMesh,
                eyeMesh,
                bodyMaterial,
                finMaterial,
                eyeMaterial
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

        EditorUtility.ClearProgressBar();

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath
            );

        Selection.activeObject = prefab;

        Mesh generated =
            AssetDatabase.LoadAssetAtPath<Mesh>(
                BodyMeshPath
            );

        int triangles =
            generated != null
                ? generated.triangles.Length / 3
                : 0;

        EditorUtility.DisplayDialog(
            "Hero Yellowtail Ready",
            "Built the realistic mobile-safe Yellowtail prefab.\n\nBody mesh: about " +
            triangles +
            " triangles, plus lightweight fins/eyes.\n\nYellowtail catches will now use this model automatically.",
            "OK"
        );
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
            "Added a 6.25 kg Yellowtail to your caught-fish inventory. Open FISH and tap it to hold it.",
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

        string current = pieces[0];

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
        const int width = 512;
        const int height = 256;

        Texture2D texture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                true,
                false
            );

        Color[] pixels =
            new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float v =
                (float)y /
                (height - 1);

            for (int x = 0; x < width; x++)
            {
                float u =
                    (float)x /
                    (width - 1);

                float angle =
                    u * Mathf.PI * 2f;

                float vertical =
                    Mathf.Sin(angle);

                float top =
                    Mathf.Clamp01(
                        vertical * 0.92f +
                        0.18f
                    );

                float belly =
                    Mathf.Clamp01(
                        -vertical * 1.08f
                    );

                Color silver =
                    new Color(
                        0.50f,
                        0.63f,
                        0.68f,
                        1f
                    );

                Color blueTop =
                    new Color(
                        0.045f,
                        0.16f,
                        0.25f,
                        1f
                    );

                Color pearl =
                    new Color(
                        0.80f,
                        0.85f,
                        0.80f,
                        1f
                    );

                Color color =
                    Color.Lerp(
                        silver,
                        blueTop,
                        top
                    );

                color =
                    Color.Lerp(
                        color,
                        pearl,
                        belly * 0.82f
                    );

                float sideDistance =
                    Mathf.Min(
                        Mathf.Abs(
                            Mathf.DeltaAngle(
                                angle *
                                Mathf.Rad2Deg,
                                0f
                            )
                        ),
                        Mathf.Abs(
                            Mathf.DeltaAngle(
                                angle *
                                Mathf.Rad2Deg,
                                180f
                            )
                        )
                    );

                float stripe =
                    1f -
                    Mathf.SmoothStep(
                        0f,
                        18f,
                        sideDistance
                    );

                stripe *=
                    Mathf.SmoothStep(
                        0.06f,
                        0.18f,
                        v
                    ) *
                    (
                        1f -
                        Mathf.SmoothStep(
                            0.82f,
                            0.98f,
                            v
                        )
                    );

                color =
                    Color.Lerp(
                        color,
                        new Color(
                            0.93f,
                            0.71f,
                            0.08f,
                            1f
                        ),
                        stripe * 0.82f
                    );

                float scalePattern =
                    Mathf.Sin(
                        u * 190f +
                        Mathf.Sin(
                            v * 90f
                        ) * 0.9f
                    ) *
                    Mathf.Sin(
                        v * 155f
                    );

                float scaleLight =
                    scalePattern * 0.035f;

                color.r += scaleLight;
                color.g += scaleLight;
                color.b += scaleLight;

                float headMask =
                    1f -
                    Mathf.SmoothStep(
                        0.06f,
                        0.23f,
                        v
                    );

                color =
                    Color.Lerp(
                        color,
                        color *
                        new Color(
                            0.72f,
                            0.82f,
                            0.86f,
                            1f
                        ),
                        headMask * 0.20f
                    );

                pixels[y * width + x] =
                    color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);

        string absolute =
            Path.GetFullPath(
                TexturePath
            );

        File.WriteAllBytes(
            absolute,
            texture.EncodeToPNG()
        );

        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(
            TexturePath,
            ImportAssetOptions.ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(
                TexturePath
            ) as TextureImporter;

        if (importer != null)
        {
            importer.textureType =
                TextureImporterType.Default;

            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode =
                TextureWrapMode.Repeat;

            importer.filterMode =
                FilterMode.Bilinear;

            importer.anisoLevel = 2;
            importer.maxTextureSize = 512;
            importer.textureCompression =
                TextureImporterCompression
                    .Compressed;

            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(
            TexturePath
        );
    }

    private static Mesh CreateBodyMesh()
    {
        const int rings = 16;
        const int segments = 16;

        List<Vector3> vertices =
            new List<Vector3>();

        List<Vector2> uvs =
            new List<Vector2>();

        List<int> triangles =
            new List<int>();

        List<BoneWeight> weights =
            new List<BoneWeight>();

        for (int ring = 0;
             ring < rings;
             ring++)
        {
            float t =
                (float)ring /
                (rings - 1);

            float z =
                Mathf.Lerp(
                    0.78f,
                    -0.67f,
                    t
                );

            float width =
                BodyWidth(t);

            float height =
                width *
                Mathf.Lerp(
                    0.72f,
                    0.60f,
                    t
                );

            for (int segment = 0;
                 segment <= segments;
                 segment++)
            {
                float u =
                    (float)segment /
                    segments;

                float angle =
                    u *
                    Mathf.PI *
                    2f;

                float x =
                    Mathf.Cos(angle) *
                    width;

                float y =
                    Mathf.Sin(angle) *
                    height;

                if (y < 0f)
                    y *= 0.92f;

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

                float bonePosition =
                    t * 3f;

                int bone0 =
                    Mathf.Clamp(
                        Mathf.FloorToInt(
                            bonePosition
                        ),
                        0,
                        3
                    );

                int bone1 =
                    Mathf.Min(
                        bone0 + 1,
                        3
                    );

                float blend =
                    Mathf.Clamp01(
                        bonePosition -
                        bone0
                    );

                BoneWeight weight =
                    new BoneWeight
                    {
                        boneIndex0 = bone0,
                        weight0 = 1f - blend,
                        boneIndex1 = bone1,
                        weight1 = blend
                    };

                weights.Add(weight);
            }
        }

        int row =
            segments + 1;

        for (int ring = 0;
             ring < rings - 1;
             ring++)
        {
            for (int segment = 0;
                 segment < segments;
                 segment++)
            {
                int a =
                    ring * row +
                    segment;

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

        Mesh mesh =
            new Mesh
            {
                name =
                    "HeroYellowtailBody"
            };

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(
            triangles,
            0
        );

        mesh.boneWeights =
            weights.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        ReplaceMeshAsset(
            BodyMeshPath,
            mesh
        );

        return AssetDatabase.LoadAssetAtPath<Mesh>(
            BodyMeshPath
        );
    }

    private static float BodyWidth(float t)
    {
        if (t < 0.10f)
            return Mathf.Lerp(0.07f, 0.24f, t / 0.10f);

        if (t < 0.36f)
            return Mathf.Lerp(0.24f, 0.34f, (t - 0.10f) / 0.26f);

        if (t < 0.62f)
            return Mathf.Lerp(0.34f, 0.30f, (t - 0.36f) / 0.26f);

        if (t < 0.88f)
            return Mathf.Lerp(0.30f, 0.14f, (t - 0.62f) / 0.26f);

        return Mathf.Lerp(0.14f, 0.055f, (t - 0.88f) / 0.12f);
    }

    private static Mesh CreateTailMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(0f, 0f, 0.02f),
            new Vector3(0f, 0.16f, -0.12f),
            new Vector3(0f, 0.42f, -0.48f),
            new Vector3(0f, 0.12f, -0.37f),
            new Vector3(0f, 0f, -0.25f),
            new Vector3(0f, -0.12f, -0.37f),
            new Vector3(0f, -0.42f, -0.48f),
            new Vector3(0f, -0.16f, -0.12f)
        };

        int[] triangles =
        {
            0, 1, 3,
            0, 3, 4,
            4, 5, 7,
            4, 7, 0,
            1, 2, 3,
            5, 6, 7,

            3, 1, 0,
            4, 3, 0,
            7, 5, 4,
            0, 7, 4,
            3, 2, 1,
            7, 6, 5
        };

        return CreateStaticMesh(
            "HeroYellowtailTail",
            vertices,
            triangles,
            TailMeshPath
        );
    }

    private static Mesh CreateFinMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(0f, 0f, 0.24f),
            new Vector3(0f, 0.20f, 0.02f),
            new Vector3(0f, 0.14f, -0.22f),
            new Vector3(0f, 0f, -0.30f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3,
            2, 1, 0,
            3, 2, 0
        };

        return CreateStaticMesh(
            "HeroYellowtailFin",
            vertices,
            triangles,
            FinMeshPath
        );
    }

    private static Mesh CreateEyeMesh()
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
                    Mathf.Cos(angle) * 0.055f,
                    Mathf.Sin(angle) * 0.055f,
                    0f
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
            "HeroYellowtailEye",
            vertices.ToArray(),
            triangles.ToArray(),
            EyeMeshPath
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

        return AssetDatabase.LoadAssetAtPath<Mesh>(
            path
        );
    }

    private static void ReplaceMeshAsset(
        string path,
        Mesh mesh)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(
                path) != null)
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

    private static Material CreateOrReplaceMaterial(
        string path,
        Shader shader)
    {
        Material existing =
            AssetDatabase.LoadAssetAtPath<Material>(
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
        Mesh tailMesh,
        Mesh finMesh,
        Mesh eyeMesh,
        Material bodyMaterial,
        Material finMaterial,
        Material eyeMaterial)
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
            new Transform[4];

        float[] boneZ =
        {
            0.52f,
            0.12f,
            -0.28f,
            -0.64f
        };

        Transform parent =
            rig.transform;

        for (int i = 0; i < bones.Length; i++)
        {
            GameObject bone =
                new GameObject(
                    "Spine" + i
                );

            bone.transform.SetParent(
                parent,
                false
            );

            if (i == 0)
            {
                bone.transform.localPosition =
                    new Vector3(
                        0f,
                        0f,
                        boneZ[i]
                    );
            }
            else
            {
                bone.transform.localPosition =
                    new Vector3(
                        0f,
                        0f,
                        boneZ[i] -
                        boneZ[i - 1]
                    );
            }

            bones[i] = bone.transform;
            parent = bone.transform;
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

        renderer.sharedMaterial =
            bodyMaterial;

        renderer.rootBone =
            bones[0];

        renderer.bones = bones;

        Matrix4x4[] bindposes =
            new Matrix4x4[bones.Length];

        for (int i = 0;
             i < bones.Length;
             i++)
        {
            bindposes[i] =
                bones[i].worldToLocalMatrix *
                body.transform.localToWorldMatrix;
        }

        bodyMesh.bindposes = bindposes;
        EditorUtility.SetDirty(bodyMesh);

        GameObject tail =
            CreateMeshObject(
                "Tail",
                bones[3],
                tailMesh,
                finMaterial
            );

        tail.transform.localPosition =
            new Vector3(
                0f,
                0f,
                -0.05f
            );

        GameObject dorsal =
            CreateMeshObject(
                "DorsalFin",
                bones[1],
                finMesh,
                finMaterial
            );

        dorsal.transform.localPosition =
            new Vector3(
                0f,
                0.24f,
                -0.10f
            );

        dorsal.transform.localScale =
            new Vector3(
                0.82f,
                0.82f,
                1.3f
            );

        GameObject anal =
            CreateMeshObject(
                "AnalFin",
                bones[2],
                finMesh,
                finMaterial
            );

        anal.transform.localPosition =
            new Vector3(
                0f,
                -0.18f,
                -0.04f
            );

        anal.transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                180f
            );

        anal.transform.localScale =
            new Vector3(
                0.58f,
                0.58f,
                0.72f
            );

        GameObject leftPectoral =
            CreateMeshObject(
                "LeftPectoral",
                bones[0],
                finMesh,
                finMaterial
            );

        leftPectoral.transform.localPosition =
            new Vector3(
                0.20f,
                -0.02f,
                -0.03f
            );

        leftPectoral.transform.localRotation =
            Quaternion.Euler(
                12f,
                -18f,
                -28f
            );

        leftPectoral.transform.localScale =
            new Vector3(
                0.62f,
                0.62f,
                0.80f
            );

        GameObject rightPectoral =
            CreateMeshObject(
                "RightPectoral",
                bones[0],
                finMesh,
                finMaterial
            );

        rightPectoral.transform.localPosition =
            new Vector3(
                -0.20f,
                -0.02f,
                -0.03f
            );

        rightPectoral.transform.localRotation =
            Quaternion.Euler(
                12f,
                18f,
                28f
            );

        rightPectoral.transform.localScale =
            new Vector3(
                0.62f,
                0.62f,
                0.80f
            );

        CreateEye(
            "LeftEye",
            root.transform,
            eyeMesh,
            eyeMaterial,
            new Vector3(
                0.205f,
                0.055f,
                0.58f
            ),
            Quaternion.Euler(
                0f,
                90f,
                0f
            )
        );

        CreateEye(
            "RightEye",
            root.transform,
            eyeMesh,
            eyeMaterial,
            new Vector3(
                -0.205f,
                0.055f,
                0.58f
            ),
            Quaternion.Euler(
                0f,
                -90f,
                0f
            )
        );

        animator.Configure(
            bones[1],
            bones[2],
            bones[3],
            tail.transform,
            leftPectoral.transform,
            rightPectoral.transform
        );

        root.transform.localScale =
            Vector3.one;

        return root;
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
            .sharedMesh = mesh;

        gameObject.GetComponent<MeshRenderer>()
            .sharedMaterial = material;

        return gameObject;
    }

    private static void CreateEye(
        string name,
        Transform parent,
        Mesh mesh,
        Material material,
        Vector3 position,
        Quaternion rotation)
    {
        GameObject eye =
            CreateMeshObject(
                name,
                parent,
                mesh,
                material
            );

        eye.transform.localPosition =
            position;

        eye.transform.localRotation =
            rotation;
    }
}
