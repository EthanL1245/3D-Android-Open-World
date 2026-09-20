using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UpdatedFishingAssetsInstaller
{
    [MenuItem("Tools/Open World/Install Updated Fishing Assets (One Click)")]
    public static void Install()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Fishing Asset Installer",
                "Exit Play Mode first.",
                "OK"
            );

            return;
        }

        try
        {
            Dictionary<string, string> packages =
                FindPackages();

            if (packages.Count < 4)
            {
                string defaultFolder =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.UserProfile
                        ),
                        "Downloads"
                    );

                string selected =
                    EditorUtility.OpenFolderPanel(
                        "Choose the folder containing the 4 fishing ZIP files",
                        Directory.Exists(defaultFolder)
                            ? defaultFolder
                            : string.Empty,
                        string.Empty
                    );

                if (string.IsNullOrWhiteSpace(
                        selected))
                {
                    return;
                }

                packages =
                    FindPackagesInFolder(
                        selected
                    );
            }

            Require(
                packages,
                "yellow",
                "YellowGoatfish ZIP"
            );

            Require(
                packages,
                "black",
                "Black Spot Goatfish ZIP"
            );

            Require(
                packages,
                "mackerel",
                "Mackerel.zip"
            );

            Require(
                packages,
                "gaf",
                "Gaf.zip"
            );

            EditorUtility.DisplayProgressBar(
                "Fishing Asset Pack",
                "Installing updated Goatfish...",
                0.08f
            );

            GoatfishPairImporter
                .ImportPairFromZipPaths(
                    packages["yellow"],
                    packages["black"],
                    false
                );

            EditorUtility.DisplayProgressBar(
                "Fishing Asset Pack",
                "Installing animated Mackerel...",
                0.58f
            );

            MackerelImporter.ImportFromZip(
                packages["mackerel"],
                false
            );

            EditorUtility.DisplayProgressBar(
                "Fishing Asset Pack",
                "Installing fishing hook/gaff...",
                0.80f
            );

            FishingGaffImporter.ImportFromZip(
                packages["gaf"],
                false
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Fishing/Mackerel.prefab"
                );

            EditorUtility.DisplayDialog(
                "Fishing Assets Installed",
                "Done. Updated Yellow Goatfish, Black Spot Goatfish, animated Mackerel, and the Gaf hook are installed. No Inspector wiring is required.",
                "OK"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception
            );

            EditorUtility.DisplayDialog(
                "Fishing Asset Installer Failed",
                exception.Message +
                "\n\nThe full error is in the Unity Console.",
                "OK"
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static Dictionary<string, string> FindPackages()
    {
        string user =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile
            );

        string desktop =
            Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory
            );

        string[] folders =
        {
            Path.Combine(
                user,
                "Downloads"
            ),
            desktop,
            Path.GetFullPath(".")
        };

        Dictionary<string, string> result =
            new Dictionary<string, string>();

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
                continue;

            Merge(
                result,
                FindPackagesInFolder(
                    folder
                )
            );
        }

        return result;
    }

    private static Dictionary<string, string> FindPackagesInFolder(
        string folder)
    {
        Dictionary<string, string> result =
            new Dictionary<string, string>();

        if (!Directory.Exists(folder))
            return result;

        string[] files =
            Directory.GetFiles(
                folder,
                "*.zip",
                SearchOption.TopDirectoryOnly
            );

        foreach (string file in files)
        {
            string name =
                Path.GetFileNameWithoutExtension(
                    file
                )
                .ToLowerInvariant();

            if (name.Contains("yellow") &&
                name.Contains("goat"))
            {
                result["yellow"] = file;
            }
            else if (name.Contains("black") &&
                     name.Contains("goat"))
            {
                result["black"] = file;
            }
            else if (name.Contains("mackerel"))
            {
                result["mackerel"] = file;
            }
            else if (name == "gaf" ||
                     name.Contains("gaff"))
            {
                result["gaf"] = file;
            }
        }

        return result;
    }

    private static void Merge(
        Dictionary<string, string> destination,
        Dictionary<string, string> source)
    {
        foreach (KeyValuePair<string, string> pair
                 in source)
        {
            if (!destination.ContainsKey(
                    pair.Key))
            {
                destination[
                    pair.Key
                ] =
                    pair.Value;
            }
        }
    }

    private static void Require(
        Dictionary<string, string> packages,
        string key,
        string displayName)
    {
        if (!packages.TryGetValue(
                key,
                out string path) ||
            !File.Exists(path))
        {
            throw new FileNotFoundException(
                displayName +
                " was not found. Keep all four uploaded ZIPs in Downloads or choose their folder when prompted."
            );
        }
    }
}
