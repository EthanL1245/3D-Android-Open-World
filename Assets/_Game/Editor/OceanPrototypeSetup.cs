using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OceanPrototypeSetup
{
    private const string WaterRoot = "Assets/_Game/Water";
    private const string MeshFolder = WaterRoot + "/Meshes";
    private const string MaterialFolder = WaterRoot + "/Materials";

    private const string MeshPath =
        MeshFolder + "/OceanGrid.asset";

    private const string MaterialPath =
        MaterialFolder + "/OceanWater.mat";

    [MenuItem("Tools/Open World/Setup Ocean Prototype")]
    public static void SetupOceanPrototype()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Ocean Prototype",
                "Exit Play Mode before running the setup.",
                "OK"
            );
            return;
        }

        EnsureFolders();

        GameObject player = GameObject.Find("Player");

        if (player == null)
        {
            EditorUtility.DisplayDialog(
                "Ocean Prototype",
                "Could not find a GameObject named 'Player' in the open scene.",
                "OK"
            );
            return;
        }

        Transform playerCamera =
            player.transform.Find("PlayerCamera");

        if (playerCamera == null)
        {
            Camera camera =
                player.GetComponentInChildren<Camera>();

            if (camera != null)
                playerCamera = camera.transform;
        }

        if (playerCamera == null)
        {
            EditorUtility.DisplayDialog(
                "Ocean Prototype",
                "Could not find PlayerCamera under Player.",
                "OK"
            );
            return;
        }

        Mesh oceanMesh = GetOrCreateOceanMesh();
        Material oceanMaterial = GetOrCreateOceanMaterial();

        if (oceanMaterial == null)
            return;

        GameObject oceanObject =
            GameObject.Find("OceanSystem");

        bool createdOcean = oceanObject == null;

        if (createdOcean)
        {
            oceanObject = new GameObject("OceanSystem");
            Undo.RegisterCreatedObjectUndo(
                oceanObject,
                "Create Ocean System"
            );

            // Low terrain becomes shoreline/ocean immediately.
            oceanObject.transform.position =
                new Vector3(0f, 2.5f, 0f);
        }

        MeshFilter meshFilter =
            GetOrAddComponent<MeshFilter>(oceanObject);

        MeshRenderer meshRenderer =
            GetOrAddComponent<MeshRenderer>(oceanObject);

        OceanWater oceanWater =
            GetOrAddComponent<OceanWater>(oceanObject);

        meshFilter.sharedMesh = oceanMesh;
        meshRenderer.sharedMaterial = oceanMaterial;
        meshRenderer.shadowCastingMode =
            ShadowCastingMode.Off;

        meshRenderer.receiveShadows = false;

        oceanWater.Configure(
            meshRenderer,
            player.transform
        );

        FirstPersonController controller =
            player.GetComponent<FirstPersonController>();

        if (controller != null)
        {
            controller.SetOceanWater(oceanWater);
            EditorUtility.SetDirty(controller);
        }

        Canvas canvas =
            Object.FindFirstObjectByType<Canvas>();

        Image overlay =
            EnsureUnderwaterOverlay(canvas);

        UnderwaterVisuals visuals =
            playerCamera.GetComponent<UnderwaterVisuals>();

        if (visuals == null)
        {
            visuals =
                Undo.AddComponent<UnderwaterVisuals>(
                    playerCamera.gameObject
                );
        }

        visuals.Configure(oceanWater, overlay);

        ConfigureTouchLayering(canvas);

        EditorUtility.SetDirty(oceanObject);
        EditorUtility.SetDirty(oceanWater);
        EditorUtility.SetDirty(visuals);

        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Selection.activeGameObject = oceanObject;

        EditorUtility.DisplayDialog(
            "Ocean Prototype Ready",
            "Created the animated ocean, swimming support, underwater fog/tint, and wired the player.\n\n" +
            "Sea level starts at Y = " +
            oceanObject.transform.position.y.ToString("0.0") +
            ". Select OceanSystem and move only its Y position if you want more or less flooded terrain.",
            "OK"
        );
    }

    private static T GetOrAddComponent<T>(
        GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component == null)
        {
            component =
                Undo.AddComponent<T>(gameObject);
        }

        return component;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/_Game", "Water");
        EnsureFolder(WaterRoot, "Meshes");
        EnsureFolder(WaterRoot, "Materials");
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

    private static Mesh GetOrCreateOceanMesh()
    {
        Mesh existing =
            AssetDatabase.LoadAssetAtPath<Mesh>(
                MeshPath
            );

        if (existing != null)
            return existing;

        const float size = 600f;
        const int segments = 160;

        int row = segments + 1;
        int vertexCount = row * row;

        Vector3[] vertices =
            new Vector3[vertexCount];

        Vector2[] uv =
            new Vector2[vertexCount];

        int[] triangles =
            new int[segments * segments * 6];

        int vertexIndex = 0;

        for (int z = 0; z <= segments; z++)
        {
            float zPercent =
                (float)z / segments;

            float localZ =
                (zPercent - 0.5f) * size;

            for (int x = 0; x <= segments; x++)
            {
                float xPercent =
                    (float)x / segments;

                float localX =
                    (xPercent - 0.5f) * size;

                vertices[vertexIndex] =
                    new Vector3(
                        localX,
                        0f,
                        localZ
                    );

                uv[vertexIndex] =
                    new Vector2(
                        xPercent,
                        zPercent
                    );

                vertexIndex++;
            }
        }

        int triangleIndex = 0;

        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int i0 = z * row + x;
                int i1 = i0 + 1;
                int i2 = i0 + row;
                int i3 = i2 + 1;

                triangles[triangleIndex++] = i0;
                triangles[triangleIndex++] = i2;
                triangles[triangleIndex++] = i1;

                triangles[triangleIndex++] = i1;
                triangles[triangleIndex++] = i2;
                triangles[triangleIndex++] = i3;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "OceanGrid"
        };

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();

        mesh.bounds =
            new Bounds(
                Vector3.zero,
                new Vector3(
                    size,
                    8f,
                    size
                )
            );

        AssetDatabase.CreateAsset(
            mesh,
            MeshPath
        );

        return mesh;
    }

    private static Material GetOrCreateOceanMaterial()
    {
        Shader shader =
            Shader.Find("OpenWorld/OceanWater");

        if (shader == null)
        {
            EditorUtility.DisplayDialog(
                "Ocean Shader Missing",
                "Unity has not imported the OpenWorld/OceanWater shader yet. Wait for compilation/import to finish, then run this setup command again.",
                "OK"
            );
            return null;
        }

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                MaterialPath
            );

        if (material == null)
        {
            material =
                new Material(shader)
                {
                    name = "OceanWater"
                };

            AssetDatabase.CreateAsset(
                material,
                MaterialPath
            );
        }
        else
        {
            material.shader = shader;
        }

        material.SetColor(
            "_ShallowColor",
            new Color(
                0.04f,
                0.50f,
                0.67f,
                1f
            )
        );

        material.SetColor(
            "_DeepColor",
            new Color(
                0.008f,
                0.08f,
                0.24f,
                1f
            )
        );

        material.SetColor(
            "_FoamColor",
            new Color(
                0.86f,
                0.96f,
                1f,
                1f
            )
        );

        material.SetFloat("_Alpha", 0.72f);
        material.SetFloat("_Smoothness", 0.84f);

        EditorUtility.SetDirty(material);

        return material;
    }

    private static Image EnsureUnderwaterOverlay(
        Canvas canvas)
    {
        if (canvas == null)
            return null;

        Transform existing =
            canvas.transform.Find(
                "UnderwaterOverlay"
            );

        Image image;

        if (existing == null)
        {
            GameObject overlayObject =
                new GameObject(
                    "UnderwaterOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            Undo.RegisterCreatedObjectUndo(
                overlayObject,
                "Create Underwater Overlay"
            );

            overlayObject.transform.SetParent(
                canvas.transform,
                false
            );

            image =
                overlayObject.GetComponent<Image>();
        }
        else
        {
            image = existing.GetComponent<Image>();

            if (image == null)
            {
                image =
                    Undo.AddComponent<Image>(
                        existing.gameObject
                    );
            }
        }

        RectTransform rect =
            image.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.raycastTarget = false;
        image.color =
            new Color(
                0f,
                0.24f,
                0.34f,
                0f
            );

        image.transform.SetAsFirstSibling();

        return image;
    }

    private static void ConfigureTouchLayering(
        Canvas canvas)
    {
        if (canvas == null)
            return;

        TouchLookArea touchLook =
            Object.FindFirstObjectByType<TouchLookArea>();

        if (touchLook != null)
        {
            RectTransform rect =
                touchLook.transform as RectTransform;

            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            touchLook.transform.SetSiblingIndex(
                Mathf.Min(
                    1,
                    canvas.transform.childCount - 1
                )
            );
        }

        MobileJoystick joystick =
            Object.FindFirstObjectByType<MobileJoystick>();

        if (joystick != null)
        {
            joystick.transform.SetAsLastSibling();
        }
    }
}
