using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public static class FishingGaffImporter
{
    private const string Root =
        "Assets/_Game/Fishing/Gaff";

    private const string Source =
        Root + "/Source";

    private const string TexturePath =
        Source + "/Gaff_Texture.jpg";

    private const string FbxPath =
        Source + "/Gaff.fbx";

    private const string MaterialPath =
        Root + "/Gaff.mat";

    private const string PrefabPath =
        "Assets/Resources/Fishing/FishingGaff.prefab";

    private const string BlenderPrefsKey =
        "OpenWorld.Goatfish.BlenderExecutable";

    public static void ImportFromZip(
        string zipPath,
        bool showDialog)
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException(
                "Exit Play Mode before importing the fishing hook."
            );
        }

        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException(
                "Gaf.zip was not found.",
                zipPath
            );
        }

        EnsureFolderRecursive(Source);
        EnsureFolderRecursive(
            "Assets/Resources/Fishing"
        );

        string library =
            Path.GetFullPath(
                "Library/FishingGaffImport"
            );

        Directory.CreateDirectory(
            library
        );

        string blendPath =
            Path.Combine(
                library,
                "Gaf.blend"
            );

        Extract(
            zipPath,
            blendPath
        );

        ExportBlend(
            ResolveBlenderExecutable(),
            blendPath,
            Path.GetFullPath(
                FbxPath
            )
        );

        AssetDatabase.Refresh();

        ConfigureTexture();
        ConfigureFbx();
        BuildPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Fishing Hook Imported",
                "The Gaf model is installed as the caught-fish hook/gaff.",
                "OK"
            );
        }
    }

    private static void Extract(
        string zipPath,
        string blendPath)
    {
        using FileStream stream =
            File.OpenRead(zipPath);

        using ZipArchive archive =
            new ZipArchive(
                stream,
                ZipArchiveMode.Read
            );

        ZipArchiveEntry blend =
            archive.Entries
                .FirstOrDefault(
                    item =>
                        item.FullName.EndsWith(
                            ".blend",
                            StringComparison.OrdinalIgnoreCase
                        )
                );

        ZipArchiveEntry texture =
            archive.Entries
                .FirstOrDefault(
                    item =>
                    {
                        string ext =
                            Path.GetExtension(
                                item.FullName
                            )
                            .ToLowerInvariant();

                        return
                            ext == ".jpg" ||
                            ext == ".jpeg" ||
                            ext == ".png";
                    }
                );

        if (blend == null ||
            texture == null)
        {
            throw new InvalidDataException(
                "Gaf.zip must contain the Blend model and texture."
            );
        }

        using (Stream input = blend.Open())
        using (FileStream output =
               File.Create(blendPath))
        {
            input.CopyTo(output);
        }

        WriteEntry(
            texture,
            TexturePath
        );
    }

    private static void ExportBlend(
        string blender,
        string blendPath,
        string destinationFbx)
    {
        if (!File.Exists(
                blendPath))
        {
            throw new FileNotFoundException(
                "The extracted Gaf Blend source is missing.",
                blendPath
            );
        }

        string scriptFolder =
            Path.GetFullPath(
                "Library/FishingGaffImport"
            );

        Directory.CreateDirectory(
            scriptFolder
        );

        string scriptPath =
            Path.Combine(
                scriptFolder,
                "ExportFishingGaff.py"
            );

        string python =
@"import bpy
import os
import sys

args = sys.argv
out_path = args[args.index('--') + 1]

meshes = [
    obj for obj in bpy.context.scene.objects
    if obj.type == 'MESH'
]

if not meshes:
    raise RuntimeError('No mesh objects were found in the Gaf Blend file.')

# Background Blender has no normal 3D-view selection context. Avoid
# bpy.ops.object.select_all(), which can fail depending on active context.
for obj in bpy.context.view_layer.objects:
    try:
        obj.select_set(False)
    except Exception:
        pass

for obj in meshes:
    obj.hide_viewport = False
    obj.hide_render = False

    try:
        obj.hide_set(False)
    except Exception:
        pass

    try:
        obj.select_set(True)
    except Exception:
        pass

try:
    bpy.context.view_layer.objects.active = meshes[0]
except Exception:
    pass

os.makedirs(os.path.dirname(out_path), exist_ok=True)

# Remove any stale file so a prior partial export can never look successful.
if os.path.exists(out_path):
    os.remove(out_path)

bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    object_types={'MESH'},
    apply_unit_scale=True,
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward='-Z',
    axis_up='Y',
    path_mode='AUTO'
)

