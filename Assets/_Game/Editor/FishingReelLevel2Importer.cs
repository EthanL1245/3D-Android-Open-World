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
using Object = UnityEngine.Object;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

/// <summary>
/// Builds the Level 2 reel as a separate FishingRodReel prefab. The starter rod,
/// ReelMount, RodTip, transform hierarchy and authored reel Animation are cloned
/// unchanged; only the five visible reel meshes/material are replaced from the
/// supplied Level 2 Blender model.
/// </summary>
public static class FishingReelLevel2Importer
{
    private const string PackageName = "Level 2 Fishing Reel.zip";
    private const string StarterPrefabPath = "Assets/Resources/Fishing/FishingRodReel.prefab";
    private const string Level2PrefabPath = "Assets/Resources/Fishing/FishingRodReelLevel2.prefab";
    private const string Root = "Assets/_Game/Fishing/RodReel/Level2";
    private const string Source = Root + "/Source";
    private const string Generated = Root + "/Generated";
    private const string TexturePath = Source + "/Level2FishingReelTexture.png";
    private const string MaterialPath = Generated + "/Level2FishingReel.mat";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";
    private const string AutoSessionKey = "OpenWorld.AutoImport.FishingReelLevel2.20260925.v1";
    private const uint ExpectedTriangles = 1772;

    private static readonly string[] ReelParts =
    {
        "ReelFootAndBody", "Rotor", "ReelHousing", "Spool", "Handle"
    };

    [Serializable] private sealed class GeometryFile { public GeometryPart[] parts; }
    [Serializable] private sealed class GeometryPart
    {
        public string name;
        public float[] position;
        public float[] vertices;
        public float[] normals;
        public float[] uv;
        public int[] triangles;
    }

