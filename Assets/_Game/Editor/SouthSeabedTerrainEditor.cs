#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bakes the scene marker once into a real editable TerrainData asset.</summary>
[InitializeOnLoad]
public sealed class SouthSeabedTerrainEditor : IProcessSceneWithReport
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    private const string DataPath = "Assets/_Game/EditableFishingMap/SouthOceanSeabed.asset";
    private const float Clearance = 5f;
    private const float BlendWidth = 96f;
    public int callbackOrder => -1000;

    static SouthSeabedTerrainEditor()
    {
        EditorApplication.delayCall += BakeLoadedScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += BakeLoadedScenes;
    }

    [MenuItem("Tools/Open World/Terrain/Bake South Ocean Seabed")]
    private static void BakeLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BakeLoadedScenes;
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path != ScenePath || !scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (SouthSeabedTerrain marker in root.GetComponentsInChildren<SouthSeabedTerrain>(true))
                    try
                    {
                        if (Bake(marker, true))
                        {
                            EditorSceneManager.MarkSceneDirty(scene);
                            Debug.Log("[SOUTH SEABED] Added editable South Ocean Seabed, entirely below water. Ctrl+S saves the scene. Existing terrain was not sculpted or repainted.", marker);
                            SceneView.RepaintAll();
                        }
                    }
                    catch (Exception error) { Debug.LogException(error, marker); }
        }
    }

    // Also handles a batch build that never opened the scene in the Editor.
    // Unsaved generated data is embedded in the build scene; no runtime generator.
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (scene.path != ScenePath || report == null) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (SouthSeabedTerrain marker in root.GetComponentsInChildren<SouthSeabedTerrain>(true))
                Bake(marker, false);
    }

    private static bool Bake(SouthSeabedTerrain marker, bool persist)
    {
        // After the first bake, every sculpt/paint/transform edit belongs to the user.
        if (marker.terrain != null && marker.terrain.terrainData != null) return false;
        Terrain source = marker.sourceTerrain;
        if (source == null || source.terrainData == null || marker.water == null)
            throw new InvalidOperationException("South seabed is missing its source terrain or ocean reference.");
        if (source.transform.rotation != Quaternion.identity || source.transform.lossyScale != Vector3.one)
            throw new InvalidOperationException("South seabed expects the existing unrotated, unscaled terrain.");
        TerrainData original = source.terrainData;
        Vector3 size = original.size;
        Vector3 origin = source.transform.position - new Vector3(0, 0, size.z);
        float sea = marker.water.BaseWaterLevel;
        if (origin.y >= sea - Clearance)
            throw new InvalidOperationException("Terrain base is too high for a completely submerged seabed.");

        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
        if (data == null)
        {
            data = Generate(source, origin, sea);
            Validate(data, source, origin, sea);
            if (persist)
            {
                AssetDatabase.CreateAsset(data, DataPath);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
        }
        // Marker is a separate, newly added scene root, outside protected scenery.
        marker.transform.position = origin;
        Terrain tile = marker.GetComponent<Terrain>();
        if (tile == null) tile = marker.gameObject.AddComponent<Terrain>();
        TerrainCollider collider = marker.GetComponent<TerrainCollider>();
        if (collider == null) collider = marker.gameObject.AddComponent<TerrainCollider>();
        tile.terrainData = data;
        collider.terrainData = data;
        marker.gameObject.layer = source.gameObject.layer;
        tile.materialTemplate = source.materialTemplate;
        tile.heightmapPixelError = Mathf.Max(8f, source.heightmapPixelError);
        tile.basemapDistance = source.basemapDistance;
        tile.drawInstanced = true;
        tile.drawTreesAndFoliage = false;
        tile.treeDistance = 0f;
        tile.detailObjectDistance = 0f;
        tile.shadowCastingMode = source.shadowCastingMode;
        tile.groupingID = source.groupingID;
        tile.allowAutoConnect = true;
        marker.terrain = tile;
        marker.ConnectNeighbors();
        tile.Flush();
        if (persist) EditorUtility.SetDirty(marker);
        return true;
    }

    private static TerrainData Generate(Terrain source, Vector3 origin, float sea)
    {
        TerrainData original = source.terrainData;
        int n = original.heightmapResolution;
        var data = new TerrainData
        {
            name = "South Ocean Seabed",
            heightmapResolution = n,
            alphamapResolution = 256,
            baseMapResolution = 512,
            size = original.size,
            terrainLayers = original.terrainLayers
        };
        // Same authored height resolution/vertical range: exact shared-edge samples.
        float[,] heights = new float[n, n];
        float[,] southEdge = original.GetHeights(0, 0, n, 1);
        var world = source.GetComponentInParent<IslandExpansionWorld>();
        if (world == null) world = UnityEngine.Object.FindFirstObjectByType<IslandExpansionWorld>();
        float depth = world != null && world.Config != null ? world.Config.OceanDepth : 65f;
        depth = Mathf.Max(20f, depth);
        for (int z = 0; z < n; z++)
        {
            float v = z / (float)(n - 1);
            float fromJoin = (1f - v) * data.size.z;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fromJoin / BlendWidth));
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)(n - 1);
                float wx = origin.x + u * data.size.x;
                float wz = origin.z + v * data.size.z;
                // No island generators: only the existing world-space seabed relief.
                float h = SeabedRelief.Height(wx, wz, sea - depth, sea);
                float join = source.transform.position.y + southEdge[0, x] * original.size.y;
                h = Mathf.Min(sea - Clearance, Mathf.Lerp(join, h, blend));
                heights[z, x] = Mathf.Clamp01((h - origin.y) / data.size.y);
            }
        }
        data.SetHeights(0, 0, heights);
        Paint(data, sea - origin.y);
        return data;
    }

    private static void Validate(TerrainData data, Terrain source, Vector3 origin, float sea)
    {
        int n = data.heightmapResolution;
        float[,] h = data.GetHeights(0, 0, n, n);
        float tolerance = data.size.y / 32767f + .001f;
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
        {
            float height = origin.y + h[z, x] * data.size.y;
            if (float.IsNaN(height) || height > sea - Clearance + tolerance)
                throw new InvalidOperationException("South seabed height exceeds its underwater ceiling.");
        }
        for (int x = 0; x < n; x++)
        {
            float expected = Mathf.Min(sea - Clearance, source.transform.position.y +
                source.terrainData.GetInterpolatedHeight(x / (float)(n - 1), 0f));
            float actual = origin.y + h[n - 1, x] * data.size.y;
            if (Mathf.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException("South seabed does not match the existing southern edge.");
        }
    }

    private static void Paint(TerrainData data, float localSea)
    {
        TerrainLayer[] layers = data.terrainLayers;
        if (layers.Length == 0) return;
        int sand = 0, rock = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            string name = layers[i].name.ToLowerInvariant();
            if (name.Contains("sand")) sand = i;
            if (name.Contains("stone") || name.Contains("rock")) rock = i;
        }
        // Prefer the existing underwater-cliff material over dry island stone.
        for (int i = 0; i < layers.Length; i++)
            if (layers[i] != null && (layers[i].name.ToLowerInvariant().Contains("underwater") ||
                layers[i].name.ToLowerInvariant().Contains("submerged"))) rock = i;
        int r = data.alphamapResolution;
        float[,,] weights = new float[r, r, layers.Length];
        for (int z = 0; z < r; z++) for (int x = 0; x < r; x++)
        {
            float u = x / (float)(r - 1), v = z / (float)(r - 1);
            float depth = localSea - data.GetInterpolatedHeight(u, v);
            float stone = SeabedRelief.RockWeight(depth, data.GetSteepness(u, v));
            weights[z, x, sand] += 1f - stone;
            weights[z, x, rock] += stone;
        }
        data.SetAlphamaps(0, 0, weights);
    }
}
#endif
