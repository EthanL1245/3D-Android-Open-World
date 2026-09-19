using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HomeBaseEconomySetup
{
    private const string RootFolder =
        "Assets/_Game/HomeBase";

    private const string MaterialFolder =
        RootFolder + "/Materials";

    [MenuItem("Tools/Open World/Setup Home Base Economy")]
    public static void SetupHomeBaseEconomy()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Home Base Economy",
                "Exit Play Mode before running the setup.",
                "OK"
            );

            return;
        }

        GameObject player =
            GameObject.Find("Player");

        if (player == null)
        {
            EditorUtility.DisplayDialog(
                "Home Base Economy",
                "Could not find Player.",
                "OK"
            );

            return;
        }

        Camera camera =
            player.GetComponentInChildren<Camera>();

        Canvas canvas =
            Object.FindFirstObjectByType<Canvas>();

        FishingInventory inventory =
            player.GetComponent<FishingInventory>();

        FishingSystem fishing =
            player.GetComponent<FishingSystem>();

        if (camera == null ||
            canvas == null ||
            inventory == null)
        {
            EditorUtility.DisplayDialog(
                "Home Base Economy",
                "PlayerCamera, Canvas, and FishingInventory are required.",
                "OK"
            );

            return;
        }

        EnsureFolders();

        GameObject oldBase =
            GameObject.Find("HomeBase");

        if (oldBase != null)
        {
            Undo.DestroyObjectImmediate(
                oldBase
            );
        }

        Terrain terrain =
            Object.FindFirstObjectByType<Terrain>();

        OceanWater ocean =
            Object.FindFirstObjectByType<OceanWater>();

        Vector3 center =
            player.transform.position;

        float groundY =
            center.y;

        if (terrain != null)
        {
            groundY =
                terrain.SampleHeight(center) +
                terrain.transform.position.y;
        }

        if (ocean != null)
        {
            groundY =
                Mathf.Max(
                    groundY,
                    ocean.BaseWaterLevel +
                    0.55f
                );
        }

        center.y =
            groundY +
            0.18f;

        RemoveDecorNear(center);

        Material deckMaterial =
            GetOrCreateMaterial(
                "Deck",
                new Color(
                    0.28f,
                    0.17f,
                    0.07f
                )
            );

        Material marketMaterial =
            GetOrCreateMaterial(
                "Market",
                new Color(
                    0.50f,
                    0.15f,
                    0.10f
                )
            );

        Material shopMaterial =
            GetOrCreateMaterial(
                "TankShop",
                new Color(
                    0.08f,
                    0.28f,
                    0.44f
                )
            );

        Material trimMaterial =
            GetOrCreateMaterial(
                "Trim",
                new Color(
                    0.07f,
                    0.08f,
                    0.09f
                )
            );

        GameObject homeBase =
            new GameObject("HomeBase");

        Undo.RegisterCreatedObjectUndo(
            homeBase,
            "Create Home Base"
        );

        homeBase.transform.position =
            center;

        CreateCube(
            "SpawnPlatform",
            homeBase.transform,
            new Vector3(
                0f,
                0f,
                0f
            ),
            new Vector3(
                18f,
                0.35f,
                14f
            ),
            deckMaterial
        );

        Transform marketPoint =
            CreateKiosk(
                "FishMarket",
                homeBase.transform,
                new Vector3(
                    -5.2f,
                    1.35f,
                    3.4f
                ),
                marketMaterial,
                trimMaterial
            );

        Transform shopPoint =
            CreateKiosk(
                "TankShop",
                homeBase.transform,
                new Vector3(
                    5.2f,
                    1.35f,
                    3.4f
                ),
                shopMaterial,
                trimMaterial
            );

        CreateCube(
            "DockRailLeft",
            homeBase.transform,
            new Vector3(
                -8.7f,
                0.70f,
                0f
            ),
            new Vector3(
                0.16f,
                1.25f,
                13.4f
            ),
            trimMaterial
        );

        CreateCube(
            "DockRailRight",
            homeBase.transform,
            new Vector3(
                8.7f,
                0.70f,
                0f
            ),
            new Vector3(
                0.16f,
                1.25f,
                13.4f
            ),
            trimMaterial
        );

        HomeBaseSystem baseSystem =
            homeBase.AddComponent<HomeBaseSystem>();

        baseSystem.Configure(
            player.transform,
            marketPoint,
            shopPoint
        );

        EconomySystem economy =
            player.GetComponent<EconomySystem>();

        if (economy == null)
        {
            economy =
                Undo.AddComponent<EconomySystem>(
                    player
                );
        }

        AquariumSystem aquarium =
            player.GetComponent<AquariumSystem>();

        if (aquarium == null)
        {
            aquarium =
                Undo.AddComponent<AquariumSystem>(
                    player
                );
        }

        aquarium.Configure(
            player.transform,
            camera,
            inventory,
            economy
        );

        EconomyHUD hud =
            canvas.GetComponent<EconomyHUD>();

        if (hud == null)
        {
            hud =
                Undo.AddComponent<EconomyHUD>(
                    canvas.gameObject
                );
        }

        hud.Configure(
            player.transform,
            economy,
            inventory,
            fishing,
            baseSystem,
            aquarium
        );

        CharacterController controller =
            player.GetComponent<CharacterController>();

        float platformTop =
            center.y +
            0.175f;

        Vector3 playerPosition =
            player.transform.position;

        playerPosition.y =
            platformTop +
            0.08f;

        player.transform.position =
            playerPosition;

        EditorUtility.SetDirty(
            homeBase
        );

        EditorUtility.SetDirty(
            baseSystem
        );

        EditorUtility.SetDirty(
            economy
        );

        EditorUtility.SetDirty(
            aquarium
        );

        EditorUtility.SetDirty(
            hud
        );

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Selection.activeGameObject =
            homeBase;

        EditorUtility.DisplayDialog(
            "Home Base Economy Ready",
            "Created the spawn platform, fish market, tank shop, persistent coin counter, fish selling, purchasable/placeable aquariums, and aquarium fish storage.\n\nWalk to the red market kiosk to sell fish. Walk to the blue tank shop to buy a tank for 250 coins. After placing a tank, stand near it and use AQUARIUM to move caught fish in or out.",
            "OK"
        );
    }

    private static Transform CreateKiosk(
        string name,
        Transform parent,
        Vector3 localPosition,
        Material material,
        Material trim)
    {
        GameObject root =
            new GameObject(name);

        root.transform.SetParent(
            parent,
            false
        );

        root.transform.localPosition =
            localPosition;

        CreateCube(
            "Counter",
            root.transform,
            Vector3.zero,
            new Vector3(
                3.4f,
                1.9f,
                1.4f
            ),
            material
        );

        CreateCube(
            "Top",
            root.transform,
            new Vector3(
                0f,
                1.08f,
                0f
            ),
            new Vector3(
                3.8f,
                0.18f,
                1.7f
            ),
            trim
        );

        CreateCube(
            "Marker",
            root.transform,
            new Vector3(
                0f,
                1.75f,
                0f
            ),
            new Vector3(
                1.25f,
                0.60f,
                0.20f
            ),
            material
        );

        return root.transform;
    }

    private static GameObject CreateCube(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject cube =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        cube.name = name;

        cube.transform.SetParent(
            parent,
            false
        );

        cube.transform.localPosition =
            localPosition;

        cube.transform.localScale =
            localScale;

        cube.GetComponent<Renderer>()
            .sharedMaterial = material;

        return cube;
    }

    private static void RemoveDecorNear(
        Vector3 center)
    {
        GameObject decor =
            GameObject.Find("WorldDecor");

        if (decor == null)
            return;

        List<Transform> remove =
            new List<Transform>();

        foreach (Transform group
                 in decor.transform)
        {
            foreach (Transform child
                     in group)
            {
                Vector3 delta =
                    child.position -
                    center;

                delta.y = 0f;

                if (Mathf.Abs(delta.x) < 10f &&
                    Mathf.Abs(delta.z) < 8f)
                {
                    remove.Add(child);
                }
            }
        }

        foreach (Transform target in remove)
        {
            Undo.DestroyObjectImmediate(
                target.gameObject
            );
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(
                RootFolder))
        {
            AssetDatabase.CreateFolder(
                "Assets/_Game",
                "HomeBase"
            );
        }

        if (!AssetDatabase.IsValidFolder(
                MaterialFolder))
        {
            AssetDatabase.CreateFolder(
                RootFolder,
                "Materials"
            );
        }
    }

    private static Material GetOrCreateMaterial(
        string name,
        Color color)
    {
        string path =
            MaterialFolder +
            "/" +
            name +
            ".mat";

        Material material =
            AssetDatabase
                .LoadAssetAtPath<Material>(
                    path
                );

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );

        if (material == null)
        {
            material =
                new Material(shader);

            material.name = name;

            AssetDatabase.CreateAsset(
                material,
                path
            );
        }

        if (material.HasProperty(
                "_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }

        if (material.HasProperty(
                "_Smoothness"))
        {
            material.SetFloat(
                "_Smoothness",
                0.20f
            );
        }

        EditorUtility.SetDirty(
            material
        );

        return material;
    }
}
