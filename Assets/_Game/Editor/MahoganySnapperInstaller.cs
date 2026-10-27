using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Uses the same animated fish pipeline as the other snapper species. The local
// FBX or Blender .blend and PNG can be in Assets or the user's Desktop/Downloads.
// When only .blend is available the local Blender executable exports the rig.
[InitializeOnLoad]
public static class MahoganySnapperInstaller
{
    private const string Source = "Assets/_Game/Reef/Source";
    private const string PrefabPath = "Assets/Resources/Fishing/MahoganySnapper.prefab";
    private const string FbxName = "MahoganySnapper.fbx";
    private const string PngName = "MahoganySnapperTexture.png";
    private static bool installing;
    private static bool warned;

    private static string Root => Directory.GetParent(Application.dataPath).FullName;
    private static string FullPath(string name) => Path.Combine(Root, Source, name);

    static MahoganySnapperInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { EnsureInstalled(); }
            catch (Exception e) { EditorApplication.isPlaying = false; Debug.LogException(e); }
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
        catch (Exception e) { Debug.LogError("[MAHOGANY SNAPPER] Auto-import failed: " + e); }
    }

    public static void EnsureInstalled()
    {
        if (installing) return;
        installing = true;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool valid = prefab != null &&
                         prefab.GetComponent<RedSnapperPresentation>() != null &&
                         prefab.GetComponentInChildren<Animator>(true) != null;
            if (valid && !SourceNewerThanPrefab()) return;

            if (!FindAndStageSources())
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("[MAHOGANY SNAPPER] Animated FBX (or Blender .blend) and PNG not found. " +
                        "Put them anywhere under Unity Assets, Desktop or Downloads, " +
                        "then reopen Unity. Or use Tools > Open World > Locate Mahogany Snapper Files. " +
                        "The generic fish remains usable meanwhile.");
                }
                return;
            }
            BuildPrefab();
        }
        finally { installing = false; }
    }

    private static bool SourceNewerThanPrefab()
    {
        string prefab = Path.Combine(Root, PrefabPath);
        if (!File.Exists(prefab)) return true;
        DateTime built = File.GetLastWriteTimeUtc(prefab);
        return (File.Exists(FullPath(FbxName)) && File.GetLastWriteTimeUtc(FullPath(FbxName)) > built) ||
               (File.Exists(FullPath(PngName)) && File.GetLastWriteTimeUtc(FullPath(PngName)) > built);
    }

    private static bool FindAndStageSources()
    {
        bool missingFbx = !File.Exists(FullPath(FbxName));
        bool missingPng = !File.Exists(FullPath(PngName));
        if (!missingFbx && !missingPng) return true;
        string fbx = missingFbx ? FindFile(false) : null;
        string png = missingPng ? FindFile(true) : null;
        if (missingFbx && fbx == null && (png != null || !missingPng))
        {
            string blend = FindFile(false, png != null ? Path.GetDirectoryName(png) : null, "*.blend");
            if (blend != null && ExportBlenderSource(blend))
                missingFbx = false; // Exported directly into the canonical Unity FBX location.
        }
        if ((missingFbx && fbx == null) || (missingPng && png == null)) return false;

        Directory.CreateDirectory(Path.GetDirectoryName(FullPath(FbxName)));
        if (missingFbx) Copy(fbx, FbxName);
        if (missingPng) Copy(png, PngName);
        ImportSources();
        Debug.Log("[MAHOGANY SNAPPER] Found local model and texture; copied into " + Source);
        return true;
    }

    // Runs the Blender exporter shipped with the project. No manual FBX export
    // is necessary provided Blender is installed on this computer.
    private static bool ExportBlenderSource(string blend)
    {
        string blender = FindBlenderExecutable();
        if (blender == null)
        {
            Debug.LogError("[MAHOGANY SNAPPER] Found " + blend +
                " but no Blender executable. A .blend cannot be animated/imported " +
                "as an FBX without Blender. Install Blender, then reopen Unity.");
            return false;
        }
        string exporter = Path.Combine(Root, "Tools", "MahoganySnapper", "export_snapper.py");
        if (!File.Exists(exporter))
        {
            Debug.LogError("[MAHOGANY SNAPPER] Missing exporter script: " + exporter);
            return false;
        }
        string destination = FullPath(FbxName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        string temporary = Path.Combine(Path.GetDirectoryName(destination), "MahoganySnapper-exporting.fbx");
        if (File.Exists(temporary)) File.Delete(temporary);
        string args = "--background " + Quote(blend) + " --python " + Quote(exporter) +
                      " -- " + Quote(temporary);
        var info = new System.Diagnostics.ProcessStartInfo(blender, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var log = new StringBuilder();
        try
        {
            using (var process = new System.Diagnostics.Process { StartInfo = info })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null) lock (log) log.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null) lock (log) log.AppendLine(e.Data);
                };
                Debug.Log("[MAHOGANY SNAPPER] Exporting local Blender fish animation from " + blend);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(180000))
                {
                    process.Kill();
                    Debug.LogError("[MAHOGANY SNAPPER] Blender FBX export exceeded 3 minutes.");
                    return false;
                }
                process.WaitForExit(); // Flush output callbacks before reporting.
                if (process.ExitCode != 0 || !File.Exists(temporary) ||
                    new FileInfo(temporary).Length < 1024)
                {
                    Debug.LogError("[MAHOGANY SNAPPER] Blender FBX export failed (exit " +
                        process.ExitCode + "). Blender output:\n" + log);
                    return false;
                }
            }
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(temporary, destination);
            Debug.Log("[MAHOGANY SNAPPER] Animated FBX successfully exported from .blend.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[MAHOGANY SNAPPER] Could not launch Blender exporter: " + e);
            return false;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string FindBlenderExecutable()
    {
        var paths = new List<string>();
        var env = Environment.GetEnvironmentVariable("BLENDER_BIN");
        if (!string.IsNullOrEmpty(env)) paths.Add(env.Trim('"'));
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in pathEnv.Split(Path.PathSeparator))
            if (!string.IsNullOrEmpty(dir)) paths.Add(Path.Combine(dir.Trim('"'), "blender.exe"));
        var programLocations = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        foreach (string program in programLocations)
        {
            if (string.IsNullOrEmpty(program)) continue;
            foreach (string parent in new[] {
                Path.Combine(program, "Blender Foundation"),
                Path.Combine(program, "Programs", "Blender Foundation")
            })
            {
                if (!Directory.Exists(parent)) continue;
                try
                {
                    paths.Add(Path.Combine(parent, "blender.exe"));
                    foreach (string version in Directory.GetDirectories(parent, "Blender*")
                        .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase))
                        paths.Add(Path.Combine(version, "blender.exe"));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return paths.FirstOrDefault(File.Exists);
    }

    private static void Copy(string from, string name)
    {
        string to = FullPath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(to));
        if (!string.Equals(Path.GetFullPath(from), Path.GetFullPath(to),
                           StringComparison.OrdinalIgnoreCase))
            File.Copy(from, to, true);
    }

    private static void ImportSources()
    {
        AssetDatabase.ImportAsset(Source + "/" + FbxName, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Source + "/" + PngName, ImportAssetOptions.ForceSynchronousImport);
    }

    private static string FindFile(bool png, string preferredFolder = null, string extensionPattern = null)
    {
        var roots = new List<string>();
        if (!string.IsNullOrEmpty(preferredFolder)) roots.Add(preferredFolder);
        roots.Add(Application.dataPath);
        roots.Add(Root);
        roots.Add(Path.Combine(Root, "ArtSources"));
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        roots.Add(Path.Combine(home, "Desktop"));
        roots.Add(Path.Combine(home, "Downloads"));
        roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        roots.Add(Path.Combine(home, "OneDrive", "Desktop"));
        roots.Add(Path.Combine(home, "OneDrive", "Downloads"));

        var files = new List<string>();
        foreach (var folder in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
            try
            {
                // Scan Assets recursively; scan external locations shallowly to
                // avoid slowing down the Editor or traversing all user files.
                bool assets = string.Equals(Path.GetFullPath(folder),
                    Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase);
                if (assets)
                    files.AddRange(Directory.GetFiles(folder, extensionPattern ?? (png ? "*.png" : "*.fbx"),
                        SearchOption.AllDirectories).Where(f => Rank(f, png) < 100));
                else
                {
                    var dirs = new List<string> { folder };
                    if (folder != Root && folder != Path.Combine(Root, "ArtSources"))
                        dirs.AddRange(Directory.GetDirectories(folder));
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            files.AddRange(Directory.GetFiles(dir, extensionPattern ?? (png ? "*.png" : "*.fbx"),
                                SearchOption.TopDirectoryOnly).Where(f => Rank(f, png) < 100));
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return files.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => Rank(f, png)).ThenByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private static int Rank(string path, bool png)
    {
        string n = new string(Path.GetFileNameWithoutExtension(path)
            .ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (!n.StartsWith("mahoganysnapper", StringComparison.Ordinal)) return 100;
        if (png)
        {
            if (n == "mahoganysnappertexture") return 0;
            if (n.StartsWith("mahoganysnappertexture", StringComparison.Ordinal)) return 1;
            return n == "mahoganysnapper" ? 2 : 100;
        }
        if (n.Contains("struggl")) return 100;
        if (n == "mahoganysnapper") return 0;
        if (n.StartsWith("mahoganysnapperswim", StringComparison.Ordinal)) return 1;
        return 2; // Files such as "Mahogany Snapper (1).fbx"
    }

    private static void BuildPrefab()
    {
        if (!File.Exists(FullPath(FbxName)) || !File.Exists(FullPath(PngName)))
            throw new InvalidOperationException("Mahogany Snapper FBX or texture is missing.");
        SeaBassImporter.InstallModel("MahoganySnapper", PrefabPath, Source);
        Debug.Log("[MAHOGANY SNAPPER] Animated prefab installed for fishing, holding, " +
                  "aquariums, ambient fish, and the fish index.");
    }

    [MenuItem("Tools/Open World/Rebuild Mahogany Snapper")]
    public static void Rebuild()
    {
        if (installing) return;
        installing = true;
        try
        {
            if (!FindAndStageSources())
                throw new InvalidOperationException("Mahogany Snapper animated FBX / Blender .blend and PNG not available. " +
                    "Use Tools > Open World > Locate Mahogany Snapper Files.");
            BuildPrefab();
        }
        finally { installing = false; }
    }

    [MenuItem("Tools/Open World/Locate Mahogany Snapper Files")]
    public static void LocateFiles()
    {
        // Only needed if files are outside the usual folders.
        string fbx = EditorUtility.OpenFilePanel("Select animated Mahogany Snapper FBX",
                                                  Application.dataPath, "fbx");
        if (string.IsNullOrEmpty(fbx)) return;
        string png = FindFile(true, Path.GetDirectoryName(fbx));
        if (png == null)
            png = EditorUtility.OpenFilePanel("Select Mahogany Snapper texture",
                                                Path.GetDirectoryName(fbx), "png");
        if (string.IsNullOrEmpty(png)) return;
        if (installing) return;
        installing = true;
        try
        {
            Copy(fbx, FbxName);
            Copy(png, PngName);
            ImportSources();
            BuildPrefab();
        }
        catch (Exception e) { Debug.LogError("[MAHOGANY SNAPPER] Manual import failed: " + e); }
        finally { installing = false; }
    }
}

// Also respond when a model or PNG is dropped into Assets while Unity is open.
public sealed class MahoganySnapperFileWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                                string[] moved, string[] movedFrom)
    {
        if (imported.Any(IsInput) || moved.Any(IsInput))
            EditorApplication.delayCall += MahoganySnapperInstaller.AutoInstall;
    }

    private static bool IsInput(string path)
    {
        string ext = Path.GetExtension(path);
        return (string.Equals(ext, ".fbx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".blend", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase)) &&
               Path.GetFileName(path).Replace(" ", "").ToLowerInvariant().Contains("mahoganysnapper");
    }
}

public sealed class MahoganySnapperBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => MahoganySnapperInstaller.EnsureInstalled();
}
