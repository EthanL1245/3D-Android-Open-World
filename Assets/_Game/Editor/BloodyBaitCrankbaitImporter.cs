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
/// Imports the supplied blue/red Bloody Bait body while preserving the proven
/// Neon Breach crankbait presentation: hook meshes/animation, Animator,
/// LineAttach, lighting, root scale/orientation and runtime retrieve behavior.
/// </summary>
public static class BloodyBaitCrankbaitImporter
{
    private const string DisplayName = "Bloody Bait Crankbait";
    private const string PackageFileName = "Lipless Crankbait Blue with Red Head.zip";
    private const string BasePrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string OutputPrefabPath = "Assets/Resources/Fishing/BloodyBaitCrankbait.prefab";
    private const string RootFolder = "Assets/_Game/Fishing/BloodyBait";
    private const string SourceFolder = RootFolder + "/Source";
    internal const string SourceFbxPath = SourceFolder + "/BodyGeometry.fbx";
    private const string TexturePath = SourceFolder + "/BodyTexture.jpg";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string BodyMeshPath = MeshFolder + "/Body.asset";
    private const string MaterialPath = RootFolder + "/Body.mat";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";
    private const string AutoSessionKey = "OpenWorld.AutoImport.BloodyBait.20260925";

    private const string ExpectedStlSha256 = "f69504acf187ebd6ff4230f5031434263df1d8fdfdce4b831f810b60178c12a3";
    private const string ExpectedBlendSha256 = "b377bc37ef62f940c4558818d26d071a13ca33ccd15ba1916cf0c6cfe6943565";
    private const string ExpectedTextureSha256 = "9359cd871e246d400048582eb44447fccefa10687e3b399b1eea72352803bc57";
    private const int ExpectedTriangleCount = 1720;

