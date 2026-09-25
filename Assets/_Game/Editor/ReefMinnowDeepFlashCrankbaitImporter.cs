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
/// Imports the two newly-authored lure appearances while preserving the exact
/// proven Neon Breach gameplay/presentation contract: hooks, hook animation,
/// Animator/controller, LineAttach, lighting, root scale/orientation and runtime
/// retrieve behavior are copied unchanged. Only the body geometry/UV + body
/// texture differ per lure.
/// </summary>
public static class ReefMinnowDeepFlashCrankbaitImporter
{
    private const string BasePrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";
    private const string SharedStlSha256 = "ecb16deecf91e3b7a67f239a379a2f6de72a10540e97e5b40280bae5f8d9a499";
    private const int ExpectedTriangleCount = 1720;
    private const string AutoSessionKey = "OpenWorld.AutoImport.ReefMinnowDeepFlash.20260925";

    private sealed class VariantSpec
    {
        public readonly string DisplayName;
        public readonly string ZipStem;
        public readonly string BlendSha256;
        public readonly string TextureSha256;
        public readonly string Folder;
        public readonly string SourceFbxPath;
        public readonly string TexturePath;
        public readonly string MeshPath;
        public readonly string MaterialPath;
        public readonly string OutputPrefabPath;
        public readonly string PrefabName;

        public VariantSpec(
            string displayName,
            string zipStem,
            string blendSha256,
            string textureSha256,
            string folder,
            string outputPrefabPath,
            string prefabName)
        {
            DisplayName=displayName;
            ZipStem=zipStem;
            BlendSha256=blendSha256;
            TextureSha256=textureSha256;
            Folder=folder;
            SourceFbxPath=folder+"/Source/BodyGeometry.fbx";
            TexturePath=folder+"/Source/BodyTexture.jpg";
            MeshPath=folder+"/Meshes/Body.asset";
            MaterialPath=folder+"/Body.mat";
            OutputPrefabPath=outputPrefabPath;
            PrefabName=prefabName;
        }
    }

    private static readonly VariantSpec ReefMinnow = new VariantSpec(
        "Reef Minnow",
        "Lipless Crankbait Black with Blue Stripes",
        "bd13ce359bd110ed2b0b9ba5a6ba14b5c5dc8e8e4dd85fdf847402ebd58f5f0f",
        "9b57567f44a8c8000ebf332ca4d9f3be920c3ebb9df299dec8f55dc58a3b885e",
        "Assets/_Game/Fishing/ReefMinnow",
        "Assets/Resources/Fishing/ReefMinnowCrankbait.prefab",
        "ReefMinnowCrankbait");

    private static readonly VariantSpec DeepFlash = new VariantSpec(
        "Deep Flash",
        "Lipless Crankbait Gold with Red Back",
        "80decd967ff38f3afb6423b4356a058223d14f0ba2e9ce0441f4a15251c1b610",
        "c339b3636a22d3f925f3b030aae59dfb51330b02bb8d625fd500b9ff9fc1a5cf",
        "Assets/_Game/Fishing/DeepFlash",
        "Assets/Resources/Fishing/DeepFlashCrankbait.prefab",
        "DeepFlashCrankbait");