    [InitializeOnLoadMethod]
    private static void AutoImport()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoSessionKey, false)) return;
        SessionState.SetBool(AutoSessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath) == null) return;
            string package = FindPackage();
            if (string.IsNullOrWhiteSpace(package)) return;
            if (!TryResolveBlender(false, out string blender)) return;
            try
            {
                Import(package, blender);
                Debug.Log("Level 2 Fishing Reel imported. Starter reel mechanics/animation were preserved.");
            }
            catch (Exception e) { Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); }
        };
    }

    [MenuItem("Tools/Open World/Import Level 2 Fishing Reel (One Click)")]
    public static void ImportOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Level 2 Fishing Reel", "Exit Play Mode first.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Level 2 Fishing Reel", "Starter FishingRodReel.prefab is missing. Install the starter rod/reel first.", "OK");
            return;
        }

        string package = FindPackage();
        if (string.IsNullOrWhiteSpace(package))
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            package = EditorUtility.OpenFilePanel("Choose " + PackageName, Directory.Exists(downloads) ? downloads : string.Empty, "zip");
            if (string.IsNullOrWhiteSpace(package)) return;
        }

        try
        {
            if (!TryResolveBlender(true, out string blender)) return;
            Import(package, blender);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Level2PrefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("Level 2 Fishing Reel Ready", "The Level 2 reel is installed. It keeps the starter reel animation/mechanics and is available in the Tackle Store for 1,500 coins.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Level 2 Fishing Reel Failed", e.Message + "\n\nSee the Unity Console for details.", "OK");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void Import(string zipPath, string blender)
    {
        EnsureFolder(Source);
        EnsureFolder(Generated);
        EnsureFolder("Assets/Resources/Fishing");

        string library = Path.GetFullPath("Library/FishingReelLevel2Import");
        Directory.CreateDirectory(library);
        string blendPath = Path.Combine(library, "Level 2 Fishing Reel.blend");
        string jsonPath = Path.Combine(library, "Level2FishingReelGeometry.json");

        EditorUtility.DisplayProgressBar("Level 2 Fishing Reel", "Reading supplied model...", 0.08f);
        ExtractPackage(zipPath, blendPath, out byte[] textureBytes);

        EditorUtility.DisplayProgressBar("Level 2 Fishing Reel", "Reading Blender geometry + UVs...", 0.30f);
        ExportGeometryJson(blender, blendPath, jsonPath);
        GeometryFile geometry = JsonUtility.FromJson<GeometryFile>(File.ReadAllText(jsonPath));
        ValidateGeometry(geometry);

        EditorUtility.DisplayProgressBar("Level 2 Fishing Reel", "Importing texture...", 0.52f);
        WriteBytesIfChanged(Path.GetFullPath(TexturePath), textureBytes);
        ConfigureTexture();

        EditorUtility.DisplayProgressBar("Level 2 Fishing Reel", "Building Level 2 reel prefab...", 0.74f);
        BuildPrefab(geometry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ExtractPackage(string zipPath, string blendPath, out byte[] textureBytes)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException(PackageName + " was not found.", zipPath);
        using FileStream stream = File.OpenRead(zipPath);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry blend = FindNamedEntry(archive, "Level 2 Fishing Reel.blend") ?? SingleEntryWithExtension(archive, ".blend");
        ZipArchiveEntry stl = FindNamedEntry(archive, "Level 2 Fishing Reel.stl") ?? SingleEntryWithExtension(archive, ".stl");
        ZipArchiveEntry texture = FindNamedEntry(archive, "Fishing Reel Textures.png") ?? SingleImageEntry(archive);
        if (blend == null || stl == null || texture == null)
            throw new InvalidDataException("Level 2 Fishing Reel.zip must contain one .blend, one .stl and its texture image.");

        byte[] blendBytes = ReadAll(blend);
        byte[] stlBytes = ReadAll(stl);
        textureBytes = ReadAll(texture);
        if (blendBytes.Length == 0) throw new InvalidDataException("The Level 2 .blend file is empty.");
        if (stlBytes.Length < 84 || BitConverter.ToUInt32(stlBytes, 80) != ExpectedTriangles)
            throw new InvalidDataException("The Level 2 reel STL does not match the expected 1772-triangle model.");
        if (!LooksLikeImage(textureBytes)) throw new InvalidDataException("The Level 2 reel texture is not a valid PNG/JPEG image.");

        Directory.CreateDirectory(Path.GetDirectoryName(blendPath));
        File.WriteAllBytes(blendPath, blendBytes);
    }

    private static void ExportGeometryJson(string blender, string blendPath, string jsonPath)
    {
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        string scriptPath = Path.Combine(Path.GetDirectoryName(jsonPath), "ExportLevel2FishingReel.py");
        string python =
@"import bpy, json, os, sys
from mathutils import Matrix
args=sys.argv
out_path=args[args.index('--')+1]
name_map={'Plane':'ReelFootAndBody','Cylinder':'Rotor','Cylinder.001':'ReelHousing','Cylinder.002':'Spool','Cylinder.003':'Handle'}
scene=bpy.context.scene
scene.frame_set(1)
objs={o.name:o for o in scene.objects if o.type=='MESH'}
missing=[n for n in name_map if n not in objs]
if missing:
    raise RuntimeError('Level 2 reel Blend is missing expected mesh object(s): '+', '.join(missing))
basis=Matrix(((1,0,0),(0,-1,0),(0,0,-1))) * 0.04
parts=[]
depsgraph=bpy.context.evaluated_depsgraph_get()
for source_name,target_name in name_map.items():
    obj=objs[source_name]
    eval_obj=obj.evaluated_get(depsgraph)
    mesh=eval_obj.to_mesh(preserve_all_data_layers=True,depsgraph=depsgraph)
    try:
        mesh.calc_loop_triangles()
        rest=obj.matrix_local.copy()
        linear=basis @ rest.to_3x3()
        normal_matrix=linear.inverted().transposed()
        pos=basis @ rest.translation
        uv_layer=mesh.uv_layers.active.data if mesh.uv_layers.active else None
        vertices=[]; normals=[]; uvs=[]; triangles=[]
        for tri in mesh.loop_triangles:
            idx=[]
            for loop_index in tri.loops:
                loop=mesh.loops[loop_index]
                v=mesh.vertices[loop.vertex_index]
                p=linear @ v.co
                n=(normal_matrix @ loop.normal).normalized()
                uv=uv_layer[loop_index].uv if uv_layer else (0.0,0.0)
                idx.append(len(vertices)//3)
                vertices.extend((p.x,p.y,p.z))
                normals.extend((n.x,n.y,n.z))
                uvs.extend((float(uv[0]),float(uv[1])))
            if linear.determinant()<0: idx[1],idx[2]=idx[2],idx[1]
            triangles.extend(idx)
        parts.append({'name':target_name,'position':[pos.x,pos.y,pos.z],'vertices':vertices,'normals':normals,'uv':uvs,'triangles':triangles})
    finally:
        eval_obj.to_mesh_clear()
os.makedirs(os.path.dirname(out_path),exist_ok=True)
with open(out_path,'w',encoding='utf-8') as f: json.dump({'parts':parts},f,separators=(',',':'))
";
        File.WriteAllText(scriptPath, python);

        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = blender,
            Arguments = "--background " + Quote(blendPath) + " --python " + Quote(scriptPath) + " -- " + Quote(jsonPath),
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
            throw new TimeoutException("Blender took more than two minutes to read the Level 2 reel.");
        }
        if (process.ExitCode != 0 || !File.Exists(jsonPath))
        {
            Debug.LogError("Level 2 reel Blender output:\n" + output + "\n\nErrors:\n" + error);
            throw new InvalidOperationException("Blender could not export the Level 2 reel geometry. The full error is in the Console.");
        }
    }

    private static void ValidateGeometry(GeometryFile file)
    {
        if (file == null || file.parts == null || file.parts.Length != ReelParts.Length)
            throw new InvalidDataException("Level 2 reel geometry did not contain exactly five reel parts.");
        if (!new HashSet<string>(file.parts.Select(p => p.name)).SetEquals(ReelParts))
            throw new InvalidDataException("Level 2 reel part names do not match the existing animated reel hierarchy.");
        long triangles = 0;
        foreach (GeometryPart part in file.parts)
        {
            if (part.position == null || part.position.Length != 3 || part.vertices == null || part.vertices.Length == 0 || part.vertices.Length % 3 != 0 ||
                part.normals == null || part.normals.Length != part.vertices.Length || part.uv == null || part.uv.Length != part.vertices.Length / 3 * 2 ||
                part.triangles == null || part.triangles.Length == 0 || part.triangles.Length % 3 != 0)
                throw new InvalidDataException("Invalid Level 2 reel mesh data for " + part.name + ".");
            triangles += part.triangles.Length / 3;
        }
        if (triangles != ExpectedTriangles)
            throw new InvalidDataException("Level 2 reel geometry contains " + triangles + " triangles; expected " + ExpectedTriangles + ".");
    }

    private static void BuildPrefab(GeometryFile geometry)
    {
        GameObject starter = AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath);
        if (starter == null) throw new InvalidOperationException("Starter FishingRodReel prefab is missing.");

        GameObject root = PrefabUtility.InstantiatePrefab(starter) as GameObject;
        if (root == null) root = Object.Instantiate(starter);
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "FishingRodReelLevel2";

            Transform reel = FindDeepChild(root.transform, "ReelMount");
            FishingRodView rodView = root.GetComponent<FishingRodView>();
            Animation animation = reel != null ? reel.GetComponent<Animation>() : null;
            if (reel == null || rodView == null || rodView.RodTip == null || animation == null || animation.clip == null)
                throw new InvalidOperationException("Starter prefab is missing ReelMount, RodTip or authored reel animation.");

            TransformSnapshot reelSnapshot = new TransformSnapshot(reel);
            TransformSnapshot tipSnapshot = new TransformSnapshot(rodView.RodTip);
            AnimationClip originalClip = animation.clip;
            var targets = new Dictionary<string,Transform>();
            var snapshots = new Dictionary<string,TransformSnapshot>();
            foreach (string name in ReelParts)
            {
                Transform target = reel.Find(name);
                if (target == null || target.GetComponent<MeshFilter>() == null || target.GetComponent<MeshRenderer>() == null)
                    throw new InvalidOperationException("Starter reel is missing visual part: " + name);
                targets[name] = target;
                snapshots[name] = new TransformSnapshot(target);
            }

            Material material = BuildMaterial();
            foreach (GeometryPart part in geometry.parts)
            {
                Transform target = targets[part.name];
                Mesh mesh = BuildMesh(part, target.localPosition);
                string meshPath = Generated + "/" + part.name + ".asset";
                Mesh persistent = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (persistent == null)
                {
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    persistent = mesh;
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, persistent);
                    Object.DestroyImmediate(mesh);
                    EditorUtility.SetDirty(persistent);
                }
                target.GetComponent<MeshFilter>().sharedMesh = persistent;
                target.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            if (!reelSnapshot.Matches(reel) || !tipSnapshot.Matches(rodView.RodTip) || animation.clip != originalClip)
                throw new InvalidOperationException("Level 2 import attempted to change starter mechanics/animation transforms.");
            foreach (string name in ReelParts)
                if (!snapshots[name].Matches(targets[name]))
                    throw new InvalidOperationException("Level 2 import changed animated transform '" + name + "'.");

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, Level2PrefabPath);
            if (saved == null) throw new InvalidOperationException("Unity could not save FishingRodReelLevel2.prefab.");
        }
        finally { if (root != null) Object.DestroyImmediate(root); }
    }

    private readonly struct TransformSnapshot
    {
        private readonly Transform parent;
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;
        public TransformSnapshot(Transform t){parent=t.parent;position=t.localPosition;rotation=t.localRotation;scale=t.localScale;}
        public bool Matches(Transform t)=>t.parent==parent && Vector3.Distance(t.localPosition,position)<0.000001f && Quaternion.Angle(t.localRotation,rotation)<0.0001f && Vector3.Distance(t.localScale,scale)<0.000001f;
    }

    private static Mesh BuildMesh(GeometryPart part, Vector3 preservedTargetPosition)
    {
        int count = part.vertices.Length / 3;
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var uv = new Vector2[count];
        Vector3 authoredOrigin = new Vector3(part.position[0],part.position[1],part.position[2]);
        Vector3 offset = authoredOrigin - preservedTargetPosition;
        for (int i=0;i<count;i++)
        {
            int v=i*3,u=i*2;
            vertices[i]=new Vector3(part.vertices[v],part.vertices[v+1],part.vertices[v+2])+offset;
            normals[i]=new Vector3(part.normals[v],part.normals[v+1],part.normals[v+2]);
            uv[i]=new Vector2(part.uv[u],part.uv[u+1]);
        }
        Mesh mesh=new Mesh{name=part.name+"_Level2"};
        if(count>65535)mesh.indexFormat=IndexFormat.UInt32;
        mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=part.triangles;
        mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }

    private static Material BuildMaterial()
    {
        Shader shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Fishing/FishingEquipment.shader");
        if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("FishingEquipment shader is missing or has errors.");
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if(texture==null)throw new InvalidOperationException("Level 2 reel texture did not import.");
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null){material=new Material(shader){name="Level2FishingReel"};AssetDatabase.CreateAsset(material,MaterialPath);}
        material.shader=shader;material.shaderKeywords=Array.Empty<string>();
        material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);
        material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
        material.SetFloat("_Metallic",0.25f);material.SetFloat("_Smoothness",0.55f);EditorUtility.SetDirty(material);return material;
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(TexturePath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        TextureImporter importer=AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Unity could not import the Level 2 reel texture.");
        importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.sRGBTexture=true;
        importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;
        importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
    }

    private static string FindPackage()
    {
        string user=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach(string folder in new[]{Path.Combine(user,"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.GetFullPath(".")})
        {
            if(!Directory.Exists(folder))continue;
            string exact=Path.Combine(folder,PackageName);if(File.Exists(exact))return exact;
            string match=Directory.GetFiles(folder,"*.zip",SearchOption.TopDirectoryOnly)
                .Where(file=>Normalize(Path.GetFileNameWithoutExtension(file)).Contains("level2fishingreel"))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(!string.IsNullOrWhiteSpace(match))return match;
        }
        return null;
    }

    private static bool TryResolveBlender(bool allowDialog,out string blender)
    {
        blender=EditorPrefs.GetString(BlenderPrefsKey,string.Empty);
        if(!string.IsNullOrWhiteSpace(blender)&&File.Exists(blender))return true;
        var candidates=new List<string>();
        foreach(string baseFolder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
        {
            string root=Path.Combine(baseFolder,"Blender Foundation");if(Directory.Exists(root))candidates.AddRange(Directory.GetFiles(root,"blender.exe",SearchOption.AllDirectories));
        }
        blender=candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if(!string.IsNullOrWhiteSpace(blender)){EditorPrefs.SetString(BlenderPrefsKey,blender);return true;}
        if(!allowDialog)return false;
        blender=EditorUtility.OpenFilePanel("Locate Blender",string.Empty,"exe");
        if(string.IsNullOrWhiteSpace(blender)||!File.Exists(blender))return false;
        EditorPrefs.SetString(BlenderPrefsKey,blender);return true;
    }

    private static ZipArchiveEntry FindNamedEntry(ZipArchive archive,string expectedName)
    {string expected=Normalize(expectedName);return archive.Entries.FirstOrDefault(e=>!string.IsNullOrEmpty(e.Name)&&Normalize(e.Name)==expected);}
    private static ZipArchiveEntry SingleEntryWithExtension(ZipArchive archive,string extension)
    {var matches=archive.Entries.Where(e=>!string.IsNullOrEmpty(e.Name)&&string.Equals(Path.GetExtension(e.Name),extension,StringComparison.OrdinalIgnoreCase)).ToArray();return matches.Length==1?matches[0]:null;}
    private static ZipArchiveEntry SingleImageEntry(ZipArchive archive)
    {var matches=archive.Entries.Where(e=>{string ext=Path.GetExtension(e.Name).ToLowerInvariant();return ext==".png"||ext==".jpg"||ext==".jpeg";}).ToArray();return matches.Length==1?matches[0]:null;}
    private static bool LooksLikeImage(byte[] bytes)=>bytes!=null&&bytes.Length>=8&&((bytes[0]==0x89&&bytes[1]==0x50&&bytes[2]==0x4E&&bytes[3]==0x47)||(bytes[0]==0xFF&&bytes[1]==0xD8&&bytes[2]==0xFF));
    private static byte[] ReadAll(ZipArchiveEntry entry){using Stream input=entry.Open();using MemoryStream output=new MemoryStream();input.CopyTo(output);return output.ToArray();}
    private static Transform FindDeepChild(Transform root,string name){foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t!=null&&t.name==name)return t;return null;}
    private static string Normalize(string value)=>new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static void WriteBytesIfChanged(string path,byte[] bytes){Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes))return;File.WriteAllBytes(path,bytes);}
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');if(slash<=0)return;string parent=path.Substring(0,slash);EnsureFolder(parent);if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,path.Substring(slash+1));}
    private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
}
