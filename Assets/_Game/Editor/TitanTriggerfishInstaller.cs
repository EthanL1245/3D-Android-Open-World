using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Titan Triggerfish uses the same authored fish pipeline as Red Snapper.
// Reads the user's local FBX and atlas PNG; no Blender export is necessary.
[InitializeOnLoad]
public static class TitanTriggerfishInstaller
{
    private const string Source = "Assets/_Game/Reef/Source";
    private const string PrefabPath = "Assets/Resources/Fishing/TitanTriggerfish.prefab";
    private const string ModelName = "TitanTriggerfish.fbx";
    private const string TextureName = "TitanTriggerfishTexture.png";
    private static bool installing;
    private static bool warned;

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string SourcePath(string name) => Path.Combine(ProjectRoot, Source, name);

    static TitanTriggerfishInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { EnsureInstalled(); }
            catch (Exception e)
            {
                EditorApplication.isPlaying = false;
                Debug.LogException(e);
            }
        };
    }

    public static void AutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoInstall;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { EnsureInstalled(); }
        catch (Exception e) { Debug.LogError("[TITAN TRIGGERFISH] Automatic import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        installing = true;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool ready = prefab != null &&
                         prefab.GetComponent<RedSnapperPresentation>() != null &&
                         prefab.GetComponentInChildren<Animator>(true) != null;
            if (ready)
            {
                var animator = prefab.GetComponentInChildren<Animator>(true);
                ready = animator.runtimeAnimatorController != null &&
                        animator.runtimeAnimatorController.animationClips.Any(clip =>
                            clip != null && clip.name == "Swim");
            }
            if (ready && !SourceNewerThanPrefab()) return;

            if (!StageSources())
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("[TITAN TRIGGERFISH] FBX and/or texture not found. " +
                        "Keep 'Titan Triggerfish.fbx' and 'Titan Triggerfish Texture.png' " +
                        "in Downloads, Desktop, Documents, or Unity Assets. " +
                        "Or choose Tools > Open World > Locate Titan Triggerfish Files.");
                }
                return;
            }
            BuildPrefab();
        }
        finally { installing = false; }
    }

    private static bool SourceNewerThanPrefab()
    {
        var prefab = Path.Combine(ProjectRoot, PrefabPath);
        if (!File.Exists(prefab)) return true;
        var timestamp = File.GetLastWriteTimeUtc(prefab);
        return File.Exists(SourcePath(ModelName)) &&
               File.GetLastWriteTimeUtc(SourcePath(ModelName)) > timestamp ||
               File.Exists(SourcePath(TextureName)) &&
               File.GetLastWriteTimeUtc(SourcePath(TextureName)) > timestamp;
    }

    private static bool StageSources()
    {
        bool needsModel = !File.Exists(SourcePath(ModelName));
        bool needsTexture = !File.Exists(SourcePath(TextureName));
        if (!needsModel && !needsTexture) return true;
        string model = needsModel ? FindLocalFile(false) : null;
        string texture = needsTexture ? FindLocalFile(true) : null;
        if ((needsModel && model == null) || (needsTexture && texture == null)) return false;
        if (needsModel) Copy(model, ModelName);
        if (needsTexture) Copy(texture, TextureName);
        ImportSources();
        return true;
    }

    private static void Copy(string source, string name)
    {
        var target = SourcePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target),
                           StringComparison.OrdinalIgnoreCase))
            File.Copy(source, target, true);
    }

    private static void ImportSources()
    {
        AssetDatabase.ImportAsset(Source + "/" + ModelName, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Source + "/" + TextureName, ImportAssetOptions.ForceSynchronousImport);
    }

    private static string FindLocalFile(bool texture, string preferred = null)
    {
        var roots = new List<string>();
        if (!string.IsNullOrEmpty(preferred)) roots.Add(preferred);
        roots.Add(Application.dataPath);
        roots.Add(ProjectRoot);
        roots.Add(Path.Combine(ProjectRoot, "ArtSources"));
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        roots.Add(Path.Combine(user, "Downloads"));
        roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        roots.Add(Path.Combine(user, "Desktop"));
        roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        roots.Add(Path.Combine(user, "OneDrive", "Downloads"));
        roots.Add(Path.Combine(user, "OneDrive", "Desktop"));
        var found = new List<string>();
        foreach (var folder in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
            try
            {
                bool assets = string.Equals(Path.GetFullPath(folder),
                    Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase);
                if (assets)
                    found.AddRange(Directory.GetFiles(folder, texture ? "*.png" : "*.fbx",
                        SearchOption.AllDirectories).Where(f => Rank(f, texture) < 100));
                else
                {
                    var folders = new List<string> { folder };
                    if (!string.Equals(folder, ProjectRoot, StringComparison.OrdinalIgnoreCase))
                        folders.AddRange(Directory.GetDirectories(folder));
                    foreach (var dir in folders)
                        try
                        {
                            found.AddRange(Directory.GetFiles(dir, texture ? "*.png" : "*.fbx",
                                SearchOption.TopDirectoryOnly).Where(f => Rank(f, texture) < 100));
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return found.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => Rank(p, texture))
            .ThenByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private static int Rank(string path, bool texture)
    {
        string normalized = new string(Path.GetFileNameWithoutExtension(path)
            .ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (texture)
        {
            if (normalized == "titantriggerfishtexture") return 0;
            if (normalized.StartsWith("titantriggerfishtexture", StringComparison.Ordinal)) return 1;
            return 100;
        }
        if (normalized == "titantriggerfish") return 0;
        if (normalized.StartsWith("titantriggerfishswim", StringComparison.Ordinal)) return 1;
        if (normalized.StartsWith("titantriggerfish", StringComparison.Ordinal) &&
            !normalized.Contains("struggl")) return 2;
        return 100;
    }

    private static void BuildPrefab()
    {
        if (!File.Exists(SourcePath(ModelName)) || !File.Exists(SourcePath(TextureName)))
            throw new InvalidOperationException("Titan Triggerfish FBX or texture is missing.");
        // The supplied FBX has ONE authored animation stack. Install it as Swim;
        // CaughtFishFlop supplies the standard held-fish motion, as for snappers.
        SeaBassImporter.InstallModel("TitanTriggerfish", PrefabPath, Source);
        Debug.Log("[TITAN TRIGGERFISH] Animated prefab installed: swimming, held fish, " +
                  "aquariums, multiplayer visuals, fish index and inventory.");
    }

    [MenuItem("Tools/Open World/Rebuild Titan Triggerfish")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!StageSources())
                throw new InvalidOperationException("Titan Triggerfish FBX/PNG not found. " +
                    "Use Tools > Open World > Locate Titan Triggerfish Files.");
            BuildPrefab();
        }
        finally { installing = false; }
    }

    [MenuItem("Tools/Open World/Locate Titan Triggerfish Files")]
    public static void LocateFiles()
    {
        string model = EditorUtility.OpenFilePanel("Select Titan Triggerfish FBX",
                                                    Application.dataPath, "fbx");
        if (string.IsNullOrEmpty(model)) return;
        string texture = FindLocalFile(true, Path.GetDirectoryName(model));
        if (texture == null)
            texture = EditorUtility.OpenFilePanel("Select Titan Triggerfish texture",
                                                   Path.GetDirectoryName(model), "png");
        if (string.IsNullOrEmpty(texture)) return;
        if (installing) return;
        installing = true;
        try
        {
            Copy(model, ModelName);
            Copy(texture, TextureName);
            ImportSources();
            BuildPrefab();
        }
        catch (Exception e) { Debug.LogError("[TITAN TRIGGERFISH] Import failed: " + e); }
        finally { installing = false; }
    }
}

public sealed class TitanTriggerfishFileWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                                string[] moved, string[] movedFrom)
    {
        if (imported.Any(IsRelevant) || moved.Any(IsRelevant))
            EditorApplication.delayCall += TitanTriggerfishInstaller.AutoInstall;
    }

    private static bool IsRelevant(string path)
    {
        string ext = Path.GetExtension(path);
        return (string.Equals(ext, ".fbx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase)) &&
               Path.GetFileName(path).Replace(" ", "").ToLowerInvariant()
                   .Contains("titantriggerfish");
    }
}

public sealed class TitanTriggerfishBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => TitanTriggerfishInstaller.EnsureInstalled();
}
