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
/// Imports the user's Giant Trevally package through the same authored-fish pipeline
/// used by the other rigged species. SeaBassImporter installs RedSnapperPresentation,
/// which supplies aquarium train-track routing/turning, held-fish flopping, animated
/// mouth anchoring and unconscious/dead presentation.
/// </summary>
public static class GiantTrevallyImporter
{
    private const string ZipName="Giant Trevally.zip";
    private const string BlendMember="Giant Trevally.blend";
    private const string TextureMember="Giant Trevally Texture.png";
    private const string AssetName="GiantTrevally";
    private const string SourceFolder="Assets/_Game/Reef/Source";
    private const string PrefabPath="Assets/Resources/Fishing/GiantTrevally.prefab";
    private const string BlenderPrefsKey="OpenWorld.Goatfish.BlenderExecutable";
    private const string AutoSessionKey="OpenWorld.AutoImport.GiantTrevally.20261009.v1";

    [InitializeOnLoadMethod]
    private static void AutoImport()
    {
        if(Application.isBatchMode || SessionState.GetBool(AutoSessionKey,false))return;
        SessionState.SetBool(AutoSessionKey,true);
        EditorApplication.delayCall+=()=>EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                if(IsReady(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)))return;
                string zip=FindPackage();
                if(string.IsNullOrEmpty(zip))
                {
                    Debug.LogWarning("[GIANT TREVALLY] Source package not found. Save Giant Trevally.zip in your project's root or Downloads folder, then use Tools > Open World > Import Giant Trevally (One Click).");
                    return;
                }
                Import(zip,false);
            }
            catch(Exception e){Debug.LogError("[GIANT TREVALLY] Auto-import failed: "+e);}
            finally{EditorUtility.ClearProgressBar();}
        };
    }

    [MenuItem("Tools/Open World/Import Giant Trevally (One Click)")]
    public static void ImportOneClick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Giant Trevally Import","Exit Play Mode first.","OK");
            return;
        }

        try
        {
            string zip=FindPackage();
            if(string.IsNullOrEmpty(zip))
                zip=EditorUtility.OpenFilePanel("Select "+ZipName,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"zip");
            if(string.IsNullOrEmpty(zip))return;
            Import(zip,true);
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Giant Trevally Import Failed",e.Message+"\n\nSee Console for details.","OK");
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void Import(string zipPath,bool interactive)
    {
        EnsureFolder(SourceFolder);
        EnsureFolder("Assets/Resources/Fishing");

        string work=Path.GetFullPath(Path.Combine("Library","FishImports",AssetName));
        Directory.CreateDirectory(work);

        EditorUtility.DisplayProgressBar("Giant Trevally","Reading supplied package...",.10f);
        byte[] blendBytes;
        byte[] textureBytes;
        using(FileStream stream=File.OpenRead(zipPath))
        using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            blendBytes=ReadRequired(archive,BlendMember);
            textureBytes=ReadRequired(archive,TextureMember);
        }
        if(blendBytes.Length<1024)throw new InvalidDataException("Giant Trevally.blend is not a usable Blender model.");
        if(!LooksLikeImage(textureBytes))throw new InvalidDataException("Giant Trevally Texture.png is not a readable PNG/JPEG.");

        string textureAsset=SourceFolder+"/"+AssetName+"Texture.png";
        EditorUtility.DisplayProgressBar("Giant Trevally","Installing supplied texture...",.25f);
        WriteTexturePng(textureAsset,textureBytes);

        string blendPath=Path.Combine(work,AssetName+".blend");
        File.WriteAllBytes(blendPath,blendBytes);
        string fbxAsset=SourceFolder+"/"+AssetName+".fbx";
        string fbxFull=Path.GetFullPath(fbxAsset);
        Directory.CreateDirectory(Path.GetDirectoryName(fbxFull));

        if(!TryResolveBlender(interactive,out string blender))
            throw new InvalidOperationException("The supplied Giant Trevally is a Blender rig. Blender could not be located automatically. Run the one-click importer and select blender.exe once.");

        EditorUtility.DisplayProgressBar("Giant Trevally","Exporting supplied rig + authored swim action...",.48f);
        ExportFbx(blender,blendPath,fbxFull,work);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        EditorUtility.DisplayProgressBar("Giant Trevally","Building gameplay prefab with shared fish presentation...",.76f);
        SeaBassImporter.InstallModel(AssetName,PrefabPath,SourceFolder,textureAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Validate(prefab);
        FishingTuning.Reload();
        if(!FishingTuning.IsValid)
            throw new InvalidOperationException("Giant Trevally prefab imported, but fishing tuning is invalid: "+FishingTuning.ValidationError);

        Selection.activeObject=prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[GIANT TREVALLY] SUCCESS — supplied model/texture/authored swim animation installed with shared train-track aquarium turning + held-flopping presentation.");
        if(interactive)
            EditorUtility.DisplayDialog("Giant Trevally Ready","Giant Trevally is installed using the same aquarium train-track swimming/turning and held-fish flopping system as the other authored fish.","OK");
    }

    private static void Validate(GameObject prefab)
    {
        if(!IsReady(prefab))
            throw new InvalidOperationException("Generated Giant Trevally prefab is missing its Animator, RedSnapperPresentation or AuthoredMouthAnchor.");

        foreach(Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials=renderer.sharedMaterials;
            if(materials==null || materials.Length==0)throw new InvalidOperationException("Giant Trevally renderer has no material.");
            foreach(Material material in materials)
            {
                if(material==null)throw new InvalidOperationException("Giant Trevally renderer has a missing material.");
                Texture texture=material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap"):material.mainTexture;
                if(texture==null)throw new InvalidOperationException("Giant Trevally authored texture is missing from the generated prefab.");
            }
        }
    }

    private static bool IsReady(GameObject prefab)
    {
        if(prefab==null || prefab.GetComponent<RedSnapperPresentation>()==null)return false;
        if(prefab.GetComponentInChildren<Animator>(true)==null)return false;
        return prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="AuthoredMouthAnchor");
    }

    private static void ExportFbx(string blender,string blend,string output,string work)
    {
        string script=Path.Combine(work,"ExportGiantTrevally.py");
        string log=Path.Combine(work,"BlenderExport.log");
        File.WriteAllText(script,@"import bpy, os, sys
out_path=sys.argv[sys.argv.index('--')+1]
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
if not meshes or not arms: raise RuntimeError('Expected a fish mesh and armature in supplied blend')
arm=None
for mesh in meshes:
    for mod in mesh.modifiers:
        if mod.type=='ARMATURE' and mod.object is not None:
            arm=mod.object
            break
    if arm is not None: break
if arm is None: arm=arms[0]
if arm.animation_data is None: arm.animation_data_create()
action=arm.animation_data.action
actions=list(bpy.data.actions)
if action is None:
    for candidate in actions:
        n=candidate.name.lower()
        if 'swim' in n or 'armatureaction' in n or n=='armature':
            action=candidate
            break
if action is None and actions:
    def span(a):
        try: return float(a.frame_range[1]-a.frame_range[0])
        except: return 0.0
    action=max(actions,key=span)
if action is None: raise RuntimeError('No authored animation action found in supplied Giant Trevally blend')
arm.animation_data.action=action
try:
    bpy.context.scene.frame_start=int(action.frame_range[0])
    bpy.context.scene.frame_end=int(action.frame_range[1])
except: pass
for obj in bpy.context.view_layer.objects:
    try: obj.select_set(False)
    except: pass
for obj in meshes+[arm]:
    try:
        obj.hide_viewport=False
        obj.hide_render=False
        obj.hide_set(False)
        obj.select_set(True)
    except: pass
bpy.context.view_layer.objects.active=arm
os.makedirs(os.path.dirname(out_path),exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out_path,use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,path_mode='AUTO')
if not os.path.isfile(out_path) or os.path.getsize(out_path)<1024: raise RuntimeError('FBX export failed')
print('EXPORTED',out_path,os.path.getsize(out_path),action.name)
");

        if(File.Exists(output))File.Delete(output);
        ProcessStartInfo psi=new ProcessStartInfo
        {
            FileName=blender,
            Arguments=Quote(blend)+" --background --python "+Quote(script)+" -- "+Quote(output),
            UseShellExecute=false,
            CreateNoWindow=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            WorkingDirectory=work
        };
        using(Process process=Process.Start(psi))
        {
            string stdout=process.StandardOutput.ReadToEnd();
            string stderr=process.StandardError.ReadToEnd();
            if(!process.WaitForExit(180000))
            {
                try{process.Kill();}catch{}
                throw new TimeoutException("Blender Giant Trevally export timed out.");
            }
            File.WriteAllText(log,stdout+"\n--- STDERR ---\n"+stderr);
            if(process.ExitCode!=0 || !File.Exists(output) || new FileInfo(output).Length<1024)
                throw new InvalidOperationException("Blender could not export the supplied Giant Trevally rig/animation. See "+log+".\n"+Tail(stderr,900));
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
        {
            try{candidates.AddRange(Directory.GetFiles(foundation,"blender.exe",SearchOption.AllDirectories).OrderByDescending(x=>x));}
            catch{}
        }
        candidates.Add(@"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe");
        candidates.Add(@"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe");
        candidates.Add(@"C:\Program Files\Blender Foundation\Blender 4.4\blender.exe");
        candidates.Add(@"C:\Program Files\Blender Foundation\Blender 4.3\blender.exe");
        blender=candidates.FirstOrDefault(File.Exists);
        if(!string.IsNullOrEmpty(blender))
        {
            EditorPrefs.SetString(BlenderPrefsKey,blender);
            return true;
        }

        if(!interactive)return false;
        string chosen=EditorUtility.OpenFilePanel("Select Blender executable",foundation,"exe");
        if(string.IsNullOrEmpty(chosen) || !File.Exists(chosen))return false;
        blender=chosen;
        EditorPrefs.SetString(BlenderPrefsKey,blender);
        return true;
    }

    private static byte[] ReadRequired(ZipArchive archive,string member)
    {
        ZipArchiveEntry entry=archive.Entries.FirstOrDefault(e=>string.Equals(e.FullName,member,StringComparison.OrdinalIgnoreCase));
        if(entry==null)entry=archive.Entries.FirstOrDefault(e=>string.Equals(Path.GetFileName(e.FullName),member,StringComparison.OrdinalIgnoreCase));
        if(entry==null)throw new InvalidDataException("Missing '"+member+"' in supplied Giant Trevally ZIP.");
        using(Stream stream=entry.Open())using(MemoryStream memory=new MemoryStream())
        {stream.CopyTo(memory);return memory.ToArray();}
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        if(bytes==null || bytes.Length<12)return false;
        bool png=bytes[0]==0x89&&bytes[1]==0x50&&bytes[2]==0x4E&&bytes[3]==0x47;
        bool jpg=bytes[0]==0xFF&&bytes[1]==0xD8&&bytes[2]==0xFF;
        return png||jpg;
    }

    private static void WriteTexturePng(string assetPath,byte[] bytes)
    {
        Texture2D decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(decoded,bytes,false) || decoded.width<2 || decoded.height<2)
                throw new InvalidDataException("Unity could not decode Giant Trevally Texture.png.");
            string full=Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full,decoded.EncodeToPNG());
        }
        finally{Object.DestroyImmediate(decoded);}
        AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
    }

    private static string FindPackage()
    {
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots={
            project,Path.Combine(project,"Packages"),Path.Combine(project,"UserPackages"),
            Path.Combine(home,"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(home,"OneDrive","Downloads"),
            Path.Combine(home,"OneDrive","Desktop"),Path.Combine(home,"OneDrive","Documents")
        };
        foreach(string root in roots)
        {
            if(string.IsNullOrEmpty(root) || !Directory.Exists(root))continue;
            string exact=Path.Combine(root,ZipName);
            if(File.Exists(exact))return exact;
            try
            {
                string hit=Directory.EnumerateFiles(root,"*.zip",SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f=>string.Equals(Path.GetFileName(f),ZipName,StringComparison.OrdinalIgnoreCase));
                if(!string.IsNullOrEmpty(hit))return hit;
            }
            catch{}
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts=path.Split('/');
        string current=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=current+"/"+parts[i];
            if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
            current=next;
        }
    }

    private static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
    private static string Tail(string text,int max)=>string.IsNullOrEmpty(text)?string.Empty:(text.Length<=max?text:text.Substring(text.Length-max));
}