    [InitializeOnLoadMethod]
    private static void AutoImportWhenPackagesAreAvailable()
    {
        if(Application.isBatchMode || SessionState.GetBool(AutoSessionKey,false))return;
        SessionState.SetBool(AutoSessionKey,true);
        EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath)==null)return;

            bool needsReef=AssetDatabase.LoadAssetAtPath<GameObject>(ReefMinnow.OutputPrefabPath)==null;
            bool needsDeep=AssetDatabase.LoadAssetAtPath<GameObject>(DeepFlash.OutputPrefabPath)==null;
            if(!needsReef && !needsDeep)return;

            string reef=needsReef?FindPackage(ReefMinnow):null;
            string deep=needsDeep?FindPackage(DeepFlash):null;
            if((needsReef && reef==null) || (needsDeep && deep==null))return;

            try
            {
                if(needsReef)ImportVariant(ReefMinnow,reef);
                if(needsDeep)ImportVariant(DeepFlash,deep);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Automatically imported the new Reef Minnow and Deep Flash crankbait models.");
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        };
    }

    [MenuItem("Tools/Open World/Import Reef Minnow + Deep Flash Crankbaits (One Click)")]
    public static void ImportBothOneClick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Crankbait Variants","Exit Play Mode first.","OK");
            return;
        }
        if(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath)==null)
        {
            EditorUtility.DisplayDialog(
                "Crankbait Variants",
                "Neon Breach is missing. It is the approved template for animation, hooks, LineAttach, size, orientation and lighting.",
                "OK");
            return;
        }

        string reef=ResolvePackage(ReefMinnow);
        if(string.IsNullOrWhiteSpace(reef))return;
        string deep=ResolvePackage(DeepFlash);
        if(string.IsNullOrWhiteSpace(deep))return;

        try
        {
            ImportVariant(ReefMinnow,reef);
            ImportVariant(DeepFlash,deep);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(ReefMinnow.OutputPrefabPath);
            EditorUtility.DisplayDialog(
                "Crankbait Variants Ready",
                "Reef Minnow now uses the black/blue model and Deep Flash uses the gold/red model. Their existing mechanics are unchanged. Both copy Neon Breach's exact hook animation, LineAttach, lighting, size/orientation and retrieve presentation.",
                "OK");
        }
        catch(Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Crankbait Import Failed",
                exception.Message+"\n\nThe existing Neon Breach and Fire Shad prefabs were not modified. See Console for the full error.",
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void ImportVariant(VariantSpec spec,string zipPath)
    {
        EnsureFolderRecursive(spec.Folder+"/Source");
        EnsureFolderRecursive(spec.Folder+"/Meshes");
        EnsureFolderRecursive("Assets/Resources/Fishing");

        string library=Path.GetFullPath("Library/CrankbaitVariantImport/"+Normalize(spec.DisplayName));
        Directory.CreateDirectory(library);
        string blendPath=Path.Combine(library,"Source.blend");
        string exportedFbx=Path.Combine(library,"BodyGeometry.fbx");

        EditorUtility.DisplayProgressBar(spec.DisplayName,"Validating supplied model package...",0.08f);
        ExtractAndValidate(spec,zipPath,blendPath,out byte[] textureBytes);

        EditorUtility.DisplayProgressBar(spec.DisplayName,"Exporting authored body geometry and UV mapping...",0.28f);
        ExportGeometry(ResolveBlenderExecutable(),blendPath,exportedFbx);

        EditorUtility.DisplayProgressBar(spec.DisplayName,"Importing body and texture...",0.48f);
        CopyFileIfChanged(exportedFbx,Path.GetFullPath(spec.SourceFbxPath));
        WriteBytesIfChanged(Path.GetFullPath(spec.TexturePath),textureBytes);
        AssetDatabase.ImportAsset(spec.SourceFbxPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        ConfigureTexture(spec.TexturePath);

        GameObject source=AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourceFbxPath);
        if(source==null)throw new InvalidOperationException("Unity could not load "+spec.DisplayName+" geometry source.");

        EditorUtility.DisplayProgressBar(spec.DisplayName,"Applying appearance to proven crankbait template...",0.74f);
        BuildVariantPrefab(spec,source);
    }

    private static void ExtractAndValidate(VariantSpec spec,string zipPath,string blendPath,out byte[] textureBytes)
    {
        if(!File.Exists(zipPath))throw new FileNotFoundException(spec.DisplayName+" ZIP was not found.",zipPath);

        using FileStream stream=File.OpenRead(zipPath);
        using ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read);
        ZipArchiveEntry stl=archive.Entries.FirstOrDefault(e=>e.FullName.EndsWith(".stl",StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry blend=archive.Entries.FirstOrDefault(e=>e.FullName.EndsWith(".blend",StringComparison.OrdinalIgnoreCase));
        ZipArchiveEntry texture=archive.Entries.FirstOrDefault(e=>
        {
            string ext=Path.GetExtension(e.FullName).ToLowerInvariant();
            return ext==".jpg" || ext==".jpeg" || ext==".png";
        });
        if(stl==null || blend==null || texture==null)
            throw new InvalidDataException(spec.DisplayName+" package must contain its STL, Blend source and texture image.");

        byte[] stlBytes=ReadAll(stl);
        byte[] blendBytes=ReadAll(blend);
        textureBytes=ReadAll(texture);

        if(!HashEquals(stlBytes,SharedStlSha256) ||
           !HashEquals(blendBytes,spec.BlendSha256) ||
           !HashEquals(textureBytes,spec.TextureSha256))
            throw new InvalidDataException("Choose the exact supplied '"+spec.ZipStem+".zip'.");

        if(stlBytes.Length<84 || BitConverter.ToUInt32(stlBytes,80)!=ExpectedTriangleCount)
            throw new InvalidDataException(spec.DisplayName+" STL topology does not match the supplied revision.");

        Directory.CreateDirectory(Path.GetDirectoryName(blendPath));
        File.WriteAllBytes(blendPath,blendBytes);
    }

    private static void ExportGeometry(string blender,string blendPath,string destinationFbx)
    {
        string scriptPath=Path.Combine(Path.GetDirectoryName(destinationFbx),"ExportBody.py");
        string python=
@"import bpy
import os
import sys

args=sys.argv
out_path=args[args.index('--')+1]
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

if len(selected)!=3:
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
    raise RuntimeError('FBX export produced no file.')
";
        File.WriteAllText(scriptPath,python);

        ProcessStartInfo info=new ProcessStartInfo
        {
            FileName=blender,
            Arguments="--background "+Quote(blendPath)+" --python "+Quote(scriptPath)+" -- "+Quote(destinationFbx),
            UseShellExecute=false,
            CreateNoWindow=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true
        };
        using Process process=Process.Start(info);
        if(process==null)throw new InvalidOperationException("Blender could not be started.");
        string output=process.StandardOutput.ReadToEnd();
        string error=process.StandardError.ReadToEnd();
        if(!process.WaitForExit(120000))
        {
            try{process.Kill();}catch{}
            throw new TimeoutException("Blender took more than 2 minutes to export a crankbait body.");
        }
        if(process.ExitCode!=0 || !File.Exists(destinationFbx))
        {
            Debug.LogError("Crankbait Blender output:\n"+output+"\n\nErrors:\n"+error);
            throw new InvalidOperationException("Blender could not export the crankbait geometry.");
        }
    }

    private static void ConfigureTexture(string texturePath)
    {
        AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        TextureImporter importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Could not import crankbait texture at "+texturePath+".");
        importer.sRGBTexture=true;
        importer.mipmapEnabled=true;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Trilinear;
        importer.anisoLevel=4;
        importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void BuildVariantPrefab(VariantSpec spec,GameObject source)
    {
        MeshFilter[] sourceFilters=source.GetComponentsInChildren<MeshFilter>(true)
            .Where(f=>f!=null && f.sharedMesh!=null).ToArray();
        if(sourceFilters.Length!=3)
            throw new InvalidOperationException(spec.DisplayName+" source imported with "+sourceFilters.Length+" mesh parts; expected 3.");

        int triangles=sourceFilters.Sum(f=>CountTriangles(f.sharedMesh));
        if(triangles!=ExpectedTriangleCount)
            throw new InvalidOperationException(spec.DisplayName+" imported triangle count is "+triangles+"; expected "+ExpectedTriangleCount+".");

        MeshFilter sourceBody=LargestMesh(sourceFilters);
        GameObject root=PrefabUtility.LoadPrefabContents(BasePrefabPath);
        try
        {
            Transform authoredModel=FindDeepChild(root.transform,"AuthoredModel");
            Transform lineAttach=FindDeepChild(root.transform,"LineAttach");
            Animator animator=root.GetComponentInChildren<Animator>(true);
            if(authoredModel==null || lineAttach==null || animator==null || animator.runtimeAnimatorController==null)
                throw new InvalidOperationException("Neon Breach prefab is missing its authored model, animation controller or LineAttach.");

            MeshFilter[] targetFilters=authoredModel.GetComponentsInChildren<MeshFilter>(true)
                .Where(f=>f!=null && f.sharedMesh!=null && !IsUnderLightingRig(f.transform,authoredModel)).ToArray();
            if(targetFilters.Length!=3)
                throw new InvalidOperationException("Neon Breach must contain exactly body + two animated hook meshes.");

            MeshFilter targetBody=LargestMesh(targetFilters);
            Renderer bodyRenderer=targetBody.GetComponent<Renderer>();
            if(bodyRenderer==null || bodyRenderer.sharedMaterial==null)
                throw new InvalidOperationException("Neon Breach body material could not be identified.");

            Transform lineParent=lineAttach.parent;
            Vector3 linePos=lineAttach.localPosition;
            Quaternion lineRot=lineAttach.localRotation;
            Vector3 lineScale=lineAttach.localScale;
            RuntimeAnimatorController controller=animator.runtimeAnimatorController;
            Vector3 rootPos=root.transform.localPosition;
            Quaternion rootRot=root.transform.localRotation;
            Vector3 rootScale=root.transform.localScale;
            Dictionary<Transform,TransformSnapshot> transforms=targetFilters.ToDictionary(f=>f.transform,f=>new TransformSnapshot(f.transform));

            Mesh body=BakeSourceMeshIntoTargetSpace(sourceBody,source.transform,targetBody,authoredModel,spec.PrefabName+"Body");
            if(AssetDatabase.LoadAssetAtPath<Mesh>(spec.MeshPath)!=null)AssetDatabase.DeleteAsset(spec.MeshPath);
            AssetDatabase.CreateAsset(body,spec.MeshPath);
            targetBody.sharedMesh=body;

            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexturePath);
            if(texture==null)throw new InvalidOperationException(spec.DisplayName+" texture did not import.");
            if(AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath)!=null)AssetDatabase.DeleteAsset(spec.MaterialPath);
            Material material=new Material(bodyRenderer.sharedMaterial){name=spec.DisplayName+" Body"};
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
            else material.mainTexture=texture;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
            AssetDatabase.CreateAsset(material,spec.MaterialPath);
            Material[] slots=new Material[Mathf.Max(1,bodyRenderer.sharedMaterials.Length)];
            for(int i=0;i<slots.Length;i++)slots[i]=material;
            bodyRenderer.sharedMaterials=slots;

            // Hooks are deliberately inherited from Neon Breach. Their meshes,
            // local transforms and animation are never replaced or repositioned.
            root.name=spec.PrefabName;

            if(lineAttach.parent!=lineParent ||
               !Approximately(lineAttach.localPosition,linePos) ||
               Quaternion.Angle(lineAttach.localRotation,lineRot)>0.0001f ||
               !Approximately(lineAttach.localScale,lineScale))
                throw new InvalidOperationException(spec.DisplayName+" build altered LineAttach. Save aborted.");
            if(root.GetComponentInChildren<Animator>(true).runtimeAnimatorController!=controller)
                throw new InvalidOperationException(spec.DisplayName+" build altered Animator/controller. Save aborted.");
            if(!Approximately(root.transform.localPosition,rootPos) ||
               Quaternion.Angle(root.transform.localRotation,rootRot)>0.0001f ||
               !Approximately(root.transform.localScale,rootScale))
                throw new InvalidOperationException(spec.DisplayName+" build altered lure size/orientation. Save aborted.");
            foreach(var pair in transforms)
                if(!pair.Value.Matches(pair.Key))
                    throw new InvalidOperationException(spec.DisplayName+" build altered animated transform '"+pair.Key.name+"'. Save aborted.");

            PrefabUtility.SaveAsPrefabAsset(root,spec.OutputPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static MeshFilter LargestMesh(MeshFilter[] filters)
    {
        MeshFilter best=filters[0];
        float score=-1f;
        for(int i=0;i<filters.Length;i++)
        {
            Renderer renderer=filters[i].GetComponent<Renderer>();
            Bounds bounds=renderer!=null?renderer.bounds:filters[i].sharedMesh.bounds;
            Vector3 size=bounds.size;
            float candidate=Mathf.Max(size.x,Mathf.Max(size.y,size.z))*Mathf.Max(0.000001f,size.x*size.y*size.z);
            if(candidate>score){score=candidate;best=filters[i];}
        }
        return best;
    }

    private static Mesh BakeSourceMeshIntoTargetSpace(MeshFilter sourceFilter,Transform sourceRoot,MeshFilter targetFilter,Transform targetRoot,string meshName)
    {
        Mesh source=sourceFilter.sharedMesh;
        Matrix4x4 sourceToModel=sourceRoot.worldToLocalMatrix*sourceFilter.transform.localToWorldMatrix;
        Matrix4x4 targetToModel=targetRoot.worldToLocalMatrix*targetFilter.transform.localToWorldMatrix;
        Matrix4x4 sourceLocalToTargetLocal=targetToModel.inverse*sourceToModel;

        Mesh result=new Mesh{name=meshName,indexFormat=source.indexFormat};
        Vector3[] sourceVertices=source.vertices;
        Vector3[] vertices=new Vector3[sourceVertices.Length];
        for(int i=0;i<vertices.Length;i++)vertices[i]=sourceLocalToTargetLocal.MultiplyPoint3x4(sourceVertices[i]);
        result.vertices=vertices;

        for(int channel=0;channel<8;channel++)
        {
            List<Vector4> uv=new List<Vector4>();
            source.GetUVs(channel,uv);
            if(uv.Count==source.vertexCount)result.SetUVs(channel,uv);
        }
        if(source.colors!=null && source.colors.Length==source.vertexCount)result.colors=source.colors;

        result.subMeshCount=source.subMeshCount;
        for(int sub=0;sub<source.subMeshCount;sub++)result.SetIndices(source.GetIndices(sub),source.GetTopology(sub),sub,false);

        Vector3[] normals=source.normals;
        if(normals!=null && normals.Length==source.vertexCount)
        {
            Matrix4x4 normalMatrix=sourceLocalToTargetLocal.inverse.transpose;
            Vector3[] transformed=new Vector3[normals.Length];
            for(int i=0;i<normals.Length;i++)transformed[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
            result.normals=transformed;
        }
        else result.RecalculateNormals();

        Vector4[] tangents=source.tangents;
        if(tangents!=null && tangents.Length==source.vertexCount)
        {
            Vector4[] transformed=new Vector4[tangents.Length];
            for(int i=0;i<tangents.Length;i++)
            {
                Vector3 direction=sourceLocalToTargetLocal.MultiplyVector(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z)).normalized;
                transformed[i]=new Vector4(direction.x,direction.y,direction.z,tangents[i].w);
            }
            result.tangents=transformed;
        }
        else{try{result.RecalculateTangents();}catch{}}
        result.RecalculateBounds();
        return result;
    }

    private static int CountTriangles(Mesh mesh)
    {
        int count=0;
        for(int i=0;i<mesh.subMeshCount;i++)
        {
            if(mesh.GetTopology(i)!=MeshTopology.Triangles)
                throw new InvalidOperationException("Crankbait source contains non-triangle geometry.");
            count+=(int)mesh.GetIndexCount(i)/3;
        }
        return count;
    }

    private static bool IsUnderLightingRig(Transform transform,Transform authoredModel)
    {
        for(Transform current=transform;current!=null && current!=authoredModel;current=current.parent)
            if(current.name=="AuthoredLightingRig")return true;
        return false;
    }

    private static string ResolvePackage(VariantSpec spec)
    {
        string found=FindPackage(spec);
        if(!string.IsNullOrWhiteSpace(found))return found;
        string downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        string selected=EditorUtility.OpenFilePanel("Choose "+spec.ZipStem+".zip",Directory.Exists(downloads)?downloads:string.Empty,"zip");
        return selected;
    }

    private static string FindPackage(VariantSpec spec)
    {
        string user=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] folders={Path.Combine(user,"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.GetFullPath(".")};
        string normalizedStem=Normalize(spec.ZipStem);
        foreach(string folder in folders)
        {
            if(!Directory.Exists(folder))continue;
            string[] matches=Directory.GetFiles(folder,"*.zip",SearchOption.TopDirectoryOnly)
                .Where(f=>Normalize(Path.GetFileNameWithoutExtension(f)).Contains(normalizedStem))
                .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
            for(int i=0;i<matches.Length;i++)if(IsExactPackage(spec,matches[i]))return matches[i];
        }
        return null;
    }

    private static bool IsExactPackage(VariantSpec spec,string path)
    {
        try
        {
            using FileStream stream=File.OpenRead(path);
            using ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read);
            ZipArchiveEntry stl=archive.Entries.FirstOrDefault(e=>e.FullName.EndsWith(".stl",StringComparison.OrdinalIgnoreCase));
            ZipArchiveEntry blend=archive.Entries.FirstOrDefault(e=>e.FullName.EndsWith(".blend",StringComparison.OrdinalIgnoreCase));
            ZipArchiveEntry texture=archive.Entries.FirstOrDefault(e=>
            {
                string ext=Path.GetExtension(e.FullName).ToLowerInvariant();
                return ext==".jpg" || ext==".jpeg" || ext==".png";
            });
            return stl!=null && blend!=null && texture!=null &&
                   HashEquals(ReadAll(stl),SharedStlSha256) &&
                   HashEquals(ReadAll(blend),spec.BlendSha256) &&
                   HashEquals(ReadAll(texture),spec.TextureSha256);
        }
        catch{return false;}
    }

    private static string ResolveBlenderExecutable()
    {
        string saved=EditorPrefs.GetString(BlenderPrefsKey,string.Empty);
        if(!string.IsNullOrWhiteSpace(saved) && File.Exists(saved))return saved;
        List<string> candidates=new List<string>();
        foreach(string baseFolder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
        {
            string root=Path.Combine(baseFolder,"Blender Foundation");
            if(!Directory.Exists(root))continue;
            try{candidates.AddRange(Directory.GetFiles(root,"blender.exe",SearchOption.AllDirectories));}catch{}
        }
        string found=candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if(!string.IsNullOrWhiteSpace(found)){EditorPrefs.SetString(BlenderPrefsKey,found);return found;}
        string selected=EditorUtility.OpenFilePanel("Locate Blender",string.Empty,"exe");
        if(string.IsNullOrWhiteSpace(selected) || !File.Exists(selected))throw new FileNotFoundException("Blender.exe is required once to read the new crankbait UV mapping.");
        EditorPrefs.SetString(BlenderPrefsKey,selected);return selected;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using Stream input=entry.Open();
        using MemoryStream memory=new MemoryStream();
        input.CopyTo(memory);return memory.ToArray();
    }

    private static bool HashEquals(byte[] bytes,string expected)
    {
        using SHA256 sha=SHA256.Create();
        string actual=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty).ToLowerInvariant();
        return string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase);
    }

    private static string Sha256File(string path)
    {
        using FileStream input=File.OpenRead(path);
        using SHA256 sha=SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(input)).Replace("-",string.Empty).ToLowerInvariant();
    }

    private static void CopyFileIfChanged(string source,string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if(File.Exists(destination) && string.Equals(Sha256File(source),Sha256File(destination),StringComparison.OrdinalIgnoreCase))return;
        File.Copy(source,destination,true);
    }

    private static void WriteBytesIfChanged(string destination,byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if(File.Exists(destination) && File.ReadAllBytes(destination).SequenceEqual(bytes))return;
        File.WriteAllBytes(destination,bytes);
    }

    private static string Normalize(string value)=>(value??string.Empty).Replace(" ",string.Empty).Replace("_",string.Empty).Replace("-",string.Empty).ToLowerInvariant();
    private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
    private static bool Approximately(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<=0.0000000001f;

    private static Transform FindDeepChild(Transform root,string name)
    {
        Transform[] all=root.GetComponentsInChildren<Transform>(true);
        for(int i=0;i<all.Length;i++)if(all[i]!=null && all[i].name==name)return all[i];
        return null;
    }

    private static void EnsureFolderRecursive(string path)
    {
        string[] parts=path.Split('/');
        string current="Assets";
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }

    internal static bool IsVariantSourceFbx(string assetPath)
    {
        return string.Equals(assetPath,ReefMinnow.SourceFbxPath,StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assetPath,DeepFlash.SourceFbxPath,StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;
        public TransformSnapshot(Transform transform){position=transform.localPosition;rotation=transform.localRotation;scale=transform.localScale;}
        public bool Matches(Transform transform)=>Approximately(transform.localPosition,position) && Quaternion.Angle(transform.localRotation,rotation)<=0.0001f && Approximately(transform.localScale,scale);
    }
}

/// <summary>Applies geometry-only import settings before Unity reads the two variant FBXs.</summary>
public sealed class ReefMinnowDeepFlashGeometryPostprocessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if(!ReefMinnowDeepFlashCrankbaitImporter.IsVariantSourceFbx(assetPath))return;
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
