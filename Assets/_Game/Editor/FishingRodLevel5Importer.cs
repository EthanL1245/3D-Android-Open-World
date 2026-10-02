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
/// Imports the supplied Level 5 rod model/UV/texture onto a clone of the proven
/// Level 4 rod prefab. RodTip, ReelMount, casting pose, flex, line handling and reel
/// animation hierarchy are preserved; only the visible RodBlank is replaced.
/// </summary>
public static class FishingRodLevel5Importer
{
    private const string PackageName="Level 5 Fishing Rod.zip";
    private const string Level4PrefabPath="Assets/Resources/Fishing/FishingRodReelLevel4Rod.prefab";
    private const string Level5PrefabPath="Assets/Resources/Fishing/FishingRodReelLevel5Rod.prefab";
    private const string Root="Assets/_Game/Fishing/RodReel/Level5Rod";
    private const string Source=Root+"/Source";
    private const string Generated=Root+"/Generated";
    private const string TexturePath=Source+"/Level5FishingRodTexture.jpg";
    private const string MaterialPath=Generated+"/Level5FishingRod.mat";
    private const string MeshPath=Generated+"/Level5FishingRod.asset";
    private const string BlenderPrefsKey="OpenWorld.Goatfish.BlenderExecutable";
    private const string AutoSessionKey="OpenWorld.AutoImport.FishingRodLevel5.20261002.v1";
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
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Level4PrefabPath)==null)return;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Level5PrefabPath)!=null)return;
            string package=FindPackage();
            if(string.IsNullOrWhiteSpace(package) || !TryResolveBlender(false,out string blender))return;
            try{Import(package,blender);Debug.Log("[LEVEL 5 ROD] Imported automatically from "+package);}
            catch(Exception e){Debug.LogException(e);}
            finally{EditorUtility.ClearProgressBar();}
        };
    }

    [MenuItem("Tools/Open World/Import Level 5 Fishing Rod (One Click)")]
    public static void ImportOneClick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {EditorUtility.DisplayDialog("Level 5 Fishing Rod","Exit Play Mode first.","OK");return;}
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Level4PrefabPath)==null)
        {EditorUtility.DisplayDialog("Level 5 Fishing Rod","The Level 4 rod prefab is missing. Install the existing Level 4 fishing content first.","OK");return;}

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
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(Level5PrefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("Level 5 Fishing Rod Ready","Level 5 is installed with the exact Level 4 casting/reel mechanics. Cost: 25,000 coins. Damage: 25–35 per burst. Critical chance: 13% for 2× damage.","OK");
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Level 5 Fishing Rod Failed",e.Message+"\n\nSee Console for details.","OK");
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void Import(string zipPath,string blender)
    {
        EnsureFolder(Source);EnsureFolder(Generated);EnsureFolder("Assets/Resources/Fishing");
        string library=Path.GetFullPath("Library/FishingRodLevel5Import");Directory.CreateDirectory(library);
        string blendPath=Path.Combine(library,"Level 5 Fishing Rod.blend");
        string jsonPath=Path.Combine(library,"Level5FishingRodGeometry.json");

        EditorUtility.DisplayProgressBar("Level 5 Fishing Rod","Validating supplied package...",.08f);
        ExtractPackage(zipPath,blendPath,out byte[] textureBytes);
        EditorUtility.DisplayProgressBar("Level 5 Fishing Rod","Reading Blender geometry and UVs...",.30f);
        ExportGeometry(blender,blendPath,jsonPath);
        Geometry geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText(jsonPath));
        ValidateGeometry(geometry);
        EditorUtility.DisplayProgressBar("Level 5 Fishing Rod","Importing supplied texture...",.55f);
        WriteBytesIfChanged(Path.GetFullPath(TexturePath),textureBytes);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTexture();
        EditorUtility.DisplayProgressBar("Level 5 Fishing Rod","Building Level 4-compatible gameplay prefab...",.78f);
        BuildPrefab(geometry);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("[LEVEL 5 ROD] SUCCESS — Level 5 visual installed on the Level 4 rod hierarchy; RodTip/ReelMount/gameplay transforms preserved.");
    }

    private static void ExtractPackage(string zipPath,string blendPath,out byte[] textureBytes)
    {
        if(!File.Exists(zipPath))throw new FileNotFoundException(PackageName+" was not found.",zipPath);
        using FileStream stream=File.OpenRead(zipPath);
        using ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read);
        ZipArchiveEntry blend=FindNamed(archive,"Level 5 Fishing Rod.blend")??SingleExt(archive,".blend");
        ZipArchiveEntry stl=FindNamed(archive,"Level 5 Fishing Rod.stl")??SingleExt(archive,".stl");
        ZipArchiveEntry texture=FindNamed(archive,"Level 5 Fishing Rod Textures.jpg")??FindNamed(archive,"Level 5 Fishing Rod Texture.jpg")??SingleImage(archive);
        if(blend==null || stl==null || texture==null)throw new InvalidDataException("The ZIP must contain the Level 5 rod .blend, .stl and texture image.");
        byte[] blendBytes=ReadAll(blend),stlBytes=ReadAll(stl);textureBytes=ReadAll(texture);
        if(blendBytes.Length==0)throw new InvalidDataException("The Level 5 rod .blend is empty.");
        if(stlBytes.Length<84 || BitConverter.ToUInt32(stlBytes,80)!=ExpectedTriangles)throw new InvalidDataException("The Level 5 rod STL does not match the supplied 1340-triangle model.");
        if(!LooksLikeImage(textureBytes))throw new InvalidDataException("The Level 5 rod texture is not a valid JPG/PNG image.");
        Directory.CreateDirectory(Path.GetDirectoryName(blendPath));File.WriteAllBytes(blendPath,blendBytes);
    }

    private static void ExportGeometry(string blender,string blendPath,string jsonPath)
    {
        if(File.Exists(jsonPath))File.Delete(jsonPath);
        string scriptPath=Path.Combine(Path.GetDirectoryName(jsonPath),"ExportLevel5FishingRod.py");
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
if best is None: raise RuntimeError('No mesh object exists in the Level 5 rod Blend.')
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
        ProcessStartInfo info=new ProcessStartInfo
        {
            FileName=blender,
            Arguments="--background "+Quote(blendPath)+" --python "+Quote(scriptPath)+" -- "+Quote(jsonPath),
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true
        };
        using Process process=Process.Start(info);
        if(process==null)throw new InvalidOperationException("Blender could not be started.");
        string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();
        if(!process.WaitForExit(120000)){try{process.Kill();}catch{}throw new TimeoutException("Blender took more than two minutes to read the Level 5 rod.");}
        if(process.ExitCode!=0 || !File.Exists(jsonPath))
        {
            Debug.LogError("Level 5 rod Blender output:\n"+output+"\n\nErrors:\n"+error);
            throw new InvalidOperationException("Blender could not export the Level 5 rod geometry/UVs.");
        }
    }

    private static void ValidateGeometry(Geometry g)
    {
        if(g==null || g.vertices==null || g.vertices.Length==0 || g.vertices.Length%3!=0 ||
           g.uv==null || g.uv.Length!=g.vertices.Length/3*2 ||
           g.triangles==null || g.triangles.Length/3!=ExpectedTriangles)
            throw new InvalidDataException("The exported Level 5 rod geometry/UVs are invalid or do not match 1340 triangles.");
    }

    private static void BuildPrefab(Geometry g)
    {
        GameObject level4=AssetDatabase.LoadAssetAtPath<GameObject>(Level4PrefabPath);
        if(level4==null)throw new InvalidOperationException("Level 4 rod prefab is missing.");
        GameObject root=PrefabUtility.InstantiatePrefab(level4) as GameObject;if(root==null)root=Object.Instantiate(level4);
        try
        {
            if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            root.name="FishingRodReelLevel5Rod";
            Transform blank=FindDeepChild(root.transform,"RodBlank"),tip=FindDeepChild(root.transform,"RodTip"),mount=FindDeepChild(root.transform,"ReelMount");
            FishingRodView view=root.GetComponent<FishingRodView>();
            if(blank==null || tip==null || mount==null || view==null)throw new InvalidOperationException("Level 4 prefab is missing RodBlank, RodTip, ReelMount or FishingRodView.");
            MeshFilter filter=blank.GetComponent<MeshFilter>();MeshRenderer renderer=blank.GetComponent<MeshRenderer>();
            if(filter==null || renderer==null)throw new InvalidOperationException("Level 4 RodBlank renderer is missing.");

            Vector3 tipPosition=tip.localPosition,mountPosition=mount.localPosition;Quaternion mountRotation=mount.localRotation;
            Animation animation=mount.GetComponent<Animation>();AnimationClip reelClip=animation!=null?animation.clip:null;
            Material level4Material=renderer.sharedMaterial;

            Mesh mesh=BuildMesh(g,Mathf.Max(.25f,Mathf.Abs(tip.localPosition.y)));
            Mesh persistent=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if(persistent==null){AssetDatabase.CreateAsset(mesh,MeshPath);persistent=mesh;}
            else{EditorUtility.CopySerialized(mesh,persistent);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(persistent);}
            filter.sharedMesh=persistent;
            renderer.sharedMaterial=BuildMaterial(level4Material);
            renderer.SetPropertyBlock(null);
            renderer.shadowCastingMode=ShadowCastingMode.Off;

            if(Vector3.Distance(tip.localPosition,tipPosition)>.000001f || Vector3.Distance(mount.localPosition,mountPosition)>.000001f ||
               Quaternion.Angle(mount.localRotation,mountRotation)>.0001f || (animation!=null && animation.clip!=reelClip))
                throw new InvalidOperationException("Level 5 import attempted to change Level 4 gameplay transforms or reel animation.");

            GameObject saved=PrefabUtility.SaveAsPrefabAsset(root,Level5PrefabPath);
            if(saved==null)throw new InvalidOperationException("Unity could not save the Level 5 rod prefab.");
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
        Mesh result=new Mesh{name="Level5FishingRod"};if(count>65535)result.indexFormat=IndexFormat.UInt32;
        result.vertices=vertices;result.uv=uv;result.triangles=triangles;result.RecalculateNormals();result.RecalculateBounds();result.RecalculateTangents();return result;
    }

    private static Material BuildMaterial(Material level4Template)
    {
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if(texture==null)throw new InvalidOperationException("Level 5 rod texture failed to import.");
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null)
        {
            Shader shader=level4Template!=null?level4Template.shader:Resources.Load<Shader>("Fishing/FishingEquipment");
            if(shader==null)throw new InvalidOperationException("Fishing equipment shader is missing.");
            material=new Material(shader);AssetDatabase.CreateAsset(material,MaterialPath);
        }
        if(level4Template!=null)material.CopyPropertiesFromMaterial(level4Template);
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
        if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
        material.mainTexture=texture;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureTexture()
    {
        TextureImporter importer=AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Unity did not create a TextureImporter for the Level 5 texture.");
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
        importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=2048;
        importer.SaveAndReimport();
    }

    private static bool TryResolveBlender(bool prompt,out string blender)
    {
        blender=EditorPrefs.GetString(BlenderPrefsKey,string.Empty);
        if(File.Exists(blender))return true;
        string pf=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] versions={"5.0","4.5","4.4","4.3","4.2","4.1","4.0","3.6"};
        foreach(string version in versions)
        {
            string candidate=Path.Combine(pf,"Blender Foundation","Blender "+version,"blender.exe");
            if(File.Exists(candidate)){blender=candidate;EditorPrefs.SetString(BlenderPrefsKey,blender);return true;}
        }
        if(!prompt)return false;
        blender=EditorUtility.OpenFilePanel("Locate blender.exe",pf,"exe");
        if(!File.Exists(blender))return false;
        EditorPrefs.SetString(BlenderPrefsKey,blender);return true;
    }

    private static string FindPackage()
    {
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string project=Directory.GetParent(Application.dataPath)?.FullName;
        string[] roots={Path.Combine(home,"Downloads"),Path.Combine(home,"Desktop"),Path.Combine(home,"Documents"),project};
        foreach(string root in roots)
        {
            if(string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))continue;
            string exact=Path.Combine(root,PackageName);if(File.Exists(exact))return exact;
            try
            {
                string found=Directory.EnumerateFiles(root,"*Level*5*Fishing*Rod*.zip",SearchOption.TopDirectoryOnly).FirstOrDefault();
                if(!string.IsNullOrWhiteSpace(found))return found;
            }
            catch{}
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if(!Directory.Exists(path)){Directory.CreateDirectory(path);AssetDatabase.Refresh();}
    }
    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))if(t!=null && t.name==name)return t;
        return null;
    }
    private static ZipArchiveEntry FindNamed(ZipArchive archive,string name)=>archive.Entries.FirstOrDefault(e=>string.Equals(Path.GetFileName(e.FullName),name,StringComparison.OrdinalIgnoreCase));
    private static ZipArchiveEntry SingleExt(ZipArchive archive,string ext)=>archive.Entries.FirstOrDefault(e=>string.Equals(Path.GetExtension(e.FullName),ext,StringComparison.OrdinalIgnoreCase));
    private static ZipArchiveEntry SingleImage(ZipArchive archive)=>archive.Entries.FirstOrDefault(e=>{string x=Path.GetExtension(e.FullName);return x.Equals(".jpg",StringComparison.OrdinalIgnoreCase)||x.Equals(".jpeg",StringComparison.OrdinalIgnoreCase)||x.Equals(".png",StringComparison.OrdinalIgnoreCase);});
    private static byte[] ReadAll(ZipArchiveEntry e){using Stream s=e.Open();using MemoryStream m=new MemoryStream();s.CopyTo(m);return m.ToArray();}
    private static bool LooksLikeImage(byte[] b)=>b!=null && b.Length>8 && ((b[0]==0xFF&&b[1]==0xD8)||(b[0]==0x89&&b[1]==0x50&&b[2]==0x4E&&b[3]==0x47));
    private static void WriteBytesIfChanged(string path,byte[] bytes){Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes))return;File.WriteAllBytes(path,bytes);}
    private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
    private static Vector3 MapDirection(Vector3 v,int longAxis,int a,int b,float sign)=>new Vector3(Axis(v,a),Axis(v,longAxis)*sign,Axis(v,b));
    private static float Axis(Vector3 v,int axis)=>axis==0?v.x:axis==1?v.y:v.z;
}
