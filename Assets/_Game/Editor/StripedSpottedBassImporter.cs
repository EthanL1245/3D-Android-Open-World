using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;
using Process=System.Diagnostics.Process;
using ProcessStartInfo=System.Diagnostics.ProcessStartInfo;

/// <summary>
/// Imports the exact Striped Bass / Spotted Sand Bass packages supplied by the user.
/// Both are fed through SeaBassImporter's proven five-bone authored-fish pipeline,
/// so they receive RedSnapperPresentation: aquarium route/train-track swimming,
/// normal swim animation, held-fish flopping, mouth/line anchoring and dead-fish
/// presentation all behave identically to the existing authored species.
/// </summary>
public static class StripedSpottedBassImporter
{
    private const string SourceFolder="Assets/_Game/Reef/Source";
    private const string BlenderPrefsKey="OpenWorld.Goatfish.BlenderExecutable";
    private const string AutoSessionKey="OpenWorld.AutoImport.StripedSpottedBass.20260926.v1";

    private sealed class Package
    {
        public string ZipName,AssetName,PrefabPath,BlendMember,FbxMember,TextureMember;
        public Package(string zip,string asset,string prefab,string blend,string fbx,string texture)
        {ZipName=zip;AssetName=asset;PrefabPath=prefab;BlendMember=blend;FbxMember=fbx;TextureMember=texture;}
    }

    private static readonly Package Striped=new Package(
        "Striped Bass.zip","StripedBass","Assets/Resources/Fishing/StripedBass.prefab",
        "Striped Bass.blend",null,"Striped Bass Texture.jpg");
    private static readonly Package Spotted=new Package(
        "Spotted Sand Bass.zip","SpottedSandBass","Assets/Resources/Fishing/SpottedSandBass.prefab",
        "Spotted Sand Bass.blend","Spotted Sand Bass.fbx","Spotted Sand Bass.jpg");