    [InitializeOnLoadMethod]
    private static void AutoImportWhenReady()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoSessionKey, false)) return;
        SessionState.SetBool(AutoSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath) != null) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) == null) return;
            string package = FindPackage();
            if (string.IsNullOrWhiteSpace(package)) return;
            if (!TryResolveBlenderExecutable(false, out string blender))
            {
                Debug.Log("Bloody Bait package is ready. Use Tools/Open World/Import Bloody Bait Crankbait (One Click) if Blender has not been located yet.");
                return;
            }

            try
            {
                Import(package, blender);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Automatically imported Bloody Bait Crankbait.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        };
    }

    [MenuItem("Tools/Open World/Import Bloody Bait Crankbait (One Click)")]
    public static void ImportOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(DisplayName, "Exit Play Mode first.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) == null)
        {
            EditorUtility.DisplayDialog(DisplayName, "Neon Breach is missing. It is the approved template for animation, hooks, LineAttach, size, orientation and lighting.", "OK");
            return;
        }

        string package = FindPackage();
        if (string.IsNullOrWhiteSpace(package))
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            package = EditorUtility.OpenFilePanel("Choose " + PackageFileName, Directory.Exists(downloads) ? downloads : string.Empty, "zip");
            if (string.IsNullOrWhiteSpace(package)) return;
        }

        try
        {
            if (!TryResolveBlenderExecutable(true, out string blender)) return;
            Import(package, blender);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
            EditorUtility.DisplayDialog(
                "Bloody Bait Ready",
                "Bloody Bait Crankbait now uses the supplied blue/red authored body while keeping the existing crankbait hooks, animation, LineAttach, retrieve behavior, size and orientation unchanged.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Bloody Bait Import Failed", exception.Message + "\n\nThe existing lure prefabs were not modified. See Console for the full error.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void Import(string zipPath, string blender)
    {
        EnsureFolderRecursive(SourceFolder);
        EnsureFolderRecursive(MeshFolder);
        EnsureFolderRecursive("Assets/Resources/Fishing");

        string library = Path.GetFullPath("Library/BloodyBaitCrankbaitImport");
        Directory.CreateDirectory(library);
        string blendPath = Path.Combine(library, "Source.blend");
        string exportedFbx = Path.Combine(library, "BodyGeometry.fbx");

        EditorUtility.DisplayProgressBar(DisplayName, "Validating supplied model package...", 0.08f);
        ExtractAndValidate(zipPath, blendPath, out byte[] textureBytes);

        EditorUtility.DisplayProgressBar(DisplayName, "Exporting authored body geometry and UV mapping...", 0.28f);
        ExportGeometry(blender, blendPath, exportedFbx);

        EditorUtility.DisplayProgressBar(DisplayName, "Importing body and texture...", 0.48f);
        CopyFileIfChanged(exportedFbx, Path.GetFullPath(SourceFbxPath));
        WriteBytesIfChanged(Path.GetFullPath(TexturePath), textureBytes);
        AssetDatabase.ImportAsset(SourceFbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ConfigureTexture();

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
        if (source == null) throw new InvalidOperationException("Unity could not load Bloody Bait geometry source.");

        EditorUtility.DisplayProgressBar(DisplayName, "Applying appearance to proven crankbait template...", 0.74f);
        BuildPrefab(source);
    }

    private static void ExtractAndValidate(string zipPath, string blendPath, out byte[] textureBytes)
    {
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
            throw new InvalidDataException("Bloody Bait package must contain its STL, Blend source and texture image.");

        byte[] stlBytes = ReadAll(stl);
        byte[] blendBytes = ReadAll(blend);
        textureBytes = ReadAll(texture);
        if (!HashEquals(stlBytes, ExpectedStlSha256) || !HashEquals(blendBytes, ExpectedBlendSha256) || !HashEquals(textureBytes, ExpectedTextureSha256))
            throw new InvalidDataException("Choose the exact supplied 'Lipless Crankbait Blue with Red Head.zip'.");
        if (stlBytes.Length < 84 || BitConverter.ToUInt32(stlBytes, 80) != ExpectedTriangleCount)
            throw new InvalidDataException("Bloody Bait STL topology does not match the supplied revision.");

        Directory.CreateDirectory(Path.GetDirectoryName(blendPath));
        File.WriteAllBytes(blendPath, blendBytes);
    }

    private static void ExportGeometry(string blender, string blendPath, string destinationFbx)
    {
        string scriptPath = Path.Combine(Path.GetDirectoryName(destinationFbx), "ExportBody.py");
        string python =
@"import bpy
import os
import sys
args=sys.argv
out_path=args[args.index('--')+1]
for obj in bpy.context.scene.objects:
    try: obj.select_set(False)
    except Exception: pass
selected=[]
for obj in bpy.context.scene.objects:
    if obj.type in {'MESH','CURVE'}:
        try:
            obj.hide_viewport=False
            obj.hide_render=False
            obj.hide_set(False)
            obj.select_set(True)
            selected.append(obj)
        except Exception: pass
if len(selected)!=3:
    raise RuntimeError('Expected body + two hook geometry objects, found %d.' % len(selected))
bpy.context.view_layer.objects.active=selected[0]
os.makedirs(os.path.dirname(out_path),exist_ok=True)
if os.path.exists(out_path): os.remove(out_path)
bpy.ops.export_scene.fbx(filepath=out_path,use_selection=True,apply_unit_scale=True,add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
if not os.path.exists(out_path): raise RuntimeError('FBX export produced no file.')
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
        if (process == null) throw new InvalidOperationException("Blender could not be started.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120000))
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException("Blender took more than 2 minutes to export Bloody Bait.");
        }
        if (process.ExitCode != 0 || !File.Exists(destinationFbx))
        {
            Debug.LogError("Bloody Bait Blender output:\n" + output + "\n\nErrors:\n" + error);
            throw new InvalidOperationException("Blender could not export Bloody Bait geometry.");
        }
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Could not import Bloody Bait texture.");
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void BuildPrefab(GameObject source)
    {
        MeshFilter[] sourceFilters = source.GetComponentsInChildren<MeshFilter>(true).Where(f => f != null && f.sharedMesh != null).ToArray();
        if (sourceFilters.Length != 3) throw new InvalidOperationException("Bloody Bait source imported with " + sourceFilters.Length + " mesh parts; expected 3.");
        int triangles = sourceFilters.Sum(f => CountTriangles(f.sharedMesh));
        if (triangles != ExpectedTriangleCount) throw new InvalidOperationException("Bloody Bait imported triangle count is " + triangles + "; expected " + ExpectedTriangleCount + ".");

        MeshFilter sourceBody = LargestMesh(sourceFilters);
        GameObject root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
        try
        {
            Transform authoredModel = FindDeepChild(root.transform, "AuthoredModel");
            Transform lineAttach = FindDeepChild(root.transform, "LineAttach");
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (authoredModel == null || lineAttach == null || animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("Neon Breach prefab is missing its authored model, animation controller or LineAttach.");

            MeshFilter[] targetFilters = authoredModel.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f != null && f.sharedMesh != null && !IsUnderLightingRig(f.transform, authoredModel)).ToArray();
            if (targetFilters.Length != 3) throw new InvalidOperationException("Neon Breach must contain exactly body + two animated hook meshes.");

            MeshFilter targetBody = LargestMesh(targetFilters);
            Renderer bodyRenderer = targetBody.GetComponent<Renderer>();
            if (bodyRenderer == null || bodyRenderer.sharedMaterial == null) throw new InvalidOperationException("Neon Breach body material could not be identified.");

            Transform lineParent = lineAttach.parent;
            Vector3 linePos = lineAttach.localPosition;
            Quaternion lineRot = lineAttach.localRotation;
            Vector3 lineScale = lineAttach.localScale;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Vector3 rootPos = root.transform.localPosition;
            Quaternion rootRot = root.transform.localRotation;
            Vector3 rootScale = root.transform.localScale;
            Dictionary<Transform, TransformSnapshot> transforms = targetFilters.ToDictionary(f => f.transform, f => new TransformSnapshot(f.transform));

            Mesh body = BakeSourceMeshIntoTargetSpace(sourceBody, source.transform, targetBody, authoredModel, "BloodyBaitCrankbaitBody");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath) != null) AssetDatabase.DeleteAsset(BodyMeshPath);
            AssetDatabase.CreateAsset(body, BodyMeshPath);
            targetBody.sharedMesh = body;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) throw new InvalidOperationException("Bloody Bait texture did not import.");
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) AssetDatabase.DeleteAsset(MaterialPath);
            Material material = new Material(bodyRenderer.sharedMaterial) { name = "Bloody Bait Crankbait Body" };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture); else material.mainTexture = texture;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, MaterialPath);
            Material[] slots = new Material[Mathf.Max(1, bodyRenderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            bodyRenderer.sharedMaterials = slots;
            root.name = "BloodyBaitCrankbait";

            if (lineAttach.parent != lineParent || !Approximately(lineAttach.localPosition, linePos) || Quaternion.Angle(lineAttach.localRotation, lineRot) > 0.0001f || !Approximately(lineAttach.localScale, lineScale))
                throw new InvalidOperationException("Bloody Bait build altered LineAttach. Save aborted.");
            if (root.GetComponentInChildren<Animator>(true).runtimeAnimatorController != controller)
                throw new InvalidOperationException("Bloody Bait build altered Animator/controller. Save aborted.");
            if (!Approximately(root.transform.localPosition, rootPos) || Quaternion.Angle(root.transform.localRotation, rootRot) > 0.0001f || !Approximately(root.transform.localScale, rootScale))
                throw new InvalidOperationException("Bloody Bait build altered lure size/orientation. Save aborted.");
            foreach (var pair in transforms)
                if (!pair.Value.Matches(pair.Key)) throw new InvalidOperationException("Bloody Bait build altered animated transform '" + pair.Key.name + "'. Save aborted.");

            PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static MeshFilter LargestMesh(MeshFilter[] filters)
    {
        MeshFilter best = filters[0];
        float score = -1f;
        foreach (MeshFilter filter in filters)
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            Bounds bounds = renderer != null ? renderer.bounds : filter.sharedMesh.bounds;
            Vector3 size = bounds.size;
            float candidate = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * Mathf.Max(0.000001f, size.x * size.y * size.z);
            if (candidate > score) { score = candidate; best = filter; }
        }
        return best;
    }

    private static Mesh BakeSourceMeshIntoTargetSpace(MeshFilter sourceFilter, Transform sourceRoot, MeshFilter targetFilter, Transform targetRoot, string meshName)
    {
        Mesh source = sourceFilter.sharedMesh;
        Matrix4x4 sourceToModel = sourceRoot.worldToLocalMatrix * sourceFilter.transform.localToWorldMatrix;
        Matrix4x4 targetToModel = targetRoot.worldToLocalMatrix * targetFilter.transform.localToWorldMatrix;
        Matrix4x4 sourceLocalToTargetLocal = targetToModel.inverse * sourceToModel;

        Mesh result = new Mesh { name = meshName, indexFormat = source.indexFormat };
        Vector3[] sourceVertices = source.vertices;
        Vector3[] vertices = new Vector3[sourceVertices.Length];
        for (int i = 0; i < vertices.Length; i++) vertices[i] = sourceLocalToTargetLocal.MultiplyPoint3x4(sourceVertices[i]);
        result.vertices = vertices;

        for (int channel = 0; channel < 8; channel++)
        {
            List<Vector4> uv = new List<Vector4>();
            source.GetUVs(channel, uv);
            if (uv.Count == source.vertexCount) result.SetUVs(channel, uv);
        }
        if (source.colors != null && source.colors.Length == source.vertexCount) result.colors = source.colors;
        result.subMeshCount = source.subMeshCount;
        for (int sub = 0; sub < source.subMeshCount; sub++) result.SetIndices(source.GetIndices(sub), source.GetTopology(sub), sub, false);

        Vector3[] normals = source.normals;
        if (normals != null && normals.Length == source.vertexCount)
        {
            Matrix4x4 normalMatrix = sourceLocalToTargetLocal.inverse.transpose;
            Vector3[] transformed = new Vector3[normals.Length];
            for (int i = 0; i < normals.Length; i++) transformed[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            result.normals = transformed;
        }
        else result.RecalculateNormals();

        Vector4[] tangents = source.tangents;
        if (tangents != null && tangents.Length == source.vertexCount)
        {
            Vector4[] transformed = new Vector4[tangents.Length];
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 direction = sourceLocalToTargetLocal.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                transformed[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w);
            }
            result.tangents = transformed;
        }
        else { try { result.RecalculateTangents(); } catch { } }
        result.RecalculateBounds();
        return result;
    }

    private static int CountTriangles(Mesh mesh)
    {
        int count = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            if (mesh.GetTopology(i) != MeshTopology.Triangles) throw new InvalidOperationException("Bloody Bait source contains non-triangle geometry.");
            count += (int)mesh.GetIndexCount(i) / 3;
        }
        return count;
    }

    private static bool IsUnderLightingRig(Transform transform, Transform authoredModel)
    {
        for (Transform current = transform; current != null && current != authoredModel; current = current.parent)
            if (current.name == "AuthoredLightingRig") return true;
        return false;
    }

    private static string FindPackage()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] directCandidates =
        {
            Path.GetFullPath(PackageFileName),
            Path.Combine(user, "Downloads", PackageFileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), PackageFileName)
        };
        for (int i = 0; i < directCandidates.Length; i++)
            if (File.Exists(directCandidates[i]) && IsExactPackage(directCandidates[i])) return directCandidates[i];

        string[] folders =
        {
            Path.Combine(user, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.GetFullPath(".")
        };
        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] matches;
            try { matches = Directory.GetFiles(folder, "*.zip", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).ToArray(); }
            catch { continue; }
            for (int i = 0; i < matches.Length; i++) if (IsExactPackage(matches[i])) return matches[i];
        }
        return null;
    }

    private static bool IsExactPackage(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
            ZipArchiveEntry stl = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase));
            ZipArchiveEntry blend = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".blend", StringComparison.OrdinalIgnoreCase));
            ZipArchiveEntry texture = archive.Entries.FirstOrDefault(e =>
            {
                string ext = Path.GetExtension(e.FullName).ToLowerInvariant();
                return ext == ".jpg" || ext == ".jpeg" || ext == ".png";
            });
            return stl != null && blend != null && texture != null &&
                   HashEquals(ReadAll(stl), ExpectedStlSha256) &&
                   HashEquals(ReadAll(blend), ExpectedBlendSha256) &&
                   HashEquals(ReadAll(texture), ExpectedTextureSha256);
        }
        catch { return false; }
    }

    private static bool TryResolveBlenderExecutable(bool allowPrompt, out string blender)
    {
        blender = EditorPrefs.GetString(BlenderPrefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(blender) && File.Exists(blender)) return true;
        List<string> candidates = new List<string>();
        foreach (string baseFolder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            string root = Path.Combine(baseFolder, "Blender Foundation");
            if (!Directory.Exists(root)) continue;
            try { candidates.AddRange(Directory.GetFiles(root, "blender.exe", SearchOption.AllDirectories)); } catch { }
        }
        blender = candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(blender)) { EditorPrefs.SetString(BlenderPrefsKey, blender); return true; }
        if (!allowPrompt) return false;
        blender = EditorUtility.OpenFilePanel("Locate Blender", string.Empty, "exe");
        if (string.IsNullOrWhiteSpace(blender) || !File.Exists(blender)) return false;
        EditorPrefs.SetString(BlenderPrefsKey, blender);
        return true;
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
        if (File.Exists(destination) && string.Equals(Sha256File(source), Sha256File(destination), StringComparison.OrdinalIgnoreCase)) return;
        File.Copy(source, destination, true);
    }

    private static void WriteBytesIfChanged(string destination, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(destination) && File.ReadAllBytes(destination).SequenceEqual(bytes)) return;
        File.WriteAllBytes(destination, bytes);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 0.0000000001f;

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static void EnsureFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;
        public TransformSnapshot(Transform transform) { position = transform.localPosition; rotation = transform.localRotation; scale = transform.localScale; }
        public bool Matches(Transform transform) => Approximately(transform.localPosition, position) && Quaternion.Angle(transform.localRotation, rotation) <= 0.0001f && Approximately(transform.localScale, scale);
    }
}

/// <summary>Geometry-only import settings for the generated Bloody Bait FBX.</summary>
public sealed class BloodyBaitGeometryPostprocessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!string.Equals(assetPath, BloodyBaitCrankbaitImporter.SourceFbxPath, StringComparison.OrdinalIgnoreCase)) return;
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
