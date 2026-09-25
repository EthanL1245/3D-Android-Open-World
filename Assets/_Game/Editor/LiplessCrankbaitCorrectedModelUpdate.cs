using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

/// <summary>
/// Swaps ONLY the crankbait's visible mesh/UV data to the corrected model package.
///
/// Important: STL itself cannot contain UVs, textures, animation, or the existing
/// line attachment. The supplied corrected ZIP also contains the matching .blend,
/// so this updater validates the exact STL, exports that corrected Blend as a
/// geometry/UV-only FBX, then bakes its three meshes into the EXISTING animated
/// transforms. The working animation controller, target transforms, LineAttach,
/// lighting rig, runtime facing/retrieve code, root scale and materials stay put.
/// </summary>
public static class LiplessCrankbaitCorrectedModelUpdate
{
    private const string PrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string ExistingTexturePath = "Assets/_Game/Fishing/LiplessCrankbait/Source/LiplessCrankbaitGreenStriped.jpg";
    internal const string CorrectedSourceFolder = "Assets/_Game/Fishing/LiplessCrankbait/CorrectedModelSource";
    internal const string CorrectedSourceFbxPath = CorrectedSourceFolder + "/LiplessCrankbaitGreenStripedCorrected.fbx";
    private const string CorrectedMeshFolder = CorrectedSourceFolder + "/Meshes";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";

    private const string ExpectedStlSha256 = "50f79773fff1672adc7dc4e02166b5cae43e7d8ceb1c45842ed367a194b38c90";
    private const string ExpectedBlendSha256 = "def64323af52b2600274a5bb5418a388d2b4096f8683606ef5ea0fd50cfb20c4";
    private const string ExpectedTextureSha256 = "61ff9b0df51020e5bd94142d1d811592a73e508da9180e61c53ac24fbe439a57";
    private const int ExpectedTriangleCount = 1720;