if not os.path.exists(out_path):
    raise RuntimeError('FBX export did not produce an output file.')
";

        File.WriteAllText(
            scriptPath,
            python
        );

        ProcessStartInfo info =
            new ProcessStartInfo();

        info.FileName =
            blender;

        info.Arguments =
            "--background " +
            QuoteArgument(
                blendPath
            ) +
            " --python " +
            QuoteArgument(
                scriptPath
            ) +
            " -- " +
            QuoteArgument(
                destinationFbx
            );

        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;

        using Process process =
            Process.Start(
                info
            );

        if (process == null)
        {
            throw new InvalidOperationException(
                "Blender could not be started."
            );
        }

        string output =
            process.StandardOutput.ReadToEnd();

        string error =
            process.StandardError.ReadToEnd();

        if (!process.WaitForExit(
                120000))
        {
            try
            {
                process.Kill();
            }
            catch
            {
            }

            throw new TimeoutException(
                "Blender took more than 2 minutes to export the Gaf model."
            );
        }

        if (process.ExitCode != 0 ||
            !File.Exists(
                destinationFbx))
        {
            Debug.LogError(
                "Full Blender Gaf export output:\n" +
                output +
                "\n\nFull Blender errors:\n" +
                error
            );

            throw new InvalidOperationException(
                "Blender opened the Gaf source but could not finish the FBX export. The full Blender traceback was written to the Unity Console."
            );
        }
    }

    private static string QuoteArgument(
        string value)
    {
        return
            "\"" +
            value.Replace(
                "\"",
                "\\\""
            ) +
            "\"";
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(
            TexturePath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(
                TexturePath
            ) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Could not import the Gaf texture."
            );
        }

        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode =
            TextureWrapMode.Clamp;

        importer.maxTextureSize = 2048;

        importer.textureCompression =
            TextureImporterCompression.CompressedHQ;

        importer.SaveAndReimport();
    }

    private static void ConfigureFbx()
    {
        AssetDatabase.ImportAsset(
            FbxPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate
        );

        ModelImporter importer =
            AssetImporter.GetAtPath(
                FbxPath
            ) as ModelImporter;

        if (importer == null)
        {
            throw new InvalidOperationException(
                "Could not import Gaff.fbx."
            );
        }

        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;

        importer.materialImportMode =
            ModelImporterMaterialImportMode.None;

        importer.importNormals =
            ModelImporterNormals.Import;

        importer.importTangents =
            ModelImporterTangents.CalculateMikk;

        importer.SaveAndReimport();
    }

    private static void BuildPrefab()
    {
        GameObject source =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                FbxPath
            );

        if (source == null)
        {
            throw new InvalidOperationException(
                "Gaff FBX could not be loaded."
            );
        }

        Material material =
            BuildMaterial();

        GameObject root =
            new GameObject(
                "FishingGaff"
            );

        try
        {
            GameObject visual =
                PrefabUtility.InstantiatePrefab(
                    source
                ) as GameObject;

            if (visual == null)
            {
                visual =
                    UnityEngine.Object.Instantiate(
                        source
                    );
            }

            if (PrefabUtility.IsPartOfPrefabInstance(
                    visual))
            {
                PrefabUtility.UnpackPrefabInstance(
                    visual,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction
                );
            }

            visual.name = "Visual";

            visual.transform.SetParent(
                root.transform,
                false
            );

            foreach (Renderer renderer in
                     visual.GetComponentsInChildren<Renderer>(
                         true
                     ))
            {
                Material[] slots =
                    new Material[
                        Mathf.Max(
                            1,
                            renderer.sharedMaterials.Length
                        )
                    ];

                for (int i = 0;
                     i < slots.Length;
                     i++)
                {
                    slots[i] = material;
                }

                renderer.sharedMaterials =
                    slots;

                renderer.shadowCastingMode =
                    ShadowCastingMode.On;

                renderer.receiveShadows =
                    true;
            }

            // Source model's long +X axis is the handle, while the actual
            // curved hook is at negative X. Rotate +X upward so hook is down.
            visual.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    90f
                );

            Bounds bounds =
                CalculateBounds(
                    root.transform
                );

            float length =
                Mathf.Max(
                    bounds.size.y,
                    Mathf.Max(
                        bounds.size.x,
                        bounds.size.z
                    )
                );

            if (length > 0.0001f)
            {
                visual.transform.localScale *=
                    1.15f /
                    length;
            }

            bounds =
                CalculateBounds(
                    root.transform
                );

            Vector3 correction =
                new Vector3(
                    -bounds.center.x,
                    -bounds.min.y +
                    0.02f,
                    -bounds.center.z
                );

            visual.transform.position +=
                correction;

            GameObject point =
                new GameObject(
                    "HookPoint"
                );

            point.transform.SetParent(
                root.transform,
                false
            );

            point.transform.localPosition =
                new Vector3(
                    0f,
                    0.055f,
                    0f
                );

            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(
                    PrefabPath
                );
            }

            PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(
                root
            );
        }
    }

    private static Material BuildMaterial()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(
                MaterialPath) != null)
        {
            AssetDatabase.DeleteAsset(
                MaterialPath
            );
        }

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit"
            );

        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                TexturePath
            );

        Material material =
            new Material(shader);

        material.name = "FishingGaff";

        material.SetTexture(
            "_BaseMap",
            texture
        );

        material.SetColor(
            "_BaseColor",
            Color.white
        );

        material.SetFloat(
            "_Metallic",
            0.28f
        );

        material.SetFloat(
            "_Smoothness",
            0.50f
        );

        AssetDatabase.CreateAsset(
            material,
            MaterialPath
        );

        return material;
    }

    private static Bounds CalculateBounds(
        Transform root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        if (renderers.Length == 0)
        {
            return
                new Bounds(
                    root.position,
                    Vector3.one
                );
        }

        Bounds result =
            renderers[0].bounds;

        for (int i = 1;
             i < renderers.Length;
             i++)
        {
            result.Encapsulate(
                renderers[i].bounds
            );
        }

        return result;
    }

    private static string ResolveBlenderExecutable()
    {
        string saved =
            EditorPrefs.GetString(
                BlenderPrefsKey,
                string.Empty
            );

        if (!string.IsNullOrWhiteSpace(
                saved) &&
            File.Exists(saved))
        {
            return saved;
        }

        List<string> candidates =
            new List<string>();

        string programFiles =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles
            );

        string programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86
            );

        foreach (string baseFolder in
                 new[]
                 {
                     programFiles,
                     programFilesX86
                 })
        {
            string blenderRoot =
                Path.Combine(
                    baseFolder,
                    "Blender Foundation"
                );

            if (!Directory.Exists(
                    blenderRoot))
            {
                continue;
            }

            candidates.AddRange(
                Directory.GetFiles(
                    blenderRoot,
                    "blender.exe",
                    SearchOption.AllDirectories
                )
            );
        }

        string found =
            candidates
                .OrderByDescending(
                    item =>
                        File.GetLastWriteTimeUtc(
                            item
                        )
                )
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(
                found))
        {
            EditorPrefs.SetString(
                BlenderPrefsKey,
                found
            );

            return found;
        }

        string selected =
            EditorUtility.OpenFilePanel(
                "Locate Blender.exe",
                programFiles,
                "exe"
            );

        if (string.IsNullOrWhiteSpace(
                selected))
        {
            throw new InvalidOperationException(
                "Blender.exe is required to import Gaf.zip."
            );
        }

        EditorPrefs.SetString(
            BlenderPrefsKey,
            selected
        );

        return selected;
    }

    private static void WriteEntry(
        ZipArchiveEntry entry,
        string assetPath)
    {
        string absolute =
            Path.GetFullPath(
                assetPath
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                absolute
            )
        );

        using Stream input =
            entry.Open();

        using FileStream output =
            File.Create(
                absolute
            );

        input.CopyTo(output);
    }

    private static void EnsureFolderRecursive(
        string path)
    {
        string[] parts =
            path.Split('/');

        string current = "Assets";

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (!AssetDatabase.IsValidFolder(
                    next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]
                );
            }

            current = next;
        }
    }
}
