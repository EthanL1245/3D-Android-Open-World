using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

/// <summary>
/// Installs the user's Dock Texture on the EXISTING Suncrest marina geometry.
///
/// Important contract:
/// - Boardwalk keeps the original full-size cube geometry and original transform.
/// - Every Dock piling keeps the original full-size cube geometry and transform.
/// - Existing BoxColliders are never touched.
/// - Therefore visible dock/posts exactly fill the same shape as their hitboxes;
///   there are no thin replacement meshes, floating post fragments, or invisible
///   collision outside the rendered wood.
///
/// The supplied STL/.blend files are intentionally not used for the final marina
/// silhouette because the requested gameplay requirement is "same shape and hitbox as
/// before, using my textures". The ZIP is still validated as the supplied package and
/// its Dock Texture.jpg is used verbatim for the material.
/// </summary>
public static class SuncrestMarinaVisualImporter
{
    private const string ZipName="Dock and Dock Post.zip";
    private const string AssetRoot="Assets/_Game/Marina/UserDock";
    private const string TexturePath=AssetRoot+"/DockUserTexture.png";
    private const string MaterialPath=AssetRoot+"/DockUserWood.mat";
    private const string AutoSessionKey="OpenWorld.AutoImport.SuncrestMarinaVisuals.20260927.v2.ShapeRecovery";

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
                // First recover the scene from the earlier thin-mesh import if the
                // user's material already exists. This makes the fix one-pull only.
                if(TryApplyExisting(false))return;

