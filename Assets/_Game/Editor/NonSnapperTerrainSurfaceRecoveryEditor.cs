using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One-time, editor-only terrain texture restoration. Never sculpts heightmaps,
/// regenerates scene objects, changes an existing TerrainLayer asset, or paints
/// Snapper and its surrounding shelf. All newly created assets are persistent.
/// </summary>
public static class NonSnapperTerrainSurfaceRecoveryEditor
{
    private const string Root = "Assets/_Game/EditableFishingMap";
    private const string MainScene = "Assets/Scenes/PrototypeWorld.unity";
    private const string Menu = "Tools/Open World/Restore Grass and Rock Textures (Except Snapper)";
    private const float SnapperSafetyBuffer = 12f;

    [MenuItem(Menu)]
    public static void RestoreOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Exit Play Mode", "Saved terrain must be repaired outside Play Mode.", "OK");
            return;
        }
        if (!FishingMapSceneEditor.TryOpenFishingScene(out Scene scene)) return;
        if (scene.path != MainScene)
        {
            EditorUtility.DisplayDialog("Wrong scene",
                "Open Assets/Scenes/PrototypeWorld.unity before restoring the fishing map.", "OK");
            return;
        }
        var world = Find<IslandExpansionWorld>(scene);
        var reef = Find<ReefZone>(scene);
        var water = Find<OceanWater>(scene);
        if (world == null || reef == null || water == null || !world.HasSavedLayout ||
            world.Terrain == null || world.Terrain.terrainData == null)
        {
            EditorUtility.DisplayDialog("Editable fishing map required",
                "The map must be baked with Tools > Open World > Make Main Fishing Map Editable first. Your locally saved TerrainData and placements will not be regenerated.", "OK");
            return;
        }

        var previous = world.GetComponent<NonSnapperTerrainSurfaceState>();
        if (previous != null && previous.Restored)
        {
            EditorUtility.DisplayDialog("Surface restoration already saved",
                "The scene has already received its one-time terrain restoration. No paint or terrain assets were changed. You can continue painting manually without this command resetting it.", "OK");
            return;
        }

        var terrain = world.Terrain;
        var oldData = terrain.terrainData;
        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (oldData.terrainLayers == null || oldData.terrainLayers.Length < 2)
            throw new InvalidOperationException("Fishing map terrain is missing its source sand/grass layers.");

        // Decode the exact user-supplied images BEFORE modifying any scene data.
        byte[] grassBytes = DecodeTexture("Grass", 3);
        byte[] rockBytes = DecodeTexture("Stone", 3);

        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        string folderName = "SurfaceRecovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        AssetDatabase.CreateFolder(Root, folderName);
        string folder = Root + "/" + folderName;

        // Saving a scene alone is not a TerrainData backup: the old scene and
        // the new scene could both reference the same mutable asset. Back up
        // independently so the user's paint and Snapper landscape can recover.
        TerrainData before = Object.Instantiate(oldData);
        before.name = "Before non-Snapper surface recovery";
        AssetDatabase.CreateAsset(before, folder + "/TerrainBeforeRecovery.asset");
        try
        {
            terrain.terrainData = before;
            if (terrainCollider != null) terrainCollider.terrainData = before;
            if (!EditorSceneManager.SaveScene(scene, folder + "/SceneBeforeRecovery.unity", true))
                throw new IOException("Unable to create a scene backup; restoration was cancelled.");
        }
        finally
        {
            terrain.terrainData = oldData;
            if (terrainCollider != null) terrainCollider.terrainData = oldData;
        }

        NonSnapperTerrainSurfaceState newState = null;
        try
        {
            EditorUtility.DisplayProgressBar("Restore fishing terrain surfaces",
                "Saving grass and stone textures as project assets...", .15f);

            var grassTexture = SaveTexture(folder, "RecoveredGrass", grassBytes);
            var rockTexture = SaveTexture(folder, "RecoveredStone", rockBytes);
            var grassLayer = CreateLayer(folder + "/RecoveredSuncrestGrass.terrainlayer",
                "Recovered Suncrest and Bluewater grass", grassTexture, new Vector2(7f, 7f));
            var islandRock = CreateLayer(folder + "/RecoveredIslandStone.terrainlayer",
                "Recovered Brinebreak and Bluewater stone", rockTexture, new Vector2(9f, 9f));
            var cliffRock = CreateLayer(folder + "/RecoveredUnderwaterCliffStone.terrainlayer",
                "Recovered underwater cliff stone", rockTexture, new Vector2(9f, 9f));

            TerrainData restored = Object.Instantiate(oldData);
            restored.name = "Editable fishing terrain with permanent restored paint";
            AssetDatabase.CreateAsset(restored, folder + "/RestoredWorldTerrain.asset");

            var oldPaint = oldData.GetAlphamaps(0, 0,
                oldData.alphamapWidth, oldData.alphamapHeight);
            int oldCount = oldData.alphamapLayers;
            int width = oldData.alphamapWidth, height = oldData.alphamapHeight;
            float[,,] result = new float[height, width, oldCount + 3];

            TerrainLayer[] layers = oldData.terrainLayers
                .Concat(new[] { grassLayer, islandRock, cliffRock }).ToArray();

            Vector3 snapperCenter = SnapperIslandGeometry.Center(reef);
            float protectedDistance = Mathf.Max(SnapperIslandGeometry.FishingMargin,
                SnapperIslandGeometry.UniformSeabedOuterDistance) + SnapperSafetyBuffer;

            int changedTexels = 0, protectedTexels = 0;
            for (int z = 0; z < height; z++)
            {
                float v = height <= 1 ? 0f : z / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : x / (float)(width - 1);
                    Vector3 p = terrain.transform.position +
                        new Vector3(u * oldData.size.x, 0f, v * oldData.size.z);
                    for (int i = 0; i < oldCount; i++)
                        result[z, x, i] = oldPaint[z, x, i];

                    // Protect all of Snapper, the newly leveled northern seabed,
                    // surrounding fishing water and 12 extra metres of buffer.
                    // Do not even renormalize existing protected paint weights.
                    if (SnapperIslandGeometry.Ellipse(p, snapperCenter) <= 1f ||
                        SnapperIslandGeometry.DistanceFromShore(p, snapperCenter) <= protectedDistance)
                    {
                        protectedTexels++;
                        continue;
                    }

                    float absoluteHeight = terrain.transform.position.y +
                        oldData.GetInterpolatedHeight(u, v);
                    float elevation = absoluteHeight - water.BaseWaterLevel;
                    float slope = oldData.GetSteepness(u, v);
                    float grass = 0f, stone = 0f, cliff = 0f;

                    if (elevation > .10f)
                    {
                        float starter = IslandGeometry.Ellipse(p, reef.center,
                            new Vector2(reef.islandRadiusX, reef.islandRadiusZ));
                        float brine = world.Config == null ? float.PositiveInfinity :
                            IslandGeometry.Ellipse(p, world.NewCenter, world.Config.IslandRadii);
                        float bluewater = Vector2.Distance(
                            new Vector2(p.x, p.z),
                            new Vector2(world.PelagicCenter.x, world.PelagicCenter.z)) /
                            PelagicIslandGeometry.Radius;

                        if (starter < 1.05f)
                            grass = StarterGrass(p, reef, elevation, starter);
                        else if (brine < 1.04f)
                            stone = Mathf.SmoothStep(0f, 1f,
                                Mathf.InverseLerp(1.01f, .77f, brine)) *
                                Mathf.Clamp(.82f + slope / 220f, 0f, .97f);
                        else if (bluewater < 1.04f)
                        {
                            float interior = Mathf.SmoothStep(0f, 1f,
                                Mathf.InverseLerp(1.01f, .68f, bluewater));
                            float variation = Mathf.PerlinNoise(p.x * .061f + 27f, p.z * .057f + 91f);
                            stone = interior * (.18f + .48f * Mathf.SmoothStep(0f, 1f,
                                Mathf.InverseLerp(9f, 35f, slope)) + .19f * variation);
                            grass = interior * (1f - stone) * .65f;
                        }
                    }
                    else if (elevation < -1.5f)
                    {
                        // Stone on actual underwater escarpments; flatter areas
                        // keep their existing sand/reef texture.
                        cliff = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(12f, 37f, slope)) *
                            Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(1.5f, 5.5f, -elevation)) * .95f;
                    }

                    float total = Mathf.Clamp01(grass + stone + cliff);
                    if (total <= .005f) continue;
                    changedTexels++;
                    for (int i = 0; i < oldCount; i++)
                        result[z, x, i] *= 1f - total;
                    result[z, x, oldCount] = grass;
                    result[z, x, oldCount + 1] = stone;
                    result[z, x, oldCount + 2] = cliff;
                }
            }

            if (protectedTexels <= 0 || changedTexels <= 0)
                throw new InvalidOperationException("No safe non-Snapper terrain pixels were found; no scene changes were saved.");

            EditorUtility.DisplayProgressBar("Restore fishing terrain surfaces",
                "Persisting terrain layers and protected paint...", .70f);

            // The original TerrainData and its TerrainLayers are never changed.
            // Restore all old splat weights, then add only the 3 new layers.
            restored.terrainLayers = layers;
            restored.SetAlphamaps(0, 0, result);

            // Verify the protected region is identical in every original layer.
            // Small 8-bit splat-map quantization is inherent to Unity TerrainData.
            var verified = restored.GetAlphamaps(0, 0, width, height);
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                var p = terrain.transform.position +
                    new Vector3(x / (float)(width - 1) * oldData.size.x, 0f,
                        z / (float)(height - 1) * oldData.size.z);
                if (SnapperIslandGeometry.DistanceFromShore(p, snapperCenter) > protectedDistance)
                    continue;
                for (int layer = 0; layer < oldCount; layer++)
                {
                    if (Mathf.Abs(oldPaint[z, x, layer] - verified[z, x, layer]) > .012f)
                        throw new InvalidOperationException(
                            "Snapper paint verification failed; restoration aborted without saving scene.");
                }
                for (int layer = oldCount; layer < oldCount + 3; layer++)
                    if (verified[z, x, layer] > .012f)
                        throw new InvalidOperationException(
                            "Restoration changed a Snapper texel; scene save aborted.");
            }

            Undo.RecordObject(terrain, "Restore non-Snapper terrain textures");
            if (terrainCollider != null)
                Undo.RecordObject(terrainCollider, "Preserve terrain collider reference");
            terrain.terrainData = restored;
            if (terrainCollider != null) terrainCollider.terrainData = restored;

            newState = Undo.AddComponent<NonSnapperTerrainSurfaceState>(world.gameObject);
            newState.MarkRestored();
            EditorUtility.SetDirty(newState);
            EditorUtility.SetDirty(restored);
            EditorUtility.SetDirty(terrain);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save updated PrototypeWorld scene.");

            terrain.Flush();
            SceneView.RepaintAll();
            Debug.Log("[SURFACE RECOVERY] Restored grass and rock in " + changedTexels +
                " pixels; protected " + protectedTexels +
                " Snapper-area pixels. Permanent TerrainData, images and TerrainLayers: " + folder);
            EditorUtility.DisplayDialog("Grass and rock textures restored",
                "Saved permanent grass and stone layers in the main fishing scene.\n\n" +
                "Snapper Island and its surrounding seabed paint were left unchanged.\n" +
                "No rock, terrain height, or other scene object was moved.\n\n" +
                "Protected pixels: " + protectedTexels +
                "\nRepainted pixels: " + changedTexels + "\n\n" +
                "Save/commit PrototypeWorld.unity and the new SurfaceRecovery asset folder to GitHub.", "OK");
        }
        catch
        {
            terrain.terrainData = oldData;
            if (terrainCollider != null) terrainCollider.terrainData = oldData;
            if (newState != null) Object.DestroyImmediate(newState);
            Debug.LogError("[SURFACE RECOVERY] Recovery aborted. Original terrain references restored; " +
                "open " + folder + "/SceneBeforeRecovery.unity if needed.");
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static float StarterGrass(Vector3 p, ReefZone reef, float elevation, float q)
    {
        float interior = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.02f, .75f, q));
        float grass = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.9f, 3.2f, elevation));
        float dx = p.x - reef.center.x, dz = p.z - reef.center.z;
        float pathCenter = -reef.islandRadiusX * .12f *
            Mathf.Clamp01((dz + reef.islandRadiusZ * .76f) /
                (reef.islandRadiusZ * .84f));
        float path = dz > -reef.islandRadiusZ * .78f &&
            dz < reef.islandRadiusZ * .12f
            ? 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(1.2f, 3f, Mathf.Abs(dx - pathCenter))) : 0f;
        float pondRadius = Mathf.Clamp(reef.islandRadiusX * .18f, 8f, 18f);
        float px = (dx + reef.islandRadiusX * .12f) / pondRadius;
        float pz = (dz - reef.islandRadiusZ * .08f) / (pondRadius * .72f);
        float pondBank = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(1.05f, 1.65f, Mathf.Sqrt(px * px + pz * pz)));
        return interior * grass * (1f - Mathf.Max(path, pondBank)) * .97f;
    }

    private static Texture2D SaveTexture(string folder, string name, byte[] imageBytes)
    {
        // The original user-supplied textures are JPEG data in Base64 chunks.
        // Save under the correct extension or Unity may refuse to import them.
        bool jpeg = imageBytes.Length > 3 && imageBytes[0] == 0xff &&
            imageBytes[1] == 0xd8 && imageBytes[2] == 0xff;
        bool png = imageBytes.Length > 8 && imageBytes[0] == 0x89 &&
            imageBytes[1] == 0x50 && imageBytes[2] == 0x4e && imageBytes[3] == 0x47;
        if (!jpeg && !png)
            throw new IOException("The original texture is neither JPEG nor PNG.");
        string path = folder + "/" + name + (jpeg ? ".jpg" : ".png");
        File.WriteAllBytes(path, imageBytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            throw new IOException("Unity could not load restored surface texture: " + path);
        return texture;
    }

    private static TerrainLayer CreateLayer(string path, string name,
        Texture2D texture, Vector2 tileSize)
    {
        var layer = new TerrainLayer
        {
            name = name,
            diffuseTexture = texture,
            tileSize = tileSize
        };
        AssetDatabase.CreateAsset(layer, path);
        return layer;
    }

    private static byte[] DecodeTexture(string name, int count)
    {
        var encoded = new StringBuilder(count * 7000);
        for (int i = 0; i < count; i++)
        {
            string key = "Environment/SurfaceTextureData/" + name + "_" + i.ToString("00");
            var chunk = Resources.Load<TextAsset>(key);
            if (chunk == null || string.IsNullOrEmpty(chunk.text))
                throw new IOException("Missing original texture chunk: " + key);
            encoded.Append(chunk.text.Trim());
        }
        try { return Convert.FromBase64String(encoded.ToString()); }
        catch (FormatException error)
        {
            throw new IOException("Original supplied " + name + " texture could not be decoded.", error);
        }
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var value = root.GetComponentInChildren<T>(true);
            if (value != null) return value;
        }
        return null;
    }
}