    [MenuItem("Tools/Open World/Apply Lipless Crankbait CORRECTED STL MODEL (One Click)")]
    public static void ApplyOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Corrected Crankbait", "Exit Play Mode first.", "OK");
            return;
        }

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Corrected Crankbait",
                "The current working crankbait prefab is missing. Reimport the working lure first so its animation/line setup exists, then run this corrected-model update.",
                "OK");
            return;
        }

        string zipPath = FindCorrectedZip();
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            zipPath = EditorUtility.OpenFilePanel(
                "Choose Lipless Crankbait Green Striped STL Corrected.zip",
                Directory.Exists(downloads) ? downloads : string.Empty,
                "zip");
        }
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        try
        {
            EditorUtility.DisplayProgressBar("Corrected crankbait", "Validating the exact corrected STL package...", 0.08f);

            string library = Path.GetFullPath("Library/LiplessCrankbaitCorrectedModel");
            Directory.CreateDirectory(library);
            string blendPath = Path.Combine(library, "Lipless Crankbait Green Striped.blend");
            string exportedFbxPath = Path.Combine(library, "LiplessCrankbaitGreenStripedCorrected.fbx");
            byte[] textureBytes;
            ExtractAndValidate(zipPath, blendPath, out textureBytes);

            EditorUtility.DisplayProgressBar("Corrected crankbait", "Exporting corrected geometry + UVs only...", 0.25f);
            ExportCorrectedBlend(ResolveBlenderExecutable(), blendPath, exportedFbxPath);

            EditorUtility.DisplayProgressBar("Corrected crankbait", "Importing corrected mesh source once...", 0.46f);
            EnsureFolderRecursive(CorrectedSourceFolder);
            EnsureFolderRecursive(CorrectedMeshFolder);
            CopyFileIfChanged(exportedFbxPath, Path.GetFullPath(CorrectedSourceFbxPath));
            AssetDatabase.ImportAsset(
                CorrectedSourceFbxPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            GameObject correctedSource = AssetDatabase.LoadAssetAtPath<GameObject>(CorrectedSourceFbxPath);
            if (correctedSource == null)
                throw new InvalidOperationException("Unity could not load the corrected geometry source FBX.");

            EditorUtility.DisplayProgressBar("Corrected crankbait", "Baking corrected meshes into the existing animated lure...", 0.68f);
            ApplyMeshesWithoutTouchingAnimation(correctedSource, textureBytes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            EditorUtility.DisplayDialog(
                "Corrected Crankbait Applied",
                "The corrected model/UVs are now on the existing crankbait. Animation, belly/tail hook movement, LineAttach, line movement, retrieve-facing behavior, lure scale/orientation, lighting rig, and gameplay code were preserved.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Corrected Crankbait Failed",
                exception.Message + "\n\nThe working lure prefab is only saved after all compatibility checks pass. See Console for the full error.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void ExtractAndValidate(string zipPath, string blendPath, out byte[] textureBytes)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("The corrected crankbait ZIP was not found.", zipPath);

        using FileStream stream = File.OpenRead(zipPath);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);

        ZipArchiveEntry stl = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry blend = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".blend", StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry texture = archive.Entries.FirstOrDefault(e =>
        {
            string ext = Path.GetExtension(e.FullName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
        });

        if (stl == null || blend == null || texture == null)
            throw new InvalidDataException("The corrected package must contain its STL, Blend source, and texture image.");

        byte[] stlBytes = ReadAll(stl);
        byte[] blendBytes = ReadAll(blend);
        textureBytes = ReadAll(texture);

        if (!HashEquals(stlBytes, ExpectedStlSha256) ||
            !HashEquals(blendBytes, ExpectedBlendSha256) ||
            !HashEquals(textureBytes, ExpectedTextureSha256))
        {
            throw new InvalidDataException(
                "That is not the exact corrected crankbait package supplied for this update. Choose 'Lipless Crankbait Green Striped STL Corrected.zip'.");
        }

        // The supplied STL is binary and contains 1720 triangles. Validate the
        // topology here even though UVs must come from the matching Blend source.
        if (stlBytes.Length < 84)
            throw new InvalidDataException("The corrected STL is truncated.");
        uint stlTriangles = BitConverter.ToUInt32(stlBytes, 80);
        if (stlTriangles != ExpectedTriangleCount)
            throw new InvalidDataException("The corrected STL topology does not match the supplied lure revision.");

        Directory.CreateDirectory(Path.GetDirectoryName(blendPath));
        File.WriteAllBytes(blendPath, blendBytes);
    }

    private static void ExportCorrectedBlend(string blender, string blendPath, string destinationFbx)
    {
        string scriptPath = Path.Combine(Path.GetDirectoryName(destinationFbx), "ExportCorrectedCrankbait.py");
        string python =
@"import bpy
import os
import sys

args = sys.argv
out_path = args[args.index('--') + 1]

# Geometry/UV source only. The live Unity prefab keeps its EXISTING animator,
# clips, hook transforms, line attachment and lighting.
for obj in bpy.context.scene.objects:
    try:
        obj.select_set(False)
    except Exception:
        pass

selected = []
for obj in bpy.context.scene.objects:
    if obj.type in {'MESH', 'CURVE'}:
        try:
            obj.hide_viewport = False
            obj.hide_render = False
            obj.hide_set(False)
            obj.select_set(True)
            selected.append(obj)
        except Exception:
            pass

if len(selected) != 3:
    raise RuntimeError('Expected exactly 3 crankbait geometry objects (body + two treble hooks), found %d.' % len(selected))

bpy.context.view_layer.objects.active = selected[0]
os.makedirs(os.path.dirname(out_path), exist_ok=True)
if os.path.exists(out_path):
    os.remove(out_path)

bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    apply_unit_scale=True,
    add_leaf_bones=False,
    bake_anim=False,
    use_mesh_modifiers=True,
    axis_forward='-Z',
    axis_up='Y',
    path_mode='AUTO'
)

if not os.path.exists(out_path):
    raise RuntimeError('Corrected FBX export produced no file.')
";
        File.WriteAllText(scriptPath, python);

        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = blender,
            Arguments = "--background " + Quote(blendPath) + " --python " + Quote(scriptPath) + " -- " + Quote(destinationFbx),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(info);
        if (process == null)
            throw new InvalidOperationException("Blender could not be started.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120000))
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException("Blender took more than 2 minutes to export the corrected crankbait geometry.");
        }

        if (process.ExitCode != 0 || !File.Exists(destinationFbx))
        {
            Debug.LogError("Corrected crankbait Blender output:\n" + output + "\n\nErrors:\n" + error);
            throw new InvalidOperationException("Blender could not export the corrected crankbait geometry. See Console for Blender output.");
        }
    }

    private static void ApplyMeshesWithoutTouchingAnimation(GameObject correctedSource, byte[] textureBytes)
    {
        MeshFilter[] sourceFilters = correctedSource.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f != null && f.sharedMesh != null)
            .ToArray();

        if (sourceFilters.Length != 3)
            throw new InvalidOperationException("Corrected source imported with " + sourceFilters.Length + " mesh parts; expected exactly body + two hooks.");

        int sourceTriangles = sourceFilters.Sum(f => CountTriangles(f.sharedMesh));
        if (sourceTriangles != ExpectedTriangleCount)
            throw new InvalidOperationException(
                "Corrected FBX has " + sourceTriangles + " triangles but the validated STL has " + ExpectedTriangleCount + ". No live prefab changes were saved.");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform authoredModel = FindDeepChild(root.transform, "AuthoredModel");
            if (authoredModel == null)
                throw new InvalidOperationException("The current working lure prefab has no AuthoredModel child.");

            MeshFilter[] targetFilters = authoredModel.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f != null && f.sharedMesh != null && !IsUnderLightingRig(f.transform, authoredModel))
                .ToArray();

            if (targetFilters.Length != 3)
                throw new InvalidOperationException("The working animated lure has " + targetFilters.Length + " mesh parts; expected exactly body + two hooks.");

            Transform lineAttach = FindDeepChild(root.transform, "LineAttach");
            if (lineAttach == null)
                throw new InvalidOperationException("The working lure has no LineAttach transform.");

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("The working lure Animator/controller is missing.");

            Transform lineParent = lineAttach.parent;
            Vector3 linePosition = lineAttach.localPosition;
            Quaternion lineRotation = lineAttach.localRotation;
            Vector3 lineScale = lineAttach.localScale;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Vector3 rootPosition = root.transform.localPosition;
            Quaternion rootRotation = root.transform.localRotation;
            Vector3 rootScale = root.transform.localScale;

            Dictionary<Transform, TransformSnapshot> targetTransformSnapshots = new Dictionary<Transform, TransformSnapshot>();
            for (int i = 0; i < targetFilters.Length; i++)
                targetTransformSnapshots[targetFilters[i].transform] = new TransformSnapshot(targetFilters[i].transform);

            Dictionary<string, MeshFilter> sourceByName = BuildUniqueFilterMap(sourceFilters);
            Dictionary<string, MeshFilter> targetByName = BuildUniqueFilterMap(targetFilters);

            if (sourceByName.Count != 3 || targetByName.Count != 3 ||
                sourceByName.Keys.Any(k => !targetByName.ContainsKey(k)))
            {
                throw new InvalidOperationException(
                    "Corrected model object names do not match the existing animated body/hook transforms. The updater stopped rather than risk moving the hooks or line attachment.");
            }

            // Preserve the current material setup/layers/lighting. Only replace the
            // MeshFilter mesh with corrected geometry+UV baked into the OLD animated
            // transform's coordinate space.
            foreach (string key in targetByName.Keys)
            {
                MeshFilter sourceFilter = sourceByName[key];
                MeshFilter targetFilter = targetByName[key];
                Mesh baked = BakeSourceMeshIntoTargetSpace(
                    sourceFilter,
                    correctedSource.transform,
                    targetFilter,
                    authoredModel,
                    "Corrected_" + SanitizeAssetName(targetFilter.transform.name));

                string meshPath = CorrectedMeshFolder + "/" + baked.name + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                    AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(baked, meshPath);
                targetFilter.sharedMesh = baked;
            }

            // Keep the existing texture asset path/GUID. The corrected package's UV
            // map now lives in the replacement body mesh; preserving the material
            // means lighting/shader values remain exactly as before.
            WriteBytesIfChanged(Path.GetFullPath(ExistingTexturePath), textureBytes);
            AssetDatabase.ImportAsset(ExistingTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // Hard invariants requested by the user.
            if (lineAttach.parent != lineParent ||
                !Approximately(lineAttach.localPosition, linePosition) ||
                Quaternion.Angle(lineAttach.localRotation, lineRotation) > 0.0001f ||
                !Approximately(lineAttach.localScale, lineScale))
                throw new InvalidOperationException("Corrected mesh update altered LineAttach. Save aborted.");

            Animator animatorAfter = root.GetComponentInChildren<Animator>(true);
            if (animatorAfter == null || animatorAfter.runtimeAnimatorController != controller)
                throw new InvalidOperationException("Corrected mesh update altered the working Animator/controller. Save aborted.");

            if (!Approximately(root.transform.localPosition, rootPosition) ||
                Quaternion.Angle(root.transform.localRotation, rootRotation) > 0.0001f ||
                !Approximately(root.transform.localScale, rootScale))
                throw new InvalidOperationException("Corrected mesh update altered lure root transform/size/orientation. Save aborted.");

            foreach (var pair in targetTransformSnapshots)
                if (!pair.Value.Matches(pair.Key))
                    throw new InvalidOperationException("Corrected mesh update altered animated transform '" + pair.Key.name + "'. Save aborted.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Mesh BakeSourceMeshIntoTargetSpace(
        MeshFilter sourceFilter,
        Transform sourceModelRoot,
        MeshFilter targetFilter,
        Transform targetModelRoot,
        string meshName)
    {
        Mesh source = sourceFilter.sharedMesh;
        Matrix4x4 sourceToModel = sourceModelRoot.worldToLocalMatrix * sourceFilter.transform.localToWorldMatrix;
        Matrix4x4 targetToModel = targetModelRoot.worldToLocalMatrix * targetFilter.transform.localToWorldMatrix;
        Matrix4x4 sourceLocalToTargetLocal = targetToModel.inverse * sourceToModel;

        Mesh result = new Mesh
        {
            name = meshName,
            indexFormat = source.indexFormat
        };

        Vector3[] sourceVertices = source.vertices;
        Vector3[] vertices = new Vector3[sourceVertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = sourceLocalToTargetLocal.MultiplyPoint3x4(sourceVertices[i]);
        result.vertices = vertices;

        CopyUvChannels(source, result);
        if (source.colors != null && source.colors.Length == source.vertexCount)
            result.colors = source.colors;

        result.subMeshCount = source.subMeshCount;
        for (int sub = 0; sub < source.subMeshCount; sub++)
            result.SetIndices(source.GetIndices(sub), source.GetTopology(sub), sub, false);

        Vector3[] normals = source.normals;
        if (normals != null && normals.Length == source.vertexCount)
        {
            Matrix4x4 normalMatrix = sourceLocalToTargetLocal.inverse.transpose;
            Vector3[] transformedNormals = new Vector3[normals.Length];
            for (int i = 0; i < normals.Length; i++)
                transformedNormals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            result.normals = transformedNormals;
        }
        else
        {
            result.RecalculateNormals();
        }

        Vector4[] tangents = source.tangents;
        if (tangents != null && tangents.Length == source.vertexCount)
        {
            Vector4[] transformedTangents = new Vector4[tangents.Length];
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 dir = sourceLocalToTargetLocal.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                transformedTangents[i] = new Vector4(dir.x, dir.y, dir.z, tangents[i].w);
            }
            result.tangents = transformedTangents;
        }
        else
        {
            try { result.RecalculateTangents(); } catch { }
        }

        result.RecalculateBounds();
        return result;
    }

    private static void CopyUvChannels(Mesh source, Mesh target)
    {
        for (int channel = 0; channel < 8; channel++)
        {
            List<Vector4> values = new List<Vector4>();
            source.GetUVs(channel, values);
            if (values.Count == source.vertexCount)
                target.SetUVs(channel, values);
        }
    }

    private static Dictionary<string, MeshFilter> BuildUniqueFilterMap(IEnumerable<MeshFilter> filters)
    {
        Dictionary<string, MeshFilter> map = new Dictionary<string, MeshFilter>(StringComparer.OrdinalIgnoreCase);
        foreach (MeshFilter filter in filters)
        {
            string key = NormalizeObjectName(filter.transform.name);
            if (map.ContainsKey(key))
                return new Dictionary<string, MeshFilter>();
            map[key] = filter;
        }
        return map;
    }

    private static string NormalizeObjectName(string value)
    {
        return (value ?? string.Empty).Trim().Replace(" ", string.Empty);
    }

    private static bool IsUnderLightingRig(Transform transform, Transform authoredModel)
    {
        Transform current = transform;
        while (current != null && current != authoredModel)
        {
            if (current.name == "AuthoredLightingRig") return true;
            current = current.parent;
        }
        return false;
    }

    private static int CountTriangles(Mesh mesh)
    {
        int triangles = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            if (mesh.GetTopology(i) != MeshTopology.Triangles)
                throw new InvalidOperationException("Corrected source contains non-triangle geometry in " + mesh.name + ".");
            triangles += (int)mesh.GetIndexCount(i) / 3;
        }
        return triangles;
    }

    private static string FindCorrectedZip()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] folders =
        {
            Path.Combine(user, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.GetFullPath(".")
        };

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] candidates = Directory.GetFiles(folder, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(file => NormalizeZipName(Path.GetFileNameWithoutExtension(file)).Contains("liplesscrankbaitgreenstripedstlcorrected"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            for (int i = 0; i < candidates.Length; i++)
                if (IsCorrectedZip(candidates[i]))
                    return candidates[i];
        }
        return null;
    }

    private static bool IsCorrectedZip(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
            ZipArchiveEntry stl = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase));
            ZipArchiveEntry blend = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".blend", StringComparison.OrdinalIgnoreCase));
            return stl != null && blend != null &&
                   HashEquals(ReadAll(stl), ExpectedStlSha256) &&
                   HashEquals(ReadAll(blend), ExpectedBlendSha256);
        }
        catch { return false; }
    }

    private static string ResolveBlenderExecutable()
    {
        string saved = EditorPrefs.GetString(BlenderPrefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return saved;

        List<string> candidates = new List<string>();
        foreach (string baseFolder in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        })
        {
            string blenderRoot = Path.Combine(baseFolder, "Blender Foundation");
            if (!Directory.Exists(blenderRoot)) continue;
            try { candidates.AddRange(Directory.GetFiles(blenderRoot, "blender.exe", SearchOption.AllDirectories)); }
            catch { }
        }

        string found = candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(found))
        {
            EditorPrefs.SetString(BlenderPrefsKey, found);
            return found;
        }

        string selected = EditorUtility.OpenFilePanel("Locate Blender", string.Empty, "exe");
        if (string.IsNullOrWhiteSpace(selected) || !File.Exists(selected))
            throw new FileNotFoundException("Blender.exe is required once to read the corrected package's UV mapping. STL alone cannot store UVs/textures.");
        EditorPrefs.SetString(BlenderPrefsKey, selected);
        return selected;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using Stream input = entry.Open();
        using MemoryStream memory = new MemoryStream();
        input.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool HashEquals(byte[] bytes, string expected)
    {
        using SHA256 sha = SHA256.Create();
        string actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string Sha256File(string path)
    {
        using FileStream input = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void CopyFileIfChanged(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(destination) && string.Equals(Sha256File(source), Sha256File(destination), StringComparison.OrdinalIgnoreCase))
            return;
        File.Copy(source, destination, true);
    }

    private static void WriteBytesIfChanged(string destination, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(destination))
        {
            byte[] old = File.ReadAllBytes(destination);
            if (old.SequenceEqual(bytes)) return;
        }
        File.WriteAllBytes(destination, bytes);
    }

    private static string NormalizeZipName(string value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string SanitizeAssetName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('.', '_').Replace(' ', '_');
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.0000000001f;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void EnsureFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;

        public TransformSnapshot(Transform transform)
        {
            position = transform.localPosition;
            rotation = transform.localRotation;
            scale = transform.localScale;
        }

        public bool Matches(Transform transform)
        {
            return Approximately(transform.localPosition, position) &&
                   Quaternion.Angle(transform.localRotation, rotation) <= 0.0001f &&
                   Approximately(transform.localScale, scale);
        }
    }
}

/// <summary>
/// Sets the corrected geometry source FBX importer BEFORE Unity reads it. This
/// avoids SaveAndReimport loops and guarantees the source carries geometry/UVs only.
/// </summary>
public sealed class LiplessCrankbaitCorrectedSourcePostprocessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!string.Equals(assetPath, LiplessCrankbaitCorrectedModelUpdate.CorrectedSourceFbxPath, StringComparison.OrdinalIgnoreCase))
            return;

        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;

        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.preserveHierarchy = true;
        importer.globalScale = 1f;
        importer.useFileScale = true;
    }
}
