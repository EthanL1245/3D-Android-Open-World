using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit user-authorized editor workflow for Blender rocks already placed by
/// the player. Never generates scenery, repositions any object, or changes UVs.
/// </summary>
public static class ManualSnapperRockUploadEditor
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    private const string InventoryPath = "Assets/_Game/EditableFishingMap/ManualSnapperRocks.json";
    private const string ExistingManifest = "Assets/_Game/EditableFishingMap/UserPlacedSceneryManifest.json";

    [Serializable] private sealed class RockInfo
    {
        public string name;
        public string hierarchy;
        public string sceneObjectId;
        public string meshSource;
        public Vector3 worldPosition;
        public Vector3 worldRotation;
        public Vector3 worldScale;
        public bool humanOwned;
        public bool hasExactCollider;
    }
    [Serializable] private sealed class Inventory
    {
        public string scene = ScenePath;
        public string ownershipRule = "HUMAN-OWNED. Future AI code must preserve these Blender source files and all existing manually placed rocks: do not move, remove, replace, resize, reparent, or change materials or UVs. Collision may only be changed at user's request.";
        public string[] blenderFiles;
        public RockInfo[] rocks;
        public int rockMeshCount;
        public int rockMeshesWithCollision;
    }

    [MenuItem("Tools/Open World/Manual Scenery/Inspect Blender Rock Files")]
    public static void Inspect()
    {
        string[] files = LocateBlendFiles();
        EditorUtility.DisplayDialog("Manual Blender assets",
            files.Length == 0 ?
            "No Blender files were found in a 'Manually Added Assets' directory within this Unity project." :
            "Found " + files.Length + " Blender source files:\n\n" +
                string.Join("\n", files.Take(20).ToArray()) +
                "\n\nUse Save Snapper Rocks and Push to GitHub to record the files and collisions.",
            "OK");
    }

    [MenuItem("Tools/Open World/Manual Scenery/Save Snapper Rocks and Push to GitHub")]
    public static void SaveAndPush()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorUtility.DisplayDialog("Exit Play Mode", "Wait until Unity finishes importing, and perform this operation in Scene edit mode.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog("Open the correct Scene",
                "Open Assets/Scenes/PrototypeWorld.unity, not a prefab or shop scene, then retry.", "OK");
            return;
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string[] blendFiles = LocateBlendFiles();
        if (blendFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("Blender sources not found",
                "Your two new .blend files are not visible in any 'Manually Added Assets' folder inside THIS Unity project.\n\n" +
                "Prefer Assets/Manually Added Assets so Unity can import and instantiate them. Copy them into that folder and retry.", "OK");
            return;
        }

        var reef = scene.GetRootGameObjects().SelectMany(r =>
            r.GetComponentsInChildren<ReefZone>(true)).FirstOrDefault();
        if (reef == null)
        {
            EditorUtility.DisplayDialog("Missing map", "Cannot find the ReefZone in the opened PrototypeWorld scene.", "OK");
            return;
        }
        Vector3 center = SnapperIslandGeometry.Center(reef);

        // Existing .blend imports may have Read/Write disabled. Reimport source
        // assets first, then reacquire the live meshes rather than holding old refs.
        foreach (string asset in blendFiles.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)))
        {
            var importer = AssetImporter.GetAtPath(asset) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        MeshFilter[] candidates = FindPlacedRocks(scene, center, blendFiles);
        int already = candidates.Count(ExactCollider);
        int added = 0, skipped = 0;
        foreach (MeshFilter mesh in candidates)
        {
            if (ExactCollider(mesh)) continue;
            var body = mesh.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                skipped++;
                Debug.LogWarning("[SNAPPER ROCKS] Dynamic rigidbody rock skipped: " + mesh.name);
                continue;
            }
            MeshCollider collider = mesh.GetComponent<MeshCollider>();
            if (collider == null)
                collider = Undo.AddComponent<MeshCollider>(mesh.gameObject);
            else Undo.RecordObject(collider, "Repair imported Snapper rock collision");
            collider.sharedMesh = null;
            collider.isTrigger = false;
            collider.convex = false;
            collider.enabled = true;
            collider.sharedMesh = mesh.sharedMesh;
            EditorUtility.SetDirty(collider);
            added++;
        }
        Physics.SyncTransforms();

        if (!EditorSceneManager.SaveScene(scene))
        {
            EditorUtility.DisplayDialog("Could not save scene", "PrototypeWorld could not be saved. Git upload was not attempted.", "OK");
            return;
        }

        UserPlacedSceneryEditor.ExportManifest(scene);
        WriteInventory(blendFiles, candidates);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        int ready = candidates.Count(ExactCollider);
        int outside = candidates.Count(m => !UserPlacedScenery.Contains(m.transform));

        string result = blendFiles.Length + " .blend files in the manual-assets folder.\n" +
            candidates.Length + " placed rock meshes near Snapper Island.\n" +
            already + " already had correct colliders.\n" +
            added + " colliders added or repaired.\n" +
            ready + " colliders verified.\n" +
            skipped + " dynamic meshes skipped.\n" +
            outside + " rocks outside the protected USER PLACED SCENERY parent.\n\n" +
            "The scene and inventory are saved locally. No transforms, models, UVs, or materials were changed.";
        if (candidates.Length == 0)
            result += "\n\nNo imported, placed rock meshes matched. If your rocks are not visible outside Play Mode, place them in PrototypeWorld before expecting collision. You can still upload the source files.";
        if (outside > 0)
            result += "\n\nTo protect all placed rocks from future generated-scene changes, move their top-level parents under USER PLACED SCENERY in the Hierarchy and save.";

        bool yes = EditorUtility.DisplayDialog("Save Snapper rock sources and collisions",
            result + "\n\nCommit and push the Blender source files, saved scene, and placement inventory to upstream/main?",
            "Commit and push", "Save locally only");
        if (yes) PushRelevantFiles(blendFiles);
    }

    private static string[] LocateBlendFiles()
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string asset in AssetDatabase.GetAllAssetPaths())
            if (asset.EndsWith(".blend", StringComparison.OrdinalIgnoreCase) &&
                HasManualAssetsFolder(asset))
                paths.Add(asset.Replace('\\', '/'));

        // A root-level manual assets folder can be committed to Git, but Unity
        // cannot instantiate .blend directly unless the source is under Assets.
        foreach (string folder in Directory.GetDirectories(project))
            if (HasManualAssetsFolder(Path.GetFileName(folder)))
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    if (file.EndsWith(".blend", StringComparison.OrdinalIgnoreCase))
                        paths.Add(file.Substring(project.Length)
                            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Replace('\\', '/'));

        return paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool HasManualAssetsFolder(string path)
    {
        foreach (string part in path.Replace('\\', '/').Split('/'))
        {
            string token = new string(part.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (token.Contains("manual") && token.Contains("asset")) return true;
        }
        return false;
    }

    private static MeshFilter[] FindPlacedRocks(Scene scene, Vector3 snapper, string[] blendSources)
    {
        string[] imported = blendSources.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)).ToArray();
        float radius = SnapperIslandGeometry.RadiusX + 105f;
        float radiusSq = radius * radius;
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true))
            .Where(m => m != null && m.sharedMesh != null && m.gameObject.activeInHierarchy)
            .Where(m =>
            {
                MeshRenderer renderer = m.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) return false;
                Bounds b = renderer.bounds;
                float dx = Mathf.Clamp(snapper.x, b.min.x, b.max.x) - snapper.x;
                float dz = Mathf.Clamp(snapper.z, b.min.z, b.max.z) - snapper.z;
                if (dx * dx + dz * dz > radiusSq) return false;

                string asset = AssetDatabase.GetAssetPath(m.sharedMesh) ?? "";
                string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m.gameObject) ?? "";
                bool fromManualSource = imported.Any(p =>
                    string.Equals(p, asset, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p, prefab, StringComparison.OrdinalIgnoreCase));
                return fromManualSource || UserPlacedScenery.Contains(m.transform);
            }).Distinct().ToArray();
    }

    private static bool ExactCollider(MeshFilter mesh)
    {
        MeshCollider c = mesh.GetComponent<MeshCollider>();
        return c != null && c.enabled && !c.isTrigger && !c.convex &&
            c.sharedMesh == mesh.sharedMesh;
    }

    private static void WriteInventory(string[] blends, MeshFilter[] rocks)
    {
        var inventory = new Inventory
        {
            blenderFiles = blends,
            rockMeshCount = rocks.Length,
            rockMeshesWithCollision = rocks.Count(ExactCollider),
            rocks = rocks.Select(m => new RockInfo
            {
                name = m.gameObject.name,
                hierarchy = GetHierarchy(m.transform),
                sceneObjectId = GlobalObjectId.GetGlobalObjectIdSlow(m.gameObject).ToString(),
                meshSource = AssetDatabase.GetAssetPath(m.sharedMesh) ?? "",
                worldPosition = m.transform.position,
                worldRotation = m.transform.eulerAngles,
                worldScale = m.transform.lossyScale,
                humanOwned = UserPlacedScenery.Contains(m.transform),
                hasExactCollider = ExactCollider(m)
            }).OrderBy(r => r.hierarchy, StringComparer.Ordinal).ToArray()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(InventoryPath));
        string contents = JsonUtility.ToJson(inventory, true) + "\n";
        if (!File.Exists(InventoryPath) || File.ReadAllText(InventoryPath) != contents)
        {
            File.WriteAllText(InventoryPath, contents);
            AssetDatabase.ImportAsset(InventoryPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static string GetHierarchy(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    private static void PushRelevantFiles(string[] blends)
    {
        string dir = Path.GetDirectoryName(Application.dataPath);
        try
        {
            if (Git(dir, "branch --show-current").Trim() != "main")
                throw new InvalidOperationException("Switch to your Git main branch first.");
            string remote = Git(dir, "remote get-url upstream").Trim();
            if (remote.IndexOf("EthanL1245/3D-Android-Open-World", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("The 'upstream' Git remote is not the game repository.");

            if (!string.IsNullOrWhiteSpace(Git(dir, "diff --cached --name-only")))
                throw new InvalidOperationException("Other changes are already staged. This safety check avoids accidentally committing unrelated work.");

            var files = new List<string> { ScenePath, InventoryPath };
            if (File.Exists(Path.Combine(dir, ExistingManifest)))
                files.Add(ExistingManifest);
            string inventoryMeta = InventoryPath + ".meta";
            if (File.Exists(Path.Combine(dir, inventoryMeta)))
                files.Add(inventoryMeta);
            foreach (string path in blends)
            {
                files.Add(path);
                if (File.Exists(Path.Combine(dir, path.Replace('/', Path.DirectorySeparatorChar) + ".meta")))
                    files.Add(path + ".meta");
            }
            files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Git(dir, "add -- " + string.Join(" ", files.Select(Quote).ToArray()));

            string staged = Git(dir, "diff --cached --name-only");
            string[] paths = staged.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var allowed = new HashSet<string>(files, StringComparer.Ordinal);
            foreach (string path in paths)
                if (!allowed.Contains(path.Replace('\\', '/')))
                    throw new InvalidOperationException("Unexpected staged file: " + path +
                        ". Nothing was committed.");

            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog("No new changes", "These files are already committed locally. Confirm upstream/main is current before considering them uploaded.", "OK");
                return;
            }
            Git(dir, "commit -m " + Quote("Save user placed Snapper rocks Blender files and mesh colliders"));
            Git(dir, "push upstream main", 180000);
            EditorUtility.DisplayDialog("Uploaded to GitHub",
                "The .blend assets, PrototypeWorld scene, collider changes, and ManualSnapperRocks.json have been pushed to upstream/main.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MANUAL ROCK SAVE] GitHub upload incomplete: " + ex);
            EditorUtility.DisplayDialog("Saved locally, GitHub upload incomplete",
                "Your scene and colliders are saved locally, but the GitHub push has not succeeded:\n\n" +
                ex.Message + "\n\nRun git status in your project folder and resolve the Git issue. There is no need to redo collision setup.", "OK");
        }
    }

    private static string Quote(string argument)
    {
        return "\"" + argument.Replace("\"", "\\\"") + "\"";
    }

    private static string Git(string workingDir, string arguments, int timeoutMs = 120000)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var process = System.Diagnostics.Process.Start(start))
        {
            if (process == null) throw new IOException("Git could not start. Is Git for Windows installed?");
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
