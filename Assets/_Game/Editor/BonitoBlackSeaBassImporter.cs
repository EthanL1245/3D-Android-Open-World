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
/// Imports the authored Bonito and Black Sea Bass packages through the same proven
/// five-bone fish pipeline as Sea Bass/Red Snapper. The resulting prefabs receive
/// RedSnapperPresentation, which is the game's generic authored-fish contract for
/// aquarium train-track swimming and held/flopping behavior.
/// </summary>
public static class BonitoBlackSeaBassImporter
{
    private const string BonitoPackage="Bonito.zip";
    private const string BassPackage="Black Sea Bass.zip";
    private const string BlenderPrefsKey="OpenWorld.Goatfish.BlenderExecutable";
    private const string AutoSessionKey="OpenWorld.AutoImport.BonitoBlackSeaBass.20260926.v2";
    private const string SourceFolder="Assets/_Game/Reef/Source";

    private sealed class FishPackage
    {
        public string ZipName;
        public string AssetName;
        public string PrefabPath;
        public uint ExpectedTriangles;
        public FishPackage(string zip,string asset,string prefab,uint triangles)
        {ZipName=zip;AssetName=asset;PrefabPath=prefab;ExpectedTriangles=triangles;}
    }

    private static readonly FishPackage Bonito=new FishPackage(
        BonitoPackage,"Bonito","Assets/Resources/Fishing/Bonito.prefab",1502);
    private static readonly FishPackage BlackSeaBass=new FishPackage(
        BassPackage,"BlackSeaBass","Assets/Resources/Fishing/BlackSeaBass.prefab",1461);

