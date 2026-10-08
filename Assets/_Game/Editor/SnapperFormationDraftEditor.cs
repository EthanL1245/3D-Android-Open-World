using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Stage one art review only. Fixed, individually authored placements of the six
/// original UV-mapped FBXs. No random scatter, runtime generator or main-map edits.
/// </summary>
public static class SnapperFormationDraftEditor
{
    internal const string Root = "Assets/_Game/SnapperDraft";
    internal const string MaterialPath = Root + "/Materials/AuthoredRockUV.mat";
    private const string DraftScene = Root + "/Review/SnapperFormation_Stage1.unity";
    private const string Menu = "Tools/Open World/Snapper Draft/";
    private const float Sea = 0f;

    [Serializable] private sealed class Layout { public Piece[] pieces; }
    [Serializable] private sealed class Piece
    {
        public string name;
        public int rock;
        public float x, z, top, pitch, yaw, roll, scale;
    }

    [MenuItem(Menu + "1 - Open or Create Stage 1")]
    public static void OpenDraft()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before opening the island art review.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Save and close Prefab Mode first.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // Reopening NEVER regenerates the scene, terrain or placements.
        if (File.Exists(DraftScene))
        {
            EditorSceneManager.OpenScene(DraftScene, OpenSceneMode.Single);
            Front();
            return;
        }

        Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "/Stage1Layout.json"));
        if (layout == null || layout.pieces == null || layout.pieces.Length == 0)
            throw new InvalidOperationException("The authored stage-one layout is missing.");
        Material stone = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (stone == null || stone.mainTexture == null || stone.shader == null || stone.shader.name != "Universal Render Pipeline/Lit")
            throw new InvalidOperationException("The UV rock material, URP shader or original texture did not import.");
        var models = new GameObject[6];
        for (int i = 0; i < models.Length; i++)
        {
            string path = Root + "/Source/Rock" + (i + 1) + ".fbx";
            // Explicitly reimport the original binary, not the geometry-only legacy FBX.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ValidateSource(models[i], path);
        }
        foreach (Piece piece in layout.pieces)
            if (piece.rock < 1 || piece.rock > 6 || piece.scale < .65f || piece.scale > 1.35f)
                throw new InvalidOperationException("Invalid authored placement: " + piece.name);

        string folder = Root + "/Review";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, "Review");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.64f, .73f, .83f);
            RenderSettings.ambientEquatorColor = new Color(.50f, .51f, .48f);
            RenderSettings.ambientGroundColor = new Color(.28f, .26f, .23f);
            RenderSettings.fog = false;
            var light = new GameObject("Review Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .96f, .87f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = light;

            Terrain terrain = CreateTerrain(folder);
            var formation = new GameObject("STAGE 1 - Main connected formation");
            foreach (Piece piece in layout.pieces) Place(piece, models[piece.rock - 1], stone, formation.transform);
            CreateReviewWater(folder);
            CreateView("Front - sand and retaining cliff", new Vector3(-1f, 9f, 27f), new Vector3(-3f, 1f, -2f));
            CreateView("West - submerged foundation", new Vector3(-28f, 7f, 4f), new Vector3(-4f, 1f, -3f));
            CreateView("Back - sand shelf support", new Vector3(8f, 11f, -27f), new Vector3(-3f, 1f, -3f));
            CreateView("Overhead - footprint", new Vector3(0f, 35f, .01f), Vector3.zero);
            Physics.SyncTransforms();
            ValidateDraft(scene, terrain);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, DraftScene)) throw new IOException("Could not save draft scene.");
            Selection.activeGameObject = formation;
            Front();
            Debug.Log("Stage 1 saved: " + DraftScene + ". Review in Scene view (outside Play Mode). " +
                "Every rock is a named object with uniform scale and a mesh collider; the sand is sculptable Terrain. " +
                "Save edits with Ctrl+S. Send front, west, back and overhead screenshots before detailing or integration.");
        }
        catch (Exception error)
        {
            Debug.LogError("Draft creation failed: " + error.Message + ". The production fishing map was not changed.");
            throw;
        }
    }

    private static void ValidateSource(GameObject model, string path)
    {
        if (model == null) throw new InvalidOperationException("Missing source: " + path);
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0) throw new InvalidOperationException("No rock mesh: " + path);
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable || mesh.uv.Length != mesh.vertexCount)
                throw new InvalidOperationException("Missing authored UV0 or Read/Write support: " + path);
            Vector2[] uv = mesh.uv;
            Vector2 min = uv[0], max = uv[0];
            foreach (Vector2 p in uv) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            if ((max - min).sqrMagnitude < .01f)
                throw new InvalidOperationException("Collapsed UV map: " + path);
        }
    }

    private static void Place(Piece p, GameObject source, Material stone, Transform parent)
    {
        var pivot = new GameObject(p.name + " [Rock " + p.rock + "]");
        pivot.transform.SetParent(parent, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source, pivot.transform);
        // Remove exported scene placement while preserving FBX axis/unit conversion.
        model.transform.localPosition = Vector3.zero;
        Bounds original = BoundsOf(model);
        model.transform.position -= original.center;
        // One factor on all axes: original proportions and UVs remain intact.
        pivot.transform.localScale = Vector3.one * p.scale;
        pivot.transform.rotation = Quaternion.Euler(p.pitch, p.yaw, p.roll);
        pivot.transform.position = new Vector3(p.x, Sea, p.z);
        Bounds oriented = BoundsOf(model);
        // Deliberate sea-relative cap heights; no random placement or ground snapping.
        pivot.transform.position += Vector3.up * (Sea + p.top - oriented.max.y);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = Enumerable.Repeat(stone, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
        }
        foreach (Transform child in pivot.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = true;
    }

    private static Bounds BoundsOf(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Rock has no renderer.");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    // An explicitly shaped sand shelf sits inside the rock retaining faces.
    // The rest is a quiet, low beach foundation for later approved detailing.
    private static readonly Vector2[] Shelf = {
        new Vector2(-7.7f,-3.6f), new Vector2(-6f,-4.5f), new Vector2(-2f,-4.6f),
        new Vector2(.7f,-3.4f), new Vector2(-1f,-2.6f), new Vector2(-6.8f,-2.1f)
    };

    private static float ShelfDistance(Vector2 p)
    {
        bool inside = false;
        float distance = float.PositiveInfinity;
        for (int i = 0, j = Shelf.Length - 1; i < Shelf.Length; j = i++)
        {
            Vector2 a = Shelf[j], b = Shelf[i], ab = b - a;
            Vector2 nearest = a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            distance = Mathf.Min(distance, Vector2.Distance(p, nearest));
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside ? distance : -distance;
    }

    private static float Height(float x, float z)
    {
        float q = Mathf.Sqrt(x * x / (18f * 18f) + z * z / (14f * 14f));
        float beach = Mathf.Lerp(-2.5f, .42f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.28f, .83f, q)));
        float shelf = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.6f, .4f, ShelfDistance(new Vector2(x, z))));
        // A broad sand cap, maximum 2.30m; rock tops vary up to 3.35m.
        return beach + 1.88f * shelf;
    }

    private static Terrain CreateTerrain(string folder)
    {
        var sandSource = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Game/Environment/TerrainLayers/Sand.terrainlayer");
        var stoneSource = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Game/Environment/TerrainLayers/Rock.terrainlayer");
        if (sandSource == null || sandSource.diffuseTexture == null || stoneSource == null)
            throw new InvalidOperationException("Existing sand/stone terrain materials were not found.");
        TerrainLayer sand = Object.Instantiate(sandSource);
        sand.name = "Draft Sand";
        TerrainLayer stone = Object.Instantiate(stoneSource);
        stone.name = "Draft Terrain Stone";
        SaveAsset(sand, folder + "/Sand.terrainlayer");
        SaveAsset(stone, folder + "/Stone.terrainlayer");
        var data = new TerrainData { name = "Stage 1 editable sand foundation", heightmapResolution = 513, alphamapResolution = 256 };
        data.size = new Vector3(64f, 12f, 64f);
        data.terrainLayers = new[] { sand, stone };
        var heights = new float[513, 513];
        for (int z = 0; z < 513; z++)
        for (int x = 0; x < 513; x++) heights[z, x] = (Height(x / 512f * 64f - 32f, z / 512f * 64f - 32f) + 4f) / 12f;
        data.SetHeights(0, 0, heights);
        var alpha = new float[256, 256, 2];
        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 256; x++)
        {
            float rocky = Mathf.InverseLerp(25f, 55f, data.GetSteepness(x / 255f, z / 255f)) * .65f;
            alpha[z, x, 0] = 1f - rocky;
            alpha[z, x, 1] = rocky;
        }
        data.SetAlphamaps(0, 0, alpha);
        SaveAsset(data, folder + "/Terrain.asset");
        GameObject ground = Terrain.CreateTerrainGameObject(data);
        ground.name = "Sculptable Sand - Stage 1";
        ground.transform.position = new Vector3(-32f, -4f, -32f);
        var terrain = ground.GetComponent<Terrain>();
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (shader == null) throw new InvalidOperationException("URP terrain shader is unavailable.");
        var terrainMaterial = new Material(shader) { name = "Draft Terrain Material" };
        SaveAsset(terrainMaterial, folder + "/TerrainMaterial.mat");
        terrain.materialTemplate = terrainMaterial;
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 1f;
        return terrain;
    }

    private static void CreateReviewWater(string folder)
    {
        // Flat art-review water datum; no wave crest can hide the base-rock placement.
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Review Water - sea level 0m (visual only)";
        water.transform.localScale = Vector3.one * 12f;
        Object.DestroyImmediate(water.GetComponent<Collider>());
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Review Ocean" };
        material.SetColor("_BaseColor", new Color(.055f, .32f, .38f, 1f));
        material.SetFloat("_Smoothness", .75f);
        SaveAsset(material, folder + "/ReviewOcean.mat");
        water.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void SaveAsset(Object asset, string path)
    {
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
    }

    private static void CreateView(string name, Vector3 position, Vector3 target)
    {
        var view = new GameObject("REVIEW " + name);
        view.transform.position = position;
        view.transform.rotation = Quaternion.LookRotation(target - position);
        // Saved transforms are bookmarks, with no extra rendering cameras in the scene.
    }

    private static void ValidateDraft(Scene scene, Terrain terrain)
    {
        if (terrain.GetComponent<TerrainCollider>().terrainData != terrain.terrainData)
            throw new InvalidOperationException("Terrain collision does not match the draft.");
        foreach (var root in scene.GetRootGameObjects())
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (root.name.StartsWith("Review Water")) continue;
            var collider = filter.GetComponent<MeshCollider>();
            if (collider == null || collider.sharedMesh != filter.sharedMesh || !collider.enabled)
                throw new InvalidOperationException("Missing rock collision: " + filter.name);
        }
    }

    private static void View(Vector3 eye, Vector3 target, float distance, bool orthographic = false)
    {
        if (SceneManager.GetActiveScene().path != DraftScene) return;
        SceneView sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        sceneView.sceneLighting = true;
        sceneView.LookAt(target, Quaternion.LookRotation(target - eye), distance, orthographic, true);
        sceneView.Repaint();
    }
    [MenuItem(Menu + "2 - Front View")]
    public static void Front() => View(new Vector3(-1f, 9f, 27f), new Vector3(-3f, 1f, -2f), 22f);
    [MenuItem(Menu + "3 - West View")]
    public static void West() => View(new Vector3(-28f, 7f, 4f), new Vector3(-4f, 1f, -3f), 19f);
    [MenuItem(Menu + "4 - Back View")]
    public static void Back() => View(new Vector3(8f, 11f, -27f), new Vector3(-3f, 1f, -3f), 22f);
    [MenuItem(Menu + "5 - Overhead View")]
    public static void Top() => View(new Vector3(0f, 35f, .01f), Vector3.zero, 23f, true);
}

/// <summary>Applies only to the original six draft sources; never rewrites UVs.</summary>
public sealed class SnapperDraftRockImporter : AssetPostprocessor
{
    private bool IsDraftRock => assetPath.StartsWith(SnapperFormationDraftEditor.Root + "/Source/Rock", StringComparison.Ordinal)
        && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    private void OnPreprocessModel()
    {
        if (!IsDraftRock) return;
        var importer = (ModelImporter)assetImporter;
        importer.isReadable = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.generateSecondaryUV = false;
        importer.swapUVChannels = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
    private Material OnAssignMaterialModel(Material source, Renderer renderer)
    {
        if (!IsDraftRock) return null;
        return AssetDatabase.LoadAssetAtPath<Material>(SnapperFormationDraftEditor.MaterialPath);
    }
}
