using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One-time, starter-island ONLY terrain paint repair. Uses the already-uploaded
/// original Suncrest grass texture, creates independent backup and TerrainData
/// assets, saves PrototypeWorld, then offers a narrow user-authorized Git push.
/// Other islands, heightmaps, scenery, rock models and underwater terrain stay
/// untouched. The saved grass lock prevents accidental future re-runs.
/// </summary>
[InitializeOnLoad]
public static class StarterIslandGrassRepairEditor
{
    private const string MainScene = "Assets/Scenes/PrototypeWorld.unity";
    private const string ParentFolder = "Assets/_Game/EditableFishingMap";
    private const string GrassLayerPath =
        "Assets/_Game/EditableFishingMap/SurfaceRecovery-20261007-232425362/RecoveredSuncrestGrass.terrainlayer";
    private const string Menu = "Tools/Open World/Restore and Lock STARTER Island Grass";
    private static bool offered, running;

    static StarterIslandGrassRepairEditor()
    {
        EditorApplication.delayCall += Offer;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Offer;
    }

    private static void Offer()
    {
        if (offered || running || EditorApplication.isPlayingOrWillChangePlaymode ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Offer;
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!TryMap(scene, out var world, out var reef)) return;
        if (world.GetComponent<StarterIslandGrassLock>()?.IsSaved == true) return;

        offered = true;
        if (EditorUtility.DisplayDialog("Restore and permanently save Suncrest grass",
            "Restore the original grass texture on the STARTER ISLAND only? The sandy beach, pond banks and footpath will be preserved.\n\n" +
            "Unity will save a backup and a new permanent TerrainData asset, and protect the finished grass from future automatic world repainting. Other islands and hand-placed scenery are untouched.",
            "Back up and restore", "Later"))
            Apply();
    }

    [MenuItem(Menu)]
    public static void Apply()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorUtility.DisplayDialog("Exit Play Mode",
                "Restore the saved grass in Scene edit mode after Unity finishes importing.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!TryMap(scene, out var world, out var reef))
        {
            EditorUtility.DisplayDialog("Open the saved main fishing scene",
                "Open Assets/Scenes/PrototypeWorld.unity with your saved editable island terrain. The scene will not be regenerated.", "OK");
            return;
        }
        if (world.GetComponent<StarterIslandGrassLock>()?.IsSaved == true)
        {
            EditorUtility.DisplayDialog("Starter grass is protected",
                "The requested starter grass restoration has already been saved. Nothing was repainted or moved. Later map loads use the same saved TerrainData until you explicitly request a new change.", "OK");
            return;
        }

        Terrain terrain = world.Terrain;
        if (terrain == null || terrain.terrainData == null)
            throw new InvalidOperationException("The main fishing map has no saved TerrainData.");

        TerrainLayer originalGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath);
        if (originalGrass == null || originalGrass.diffuseTexture == null)
        {
            EditorUtility.DisplayDialog("Original grass texture is missing",
                "Could not find the saved Suncrest grass TerrainLayer at:\n" + GrassLayerPath +
                "\n\nPull the latest GitHub main branch including the SurfaceRecovery assets. No terrain was changed.", "OK");
            return;
        }

        // The existing map may have been edited in Scene view. Preserve all
        // unsaved work before any terrain edits or backup operations.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var source = terrain.terrainData;
        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        running = true;

        Directory.CreateDirectory(ParentFolder);
        AssetDatabase.Refresh();
        string folderName = "LockedSuncrestGrass-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        AssetDatabase.CreateFolder(ParentFolder, folderName);
        string folder = ParentFolder + "/" + folderName;
        TerrainData output = null;
        StarterIslandGrassLock newLock = null;