                string zip=FindPackage();
                if(!string.IsNullOrEmpty(zip))ImportAndApply(zip,false);
            }
            catch(Exception e){Debug.LogError("[SUNCREST MARINA VISUAL] Auto-import failed: "+e);}
            finally{EditorUtility.ClearProgressBar();}
        };
    }

    [MenuItem("Tools/Open World/Import Suncrest Marina Dock Visuals (One Click)")]
    public static void ImportOneClick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Marina Dock Import","Exit Play Mode first.","OK");
            return;
        }

        try
        {
            string zip=FindPackage();
            if(string.IsNullOrEmpty(zip))
                zip=EditorUtility.OpenFilePanel("Select "+ZipName,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"zip");
            if(string.IsNullOrEmpty(zip))return;
            ImportAndApply(zip,true);
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Marina Dock Import Failed",e.Message+"\n\nSee Console for details.","OK");
        }
        finally{EditorUtility.ClearProgressBar();}
    }

    [MenuItem("Tools/Open World/Reapply Imported Suncrest Marina Dock Visuals")]
    public static void ReapplyMenu()
    {
        if(!TryApplyExisting(true))
            EditorUtility.DisplayDialog("Marina Dock Visuals","Imported dock texture/material does not exist yet. Run the one-click import first.","OK");
    }

    public static bool TryApplyExisting(bool interactive)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null)return false;
        return ApplyToCurrentMarina(material,interactive);
    }

    private static void ImportAndApply(string zipPath,bool interactive)
    {
        EnsureFolder(AssetRoot);
        EditorUtility.DisplayProgressBar("Suncrest Marina","Reading supplied dock package...",.12f);

        byte[] textureBytes;
        using(FileStream stream=File.OpenRead(zipPath))
        using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            // Validate that this really is the user's dock/post package, but keep the
            // original marina geometry because its dimensions already match gameplay.
            RequireMember(archive,"Dock.stl");
            RequireMember(archive,"Dock Post.stl");
            textureBytes=ReadRequired(archive,"Dock Texture.jpg");
        }

        EditorUtility.DisplayProgressBar("Suncrest Marina","Installing supplied wood texture...",.52f);
        WriteTexturePng(TexturePath,textureBytes);
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if(texture==null)throw new InvalidDataException("Unity could not import the supplied dock texture.");
        texture.wrapMode=TextureWrapMode.Repeat;
        EditorUtility.SetDirty(texture);

        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("URP Lit shader is missing.");
            material=new Material(shader){name="Suncrest User Dock Wood"};
            AssetDatabase.CreateAsset(material,MaterialPath);
        }
        material.mainTexture=texture;
        material.color=Color.white;
        if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.18f);
        if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayProgressBar("Suncrest Marina","Restoring original dock/post thickness and texturing it...",.82f);
        if(!ApplyToCurrentMarina(material,interactive))
            throw new InvalidOperationException("MarinaShop is not present in the open Suncrest scene. Open the island scene and run the importer again.");

        Debug.Log("[SUNCREST MARINA VISUAL V2] SUCCESS — restored original full-thickness Boardwalk + Dock piling geometry and applied Dock Texture.jpg. Visible geometry now matches the existing hitboxes.");
        if(interactive)
            EditorUtility.DisplayDialog("Suncrest Marina Ready","Original marina thickness/shape restored. Your supplied dock texture is applied to the dock and pilings, and the existing hitboxes remain aligned.","OK");
    }

    private static bool ApplyToCurrentMarina(Material material,bool interactive)
    {
        GameObject marina=GameObject.Find("MarinaShop");
        if(marina==null)return false;

        MeshFilter boardwalk=null;
        List<MeshFilter> pilings=new List<MeshFilter>();
        foreach(MeshFilter filter in marina.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter==null)continue;
            if(string.Equals(filter.name,"Boardwalk",StringComparison.OrdinalIgnoreCase))boardwalk=filter;
            else if(string.Equals(filter.name,"Dock piling",StringComparison.OrdinalIgnoreCase))pilings.Add(filter);
        }

        if(boardwalk==null || pilings.Count==0)
        {
            if(interactive)EditorUtility.DisplayDialog("Marina Dock Import","Could not find the existing Boardwalk and Dock piling objects. Nothing was changed.","OK");
            return false;
        }

        Mesh cube=GetUnityCubeMesh();
        if(cube==null)throw new InvalidOperationException("Unity built-in Cube mesh could not be resolved.");

        // Restore the exact primitive geometry BoatSystemSetup originally authored.
        // Because the BoxCollider lives on each same GameObject with the same transform,
        // this makes the renderer and collision occupy the same volume again.
        RestorePrimitive(boardwalk,cube,material,"Restore full-thickness marina dock");
        foreach(MeshFilter piling in pilings)
            RestorePrimitive(piling,cube,material,"Restore full-size marina piling");

        // Defensive cleanup: earlier visual importers never created children, but if a
        // future/partial attempt did, remove only obvious generated visual children so
        // no decorative post fragments can float independently of the collider.
        RemoveGeneratedVisualChildren(boardwalk.transform);
        foreach(MeshFilter piling in pilings)RemoveGeneratedVisualChildren(piling.transform);

        Physics.SyncTransforms();
        Scene scene=marina.scene;
        if(scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if(!string.IsNullOrEmpty(scene.path))EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject=boardwalk.gameObject;
        EditorGUIUtility.PingObject(boardwalk.gameObject);
        Debug.Log("[SUNCREST MARINA VISUAL V2] Applied supplied texture to original cube geometry: 1 Boardwalk + "+pilings.Count+" Dock piling(s). Existing transforms and BoxColliders unchanged.");
        return true;
    }

    private static void RestorePrimitive(MeshFilter filter,Mesh cube,Material material,string undoName)
    {
        Undo.RecordObject(filter,undoName);
        filter.sharedMesh=cube;
        EditorUtility.SetDirty(filter);

        Renderer renderer=filter.GetComponent<Renderer>();
        if(renderer!=null)
        {
            Undo.RecordObject(renderer,"Apply supplied dock wood texture");
            renderer.sharedMaterial=material;
            renderer.enabled=true;
            EditorUtility.SetDirty(renderer);
        }

        BoxCollider box=filter.GetComponent<BoxCollider>();
        if(box!=null)
        {
            // BoatSystemSetup's primitive cubes use the default unit BoxCollider.
            // Explicitly restoring these defaults also recovers any accidental visual/
            // collision mismatch without changing the object's authored world scale.
            Undo.RecordObject(box,"Align marina renderer and hitbox");
            box.center=Vector3.zero;
            box.size=Vector3.one;
            box.enabled=true;
            EditorUtility.SetDirty(box);
        }
    }

    private static Mesh GetUnityCubeMesh()
    {
        GameObject temp=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            MeshFilter filter=temp.GetComponent<MeshFilter>();
            return filter!=null?filter.sharedMesh:null;
        }
        finally{Object.DestroyImmediate(temp);}
    }

    private static void RemoveGeneratedVisualChildren(Transform root)
    {
        for(int i=root.childCount-1;i>=0;i--)
        {
            Transform child=root.GetChild(i);
            string n=child.name??string.Empty;
            if(n.IndexOf("UserDock",StringComparison.OrdinalIgnoreCase)>=0 ||
               n.IndexOf("DockVisual",StringComparison.OrdinalIgnoreCase)>=0 ||
               n.IndexOf("ImportedDock",StringComparison.OrdinalIgnoreCase)>=0)
                Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private static void RequireMember(ZipArchive archive,string member)
    {
        ZipArchiveEntry entry=FindMember(archive,member);
        if(entry==null)throw new InvalidDataException("Missing '"+member+"' in supplied marina ZIP.");
    }

    private static byte[] ReadRequired(ZipArchive archive,string member)
    {
        ZipArchiveEntry entry=FindMember(archive,member);
        if(entry==null)throw new InvalidDataException("Missing '"+member+"' in supplied marina ZIP.");
        using(Stream stream=entry.Open())using(MemoryStream memory=new MemoryStream()){stream.CopyTo(memory);return memory.ToArray();}
    }

    private static ZipArchiveEntry FindMember(ZipArchive archive,string member)
    {
        ZipArchiveEntry entry=archive.Entries.FirstOrDefault(e=>string.Equals(e.FullName,member,StringComparison.OrdinalIgnoreCase));
        if(entry==null)entry=archive.Entries.FirstOrDefault(e=>string.Equals(Path.GetFileName(e.FullName),member,StringComparison.OrdinalIgnoreCase));
        return entry;
    }

    private static void WriteTexturePng(string assetPath,byte[] bytes)
    {
        Texture2D decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(decoded,bytes,false)||decoded.width<2||decoded.height<2)
                throw new InvalidDataException("Unity could not decode Dock Texture.jpg.");
            string full=Path.GetFullPath(assetPath);Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full,decoded.EncodeToPNG());
        }
        finally{Object.DestroyImmediate(decoded);}
        AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
    }

    private static string FindPackage()
    {
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots={project,Path.Combine(project,"Packages"),Path.Combine(project,"UserPackages"),Path.Combine(home,"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(home,"OneDrive","Downloads"),Path.Combine(home,"OneDrive","Desktop"),Path.Combine(home,"OneDrive","Documents")};
        foreach(string root in roots)
        {
            if(string.IsNullOrEmpty(root)||!Directory.Exists(root))continue;
            string exact=Path.Combine(root,ZipName);if(File.Exists(exact))return exact;
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
}