    [InitializeOnLoadMethod]
    private static void AutoImport()
    {
        if(Application.isBatchMode || SessionState.GetBool(AutoSessionKey,false))return;
        SessionState.SetBool(AutoSessionKey,true);
        EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            string bonito=FindPackage(Bonito.ZipName),bass=FindPackage(BlackSeaBass.ZipName);
            if(string.IsNullOrWhiteSpace(bonito) || string.IsNullOrWhiteSpace(bass))return;
            if(!TryResolveBlender(false,out string blender))return;
            try
            {
                ImportOne(Bonito,bonito,blender);
                ImportOne(BlackSeaBass,bass,blender);
                Debug.Log("Bonito + Black Sea Bass imported with shared aquarium/held-fish behavior.");
            }
            catch(Exception e){Debug.LogException(e);}
            finally{EditorUtility.ClearProgressBar();}
        };
    }

    [MenuItem("Tools/Open World/Import Bonito + Black Sea Bass (One Click)")]
    public static void ImportOneClick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Fish Import","Exit Play Mode first.","OK");
            return;
        }

        string bonito=FindPackage(Bonito.ZipName);
        if(string.IsNullOrWhiteSpace(bonito))bonito=Choose(Bonito.ZipName);
        if(string.IsNullOrWhiteSpace(bonito))return;
        string bass=FindPackage(BlackSeaBass.ZipName);
        if(string.IsNullOrWhiteSpace(bass))bass=Choose(BlackSeaBass.ZipName);
        if(string.IsNullOrWhiteSpace(bass))return;
        if(!TryResolveBlender(true,out string blender))return;

        try
        {
            ImportOne(Bonito,bonito,blender);
            ImportOne(BlackSeaBass,bass,blender);
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(BlackSeaBass.PrefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog(
                "Bonito + Black Sea Bass Ready",
                "Both fish are installed with their authored models/animations. They use the standard five-bone fish presentation, including aquarium train-track swimming and the same held/flopping behavior as the existing authored fish.",
                "OK");
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Fish Import Failed",e.Message+"\n\nSee the Unity Console for details.","OK");
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void ImportOne(FishPackage fish,string zipPath,string blender)
    {
        EnsureFolder(SourceFolder);
        EnsureFolder("Assets/Resources/Fishing");
        string library=Path.GetFullPath(Path.Combine("Library","FishImports",fish.AssetName));
        Directory.CreateDirectory(library);
        string blendPath=Path.Combine(library,fish.AssetName+".blend");
        string fbxAsset=SourceFolder+"/"+fish.AssetName+".fbx";
        string pngAsset=SourceFolder+"/"+fish.AssetName+"Texture.png";
        string fbxFull=Path.GetFullPath(fbxAsset);

        EditorUtility.DisplayProgressBar("Import "+fish.AssetName,"Validating supplied package...",0.12f);
        Extract(zipPath,blendPath,fish.ExpectedTriangles,out byte[] imageBytes);

        EditorUtility.DisplayProgressBar("Import "+fish.AssetName,"Converting authored texture...",0.32f);
        WriteTexturePng(pngAsset,imageBytes);

        EditorUtility.DisplayProgressBar("Import "+fish.AssetName,"Exporting rig + swim action from Blender...",0.52f);
        ExportFbx(blender,blendPath,fbxFull,library);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        EditorUtility.DisplayProgressBar("Import "+fish.AssetName,"Building gameplay prefab...",0.78f);
        SeaBassImporter.InstallModel(fish.AssetName,fish.PrefabPath);
        ValidatePrefab(fish.PrefabPath);
        AssetDatabase.SaveAssets();
    }

    private static void Extract(string zipPath,string blendPath,uint expectedTriangles,out byte[] imageBytes)
    {
        if(!File.Exists(zipPath))throw new FileNotFoundException("Fish ZIP was not found.",zipPath);
        using(FileStream stream=File.OpenRead(zipPath))
        using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            ZipArchiveEntry blend=SingleWithExtension(archive,".blend");
            ZipArchiveEntry stl=SingleWithExtension(archive,".stl");
            ZipArchiveEntry image=SingleImage(archive);
            if(blend==null || stl==null || image==null)
                throw new InvalidDataException(Path.GetFileName(zipPath)+" must contain exactly one .blend, one .stl and one texture image.");
            byte[] blendBytes=ReadAll(blend),stlBytes=ReadAll(stl);
            imageBytes=ReadAll(image);
            if(blendBytes.Length==0)throw new InvalidDataException("Fish .blend file is empty.");
            if(stlBytes.Length<84 || BitConverter.ToUInt32(stlBytes,80)!=expectedTriangles)
                throw new InvalidDataException("Unexpected fish STL revision. Expected "+expectedTriangles+" triangles.");
            if(!LooksLikeImage(imageBytes))throw new InvalidDataException("Fish texture is not a valid PNG/JPEG image.");
            Directory.CreateDirectory(Path.GetDirectoryName(blendPath));
            File.WriteAllBytes(blendPath,blendBytes);
        }
    }

    private static void WriteTexturePng(string assetPath,byte[] imageBytes)
    {
        Texture2D decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(decoded,imageBytes,false) || decoded.width<2 || decoded.height<2)
                throw new InvalidDataException("Unity could not decode the supplied fish texture.");
            byte[] png=decoded.EncodeToPNG();
            string full=Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            if(!File.Exists(full) || !File.ReadAllBytes(full).SequenceEqual(png))File.WriteAllBytes(full,png);
        }
        finally{Object.DestroyImmediate(decoded);}
        AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
    }

    private static void ExportFbx(string blender,string blendPath,string fbxPath,string workFolder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fbxPath));
        if(File.Exists(fbxPath))File.Delete(fbxPath);
        string scriptPath=Path.Combine(workFolder,"ExportFish.py");
        string logPath=Path.Combine(workFolder,"BlenderExport.log");
        string python=@"import bpy, os, sys, math, traceback
args=sys.argv
out_path=args[args.index('--')+1]

armatures=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
if not armatures or not meshes:
    raise RuntimeError('Expected an armature and at least one mesh in the fish Blend.')

# Use the armature actually bound to the fish mesh whenever possible. This is
# more reliable than simply taking the first armature in files containing helpers.
arm=None
for mesh in meshes:
    for modifier in mesh.modifiers:
        if modifier.type=='ARMATURE' and modifier.object is not None:
            arm=modifier.object
            break
    if arm is not None:
        break
if arm is None:
    arm=armatures[0]

if arm.animation_data is None:
    arm.animation_data_create()

action=arm.animation_data.action
actions=list(bpy.data.actions)