        try
        {
            EditorUtility.DisplayProgressBar("Restore Suncrest grass",
                "Creating an independent Scene and TerrainData backup...", 0.10f);

            TerrainData frozen = Object.Instantiate(source);
            frozen.name = "Suncrest grass before restoration backup";
            AssetDatabase.CreateAsset(frozen, folder + "/TerrainBeforeGrass.asset");
            try
            {
                terrain.terrainData = frozen;
                if (terrainCollider != null) terrainCollider.terrainData = frozen;
                if (!EditorSceneManager.SaveScene(scene, folder + "/SceneBeforeGrass.unity", true))
                    throw new IOException("The backup scene could not be saved. No grass was repainted.");
            }
            finally
            {
                terrain.terrainData = source;
                if (terrainCollider != null) terrainCollider.terrainData = source;
            }

            EditorUtility.DisplayProgressBar("Restore Suncrest grass",
                "Restoring the original green terrain on the starter island only...", 0.40f);

            // Never mutate the original TerrainData. All surfaces and heightmaps
            // outside Suncrest retain their original serialized asset data.
            output = Object.Instantiate(source);
            output.name = "Protected Suncrest grass and saved island terrain";
            AssetDatabase.CreateAsset(output, folder + "/ProtectedSuncrestTerrain.asset");

            int modified = PaintOnlyStarter(terrain, source, output, reef,
                world.Water != null ? world.Water.BaseWaterLevel :
                FindWaterLevel(scene));

            if (modified == 0)
                throw new InvalidOperationException("No starter-island grass pixels could be restored. Original scene remains unchanged.");

            Undo.RecordObject(terrain, "Use protected Suncrest grass TerrainData");
            if (terrainCollider != null)
                Undo.RecordObject(terrainCollider, "Keep terrain collider on saved data");

            terrain.terrainData = output;
            if (terrainCollider != null) terrainCollider.terrainData = output;

            newLock = Undo.AddComponent<StarterIslandGrassLock>(world.gameObject);
            newLock.MarkSaved();
            EditorUtility.SetDirty(newLock);
            EditorUtility.SetDirty(output);
            EditorUtility.SetDirty(terrain);

            EditorUtility.DisplayProgressBar("Restore Suncrest grass",
                "Writing protected terrain and scene assets to disk...", 0.85f);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("PrototypeWorld could not be saved; original TerrainData remains available.");
            terrain.Flush();
            SceneView.RepaintAll();

            Debug.Log("[SUNCREST GRASS] Restored and saved " + modified +
                " starter-only grass texels, with no terrain height/placement changes. Permanent asset folder: " + folder);

            bool upload = EditorUtility.DisplayDialog("Starter grass restored and protected",
                "Restored grass on the STARTER ISLAND only (" + modified + " paint texels).\n\n" +
                "Saved a new TerrainData asset and a separate backup. Beach, pond, underwater areas, all other islands and manually placed objects were left alone.\n\n" +
                "Commit and push this saved Scene and the new terrain assets to upstream/main now?",
                "Commit and push", "I'll push later");
            if (upload) PushSceneAndAssets(folder);
        }
        catch (Exception error)
        {
            terrain.terrainData = source;
            if (terrainCollider != null) terrainCollider.terrainData = source;
            if (newLock != null) Object.DestroyImmediate(newLock);
            Debug.LogError("[SUNCREST GRASS] Could not finish restoration. The source terrain has been reattached. Recover the original scene from " +
                folder + "/SceneBeforeGrass.unity if necessary. " + error);
            throw;
        }
        finally
        {
            running = false;
            EditorUtility.ClearProgressBar();
        }
    }

    private static int PaintOnlyStarter(Terrain terrain, TerrainData before,
        TerrainData after, ReefZone reef, float sea)
    {
        TerrainLayer grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath);
        var layers = before.terrainLayers;
        int grassIndex = Array.IndexOf(layers, grassLayer);
        if (grassIndex < 0)
        {
            // An older save might not contain the recovered grass. Reuse the
            // already-persistent original layer without replacing any textures.
            after.terrainLayers = layers.Concat(new[] { grassLayer }).ToArray();
            grassIndex = layers.Length;
        }

        int width = before.alphamapWidth, height = before.alphamapHeight;
        Vector3 origin = terrain.transform.position, size = before.size;
        int left = Mathf.Clamp(Mathf.FloorToInt(
            (reef.center.x - reef.islandRadiusX * 1.02f - origin.x) / size.x * (width - 1)), 0, width - 1);
        int right = Mathf.Clamp(Mathf.CeilToInt(
            (reef.center.x + reef.islandRadiusX * 1.02f - origin.x) / size.x * (width - 1)), 0, width - 1);
        int bottom = Mathf.Clamp(Mathf.FloorToInt(
            (reef.center.z - reef.islandRadiusZ * 1.02f - origin.z) / size.z * (height - 1)), 0, height - 1);
        int top = Mathf.Clamp(Mathf.CeilToInt(
            (reef.center.z + reef.islandRadiusZ * 1.02f - origin.z) / size.z * (height - 1)), 0, height - 1);

        int rectWidth = right - left + 1, rectHeight = top - bottom + 1;
        float[,,] previous = before.GetAlphamaps(left, bottom, rectWidth, rectHeight);
        int layerCount = after.alphamapLayers;
        var painted = new float[rectHeight, rectWidth, layerCount];
        int previousLayerCount = before.alphamapLayers;
        int changed = 0;

        for (int z = 0; z < rectHeight; z++)
        for (int x = 0; x < rectWidth; x++)
        {
            float u = (left + x) / (float)(width - 1);
            float v = (bottom + z) / (float)(height - 1);
            Vector3 p = origin + new Vector3(u * size.x, 0f, v * size.z);

            for (int l = 0; l < previousLayerCount; l++)
                painted[z, x, l] = previous[z, x, l];

            float q = IslandGeometry.Ellipse(p, reef.center,
                new Vector2(reef.islandRadiusX, reef.islandRadiusZ));
            if (q > 1f) continue;

            float elevation = origin.y + before.GetInterpolatedHeight(u, v) - sea;
            if (elevation < .18f) continue; // sandy coast, pond water and underwater.

            float interior = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.99f, .67f, q));
            float aboveBeach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, 1.35f, elevation));
            float dx = p.x - reef.center.x, dz = p.z - reef.center.z;
            float pathCenter = -reef.islandRadiusX * .12f *
                Mathf.Clamp01((dz + reef.islandRadiusZ * .76f) /
                    (reef.islandRadiusZ * .84f));
            float path = dz > -reef.islandRadiusZ * .78f &&
                         dz < reef.islandRadiusZ * .12f ?
                1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(1.2f, 3f, Mathf.Abs(dx - pathCenter))) : 0f;

            float pondRadius = Mathf.Clamp(reef.islandRadiusX * .18f, 8f, 18f);
            float pondX = (dx + reef.islandRadiusX * .12f) / pondRadius;
            float pondZ = (dz - reef.islandRadiusZ * .08f) / (pondRadius * .72f);
            float pondBank = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(1.05f, 1.65f, Mathf.Sqrt(pondX * pondX + pondZ * pondZ)));

            float target = Mathf.Clamp01(interior * aboveBeach *
                (1f - Mathf.Max(path, pondBank)) * .98f);
            if (target < .008f) continue;

            for (int l = 0; l < layerCount; l++)
                painted[z, x, l] *= 1f - target;
            painted[z, x, grassIndex] += target;
            changed++;
        }

        if (changed > 0)
            after.SetAlphamaps(left, bottom, painted);
        return changed;
    }

    private static float FindWaterLevel(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var water = root.GetComponentInChildren<OceanWater>(true);
            if (water != null) return water.BaseWaterLevel;
        }
        throw new InvalidOperationException("No OceanWater level was available in the saved fishing scene.");
    }

    private static bool TryMap(Scene scene, out IslandExpansionWorld world,
        out ReefZone reef)
    {
        world = null;
        reef = null;
        if (!scene.IsValid() || !scene.isLoaded || scene.path != MainScene ||
            PrefabStageUtility.GetCurrentPrefabStage() != null) return false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (world == null) world = root.GetComponentInChildren<IslandExpansionWorld>(true);
            if (reef == null) reef = root.GetComponentInChildren<ReefZone>(true);
        }
        return world != null && world.HasSavedLayout && reef != null && world.Terrain != null;
    }

    private static void PushSceneAndAssets(string folder)
    {
        string workingDir = Path.GetDirectoryName(Application.dataPath);
        try
        {
            if (Git(workingDir, "branch --show-current").Trim() != "main")
                throw new InvalidOperationException("Unity's Git checkout is not on the main branch.");
            string upstream = Git(workingDir, "remote get-url upstream").Trim();
            if (upstream.IndexOf("EthanL1245/3D-Android-Open-World",
                StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("The upstream Git remote is not the main fishing-game repository.");

            if (!string.IsNullOrWhiteSpace(Git(workingDir, "diff --cached --name-only")))
                throw new InvalidOperationException("Other files are already staged. Automatic Git upload was stopped to prevent committing unrelated work.");

            Git(workingDir, "add -- " + MainScene + " " + folder + " " + folder + ".meta");
            string staged = Git(workingDir, "diff --cached --name-only");
            string[] paths = staged.Split(new[] {'\n','\r'}, StringSplitOptions.RemoveEmptyEntries);
            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog("Nothing new to commit",
                    "The Scene and grass assets are already committed locally. Check upstream/main to confirm the push.", "OK");
                return;
            }

            foreach (string path in paths)
                if (path != MainScene && path != folder + ".meta" &&
                    !path.StartsWith(folder + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected staged path: " + path);

            Git(workingDir, "commit -m \"Save and protect restored starter island grass\"");
            Git(workingDir, "push upstream main", 180000);
            EditorUtility.DisplayDialog("Starter grass saved to GitHub",
                "PrototypeWorld.unity and the protected Suncrest TerrainData/backup folder have been committed and pushed to upstream/main. Future map updates must preserve this saved grass until you request a change.", "OK");
        }
        catch (Exception error)
        {
            Debug.LogWarning("[SUNCREST GRASS] Saved locally, but GitHub push did not finish: " + error);
            EditorUtility.DisplayDialog("Scene saved; GitHub upload incomplete",
                "The grass IS saved in your local Unity scene, but the Git push did not complete.\n\n" +
                error.Message + "\n\nCheck git status and complete the push from the project directory. The grass restoration does not need to be repeated.", "OK");
        }
    }

    private static string Git(string directory, string arguments, int timeoutMs = 120000)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var process = System.Diagnostics.Process.Start(info))
        {
            if (process == null) throw new IOException("Git for Windows failed to start.");
            if (!process.WaitForExit(timeoutMs))
            {
                process.Kill();
                throw new TimeoutException("Git timed out: " + arguments);
            }
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0)
                throw new IOException("git " + arguments + " failed: " + error + " " + output);
            return output;
        }
    }
}
