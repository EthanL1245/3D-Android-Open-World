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
/// Builds Fire Shad from the supplied orange/black authored package while using
/// the existing Neon Breach prefab as the gameplay/animation template.
///
/// Only the BODY geometry/UV + body texture are replaced. Both treble-hook
/// meshes, their animated transforms, Animator/controller, LineAttach, lighting
/// rig, root scale/orientation and all gameplay behavior are copied unchanged
/// from the proven Neon Breach crankbait.
/// </summary>
public static class FireShadCrankbaitImporter
{
    private const string BasePrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string OutputPrefabPath = "Assets/Resources/Fishing/FireShadCrankbait.prefab";
    private const string RootFolder = "Assets/_Game/Fishing/FireShad";
    private const string SourceFolder = RootFolder + "/Source";
    internal const string SourceFbxPath = SourceFolder + "/FireShadGeometry.fbx";
    private const string TexturePath = SourceFolder + "/FireShadTexture.jpg";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string BodyMeshPath = MeshFolder + "/FireShadBody.asset";
    private const string MaterialPath = RootFolder + "/FireShadBody.mat";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";

    private const string ExpectedStlSha256 = "ecb16deecf91e3b7a67f239a379a2f6de72a10540e97e5b40280bae5f8d9a499";
    private const string ExpectedBlendSha256 = "48f53e021efbdfeb05a323ce596555772e7e357c3ea5c2cec2f5ae6c2f99eced";
    private const string ExpectedTextureSha256 = "dc64ddb0b074b65bb4d7dd163f74ed363f81c31f3903df33282b13055828dbbe";
    private const int ExpectedTriangleCount = 1720;