# Blender 4.4+/5.x can store layered/slotted Actions. Action.fcurves is not
# available for every Action type anymore, so never inspect fcurves here.
# Prefer the action already assigned to the fish armature, then a clearly named
# swim/armature action, then the longest authored action by frame range.
if action is None:
    for candidate in actions:
        lower=candidate.name.lower()
        if 'swim' in lower or 'armatureaction' in lower or lower=='armature':
            action=candidate
            break
if action is None and actions:
    def span(candidate):
        try:
            r=candidate.frame_range
            return float(r[1]-r[0])
        except Exception:
            return 0.0
    action=max(actions,key=span)
if action is None:
    raise RuntimeError('Fish Blend has no authored animation action.')

arm.animation_data.action=action
scene=bpy.context.scene
try:
    scene.frame_start=int(math.floor(action.frame_range[0]))
    scene.frame_end=int(math.ceil(action.frame_range[1]))
except Exception:
    scene.frame_start=1
    scene.frame_end=max(scene.frame_end,2)
scene.frame_set(scene.frame_start)

# Avoid context-sensitive select_all in background mode. Explicitly unhide and
# select the mesh + bound armature, matching the proven Red Snapper importer.
for obj in bpy.context.view_layer.objects:
    try:
        obj.select_set(False)
    except Exception:
        pass
for obj in meshes+[arm]:
    obj.hide_viewport=False
    obj.hide_render=False
    try:
        obj.hide_set(False)
    except Exception:
        pass
    try:
        obj.select_set(True)
    except Exception:
        pass
try:
    bpy.context.view_layer.objects.active=arm
except Exception:
    pass

os.makedirs(os.path.dirname(out_path),exist_ok=True)
if os.path.exists(out_path):
    os.remove(out_path)

bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    object_types={'ARMATURE','MESH'},
    apply_unit_scale=True,
    bake_space_transform=False,
    axis_forward='-Z',
    axis_up='Y',
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode='AUTO')
if not os.path.exists(out_path):
    raise RuntimeError('Blender did not create the FBX.')
