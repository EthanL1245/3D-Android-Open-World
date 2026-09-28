using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;
using Process=System.Diagnostics.Process;
using ProcessStartInfo=System.Diagnostics.ProcessStartInfo;

/// <summary>
/// Imports the supplied Level 3 rod body/UV/texture onto a clone of the proven
/// Woodland Rod + reel prefab. Reel, RodTip, cast handling and reel animation
/// are preserved exactly; only the RodBlank visual is replaced.
/// </summary>
public static class FishingRodLevel3Importer
{
    private const string PackageName="Level 3 Fishing Rod.zip";
    private const string StarterPrefabPath="Assets/Resources/Fishing/FishingRodReel.prefab";
    private const string Level3PrefabPath="Assets/Resources/Fishing/FishingRodReelLevel3Rod.prefab";
    private const string Root="Assets/_Game/Fishing/RodReel/Level3Rod";
    private const string Source=Root+"/Source";
    private const string Generated=Root+"/Generated";
    private const string TexturePath=Source+"/Level3FishingRodTexture.jpg";
    private const string MaterialPath=Generated+"/Level3FishingRod.mat";
    private const string MeshPath=Generated+"/Level3FishingRod.asset";
    private const string BlenderPrefsKey="OpenWorld.Goatfish.BlenderExecutable";
    private const string AutoSessionKey="OpenWorld.AutoImport.FishingRodLevel3.20260925.v1";
    private const uint ExpectedTriangles=1340;

    [Serializable] private sealed class Geometry
    {
        public float[] vertices;
        public float[] uv;
        public int[] triangles;
    }