    [InitializeOnLoadMethod]
    private static void AutoImport()
    {
        if(Application.isBatchMode||SessionState.GetBool(AutoSessionKey,false))return;
        SessionState.SetBool(AutoSessionKey,true);
        EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            TryAutoOne(Striped);
            TryAutoOne(Spotted);
        };
    }

    [MenuItem("Tools/Open World/Import Striped + Spotted Sand Bass (One Click)")]
    public static void ImportOneClick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bass Import","Exit Play Mode first.","OK");
            return;
        }
        try
        {
            string striped=FindPackage(Striped.ZipName);
            if(string.IsNullOrEmpty(striped))striped=Choose(Striped.ZipName);
            if(string.IsNullOrEmpty(striped))return;
            string spotted=FindPackage(Spotted.ZipName);
            if(string.IsNullOrEmpty(spotted))spotted=Choose(Spotted.ZipName);
            if(string.IsNullOrEmpty(spotted))return;
            ImportOne(Striped,striped,true);
            ImportOne(Spotted,spotted,true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(Spotted.PrefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("New Bass Ready",
                "Striped Bass and Spotted Sand Bass are installed from your supplied models, textures and animations. Both use the exact same aquarium route swimming, held flopping, mouth line anchoring and dead-fish presentation as the existing authored fish.","OK");
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Bass Import Failed",e.Message+"\n\nSee Console for details.","OK");
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void TryAutoOne(Package package)
    {
        try
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(package.PrefabPath);
            if(IsValidPrefab(prefab))return;
            string zip=FindPackage(package.ZipName);
            if(string.IsNullOrEmpty(zip))return;
            ImportOne(package,zip,false);
            Debug.Log("[NEW BASS] Auto-imported "+package.AssetName+" from "+zip+" with shared aquarium/held-fish behavior.");
        }
        catch(Exception e){Debug.LogError("[NEW BASS] Auto-import failed for "+package.AssetName+": "+e);}
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void ImportOne(Package package,string zipPath,bool interactive)
    {
        EnsureFolder(SourceFolder);
        EnsureFolder("Assets/Resources/Fishing");
        string work=Path.GetFullPath(Path.Combine("Library","FishImports",package.AssetName));
        Directory.CreateDirectory(work);

        EditorUtility.DisplayProgressBar("Import "+package.AssetName,"Reading supplied package...",.10f);
        byte[] textureBytes;
        byte[] fbxBytes=null;
        byte[] blendBytes=null;
        using(FileStream stream=File.OpenRead(zipPath))
        using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            textureBytes=ReadRequired(archive,package.TextureMember);
            if(!LooksLikeImage(textureBytes))throw new InvalidDataException(package.TextureMember+" is not a readable PNG/JPEG.");
            if(!string.IsNullOrEmpty(package.FbxMember))fbxBytes=ReadRequired(archive,package.FbxMember);
            if(!string.IsNullOrEmpty(package.BlendMember))blendBytes=ReadRequired(archive,package.BlendMember);
        }

        string pngAsset=SourceFolder+"/"+package.AssetName+"Texture.png";
        EditorUtility.DisplayProgressBar("Import "+package.AssetName,"Preparing authored texture...",.28f);
        WriteTexturePng(pngAsset,textureBytes);

        string fbxAsset=SourceFolder+"/"+package.AssetName+".fbx";
        string fbxFull=Path.GetFullPath(fbxAsset);
        if(fbxBytes!=null&&fbxBytes.Length>32&&LooksLikeFbx(fbxBytes))
        {
            EditorUtility.DisplayProgressBar("Import "+package.AssetName,"Installing supplied FBX animation/model...",.48f);
            Directory.CreateDirectory(Path.GetDirectoryName(fbxFull));
            File.WriteAllBytes(fbxFull,fbxBytes);
        }
        else
        {
            if(blendBytes==null||blendBytes.Length<32)throw new InvalidDataException(package.ZipName+" has no usable authored model.");
            string blendPath=Path.Combine(work,package.AssetName+".blend");
            File.WriteAllBytes(blendPath,blendBytes);
            if(!TryResolveBlender(interactive,out string blender))
                throw new InvalidOperationException("Striped Bass uses the authored Blender animation, but Blender could not be located. Install Blender or select blender.exe when using the one-click importer.");
            EditorUtility.DisplayProgressBar("Import "+package.AssetName,"Exporting supplied Blender rig + animation...",.48f);
            ExportFbx(blender,blendPath,fbxFull,work);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorUtility.DisplayProgressBar("Import "+package.AssetName,"Building gameplay prefab...",.78f);
        SeaBassImporter.InstallModel(package.AssetName,package.PrefabPath);
        ValidatePrefab(package.PrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[NEW BASS] "+package.AssetName+" ready: authored model/texture/Swim animation + RedSnapperPresentation shared fish contract.");
    }

    private static void ValidatePrefab(string path)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(!IsValidPrefab(prefab))throw new InvalidOperationException("Generated fish prefab is missing its Animator, RedSnapperPresentation, or AuthoredMouthAnchor: "+path);
        RedSnapperPresentation presentation=prefab.GetComponent<RedSnapperPresentation>();
        if(presentation==null)throw new InvalidOperationException("Shared aquarium/held-fish presentation missing from "+path);
        foreach(Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach(Material material in renderer.sharedMaterials)
                if(material==null||material.mainTexture==null)
                    throw new InvalidOperationException("Authored fish texture dependency missing from "+path);
    }

    private static bool IsValidPrefab(GameObject prefab)
    {
        if(prefab==null||prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        if(prefab.GetComponentInChildren<Animator>(true)==null)return false;
        return prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="AuthoredMouthAnchor");
    }

    private static byte[] ReadRequired(ZipArchive archive,string member)
    {
        ZipArchiveEntry entry=archive.Entries.FirstOrDefault(e=>string.Equals(e.FullName,member,StringComparison.OrdinalIgnoreCase));
        if(entry==null)entry=archive.Entries.FirstOrDefault(e=>string.Equals(Path.GetFileName(e.FullName),member,StringComparison.OrdinalIgnoreCase));
        if(entry==null)throw new InvalidDataException("Missing '"+member+"' in "+Path.GetFileName(archive.ToString())+".");
        using(Stream s=entry.Open())using(MemoryStream m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        if(bytes==null||bytes.Length<12)return false;
        bool png=bytes[0]==0x89&&bytes[1]==0x50&&bytes[2]==0x4E&&bytes[3]==0x47;
        bool jpg=bytes[0]==0xFF&&bytes[1]==0xD8&&bytes[2]==0xFF;
        return png||jpg;
    }

    private static bool LooksLikeFbx(byte[] bytes)
    {
        if(bytes==null||bytes.Length<24)return false;
        string header=System.Text.Encoding.ASCII.GetString(bytes,0,Math.Min(bytes.Length,24));
        return header.StartsWith("Kaydara FBX Binary",StringComparison.Ordinal)||header.IndexOf("FBX",StringComparison.OrdinalIgnoreCase)>=0;
    }

    private static void WriteTexturePng(string assetPath,byte[] bytes)
    {
        Texture2D decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(decoded,bytes,false)||decoded.width<2||decoded.height<2)
                throw new InvalidDataException("Unity could not decode the supplied fish texture.");
            byte[] png=decoded.EncodeToPNG();
            string full=Path.GetFullPath(assetPath);Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full,png);
        }
        finally{Object.DestroyImmediate(decoded);}
        AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
    }

    private static void ExportFbx(string blender,string blend,string output,string work)
    {
        string script=Path.Combine(work,"ExportAuthoredFish.py");
        string log=Path.Combine(work,"BlenderExport.log");
        File.WriteAllText(script,@"import bpy, os, sys, math, traceback
out_path=sys.argv[sys.argv.index('--')+1]
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
if not meshes or not arms: raise RuntimeError('Expected fish mesh + armature')
arm=None
for mesh in meshes:
    for mod in mesh.modifiers:
        if mod.type=='ARMATURE' and mod.object is not None:
            arm=mod.object; break
    if arm is not None: break
if arm is None: arm=arms[0]
if arm.animation_data is None: arm.animation_data_create()
action=arm.animation_data.action
actions=list(bpy.data.actions)
if action is None:
    for a in actions:
        n=a.name.lower()
        if 'swim' in n or 'armatureaction' in n or n=='armature': action=a; break
if action is None and actions:
    def span(a):
        try: return float(a.frame_range[1]-a.frame_range[0])
        except: return 0.0
    action=max(actions,key=span)
if action is None: raise RuntimeError('No authored animation action found')
arm.animation_data.action=action
try:
    bpy.context.scene.frame_start=int(math.floor(action.frame_range[0])); bpy.context.scene.frame_end=int(math.ceil(action.frame_range[1]))
except: pass
for o in bpy.context.view_layer.objects:
    try: o.select_set(False)
    except: pass
for o in meshes+[arm]:
    try:
        o.hide_viewport=False; o.hide_render=False; o.hide_set(False); o.select_set(True)
    except: pass
bpy.context.view_layer.objects.active=arm
os.makedirs(os.path.dirname(out_path),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out_path,use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,path_mode='AUTO')
if not os.path.isfile(out_path) or os.path.getsize(out_path)<1024: raise RuntimeError('FBX export failed')
print('EXPORTED',out_path,os.path.getsize(out_path))
");
        Directory.CreateDirectory(Path.GetDirectoryName(output));if(File.Exists(output))File.Delete(output);
        ProcessStartInfo psi=new ProcessStartInfo
        {
            FileName=blender,
            Arguments=Quote(blend)+" --background --python "+Quote(script)+" -- "+Quote(output),
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=work
        };
        using(Process process=Process.Start(psi))
        {
            string stdout=process.StandardOutput.ReadToEnd();string stderr=process.StandardError.ReadToEnd();
            if(!process.WaitForExit(180000)){try{process.Kill();}catch{}throw new TimeoutException("Blender fish export timed out.");}
            File.WriteAllText(log,stdout+"\n--- STDERR ---\n"+stderr);
            if(process.ExitCode!=0||!File.Exists(output)||new FileInfo(output).Length<1024)
                throw new InvalidOperationException("Blender could not export the supplied Striped Bass animation/model. See "+log+".\n"+Tail(stderr,900));
        }
    }

    private static bool TryResolveBlender(bool interactive,out string blender)
    {
        blender=EditorPrefs.GetString(BlenderPrefsKey,string.Empty);
        if(File.Exists(blender))return true;
        List<string> candidates=new List<string>();
        string programFiles=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string foundation=Path.Combine(programFiles,"Blender Foundation");
        if(Directory.Exists(foundation))
            candidates.AddRange(Directory.GetFiles(foundation,"blender.exe",SearchOption.AllDirectories).OrderByDescending(x=>x));
        string[] common={@"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe",@"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe",@"C:\Program Files\Blender Foundation\Blender 4.4\blender.exe",@"C:\Program Files\Blender Foundation\Blender 4.3\blender.exe"};
        candidates.AddRange(common);
        blender=candidates.FirstOrDefault(File.Exists);
        if(!string.IsNullOrEmpty(blender)){EditorPrefs.SetString(BlenderPrefsKey,blender);return true;}
        if(!interactive)return false;
        string chosen=EditorUtility.OpenFilePanel("Select Blender executable",foundation,"exe");
        if(string.IsNullOrEmpty(chosen)||!File.Exists(chosen))return false;
        blender=chosen;EditorPrefs.SetString(BlenderPrefsKey,blender);return true;
    }

    private static string FindPackage(string name)
    {
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots={project,Path.Combine(project,"Packages"),Path.Combine(project,"UserPackages"),Path.Combine(home,"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(home,"OneDrive","Downloads"),Path.Combine(home,"OneDrive","Desktop"),Path.Combine(home,"OneDrive","Documents")};
        foreach(string root in roots)
        {
            if(string.IsNullOrEmpty(root)||!Directory.Exists(root))continue;
            string exact=Path.Combine(root,name);if(File.Exists(exact))return exact;
            try
            {
                string hit=Directory.EnumerateFiles(root,name,SearchOption.TopDirectoryOnly).FirstOrDefault();
                if(!string.IsNullOrEmpty(hit))return hit;
            }
            catch{}
        }
        return null;
    }

    private static string Choose(string expected)
    {
        string path=EditorUtility.OpenFilePanel("Select "+expected,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"zip");
        if(string.IsNullOrEmpty(path))return null;
        if(!string.Equals(Path.GetFileName(path),expected,StringComparison.OrdinalIgnoreCase))
        {
            bool use=EditorUtility.DisplayDialog("Different filename","Expected '"+expected+"' but selected '"+Path.GetFileName(path)+"'. Use it anyway?","Use it","Cancel");
            if(!use)return null;
        }
        return path;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }
    private static string Quote(string value)=>"\""+(value??string.Empty).Replace("\"","\\\"")+"\"";
    private static string Tail(string value,int count)=>string.IsNullOrEmpty(value)?string.Empty:(value.Length<=count?value:value.Substring(value.Length-count));
}