print('FISH_ARMATURE='+arm.name)
print('FISH_ACTION='+action.name)
print('FISH_FRAMES='+str(scene.frame_start)+':'+str(scene.frame_end))
";
        File.WriteAllText(scriptPath,python);
        ProcessStartInfo info=new ProcessStartInfo
        {
            FileName=blender,
            Arguments="--background "+Quote(blendPath)+" --python "+Quote(scriptPath)+" -- "+Quote(fbxPath),
            UseShellExecute=false,
            CreateNoWindow=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true
        };
        using(Process process=Process.Start(info))
        {
            if(process==null)throw new InvalidOperationException("Blender could not be started.");
            string output=process.StandardOutput.ReadToEnd();
            string error=process.StandardError.ReadToEnd();
            if(!process.WaitForExit(120000))
            {
                try{process.Kill();}catch{}
                throw new TimeoutException("Blender took more than two minutes to export the fish.");
            }

            try
            {
                File.WriteAllText(
                    logPath,
                    "BLENDER: "+blender+Environment.NewLine+
                    "BLEND: "+blendPath+Environment.NewLine+
                    "FBX: "+fbxPath+Environment.NewLine+Environment.NewLine+
                    "STDOUT"+Environment.NewLine+output+Environment.NewLine+Environment.NewLine+
                    "STDERR"+Environment.NewLine+error);
            }
            catch{}

            if(process.ExitCode!=0 || !File.Exists(fbxPath))
            {
                Debug.LogError("Fish Blender output:\n"+output+"\n\nErrors:\n"+error+"\n\nSaved log: "+logPath);
                string detail=LastNonEmptyLine(error);
                if(string.IsNullOrWhiteSpace(detail))detail=LastNonEmptyLine(output);
                throw new InvalidOperationException(
                    "Blender opened the fish source but could not export it."+
                    (string.IsNullOrWhiteSpace(detail)?string.Empty:"\n\nBlender: "+detail)+
                    "\n\nA full log was saved to:\n"+logPath);
            }

            Debug.Log("Fish Blender export completed.\n"+output);
        }
    }

    private static string LastNonEmptyLine(string value)
    {
        if(string.IsNullOrWhiteSpace(value))return string.Empty;
        string[] lines=value.Replace("\r",string.Empty).Split('\n');
        for(int i=lines.Length-1;i>=0;i--)
        {
            string line=lines[i].Trim();
            if(!string.IsNullOrWhiteSpace(line))return line;
        }
        return string.Empty;
    }

    private static void ValidatePrefab(string prefabPath)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(prefab==null)throw new InvalidOperationException("Fish prefab was not created: "+prefabPath);
        if(prefab.GetComponent<RedSnapperPresentation>()==null)
            throw new InvalidOperationException("Fish prefab is missing the standard aquarium/held-fish presentation component.");
        Transform[] all=prefab.GetComponentsInChildren<Transform>(true);
        if(!all.Any(t=>t!=null && t.name=="AuthoredMouthAnchor"))
            throw new InvalidOperationException("Fish prefab is missing its authored mouth anchor.");
        if(prefab.GetComponentInChildren<Animator>(true)==null)
            throw new InvalidOperationException("Fish prefab is missing its authored swim Animator.");
    }

    private static string Choose(string packageName)
    {
        string downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        return EditorUtility.OpenFilePanel("Choose "+packageName,Directory.Exists(downloads)?downloads:string.Empty,"zip");
    }

    private static string FindPackage(string packageName)
    {
        string user=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach(string folder in new[]{Path.Combine(user,"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.GetFullPath(".")})
        {
            if(!Directory.Exists(folder))continue;
            string exact=Path.Combine(folder,packageName);
            if(File.Exists(exact))return exact;
            string normalized=Normalize(Path.GetFileNameWithoutExtension(packageName));
            string match=Directory.GetFiles(folder,"*.zip",SearchOption.TopDirectoryOnly)
                .Where(f=>Normalize(Path.GetFileNameWithoutExtension(f)).Contains(normalized))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(!string.IsNullOrWhiteSpace(match))return match;
        }
        return null;
    }

    private static bool TryResolveBlender(bool allowDialog,out string blender)
    {
        blender=EditorPrefs.GetString(BlenderPrefsKey,string.Empty);
        if(!string.IsNullOrWhiteSpace(blender) && File.Exists(blender))return true;
        var candidates=new List<string>();
        foreach(string baseFolder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
        {
            string root=Path.Combine(baseFolder,"Blender Foundation");
            if(Directory.Exists(root))candidates.AddRange(Directory.GetFiles(root,"blender.exe",SearchOption.AllDirectories));
        }
        blender=candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if(!string.IsNullOrWhiteSpace(blender)){EditorPrefs.SetString(BlenderPrefsKey,blender);return true;}
        if(!allowDialog)return false;
        blender=EditorUtility.OpenFilePanel("Locate Blender",string.Empty,"exe");
        if(string.IsNullOrWhiteSpace(blender) || !File.Exists(blender))return false;
        EditorPrefs.SetString(BlenderPrefsKey,blender);return true;
    }

    private static ZipArchiveEntry SingleWithExtension(ZipArchive archive,string extension)
    {
        ZipArchiveEntry[] matches=archive.Entries.Where(e=>!string.IsNullOrEmpty(e.Name) && string.Equals(Path.GetExtension(e.Name),extension,StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length==1?matches[0]:null;
    }

    private static ZipArchiveEntry SingleImage(ZipArchive archive)
    {
        ZipArchiveEntry[] matches=archive.Entries.Where(e=>
        {
            string ext=Path.GetExtension(e.Name).ToLowerInvariant();
            return ext==".png" || ext==".jpg" || ext==".jpeg";
        }).ToArray();
        return matches.Length==1?matches[0]:null;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using(Stream input=entry.Open())using(MemoryStream output=new MemoryStream()){input.CopyTo(output);return output.ToArray();}
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        return bytes!=null && bytes.Length>=8 &&
            ((bytes[0]==0x89 && bytes[1]==0x50 && bytes[2]==0x4E && bytes[3]==0x47) ||
             (bytes[0]==0xFF && bytes[1]==0xD8 && bytes[2]==0xFF));
    }

    private static string Normalize(string value)=>new string((value??string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";

    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        int slash=path.LastIndexOf('/');
        if(slash<=0)return;
        string parent=path.Substring(0,slash);
        EnsureFolder(parent);
        if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
    }
}
