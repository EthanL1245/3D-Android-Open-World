using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class VisualWorldSetup
{
    private const string EnvironmentRoot = "Assets/_Game/Environment";
    private const string TextureFolder = EnvironmentRoot + "/Textures";
    private const string MaterialFolder = EnvironmentRoot + "/Materials";
    private const string TerrainLayerFolder = EnvironmentRoot + "/TerrainLayers";
    private const string PrefabFolder = EnvironmentRoot + "/Prefabs";

    private const string SkyMaterialPath = MaterialFolder + "/WorldSky.mat";
    private const string TreePrefabPath = PrefabFolder + "/SimpleTree.prefab";
    private const string RockPrefabPath = PrefabFolder + "/SimpleRock.prefab";

    [MenuItem("Tools/Open World/Apply Visual World Pass")]
    public static void ApplyVisualWorldPass()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Visual World Pass",
                "Exit Play Mode before running this setup.",
                "OK"
            );
            return;
        }

        // This legacy pass overwrites terrain layers and destroys WorldDecor.
        // Never run it on a baked/editable fishing map; it would erase scene edits.
        var expansion = Object.FindFirstObjectByType<IslandExpansionWorld>();
        if (expansion != null && expansion.HasSavedLayout)
        {
            EditorUtility.DisplayDialog("Editable map protected",
                "Apply Visual World Pass is a destructive legacy generator. It is disabled for your saved fishing map so terrain paint and placed scenery are preserved.", "OK");
            return;
        }

        Terrain terrain = Object.FindFirstObjectByType<Terrain>();

        if (terrain == null)
        {
            EditorUtility.DisplayDialog(
                "Visual World Pass",
                "No Terrain was found in the open scene.",
                "OK"
            );
            return;
        }

        EnsureFolders();

        EditorUtility.DisplayProgressBar(
            "Open World Visual Pass",
            "Creating terrain textures...",
            0.05f
        );

        Texture2D sandTexture = CreateOrUpdateTexture(
            TextureFolder + "/Sand.asset",
            new Color(0.44f, 0.34f, 0.20f),
            new Color(0.68f, 0.56f, 0.34f),
            7f,
            11
        );

        Texture2D grassTexture = CreateOrUpdateTexture(
            TextureFolder + "/Grass.asset",
            new Color(0.11f, 0.22f, 0.07f),
            new Color(0.25f, 0.42f, 0.13f),
            9f,
            29
        );

        Texture2D dirtTexture = CreateOrUpdateTexture(
            TextureFolder + "/Dirt.asset",
            new Color(0.18f, 0.105f, 0.055f),
            new Color(0.36f, 0.22f, 0.10f),
            8f,
            47
        );

        Texture2D rockTexture = CreateOrUpdateTexture(
            TextureFolder + "/Rock.asset",
            new Color(0.18f, 0.19f, 0.20f),
            new Color(0.42f, 0.43f, 0.41f),
            11f,
            71
        );

        TerrainLayer sandLayer = CreateOrUpdateTerrainLayer(
            TerrainLayerFolder + "/Sand.terrainlayer",
            sandTexture,
            new Vector2(6f, 6f),
            0.05f
        );

        TerrainLayer grassLayer = CreateOrUpdateTerrainLayer(
            TerrainLayerFolder + "/Grass.terrainlayer",
            grassTexture,
            new Vector2(7f, 7f),
            0.03f
        );

        TerrainLayer dirtLayer = CreateOrUpdateTerrainLayer(
            TerrainLayerFolder + "/Dirt.terrainlayer",
            dirtTexture,
            new Vector2(6f, 6f),
            0.02f
        );

        TerrainLayer rockLayer = CreateOrUpdateTerrainLayer(
            TerrainLayerFolder + "/Rock.terrainlayer",
            rockTexture,
            new Vector2(9f, 9f),
            0.08f
        );

        terrain.terrainData.terrainLayers = new[]
        {
            sandLayer,
            grassLayer,
            dirtLayer,
            rockLayer
        };

        float waterLevel = GetWaterLevel();

        EditorUtility.DisplayProgressBar(
            "Open World Visual Pass",
            "Painting terrain from height and slope...",
            0.25f
        );

        PaintTerrain(
            terrain,
            waterLevel
        );

        EditorUtility.DisplayProgressBar(
            "Open World Visual Pass",
            "Configuring sky and lighting...",
            0.55f
        );

        ConfigureSkyAndLighting();
        ConfigureTerrainRendering(terrain);
        ConfigureCamera();
        TuneOceanMaterial();

        EditorUtility.DisplayProgressBar(
            "Open World Visual Pass",
            "Creating simple world props...",
            0.70f
        );

        GameObject treePrefab = CreateOrUpdateTreePrefab();
        GameObject rockPrefab = CreateOrUpdateRockPrefab();

        EditorUtility.DisplayProgressBar(
            "Open World Visual Pass",
            "Scattering trees and rocks...",
            0.82f
        );

        ScatterDecor(
            terrain,
            waterLevel,
            treePrefab,
            rockPrefab
        );

        EditorUtility.DisplayProgressBar(
            "Open World Visual Pass",
            "Saving...",
            0.96f
        );

        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(terrain.terrainData);

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayDialog(
            "Visual World Pass Complete",
            "The terrain is now painted with sand, grass, dirt and rock; lighting and fog are tuned; the ocean colors are adjusted; and lightweight trees/rocks were scattered.\n\nYou can safely run this command again later. It rebuilds the generated decor rather than duplicating it.",
            "OK"
        );
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/_Game", "Environment");
        EnsureFolder(EnvironmentRoot, "Textures");
        EnsureFolder(EnvironmentRoot, "Materials");
        EnsureFolder(EnvironmentRoot, "TerrainLayers");
        EnsureFolder(EnvironmentRoot, "Prefabs");
    }

    private static void EnsureFolder(
        string parent,
        string name)
    {
        string path = parent + "/" + name;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static Texture2D CreateOrUpdateTexture(
        string path,
        Color darkColor,
        Color lightColor,
        float noiseScale,
        int seed)
    {
        const int size = 256;

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        if (texture == null)
        {
            texture = new Texture2D(
                size,
                size,
                TextureFormat.RGB24,
                true,
                false
            )
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };

            AssetDatabase.CreateAsset(texture, path);
        }

        Color[] pixels = new Color[size * size];

        float offsetX = seed * 13.173f;
        float offsetY = seed * 7.731f;

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;

            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;

                float largeNoise = Mathf.PerlinNoise(
                    nx * noiseScale + offsetX,
                    ny * noiseScale + offsetY
                );

                float fineNoise = Mathf.PerlinNoise(
                    nx * noiseScale * 3.7f + offsetY,
                    ny * noiseScale * 3.7f + offsetX
                );

                float noise =
                    Mathf.Clamp01(
                        largeNoise * 0.72f +
                        fineNoise * 0.28f
                    );

                Color color =
                    Color.Lerp(
                        darkColor,
                        lightColor,
                        noise
                    );

                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 2;
        texture.Apply(true, false);

        EditorUtility.SetDirty(texture);

        return texture;
    }

    private static TerrainLayer CreateOrUpdateTerrainLayer(
        string path,
        Texture2D texture,
        Vector2 tileSize,
        float smoothness)
    {
        TerrainLayer layer =
            AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);

        bool created = layer == null;

        if (created)
        {
            layer = new TerrainLayer();

            layer.name =
                System.IO.Path.GetFileNameWithoutExtension(path);

            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = texture;
        layer.tileSize = tileSize;
        layer.metallic = 0f;
        layer.smoothness = smoothness;

        EditorUtility.SetDirty(layer);

        return layer;
    }

    private static void PaintTerrain(
        Terrain terrain,
        float waterLevel)
    {
        TerrainData data = terrain.terrainData;

        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        const int layers = 4;

        float[,,] alphamaps =
            new float[height, width, layers];

        for (int z = 0; z < height; z++)
        {
            float normalizedZ =
                (float)z / (height - 1);

            if (z % 16 == 0)
            {
                EditorUtility.DisplayProgressBar(
                    "Open World Visual Pass",
                    "Painting terrain from height and slope...",
                    0.25f +
                    0.28f * normalizedZ
                );
            }

            for (int x = 0; x < width; x++)
            {
                float normalizedX =
                    (float)x / (width - 1);

                float worldHeight =
                    terrain.transform.position.y +
                    data.GetInterpolatedHeight(
                        normalizedX,
                        normalizedZ
                    );

                Vector3 normal =
                    data.GetInterpolatedNormal(
                        normalizedX,
                        normalizedZ
                    );

                float slope =
                    Vector3.Angle(
                        normal,
                        Vector3.up
                    );

                float noise =
                    Mathf.PerlinNoise(
                        normalizedX * 8f + 17.3f,
                        normalizedZ * 8f + 41.7f
                    );

                float shoreDistance =
                    Mathf.Abs(worldHeight - waterLevel);

                float sand =
                    1f -
                    Mathf.Clamp01(
                        shoreDistance / 3.8f
                    );

                sand *=
                    1f -
                    Mathf.InverseLerp(
                        18f,
                        42f,
                        slope
                    );

                if (worldHeight < waterLevel + 0.8f)
                {
                    sand =
                        Mathf.Max(
                            sand,
                            0.82f
                        );
                }

                float rock =
                    Mathf.Clamp01(
                        Mathf.InverseLerp(
                            24f,
                            52f,
                            slope
                        ) * 0.95f +
                        Mathf.InverseLerp(
                            waterLevel + 18f,
                            waterLevel + 34f,
                            worldHeight
                        ) * 0.32f
                    );

                rock *= 1f - sand * 0.75f;

                float dirt =
                    Mathf.Clamp01(
                        0.10f +
                        noise * 0.28f +
                        Mathf.InverseLerp(
                            10f,
                            30f,
                            slope
                        ) * 0.30f
                    );

                dirt *=
                    (1f - sand) *
                    (1f - rock);

                float grass =
                    Mathf.Max(
                        0f,
                        1f -
                        sand -
                        dirt -
                        rock
                    );

                if (worldHeight < waterLevel)
                {
                    grass *= 0.15f;
                    dirt *= 0.45f;
                    sand = Mathf.Max(sand, 0.75f);
                }

                float total =
                    sand +
                    grass +
                    dirt +
                    rock;

                if (total < 0.0001f)
                {
                    grass = 1f;
                    total = 1f;
                }

                alphamaps[z, x, 0] = sand / total;
                alphamaps[z, x, 1] = grass / total;
                alphamaps[z, x, 2] = dirt / total;
                alphamaps[z, x, 3] = rock / total;
            }
        }

        Undo.RecordObject(
            data,
            "Paint Open World Terrain"
        );

        data.SetAlphamaps(
            0,
            0,
            alphamaps
        );
    }

    private static void ConfigureSkyAndLighting()
    {
        Material skyMaterial =
            AssetDatabase.LoadAssetAtPath<Material>(
                SkyMaterialPath
            );

        Shader skyShader =
            Shader.Find("Skybox/Procedural");

        if (skyShader != null)
        {
            if (skyMaterial == null)
            {
                skyMaterial =
                    new Material(skyShader)
                    {
                        name = "WorldSky"
                    };

                AssetDatabase.CreateAsset(
                    skyMaterial,
                    SkyMaterialPath
                );
            }
            else
            {
                skyMaterial.shader = skyShader;
            }

            if (skyMaterial.HasProperty("_SkyTint"))
            {
                skyMaterial.SetColor(
                    "_SkyTint",
                    new Color(
                        0.34f,
                        0.55f,
                        0.80f
                    )
                );
            }

            if (skyMaterial.HasProperty("_GroundColor"))
            {
                skyMaterial.SetColor(
                    "_GroundColor",
                    new Color(
                        0.22f,
                        0.24f,
                        0.22f
                    )
                );
            }

            if (skyMaterial.HasProperty("_AtmosphereThickness"))
            {
                skyMaterial.SetFloat(
                    "_AtmosphereThickness",
                    0.85f
                );
            }

            if (skyMaterial.HasProperty("_Exposure"))
            {
                skyMaterial.SetFloat(
                    "_Exposure",
                    0.92f
                );
            }

            if (skyMaterial.HasProperty("_SunSize"))
            {
                skyMaterial.SetFloat(
                    "_SunSize",
                    0.035f
                );
            }

            RenderSettings.skybox = skyMaterial;
            EditorUtility.SetDirty(skyMaterial);
        }

        Light sun = FindDirectionalLight();

        if (sun != null)
        {
            Undo.RecordObject(
                sun,
                "Tune World Sun"
            );

            sun.color =
                new Color(
                    1f,
                    0.93f,
                    0.80f
                );

            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;

            sun.transform.rotation =
                Quaternion.Euler(
                    48f,
                    -35f,
                    0f
                );

            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);
        }

        RenderSettings.ambientMode =
            AmbientMode.Trilight;

        RenderSettings.ambientSkyColor =
            new Color(
                0.30f,
                0.43f,
                0.58f
            );

        RenderSettings.ambientEquatorColor =
            new Color(
                0.26f,
                0.30f,
                0.29f
            );

        RenderSettings.ambientGroundColor =
            new Color(
                0.11f,
                0.12f,
                0.10f
            );

        RenderSettings.ambientIntensity = 0.82f;
        RenderSettings.reflectionIntensity = 0.72f;

        RenderSettings.fog = true;
        RenderSettings.fogMode =
            FogMode.ExponentialSquared;

        RenderSettings.fogColor =
            new Color(
                0.48f,
                0.62f,
                0.70f
            );

        RenderSettings.fogDensity = 0.0018f;

        DynamicGI.UpdateEnvironment();
    }

    private static Light FindDirectionalLight()
    {
        Light[] lights =
            Object.FindObjectsByType<Light>(
                FindObjectsSortMode.None
            );

        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
                return light;
        }

        return null;
    }

    private static void ConfigureTerrainRendering(
        Terrain terrain)
    {
        Undo.RecordObject(
            terrain,
            "Tune Terrain Rendering"
        );

        terrain.drawInstanced = true;
        terrain.basemapDistance = 160f;
        terrain.heightmapPixelError = 7f;
        terrain.detailObjectDistance = 65f;
        terrain.treeDistance = 180f;

        QualitySettings.shadowDistance = 70f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.lodBias = 1.15f;
    }

    private static void ConfigureCamera()
    {
        Camera camera =
            Object.FindFirstObjectByType<Camera>();

        if (camera == null)
            return;

        Undo.RecordObject(
            camera,
            "Tune Player Camera"
        );

        camera.fieldOfView = 72f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 650f;

        EditorUtility.SetDirty(camera);
    }

    private static void TuneOceanMaterial()
    {
        OceanWater ocean =
            Object.FindFirstObjectByType<OceanWater>();

        if (ocean == null)
            return;

        MeshRenderer renderer =
            ocean.GetComponent<MeshRenderer>();

        if (renderer == null ||
            renderer.sharedMaterial == null)
        {
            return;
        }

        Material material =
            renderer.sharedMaterial;

        Undo.RecordObject(
            material,
            "Tune Ocean Material"
        );

        if (material.HasProperty("_ShallowColor"))
        {
            material.SetColor(
                "_ShallowColor",
                new Color(
                    0.025f,
                    0.34f,
                    0.48f,
                    1f
                )
            );
        }

        if (material.HasProperty("_DeepColor"))
        {
            material.SetColor(
                "_DeepColor",
                new Color(
                    0.004f,
                    0.045f,
                    0.16f,
                    1f
                )
            );
        }

        if (material.HasProperty("_FoamColor"))
        {
            material.SetColor(
                "_FoamColor",
                new Color(
                    0.72f,
                    0.90f,
                    0.94f,
                    1f
                )
            );
        }

        if (material.HasProperty("_Alpha"))
        {
            material.SetFloat(
                "_Alpha",
                0.78f
            );
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat(
                "_Smoothness",
                0.87f
            );
        }

        EditorUtility.SetDirty(material);
    }

    private static float GetWaterLevel()
    {
        OceanWater ocean =
            Object.FindFirstObjectByType<OceanWater>();

        return ocean != null
            ? ocean.BaseWaterLevel
            : 2.5f;
    }

    private static GameObject CreateOrUpdateTreePrefab()
    {
        Material trunkMaterial =
            CreateOrUpdateLitMaterial(
                MaterialFolder + "/TreeTrunk.mat",
                new Color(
                    0.20f,
                    0.105f,
                    0.045f
                ),
                0.12f
            );

        Material leafMaterial =
            CreateOrUpdateLitMaterial(
                MaterialFolder + "/TreeLeaves.mat",
                new Color(
                    0.10f,
                    0.27f,
                    0.075f
                ),
                0.06f
            );

        GameObject root =
            new GameObject("SimpleTree");

        GameObject trunk =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        trunk.name = "Trunk";
        trunk.transform.SetParent(
            root.transform,
            false
        );

        trunk.transform.localPosition =
            new Vector3(
                0f,
                1.55f,
                0f
            );

        trunk.transform.localScale =
            new Vector3(
                0.28f,
                1.55f,
                0.28f
            );

        trunk.GetComponent<Renderer>().sharedMaterial =
            trunkMaterial;

        Object.DestroyImmediate(
            trunk.GetComponent<Collider>()
        );

        GameObject canopyLower =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        canopyLower.name = "CanopyLower";
        canopyLower.transform.SetParent(
            root.transform,
            false
        );

        canopyLower.transform.localPosition =
            new Vector3(
                0f,
                3.8f,
                0f
            );

        canopyLower.transform.localScale =
            new Vector3(
                1.65f,
                1.35f,
                1.65f
            );

        canopyLower
            .GetComponent<Renderer>()
            .sharedMaterial = leafMaterial;

        Object.DestroyImmediate(
            canopyLower.GetComponent<Collider>()
        );

        GameObject canopyUpper =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        canopyUpper.name = "CanopyUpper";
        canopyUpper.transform.SetParent(
            root.transform,
            false
        );

        canopyUpper.transform.localPosition =
            new Vector3(
                0f,
                4.9f,
                0f
            );

        canopyUpper.transform.localScale =
            new Vector3(
                1.18f,
                1.05f,
                1.18f
            );

        canopyUpper
            .GetComponent<Renderer>()
            .sharedMaterial = leafMaterial;

        Object.DestroyImmediate(
            canopyUpper.GetComponent<Collider>()
        );

        CapsuleCollider collider =
            root.AddComponent<CapsuleCollider>();

        collider.center =
            new Vector3(
                0f,
                1.55f,
                0f
            );

        collider.height = 3.1f;
        collider.radius = 0.30f;

        GameObject prefab =
            PrefabUtility.SaveAsPrefabAsset(
                root,
                TreePrefabPath
            );

        Object.DestroyImmediate(root);

        return prefab;
    }

    private static GameObject CreateOrUpdateRockPrefab()
    {
        Material rockMaterial =
            CreateOrUpdateLitMaterial(
                MaterialFolder + "/WorldRock.mat",
                new Color(
                    0.24f,
                    0.26f,
                    0.27f
                ),
                0.16f
            );

        GameObject root =
            new GameObject("SimpleRock");

        GameObject rock =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        rock.name = "RockMesh";
        rock.transform.SetParent(
            root.transform,
            false
        );

        rock.transform.localScale =
            new Vector3(
                1.55f,
                0.78f,
                1.18f
            );

        rock.transform.localRotation =
            Quaternion.Euler(
                0f,
                18f,
                7f
            );

        rock.GetComponent<Renderer>().sharedMaterial =
            rockMaterial;

        Object.DestroyImmediate(
            rock.GetComponent<Collider>()
        );

        BoxCollider collider =
            root.AddComponent<BoxCollider>();

        collider.center =
            new Vector3(
                0f,
                0.15f,
                0f
            );

        collider.size =
            new Vector3(
                2.8f,
                1.5f,
                2.2f
            );

        GameObject prefab =
            PrefabUtility.SaveAsPrefabAsset(
                root,
                RockPrefabPath
            );

        Object.DestroyImmediate(root);

        return prefab;
    }

    private static Material CreateOrUpdateLitMaterial(
        string path,
        Color color,
        float smoothness)
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                path
            );

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (material == null)
        {
            material =
                new Material(shader)
                {
                    name =
                        System.IO.Path
                            .GetFileNameWithoutExtension(
                                path
                            )
                };

            AssetDatabase.CreateAsset(
                material,
                path
            );
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor(
                "_Color",
                color
            );
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat(
                "_Smoothness",
                smoothness
            );
        }

        material.enableInstancing = true;

        EditorUtility.SetDirty(material);

        return material;
    }

    private static void ScatterDecor(
        Terrain terrain,
        float waterLevel,
        GameObject treePrefab,
        GameObject rockPrefab)
    {
        GameObject existing =
            GameObject.Find("WorldDecor");

        if (existing != null)
        {
            if (existing.GetComponentInChildren<UserPlacedScenery>(true) != null)
                throw new System.InvalidOperationException("WorldDecor contains protected manual scenery. Move it to the USER PLACED SCENERY scene root before rebuilding decor.");
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject decorRoot =
            new GameObject("WorldDecor");

        Undo.RegisterCreatedObjectUndo(
            decorRoot,
            "Create World Decor"
        );

        GameObject treeRoot =
            new GameObject("Trees");

        treeRoot.transform.SetParent(
            decorRoot.transform,
            false
        );

        GameObject rockRoot =
            new GameObject("Rocks");

        rockRoot.transform.SetParent(
            decorRoot.transform,
            false
        );

        TerrainData data = terrain.terrainData;
        Vector3 terrainPosition =
            terrain.transform.position;

        Transform player =
            GameObject.Find("Player")?.transform;

        Random.State previousState =
            Random.state;

        Random.InitState(20260918);

        int treesPlaced = 0;
        int treeAttempts = 0;

        while (treesPlaced < 45 &&
               treeAttempts < 900)
        {
            treeAttempts++;

            if (TryGetPlacement(
                    terrain,
                    waterLevel,
                    player,
                    1.4f,
                    27f,
                    out Vector3 position,
                    out Vector3 normal))
            {
                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        treePrefab
                    ) as GameObject;

                if (instance == null)
                    continue;

                instance.name =
                    "Tree_" +
                    treesPlaced.ToString("00");

                instance.transform.SetParent(
                    treeRoot.transform,
                    true
                );

                instance.transform.position =
                    position;

                instance.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        Random.Range(
                            0f,
                            360f
                        ),
                        0f
                    );

                float scale =
                    Random.Range(
                        0.75f,
                        1.30f
                    );

                instance.transform.localScale =
                    Vector3.one * scale;

                treesPlaced++;
            }
        }

        int rocksPlaced = 0;
        int rockAttempts = 0;

        while (rocksPlaced < 34 &&
               rockAttempts < 800)
        {
            rockAttempts++;

            if (TryGetPlacement(
                    terrain,
                    waterLevel,
                    player,
                    -0.15f,
                    43f,
                    out Vector3 position,
                    out Vector3 normal))
            {
                if (Random.value < 0.40f &&
                    position.y <
                    waterLevel + 1.0f)
                {
                    continue;
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        rockPrefab
                    ) as GameObject;

                if (instance == null)
                    continue;

                instance.name =
                    "Rock_" +
                    rocksPlaced.ToString("00");

                instance.transform.SetParent(
                    rockRoot.transform,
                    true
                );

                instance.transform.position =
                    position;

                instance.transform.rotation =
                    Quaternion.FromToRotation(
                        Vector3.up,
                        Vector3.Lerp(
                            Vector3.up,
                            normal,
                            0.35f
                        ).normalized
                    ) *
                    Quaternion.Euler(
                        0f,
                        Random.Range(
                            0f,
                            360f
                        ),
                        0f
                    );

                float scale =
                    Random.Range(
                        0.55f,
                        1.75f
                    );

                instance.transform.localScale =
                    Vector3.one * scale;

                rocksPlaced++;
            }
        }

        Random.state = previousState;

        EditorUtility.SetDirty(decorRoot);
    }

    private static bool TryGetPlacement(
        Terrain terrain,
        float waterLevel,
        Transform player,
        float minimumHeightAboveWater,
        float maximumSlope,
        out Vector3 position,
        out Vector3 normal)
    {
        TerrainData data = terrain.terrainData;

        float normalizedX =
            Random.Range(
                0.035f,
                0.965f
            );

        float normalizedZ =
            Random.Range(
                0.035f,
                0.965f
            );

        float worldX =
            terrain.transform.position.x +
            normalizedX *
            data.size.x;

        float worldZ =
            terrain.transform.position.z +
            normalizedZ *
            data.size.z;

        float worldY =
            terrain.transform.position.y +
            data.GetInterpolatedHeight(
                normalizedX,
                normalizedZ
            );

        normal =
            data.GetInterpolatedNormal(
                normalizedX,
                normalizedZ
            );

        float slope =
            Vector3.Angle(
                normal,
                Vector3.up
            );

        position =
            new Vector3(
                worldX,
                worldY,
                worldZ
            );

        if (worldY <
            waterLevel +
            minimumHeightAboveWater)
        {
            return false;
        }

        if (slope > maximumSlope)
            return false;

        if (player != null)
        {
            Vector2 delta =
                new Vector2(
                    worldX -
                    player.position.x,
                    worldZ -
                    player.position.z
                );

            if (delta.sqrMagnitude < 14f * 14f)
                return false;
        }

        return true;
    }
}