    [InitializeOnLoadMethod]
    private static void AutoImport()
    {
        if(Application.isBatchMode || SessionState.GetBool(AutoSessionKey,false))return;
        SessionState.SetBool(AutoSessionKey,true);
        EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath)==null)return;
            string package=FindPackage();
            if(string.IsNullOrWhiteSpace(package) || !TryResolveBlender(false,out string blender))return;
            try{Import(package,blender);Debug.Log("Level 3 Fishing Rod imported automatically.");}
            catch(Exception e){Debug.LogException(e);}
            finally{EditorUtility.ClearProgressBar();}
        };
    }

    [MenuItem("Tools/Open World/Import Level 3 Fishing Rod (One Click)")]
    public static void ImportOneClick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorUtility.DisplayDialog("Level 3 Fishing Rod","Exit Play Mode first.","OK");return;}
        if(AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath)==null){EditorUtility.DisplayDialog("Level 3 Fishing Rod","Starter FishingRodReel.prefab is missing. Install the starter rod/reel first.","OK");return;}
        string package=FindPackage();
        if(string.IsNullOrWhiteSpace(package))
        {
            string downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
            package=EditorUtility.OpenFilePanel("Choose "+PackageName,Directory.Exists(downloads)?downloads:string.Empty,"zip");
            if(string.IsNullOrWhiteSpace(package))return;
        }
        try
        {
            if(!TryResolveBlender(true,out string blender))return;
            Import(package,blender);
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(Level3PrefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("Level 3 Fishing Rod Ready","The Level 3 rod is installed. It costs 6,000 coins, deals 3x Woodland damage and has an 8% critical chance for 2x its normal Level 3 damage.","OK");
        }
        catch(Exception e){Debug.LogException(e);EditorUtility.DisplayDialog("Level 3 Fishing Rod Failed",e.Message+"\n\nSee Console for details.","OK");}
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void Import(string zipPath,string blender)
    {
        EnsureFolder(Source);EnsureFolder(Generated);EnsureFolder("Assets/Resources/Fishing");
        string library=Path.GetFullPath("Library/FishingRodLevel3Import");Directory.CreateDirectory(library);
        string blendPath=Path.Combine(library,"Level 3 Fishing Rod.blend");
        string jsonPath=Path.Combine(library,"Level3FishingRodGeometry.json");

        EditorUtility.DisplayProgressBar("Level 3 Fishing Rod","Validating supplied package...",.08f);
        ExtractPackage(zipPath,blendPath,out byte[] textureBytes);
        EditorUtility.DisplayProgressBar("Level 3 Fishing Rod","Reading Blender geometry and UVs...",.30f);
        ExportGeometry(blender,blendPath,jsonPath);
        Geometry geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText(jsonPath));
        ValidateGeometry(geometry);
        EditorUtility.DisplayProgressBar("Level 3 Fishing Rod","Importing texture...",.52f);
        WriteBytesIfChanged(Path.GetFullPath(TexturePath),textureBytes);ConfigureTexture();
        EditorUtility.DisplayProgressBar("Level 3 Fishing Rod","Building gameplay prefab...",.75f);
        BuildPrefab(geometry);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
    }

    private static void ExtractPackage(string zipPath,string blendPath,out byte[] textureBytes)
    {
        if(!File.Exists(zipPath))throw new FileNotFoundException(PackageName+" was not found.",zipPath);
        using FileStream stream=File.OpenRead(zipPath);
        using ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read);
        ZipArchiveEntry blend=FindNamed(archive,"Level 3 Fishing Rod.blend")??SingleExt(archive,".blend");
        ZipArchiveEntry stl=FindNamed(archive,"Level 3 Fishing Rod.stl")??SingleExt(archive,".stl");
        ZipArchiveEntry texture=FindNamed(archive,"Level 3 Fishing Rod Texture.jpg")??SingleImage(archive);
        if(blend==null || stl==null || texture==null)throw new InvalidDataException("The ZIP must contain the Level 3 rod .blend, .stl and texture image.");
        byte[] blendBytes=ReadAll(blend),stlBytes=ReadAll(stl);textureBytes=ReadAll(texture);
        if(blendBytes.Length==0)throw new InvalidDataException("The Level 3 rod .blend is empty.");
        if(stlBytes.Length<84 || BitConverter.ToUInt32(stlBytes,80)!=ExpectedTriangles)throw new InvalidDataException("The Level 3 rod STL does not match the supplied 1340-triangle model.");
        if(!LooksLikeImage(textureBytes))throw new InvalidDataException("The Level 3 rod texture is not a valid image.");
        Directory.CreateDirectory(Path.GetDirectoryName(blendPath));File.WriteAllBytes(blendPath,blendBytes);
    }

    private static void ExportGeometry(string blender,string blendPath,string jsonPath)
    {
        if(File.Exists(jsonPath))File.Delete(jsonPath);
        string scriptPath=Path.Combine(Path.GetDirectoryName(jsonPath),"ExportLevel3FishingRod.py");
        string python=@"import bpy, json, os, sys
args=sys.argv
out_path=args[args.index('--')+1]
deps=bpy.context.evaluated_depsgraph_get()
best=None; best_count=-1
for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    eo=obj.evaluated_get(deps); me=eo.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
    try:
        me.calc_loop_triangles(); count=len(me.loop_triangles)
        if count>best_count: best=obj; best_count=count
    finally: eo.to_mesh_clear()
if best is None: raise RuntimeError('No mesh object exists in the Level 3 rod Blend.')
eo=best.evaluated_get(deps); me=eo.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
try:
    me.calc_loop_triangles(); uv_layer=me.uv_layers.active.data if me.uv_layers.active else None
    verts=[]; uvs=[]; tris=[]; world=best.matrix_world; flip=world.to_3x3().determinant()<0
    for tri in me.loop_triangles:
        ids=[]
        for li in tri.loops:
            loop=me.loops[li]; p=world @ me.vertices[loop.vertex_index].co; uv=uv_layer[li].uv if uv_layer else (0.0,0.0)
            ids.append(len(verts)//3); verts.extend((p.x,p.y,p.z)); uvs.extend((float(uv[0]),float(uv[1])))
        if flip: ids[1],ids[2]=ids[2],ids[1]
        tris.extend(ids)
    os.makedirs(os.path.dirname(out_path),exist_ok=True)
    with open(out_path,'w',encoding='utf-8') as f: json.dump({'vertices':verts,'uv':uvs,'triangles':tris},f,separators=(',',':'))
finally: eo.to_mesh_clear()
";
        File.WriteAllText(scriptPath,python);
        ProcessStartInfo info=new ProcessStartInfo{FileName=blender,Arguments="--background "+Quote(blendPath)+" --python "+Quote(scriptPath)+" -- "+Quote(jsonPath),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using Process process=Process.Start(info);if(process==null)throw new InvalidOperationException("Blender could not be started.");
        string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();
        if(!process.WaitForExit(120000)){try{process.Kill();}catch{}throw new TimeoutException("Blender took more than two minutes to read the Level 3 rod.");}
        if(process.ExitCode!=0 || !File.Exists(jsonPath)){Debug.LogError("Level 3 rod Blender output:\n"+output+"\n\nErrors:\n"+error);throw new InvalidOperationException("Blender could not export the Level 3 rod geometry.");}
    }

    private static void ValidateGeometry(Geometry g)
    {
        if(g==null || g.vertices==null || g.vertices.Length==0 || g.vertices.Length%3!=0 || g.uv==null || g.uv.Length!=g.vertices.Length/3*2 || g.triangles==null || g.triangles.Length/3!=ExpectedTriangles)
            throw new InvalidDataException("The exported Level 3 rod geometry is invalid or does not match 1340 triangles.");
    }

    // Prepared geometry uses the same tested hierarchy/animation contract as Level 3.
    // The caller owns the output paths, so future tiers do not need copied importers.
    public static void InstallPrepared(string jsonPath,string texturePath,string prefabPath,string generatedPath)
    {
        EnsureFolder(generatedPath);
        AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
        var geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText(jsonPath));
        ValidateGeometry(geometry);
        BuildPrefab(geometry,prefabPath,texturePath,generatedPath);
    }

    private static void BuildPrefab(Geometry g,string prefabPath=null,string texturePath=null,string generatedPath=null)
    {
        prefabPath= prefabPath ?? Level3PrefabPath;
        texturePath= texturePath ?? TexturePath;
        generatedPath= generatedPath ?? Generated;

        GameObject starter=AssetDatabase.LoadAssetAtPath<GameObject>(StarterPrefabPath);if(starter==null)throw new InvalidOperationException("Starter rod prefab is missing.");
        GameObject root=PrefabUtility.InstantiatePrefab(starter) as GameObject;if(root==null)root=Object.Instantiate(starter);
        try
        {
            if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            root.name=Path.GetFileNameWithoutExtension(prefabPath);
            Transform blank=FindDeepChild(root.transform,"RodBlank"),tip=FindDeepChild(root.transform,"RodTip"),mount=FindDeepChild(root.transform,"ReelMount");
            FishingRodView view=root.GetComponent<FishingRodView>();
            if(blank==null || tip==null || mount==null || view==null)throw new InvalidOperationException("Starter prefab is missing RodBlank, RodTip, ReelMount or FishingRodView.");
            MeshFilter filter=blank.GetComponent<MeshFilter>();MeshRenderer renderer=blank.GetComponent<MeshRenderer>();
            if(filter==null || renderer==null)throw new InvalidOperationException("Starter RodBlank renderer is missing.");
            Animation animation=mount.GetComponent<Animation>();AnimationClip clip=animation!=null?animation.clip:null;
            if(clip==null)throw new InvalidOperationException("Starter reel animation is missing.");
            Vector3 tipPosition=tip.localPosition,mountPosition=mount.localPosition;Quaternion mountRotation=mount.localRotation;

            Mesh mesh=BuildMesh(g,Mathf.Max(.25f,tip.localPosition.y));
            Mesh persistent=AssetDatabase.LoadAssetAtPath<Mesh>((generatedPath==Generated?MeshPath:generatedPath+"/RodMesh.asset"));
            if(persistent==null){AssetDatabase.CreateAsset(mesh,(generatedPath==Generated?MeshPath:generatedPath+"/RodMesh.asset"));persistent=mesh;}else{EditorUtility.CopySerialized(mesh,persistent);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(persistent);}
            filter.sharedMesh=persistent;renderer.sharedMaterial=BuildMaterial((generatedPath==Generated?MaterialPath:generatedPath+"/Material.mat"),texturePath);renderer.SetPropertyBlock(null);renderer.shadowCastingMode=ShadowCastingMode.Off;

            if(Vector3.Distance(tip.localPosition,tipPosition)>.000001f || Vector3.Distance(mount.localPosition,mountPosition)>.000001f || Quaternion.Angle(mount.localRotation,mountRotation)>.0001f || animation.clip!=clip)
                throw new InvalidOperationException("Level 3 rod import attempted to change gameplay transforms or reel animation.");
            GameObject saved=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);if(saved==null)throw new InvalidOperationException("Unity could not save the Level 3 rod prefab.");
        }
        finally{if(root!=null)Object.DestroyImmediate(root);}
    }

    private static Mesh BuildMesh(Geometry g,float targetLength)
    {
        int count=g.vertices.Length/3;Vector3[] source=new Vector3[count];
        Vector3 min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
        for(int i=0;i<count;i++){Vector3 p=new Vector3(g.vertices[i*3],g.vertices[i*3+1],g.vertices[i*3+2]);source[i]=p;min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
        Vector3 span=max-min;int longAxis=span.x>=span.y && span.x>=span.z?0:span.y>=span.z?1:2;
        int a=longAxis==0?1:0,b=longAxis==2?1:2;if(longAxis==1){a=0;b=2;}
        float lo=Axis(min,longAxis),hi=Axis(max,longAxis);float baseValue=Mathf.Abs(lo)<=Mathf.Abs(hi)?lo:hi;float sign=baseValue==lo?1f:-1f;
        float length=Mathf.Max(.0001f,hi-lo),scale=targetLength/length;
        float ca=(Axis(min,a)+Axis(max,a))*.5f,cb=(Axis(min,b)+Axis(max,b))*.5f;
        Vector3[] vertices=new Vector3[count];Vector2[] uv=new Vector2[count];
        for(int i=0;i<count;i++)
        {
            Vector3 p=source[i];float along=(Axis(p,longAxis)-baseValue)*sign;
            vertices[i]=new Vector3((Axis(p,a)-ca)*scale,along*scale,(Axis(p,b)-cb)*scale);
            uv[i]=new Vector2(g.uv[i*2],g.uv[i*2+1]);
        }
        int[] triangles=(int[])g.triangles.Clone();
        Vector3 tx=MapDirection(Vector3.right,longAxis,a,b,sign),ty=MapDirection(Vector3.up,longAxis,a,b,sign),tz=MapDirection(Vector3.forward,longAxis,a,b,sign);
        if(Vector3.Dot(Vector3.Cross(tx,ty),tz)<0f)
            for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
        Mesh mesh=new Mesh{name="Level3FishingRod"};if(count>65535)mesh.indexFormat=IndexFormat.UInt32;
        mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }

    private static Vector3 MapDirection(Vector3 v,int longAxis,int a,int b,float sign)=>new Vector3(Axis(v,a),Axis(v,longAxis)*sign,Axis(v,b));
    private static float Axis(Vector3 v,int axis)=>axis==0?v.x:axis==1?v.y:v.z;

    private static Material BuildMaterial(string materialPath,string texturePath)
    {
        Shader shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Fishing/FishingEquipment.shader");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("FishingEquipment shader is missing or invalid.");
        // Prepared Level 4 sources use native atlases, as the fish importer does.
        // A valid JPEG can still have an unavailable TextureImporter artifact during reload.
        Texture2D texture=texturePath.StartsWith(FishingContentUpdateSetup.Source+"/",StringComparison.Ordinal)
            ? AuthoredTextureAsset.Load(texturePath,Path.GetDirectoryName(materialPath).Replace('\\','/')+"/AuthoredTexture.asset")
            : AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(texture==null)throw new InvalidOperationException("Rod texture failed to import: "+texturePath);
        Material material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader){name="Level3FishingRod"};AssetDatabase.CreateAsset(material,materialPath);}
        material.shader=shader;material.shaderKeywords=Array.Empty<string>();material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);material.SetFloat("_Metallic",.05f);material.SetFloat("_Smoothness",.42f);
        if(material.HasProperty("_Cull"))material.SetFloat("_Cull",0f);EditorUtility.SetDirty(material);return material;
    }

    private static void ConfigureTexture()
    {
        AssetDatabase.ImportAsset(TexturePath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        TextureImporter importer=AssetImporter.GetAtPath(TexturePath) as TextureImporter;if(importer==null)throw new InvalidOperationException("Unity could not import the Level 3 rod texture.");
        importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
    }

    private static string FindPackage()
    {
        string user=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach(string folder in new[]{Path.Combine(user,"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.GetFullPath(".")})
        {
            if(!Directory.Exists(folder))continue;string exact=Path.Combine(folder,PackageName);if(File.Exists(exact))return exact;
            string match=Directory.GetFiles(folder,"*.zip",SearchOption.TopDirectoryOnly).Where(f=>Normalize(Path.GetFileNameWithoutExtension(f)).Contains("level3fishingrod")).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();if(!string.IsNullOrWhiteSpace(match))return match;
        }
        return null;
    }

    private static string Normalize(string value)=>new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static ZipArchiveEntry FindNamed(ZipArchive archive,string expected){string normalized=Normalize(expected);return archive.Entries.FirstOrDefault(e=>!string.IsNullOrEmpty(e.Name)&&Normalize(e.Name)==normalized);}
    private static ZipArchiveEntry SingleExt(ZipArchive archive,string ext){var matches=archive.Entries.Where(e=>!string.IsNullOrEmpty(e.Name)&&string.Equals(Path.GetExtension(e.Name),ext,StringComparison.OrdinalIgnoreCase)).ToArray();return matches.Length==1?matches[0]:null;}
    private static ZipArchiveEntry SingleImage(ZipArchive archive){var matches=archive.Entries.Where(e=>!string.IsNullOrEmpty(e.Name)&&new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(e.Name).ToLowerInvariant())).ToArray();return matches.Length==1?matches[0]:null;}
    private static bool LooksLikeImage(byte[] bytes)=>bytes!=null&&bytes.Length>=3&&((bytes.Length>=8&&bytes[0]==0x89&&bytes[1]==0x50&&bytes[2]==0x4e&&bytes[3]==0x47)||(bytes[0]==0xff&&bytes[1]==0xd8&&bytes[2]==0xff));
    private static byte[] ReadAll(ZipArchiveEntry entry){using Stream input=entry.Open();using MemoryStream output=new MemoryStream();input.CopyTo(output);return output.ToArray();}
    private static void WriteBytesIfChanged(string path,byte[] bytes){Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes))return;File.WriteAllBytes(path,bytes);}
    private static Transform FindDeepChild(Transform root,string name){foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t!=null&&t.name==name)return t;return null;}
    private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash);EnsureFolder(parent);if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,path.Substring(slash+1));}

    private static bool TryResolveBlender(bool allowDialog,out string blender)
    {
        blender=EditorPrefs.GetString(BlenderPrefsKey,string.Empty);if(!string.IsNullOrWhiteSpace(blender)&&File.Exists(blender))return true;
        foreach(string baseFolder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
        {
            string root=Path.Combine(baseFolder,"Blender Foundation");if(!Directory.Exists(root))continue;
            blender=Directory.GetFiles(root,"blender.exe",SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();if(!string.IsNullOrWhiteSpace(blender)){EditorPrefs.SetString(BlenderPrefsKey,blender);return true;}
        }
        if(!allowDialog){blender=null;return false;}
        blender=EditorUtility.OpenFilePanel("Locate Blender",string.Empty,"exe");if(string.IsNullOrWhiteSpace(blender)||!File.Exists(blender))return false;EditorPrefs.SetString(BlenderPrefsKey,blender);return true;
    }

    private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
}