    [MenuItem("Tools/Open World/Import Fire Shad Crankbait (One Click)")]
    public static void ImportOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Fire Shad Crankbait", "Exit Play Mode first.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath) == null)
        {
            EditorUtility.DisplayDialog(
                "Fire Shad Crankbait",
                "Neon Breach is missing. The first crankbait must exist because Fire Shad intentionally copies its exact animation, hooks, line attachment and lighting.",
                "OK");
            return;
        }

        string zipPath = FindPackage();
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            zipPath = EditorUtility.OpenFilePanel(
                "Choose Lipless Crankbait Orange with Black Stripes.zip",
                Directory.Exists(downloads) ? downloads : string.Empty,
                "zip");
        }
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        try
        {
            EnsureFolderRecursive(SourceFolder);
            EnsureFolderRecursive(MeshFolder);
            EnsureFolderRecursive("Assets/Resources/Fishing");

            string library = Path.GetFullPath("Library/FireShadCrankbaitImport");
            Directory.CreateDirectory(library);
            string blendPath = Path.Combine(library, "FireShad.blend");
            string exportedFbx = Path.Combine(library, "FireShadGeometry.fbx");

            EditorUtility.DisplayProgressBar("Fire Shad Crankbait", "Validating the supplied model package...", 0.08f);
            ExtractAndValidate(zipPath, blendPath, out byte[] textureBytes);

            EditorUtility.DisplayProgressBar("Fire Shad Crankbait", "Exporting authored geometry and UV mapping...", 0.28f);
            ExportGeometry(ResolveBlenderExecutable(), blendPath, exportedFbx);

            EditorUtility.DisplayProgressBar("Fire Shad Crankbait", "Importing Fire Shad body source...", 0.48f);
            CopyFileIfChanged(exportedFbx, Path.GetFullPath(SourceFbxPath));
            WriteBytesIfChanged(Path.GetFullPath(TexturePath), textureBytes);
            AssetDatabase.ImportAsset(SourceFbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ConfigureTexture();

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
            if (source == null)
                throw new InvalidOperationException("Unity could not load the Fire Shad geometry source.");

            EditorUtility.DisplayProgressBar("Fire Shad Crankbait", "Copying Neon Breach behavior and applying Fire Shad body...", 0.72f);
            BuildFireShadPrefab(source);

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);

            EditorUtility.DisplayDialog(
                "Fire Shad Crankbait Ready",
                "Fire Shad now uses the supplied orange/black body and texture while keeping Neon Breach's exact hook meshes/animation, 2x retrieve animation behavior, LineAttach, line movement, facing, size, orientation and lighting.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Fire Shad Import Failed",
                exception.Message + "\n\nNo changes are made to the Neon Breach prefab. See Console for the full error.",
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
            throw new FileNotFoundException("Fire Shad ZIP was not found.", zipPath);

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
            throw new InvalidDataException("Fire Shad package must contain its STL, Blend source and texture image.");

        byte[] stlBytes = ReadAll(stl);
        byte[] blendBytes = ReadAll(blend);
        textureBytes = ReadAll(texture);

        if (!HashEquals(stlBytes, ExpectedStlSha256) ||
            !HashEquals(blendBytes, ExpectedBlendSha256) ||
            !HashEquals(textureBytes, ExpectedTextureSha256))
            throw new InvalidDataException("Choose the exact supplied 'Lipless Crankbait Orange with Black Stripes.zip'.");

        if (stlBytes.Length < 84 || BitConverter.ToUInt32(stlBytes, 80) != ExpectedTriangleCount)
            throw new InvalidDataException("Fire Shad STL topology does not match the supplied revision.");

        Directory.CreateDirectory(Path.GetDirectoryName(blendPath));
        File.WriteAllBytes(blendPath, blendBytes);
    }

    private static void ExportGeometry(string blender, string blendPath, string destinationFbx)
    {
        string scriptPath = Path.Combine(Path.GetDirectoryName(destinationFbx), "ExportFireShad.py");
        string python =
@"import bpy
import os
import sys

args = sys.argv
out_path = args[args.index('--') + 1]
for obj in bpy.context.scene.objects:
    try:
        obj.select_set(False)
    except Exception:
        pass

selected=[]
for obj in bpy.context.scene.objects:
    if obj.type in {'MESH','CURVE'}:
        try:
            obj.hide_viewport=False
            obj.hide_render=False
            obj.hide_set(False)
            obj.select_set(True)
            selected.append(obj)
        except Exception:
            pass

if len(selected) != 3:
    raise RuntimeError('Expected body + two hook geometry objects, found %d.' % len(selected))

bpy.context.view_layer.objects.active=selected[0]
os.makedirs(os.path.dirname(out_path),exist_ok=True)
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
    raise RuntimeError('Fire Shad FBX export produced no file.')
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
            throw new TimeoutException("Blender took more than 2 minutes to export Fire Shad.");
        }
        if (process.ExitCode != 0 || !File.Exists(destinationFbx))
        {
            Debug.LogError("Fire Shad Blender output:\n" + output + "\n\nErrors:\n" + error);
            throw new InvalidOperationException("Blender could not export Fire Shad geometry.");
        }
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Could not import Fire Shad texture.");
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void BuildFireShadPrefab(GameObject correctedSource)
    {
        MeshFilter[] sourceFilters = correctedSource.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f != null && f.sharedMesh != null).ToArray();
        if (sourceFilters.Length != 3)
            throw new InvalidOperationException("Fire Shad source imported with " + sourceFilters.Length + " mesh parts; expected 3.");

        int triangleCount = sourceFilters.Sum(f => CountTriangles(f.sharedMesh));
        if (triangleCount != ExpectedTriangleCount)
            throw new InvalidOperationException("Fire Shad imported triangle count is " + triangleCount + "; expected " + ExpectedTriangleCount + ".");

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
            if (targetFilters.Length != 3)
                throw new InvalidOperationException("Neon Breach must contain exactly body + two animated hook meshes.");

            MeshFilter targetBody = LargestMesh(targetFilters);
            Renderer bodyRenderer = targetBody.GetComponent<Renderer>();
            if (bodyRenderer == null || bodyRenderer.sharedMaterial == null)
                throw new InvalidOperationException("Neon Breach body material could not be identified.");

            // Snapshot invariants before touching visual data.
            Transform lineParent = lineAttach.parent;
            Vector3 linePos = lineAttach.localPosition;
            Quaternion lineRot = lineAttach.localRotation;
            Vector3 lineScale = lineAttach.localScale;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Vector3 rootPos = root.transform.localPosition;
            Quaternion rootRot = root.transform.localRotation;
            Vector3 rootScale = root.transform.localScale;
            Dictionary<Transform, TransformSnapshot> transforms = targetFilters.ToDictionary(f => f.transform, f => new TransformSnapshot(f.transform));

            Mesh fireBody = BakeSourceMeshIntoTargetSpace(sourceBody, correctedSource.transform, targetBody, authoredModel, "FireShadBody");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath) != null) AssetDatabase.DeleteAsset(BodyMeshPath);
            AssetDatabase.CreateAsset(fireBody, BodyMeshPath);
            targetBody.sharedMesh = fireBody;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) throw new InvalidOperationException("Fire Shad texture did not import.");
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) AssetDatabase.DeleteAsset(MaterialPath);
            Material fireMaterial = new Material(bodyRenderer.sharedMaterial) { name = "Fire Shad Crankbait Body" };
            if (fireMaterial.HasProperty("_BaseMap")) fireMaterial.SetTexture("_BaseMap", texture);
            else fireMaterial.mainTexture = texture;
            if (fireMaterial.HasProperty("_BaseColor")) fireMaterial.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(fireMaterial, MaterialPath);
            Material[] slots = new Material[Mathf.Max(1, bodyRenderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = fireMaterial;
            bodyRenderer.sharedMaterials = slots;

            // IMPORTANT: hook meshes/materials are intentionally NOT replaced.
            // This guarantees both treble hooks remain attached and animate exactly
            // like the already-approved Neon Breach lure.
            root.name = "FireShadCrankbait";

            if (lineAttach.parent != lineParent ||
                !Approximately(lineAttach.localPosition, linePos) ||
                Quaternion.Angle(lineAttach.localRotation, lineRot) > 0.0001f ||
                !Approximately(lineAttach.localScale, lineScale))
                throw new InvalidOperationException("Fire Shad build altered LineAttach. Save aborted.");
            if (root.GetComponentInChildren<Animator>(true).runtimeAnimatorController != controller)
                throw new InvalidOperationException("Fire Shad build altered Animator/controller. Save aborted.");
            if (!Approximately(root.transform.localPosition, rootPos) ||
                Quaternion.Angle(root.transform.localRotation, rootRot) > 0.0001f ||
                !Approximately(root.transform.localScale, rootScale))
                throw new InvalidOperationException("Fire Shad build altered lure size/orientation. Save aborted.");
            foreach (var pair in transforms)
                if (!pair.Value.Matches(pair.Key))
                    throw new InvalidOperationException("Fire Shad build altered animated transform '" + pair.Key.name + "'. Save aborted.");

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
        for (int i = 0; i < filters.Length; i++)
        {
            Renderer renderer = filters[i].GetComponent<Renderer>();
            Bounds b = renderer != null ? renderer.bounds : filters[i].sharedMesh.bounds;
            Vector3 s = b.size;
            float candidate = Mathf.Max(s.x, Mathf.Max(s.y, s.z)) * Mathf.Max(0.000001f, s.x * s.y * s.z);
            if (candidate > score) { score = candidate; best = filters[i]; }
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
                Vector3 dir = sourceLocalToTargetLocal.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                transformed[i] = new Vector4(dir.x, dir.y, dir.z, tangents[i].w);
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
            if (mesh.GetTopology(i) != MeshTopology.Triangles)
                throw new InvalidOperationException("Fire Shad source contains non-triangle geometry.");
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
        string[] folders = { Path.Combine(user,"Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Path.GetFullPath(".") };
        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            string[] matches = Directory.GetFiles(folder,"*.zip",SearchOption.TopDirectoryOnly)
                .Where(f => Normalize(Path.GetFileNameWithoutExtension(f)).Contains("liplesscrankbaitorangewithblackstripes"))
                .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
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
            ZipArchiveEntry texture = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            return stl != null && blend != null && texture != null &&
                   HashEquals(ReadAll(stl),ExpectedStlSha256) && HashEquals(ReadAll(blend),ExpectedBlendSha256) && HashEquals(ReadAll(texture),ExpectedTextureSha256);
        }
        catch { return false; }
    }

    private static string ResolveBlenderExecutable()
    {
        string saved = EditorPrefs.GetString(BlenderPrefsKey,string.Empty);
        if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return saved;
        List<string> candidates = new List<string>();
        foreach (string baseFolder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
        {
            string root = Path.Combine(baseFolder,"Blender Foundation");
            if (!Directory.Exists(root)) continue;
            try { candidates.AddRange(Directory.GetFiles(root,"blender.exe",SearchOption.AllDirectories)); } catch { }
        }
        string found = candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(found)) { EditorPrefs.SetString(BlenderPrefsKey,found); return found; }
        string selected = EditorUtility.OpenFilePanel("Locate Blender",string.Empty,"exe");
        if (string.IsNullOrWhiteSpace(selected) || !File.Exists(selected)) throw new FileNotFoundException("Blender.exe is required once to read Fire Shad's UV mapping.");
        EditorPrefs.SetString(BlenderPrefsKey,selected); return selected;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using Stream input = entry.Open();
        using MemoryStream memory = new MemoryStream();
        input.CopyTo(memory); return memory.ToArray();
    }

    private static bool HashEquals(byte[] bytes, string expected)
    {
        using SHA256 sha = SHA256.Create();
        string actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty).ToLowerInvariant();
        return string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase);
    }

    private static string Sha256File(string path)
    {
        using FileStream input = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(input)).Replace("-",string.Empty).ToLowerInvariant();
    }

    private static void CopyFileIfChanged(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(destination) && string.Equals(Sha256File(source),Sha256File(destination),StringComparison.OrdinalIgnoreCase)) return;
        File.Copy(source,destination,true);
    }

    private static void WriteBytesIfChanged(string destination, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(destination) && File.ReadAllBytes(destination).SequenceEqual(bytes)) return;
        File.WriteAllBytes(destination,bytes);
    }

    private static string Normalize(string value) => (value??string.Empty).Replace(" ",string.Empty).Replace("_",string.Empty).Replace("-",string.Empty).ToLowerInvariant();
    private static string Quote(string value) => "\"" + value.Replace("\"","\\\"") + "\"";
    private static bool Approximately(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<=0.0000000001f;

    private static Transform FindDeepChild(Transform root,string name)
    {
        Transform[] all=root.GetComponentsInChildren<Transform>(true);
        for(int i=0;i<all.Length;i++)if(all[i]!=null && all[i].name==name)return all[i];
        return null;
    }

    private static void EnsureFolderRecursive(string path)
    {
        string[] parts=path.Split('/');string current="Assets";
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;
        public TransformSnapshot(Transform t){position=t.localPosition;rotation=t.localRotation;scale=t.localScale;}
        public bool Matches(Transform t)=>Approximately(t.localPosition,position) && Quaternion.Angle(t.localRotation,rotation)<=0.0001f && Approximately(t.localScale,scale);
    }
}

/// <summary>Applies geometry-only import settings before Unity reads Fire Shad FBX.</summary>
public sealed class FireShadGeometryPostprocessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if(!string.Equals(assetPath,FireShadCrankbaitImporter.SourceFbxPath,StringComparison.OrdinalIgnoreCase))return;
        ModelImporter importer=assetImporter as ModelImporter;
        if(importer==null)return;
        importer.importAnimation=false;
        importer.importCameras=false;
        importer.importLights=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importNormals=ModelImporterNormals.Import;
        importer.importTangents=ModelImporterTangents.CalculateMikk;
        importer.preserveHierarchy=true;
        importer.globalScale=1f;
        importer.useFileScale=true;
    }
}
