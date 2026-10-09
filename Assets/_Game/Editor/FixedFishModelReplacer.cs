using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// Visual-only replacement of the EXISTING Mackerel (ID 0) and Yellowfin Tuna (ID 5).
/// Uses each fish's original Unity importer and presentation component. Does not
/// touch species IDs, fish balance, inventory, biomes, animations at runtime or UI.
/// The index/store thumbnails are rendered from the same Resources/Fishing prefabs.
/// </summary>
public static class FixedFishModelReplacer
{
    private const string MackerelZip = "Mackerel Fixed.zip";
    private const string TunaZip = "YellowFin Tuna Fixed.zip";
    private const string BlenderPrefsKey = "OpenWorld.Goatfish.BlenderExecutable";

    // Intentionally do not import every time Unity opens: installing an art package
    // is tracked per-project/per-archive revision, with a manual force-reimport option.
    [InitializeOnLoadMethod]
    private static void AutoImportWhenAvailable()
    {
        if(Application.isBatchMode)return;
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            TryAuto(MackerelZip,false);
            TryAuto(TunaZip,true);
        };
    }

    [MenuItem("Tools/Open World/Replace Fixed Mackerel and Yellowfin Tuna")]
    public static void ReplaceBoth()
    {
        ImportInteractive(MackerelZip,false);
        ImportInteractive(TunaZip,true);
    }

    [MenuItem("Tools/Open World/Replace Fixed Mackerel Only")]
    public static void ReplaceMackerel()
    {
        ImportInteractive(MackerelZip,false);
    }

    [MenuItem("Tools/Open World/Replace Fixed Yellowfin Tuna Only")]
    public static void ReplaceTuna()
    {
        ImportInteractive(TunaZip,true);
    }

    private static void TryAuto(string filename,bool tuna)
    {
        string archive=FindPackage(filename);
        if(string.IsNullOrEmpty(archive))return;
        string key=InstallKey(tuna);
        string signature=Signature(archive);
        string prefab=tuna?"Assets/Resources/Fishing/YellowfinTuna.prefab":"Assets/Resources/Fishing/Mackerel.prefab";
        if(EditorPrefs.GetString(key,string.Empty)==signature &&
            AssetDatabase.LoadAssetAtPath<GameObject>(prefab)!=null)return;
        try
        {
            Replace(archive,tuna,false);
            EditorPrefs.SetString(key,signature);
        }
        catch(Exception e)
        {
            Debug.LogError("[FIXED FISH MODELS] Could not auto-import "+filename+". Use Tools > Open World > Replace Fixed ... to retry. "+e);
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static void ImportInteractive(string filename,bool tuna)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Replace Fish Models","Exit Play Mode before importing fish models.","OK");
            return;
        }
        string archive=FindPackage(filename);
        if(string.IsNullOrEmpty(archive))
            archive=EditorUtility.OpenFilePanel("Select "+filename,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"zip");
        if(string.IsNullOrEmpty(archive))return;
        try
        {
            Replace(archive,tuna,true);
            EditorPrefs.SetString(InstallKey(tuna),Signature(archive));
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Fish Model Replacement Failed",
                filename+": "+e.Message+"\n\nSee Unity Console for details.","OK");
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    private static string InstallKey(bool tuna) =>
        "OpenWorld.FixedFishArt."+(tuna?"Yellowfin.v3":"Mackerel.v2")+"."+Application.dataPath.GetHashCode();
    private static string Signature(string archive)
    {
        var info=new FileInfo(archive);
        return Path.GetFullPath(archive)+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks;
    }

    private static void Replace(string archive,bool tuna,bool interactive)
    {
        string label=tuna?"Yellowfin Tuna":"Mackerel";
        string blendName=tuna?"YellowFin Tuna.blend":"Mackerel.blend";
        string textureName=tuna?"Yellowfin Tuna Texture Fixed.png":"Mackerel Texture.png";
        if(!File.Exists(archive))throw new FileNotFoundException(label+" ZIP missing.",archive);

        byte[] modelBytes;
        byte[] textureBytes;
        using(var stream=File.OpenRead(archive))
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            modelBytes=ReadByName(zip,blendName);
            textureBytes=ReadByName(zip,textureName);
        }
        if(modelBytes.Length<1000 || modelBytes[0]!=(byte)'B' || modelBytes[1]!=(byte)'L')
            throw new InvalidDataException(label+" .blend file is invalid.");
        if(textureBytes.Length<32 || textureBytes[0]!=0x89 || textureBytes[1]!=0x50 ||
           textureBytes[2]!=0x4e || textureBytes[3]!=0x47)
            throw new InvalidDataException(label+" texture is not a PNG.");

        string work=Path.GetFullPath(Path.Combine("Library","FixedFishImports",tuna?"YellowfinTuna":"Mackerel"));
        Directory.CreateDirectory(work);
        string blend=Path.Combine(work,"NewFish.blend");
        string fbx=Path.Combine(work,tuna?"YellowfinTuna.fbx":"Mackerel.fbx");
        string convertedZip=Path.Combine(work,"PreparedFish.zip");
        File.WriteAllBytes(blend,modelBytes);
        EditorUtility.DisplayProgressBar(label,"Exporting supplied Blender mesh and UVs...",.15f);

        string blender=ResolveBlender(interactive);
        if(string.IsNullOrEmpty(blender))
            throw new InvalidOperationException("Blender executable was not found. Install Blender or use the manual menu command and select blender.exe.");
        // Both replacement models have authored skeletal swim actions. Do not flatten tuna.
        ExportBlender(blender,blend,fbx,work,false);
        if(!File.Exists(fbx) || new FileInfo(fbx).Length<1000)
            throw new InvalidDataException("Blender produced no usable fish FBX.");

        EditorUtility.DisplayProgressBar(label,"Applying new model + texture to existing fish prefab...",.65f);
        using(var stream=File.Create(convertedZip))
        using(var output=new ZipArchive(stream,ZipArchiveMode.Create))
        {
            AddEntry(output,Path.GetFileName(fbx),File.ReadAllBytes(fbx));
            // Yellowfin's original importer owns a JPEG asset at an established
            // path. Convert actual PNG pixels to JPEG before handing it to Unity,
            // instead of giving a .jpg file a PNG header.
            AddEntry(output,tuna?"YellowfinTuna_Texture.jpg":"Mackerel_Texture.png",
                tuna?ConvertPngToJpeg(textureBytes):textureBytes);
        }
        if(tuna)
            YellowfinTunaImporter.ImportFixedRiggedModelFromZip(convertedZip);
        else
            MackerelImporter.ImportFromZip(convertedZip,false);

        string prefabPath=tuna?"Assets/Resources/Fishing/YellowfinTuna.prefab":
            "Assets/Resources/Fishing/Mackerel.prefab";
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if(prefab==null)throw new InvalidOperationException("Updated fish prefab was not saved.");
        if(tuna && (prefab.GetComponent<YellowfinTunaPresentation>()==null ||
                    prefab.GetComponentInChildren<Animator>(true)==null ||
                    prefab.GetComponentInChildren<Animator>(true).runtimeAnimatorController==null))
            throw new InvalidOperationException("Yellowfin presentation was not preserved.");
        if(!tuna && (prefab.GetComponent<MackerelPresentation>()==null ||
                     prefab.GetComponentInChildren<Animator>(true)==null))
            throw new InvalidOperationException("Mackerel original swim controller was not preserved.");
        if(!prefab.GetComponentsInChildren<Renderer>(true).Any())
            throw new InvalidOperationException("Updated model has no renderer.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject=prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[FIXED FISH MODELS] Replaced "+label+" model and texture. Existing species data, presentation and UI prefab path remain unchanged.");
        if(interactive)
            EditorUtility.DisplayDialog("Fish Art Replaced",label+" model and texture replaced. Existing movement, fish index previews, stats and saved catches are unchanged.","OK");
    }

    private static byte[] ConvertPngToJpeg(byte[] png)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(texture,png,false) || texture.width<2 || texture.height<2)
                throw new InvalidDataException("Unable to decode new Yellowfin texture.");
            return texture.EncodeToJPG(98);
        }
        finally{Object.DestroyImmediate(texture);}
    }

    private static void AddEntry(ZipArchive zip,string name,byte[] data)
    {
        var entry=zip.CreateEntry(name,System.IO.Compression.CompressionLevel.Fastest);
        using(var file=entry.Open())file.Write(data,0,data.Length);
    }

    private static byte[] ReadByName(ZipArchive zip,string name)
    {
        var entry=zip.Entries.FirstOrDefault(e=>
            string.Equals(Path.GetFileName(e.FullName),name,StringComparison.OrdinalIgnoreCase));
        if(entry==null)throw new InvalidDataException("Required "+name+" is missing in the ZIP.");
        using(var input=entry.Open())
        using(var output=new MemoryStream())
        {
            input.CopyTo(output);
            return output.ToArray();
        }
    }

    private static string FindPackage(string name)
    {
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots={
            project,Path.Combine(project,"UserPackages"),Path.Combine(project,"Packages"),
            Path.Combine(home,"Downloads"),Path.Combine(home,"Desktop"),
            Path.Combine(home,"Documents"),Path.Combine(home,"OneDrive","Downloads"),
            Path.Combine(home,"OneDrive","Desktop")
        };
        foreach(string root in roots)
        {
            if(!Directory.Exists(root))continue;
            string exact=Path.Combine(root,name);
            if(File.Exists(exact))return exact;
            try
            {
                string found=Directory.EnumerateFiles(root,"*.zip",SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(x=>string.Equals(Path.GetFileName(x),name,StringComparison.OrdinalIgnoreCase));
                if(found!=null)return found;
            }
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
        return null;
    }

    private static string ResolveBlender(bool interactive)
    {
        string candidate=EditorPrefs.GetString(BlenderPrefsKey,string.Empty);
        if(File.Exists(candidate))return candidate;
        var search=new List<string>();
        string foundation=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Blender Foundation");
        if(Directory.Exists(foundation))
        {
            try{search.AddRange(Directory.GetFiles(foundation,"blender.exe",SearchOption.AllDirectories).OrderByDescending(x=>x));}
            catch(IOException){}
            catch(UnauthorizedAccessException){}
        }
        search.Add(@"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe");
        search.Add(@"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe");
        candidate=search.FirstOrDefault(File.Exists);
        if(!string.IsNullOrEmpty(candidate))
        {
            EditorPrefs.SetString(BlenderPrefsKey,candidate);
            return candidate;
        }
        if(!interactive)return null;
        string picked=EditorUtility.OpenFilePanel("Select Blender executable",foundation,"exe");
        if(string.IsNullOrEmpty(picked) || !File.Exists(picked))return null;
        EditorPrefs.SetString(BlenderPrefsKey,picked);
        return picked;
    }

    private static void ExportBlender(string blender,string blend,string fbx,string work,bool tuna)
    {
        string script=Path.Combine(work,"ExportFixedFish.py");
        File.WriteAllText(script,BlenderExportScript);
        if(File.Exists(fbx))File.Delete(fbx);
        var info=new ProcessStartInfo
        {
            FileName=blender,
            Arguments="--background "+Quote(blend)+" --python-exit-code 2 --python "+Quote(script)+
                " -- "+Quote(fbx)+" "+(tuna?"static":"rigged"),
            WorkingDirectory=work,
            UseShellExecute=false,
            CreateNoWindow=true,
            RedirectStandardError=true,
            RedirectStandardOutput=true
        };
        using(var process=Process.Start(info))
        {
            if(process==null)throw new InvalidOperationException("Could not start Blender.");
            var stdout=process.StandardOutput.ReadToEndAsync();
            var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(180000))
            {
                try{process.Kill();}catch{}
                throw new TimeoutException("Blender export timed out.");
            }
            string log=stdout.Result+"\n--- ERROR ---\n"+stderr.Result;
            File.WriteAllText(Path.Combine(work,"BlenderExport.log"),log);
            if(process.ExitCode!=0 || !File.Exists(fbx))
                throw new InvalidOperationException("Blender export failed; see Library/FixedFishImports/.../BlenderExport.log.\n"+log.Substring(Math.Max(0,log.Length-1300)));
        }
    }
    private static string Quote(string text)=>"\""+text.Replace("\"","\\\"")+"\"";

    private const string BlenderExportScript = @"import bpy, os, sys
args=sys.argv[sys.argv.index('--')+1:]
out_path, mode=args[0], args[1]
scene=bpy.context.scene
all_meshes=[o for o in scene.objects if o.type=='MESH']
all_arms=[o for o in scene.objects if o.type=='ARMATURE']
if not all_meshes: raise RuntimeError('Supplied Blender file has no mesh objects')
os.makedirs(os.path.dirname(out_path),exist_ok=True)
bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode!='OBJECT' else None
for obj in scene.objects:
    try: obj.hide_set(False)
    except: pass
    obj.hide_viewport=False
    obj.hide_render=False
    obj.select_set(False)
if mode=='rigged':
    if not all_arms: raise RuntimeError('Mackerel rig/armature is missing')
    arm=next((mod.object for m in all_meshes for mod in m.modifiers if mod.type=='ARMATURE' and mod.object),all_arms[0])
    if arm.animation_data is None: arm.animation_data_create()
    action=arm.animation_data.action
    if action is None and len(bpy.data.actions):
        action=max(bpy.data.actions,key=lambda a: float(a.frame_range[1]-a.frame_range[0]))
        arm.animation_data.action=action
    if action is None: raise RuntimeError('Mackerel requires the supplied swim animation action')
    try:
        scene.frame_start=int(action.frame_range[0])
        scene.frame_end=int(action.frame_range[1])
    except: pass
    for obj in all_meshes+[arm]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(filepath=out_path,use_selection=True,
        object_types={'MESH','ARMATURE'},apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,
        path_mode='AUTO')
else:
    # Tuna retains its existing shader-based swim; do not add a second Animator.
    # Export a UV-preserving mesh of the supplied model in its rest pose.
    for arm in all_arms: arm.data.pose_position='REST'
    scene.frame_set(scene.frame_start)
    bpy.context.view_layer.update()
    depsgraph=bpy.context.evaluated_depsgraph_get()
    copies=[]
    for source in all_meshes:
        evaluated=source.evaluated_get(depsgraph)
        mesh=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=depsgraph)
        if mesh is None or len(mesh.vertices)==0: continue
        if len(mesh.uv_layers)==0: raise RuntimeError('New Yellowfin mesh lacks its UV map')
        obj=bpy.data.objects.new(source.name+'_Static',mesh)
        scene.collection.objects.link(obj)
        obj.matrix_world=source.matrix_world.copy()
        copies.append(obj)
    if not copies: raise RuntimeError('No static Yellowfin mesh could be generated')
    for obj in copies: obj.select_set(True)
    bpy.context.view_layer.objects.active=max(copies,key=lambda o:len(o.data.vertices))
    bpy.ops.object.join()
    bpy.ops.export_scene.fbx(filepath=out_path,use_selection=True,
        object_types={'MESH'},apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',bake_anim=False,path_mode='AUTO')
if not os.path.exists(out_path) or os.path.getsize(out_path)<1024:
    raise RuntimeError('FBX file was not generated')
print('EXPORTED',mode,out_path,os.path.getsize(out_path))
";
}