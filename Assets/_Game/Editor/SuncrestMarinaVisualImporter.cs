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
/// Imports the user's "Dock and Dock Post.zip" and swaps ONLY the visible meshes /
/// material on Suncrest Marina's existing Boardwalk and Dock piling objects.
/// Existing transforms, BoxColliders, interaction objects, arrival point and marina
/// layout are left untouched, so gameplay shape/hitboxes remain exactly as authored.
///
/// The supplied STLs have no UVs. This importer normalizes their geometry to a unit
/// box matching the existing primitive dimensions and generates face-projected UVs for
/// the supplied wood texture. Both dock and piling visuals therefore fit the existing
/// marina objects without changing their collision footprint.
/// </summary>
public static class SuncrestMarinaVisualImporter
{
    private const string ZipName="Dock and Dock Post.zip";
    private const string AssetRoot="Assets/_Game/Marina/UserDock";
    private const string DockMeshPath=AssetRoot+"/DockUser_Normalized.asset";
    private const string PostMeshPath=AssetRoot+"/DockPostUser_Normalized.asset";
    private const string TexturePath=AssetRoot+"/DockUserTexture.png";
    private const string MaterialPath=AssetRoot+"/DockUserWood.mat";
    private const string AutoSessionKey="OpenWorld.AutoImport.SuncrestMarinaVisuals.20260927.v1";

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
                // If assets were generated previously, reapply them even when the ZIP
                // is no longer in Downloads. This also recovers after boat setup reruns.
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
            EditorUtility.DisplayDialog("Marina Dock Visuals","Imported dock assets do not exist yet. Run the one-click import first.","OK");
    }

    public static bool TryApplyExisting(bool interactive)
    {
        Mesh dock=AssetDatabase.LoadAssetAtPath<Mesh>(DockMeshPath);
        Mesh post=AssetDatabase.LoadAssetAtPath<Mesh>(PostMeshPath);
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(dock==null || post==null || material==null)return false;
        return ApplyToCurrentMarina(dock,post,material,interactive);
    }

    private static void ImportAndApply(string zipPath,bool interactive)
    {
        EnsureFolder(AssetRoot);
        EditorUtility.DisplayProgressBar("Suncrest Marina","Reading supplied dock package...",.10f);

        byte[] dockBytes,postBytes,textureBytes;
        using(FileStream stream=File.OpenRead(zipPath))
        using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Read))
        {
            dockBytes=ReadRequired(archive,"Dock.stl");
            postBytes=ReadRequired(archive,"Dock Post.stl");
            textureBytes=ReadRequired(archive,"Dock Texture.jpg");
        }

        EditorUtility.DisplayProgressBar("Suncrest Marina","Building supplied dock meshes...",.32f);
        Mesh dock=BuildNormalizedBinaryStl(dockBytes,"DockUser_Normalized");
        Mesh post=BuildNormalizedBinaryStl(postBytes,"DockPostUser_Normalized");
        SaveOrReplaceMesh(DockMeshPath,dock);
        SaveOrReplaceMesh(PostMeshPath,post);

        EditorUtility.DisplayProgressBar("Suncrest Marina","Installing supplied wood texture...",.58f);
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

        dock=AssetDatabase.LoadAssetAtPath<Mesh>(DockMeshPath);
        post=AssetDatabase.LoadAssetAtPath<Mesh>(PostMeshPath);
        material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(dock==null||post==null||material==null)throw new InvalidOperationException("Generated marina visual assets could not be reloaded.");

        EditorUtility.DisplayProgressBar("Suncrest Marina","Replacing visible dock/posts; keeping existing hitboxes...",.82f);
        if(!ApplyToCurrentMarina(dock,post,material,interactive))
            throw new InvalidOperationException("MarinaShop is not present in the open Suncrest scene. Open the island scene and run the importer again.");

        Debug.Log("[SUNCREST MARINA VISUAL] SUCCESS — supplied Dock + Dock Post models/texture applied to Boardwalk and Dock piling renderers. Existing transforms and colliders were preserved.");
        if(interactive)
            EditorUtility.DisplayDialog("Suncrest Marina Ready","Your supplied dock and dock-post visuals are installed. Existing marina shape, placement and hitboxes were kept unchanged.","OK");
    }

    private static bool ApplyToCurrentMarina(Mesh dock,Mesh post,Material material,bool interactive)
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
            if(interactive)EditorUtility.DisplayDialog("Marina Dock Import","Could not find the existing Boardwalk and Dock piling objects. Their colliders were not changed.","OK");
            return false;
        }

        // Mesh swap only. Do NOT alter transform scale/position/rotation or colliders.
        Undo.RecordObject(boardwalk,"Replace marina dock visual");
        boardwalk.sharedMesh=dock;
        Renderer boardRenderer=boardwalk.GetComponent<Renderer>();
        if(boardRenderer!=null){Undo.RecordObject(boardRenderer,"Texture marina dock");boardRenderer.sharedMaterial=material;}
        EditorUtility.SetDirty(boardwalk);

        foreach(MeshFilter piling in pilings)
        {
            Undo.RecordObject(piling,"Replace marina piling visual");
            piling.sharedMesh=post;
            Renderer renderer=piling.GetComponent<Renderer>();
            if(renderer!=null){Undo.RecordObject(renderer,"Texture marina piling");renderer.sharedMaterial=material;EditorUtility.SetDirty(renderer);}
            EditorUtility.SetDirty(piling);
        }

        Scene scene=marina.scene;
        if(scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if(!string.IsNullOrEmpty(scene.path))EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject=boardwalk.gameObject;
        EditorGUIUtility.PingObject(boardwalk.gameObject);
        Debug.Log("[SUNCREST MARINA VISUAL] Applied 1 dock + "+pilings.Count+" piling visuals. BoxColliders/hitboxes were not modified.");
        return true;
    }

    private static Mesh BuildNormalizedBinaryStl(byte[] bytes,string name)
    {
        if(bytes==null || bytes.Length<84)throw new InvalidDataException(name+" STL is too small.");
        uint triangleCount=BitConverter.ToUInt32(bytes,80);
        long expected=84L+triangleCount*50L;
        if(triangleCount==0 || expected>bytes.Length)throw new InvalidDataException(name+" is not a valid binary STL.");

        int vertexCount=checked((int)triangleCount*3);
        Vector3[] source=new Vector3[vertexCount];
        int offset=84;
        int vi=0;
        Vector3 min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
        Vector3 max=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);

        for(int t=0;t<triangleCount;t++)
        {
            offset+=12; // supplied STL face normal; Unity will recalculate after normalization
            for(int v=0;v<3;v++)
            {
                float x=BitConverter.ToSingle(bytes,offset);offset+=4;
                float y=BitConverter.ToSingle(bytes,offset);offset+=4;
                float z=BitConverter.ToSingle(bytes,offset);offset+=4;
                // Both supplied models use Z as thickness/height differently. This
                // consistent Blender/STL -> Unity remap makes Dock: X width, Y thin,
                // Z length and Post: X width, Y height, Z width.
                Vector3 p=new Vector3(x,z,y);
                source[vi++]=p;
                min=Vector3.Min(min,p);max=Vector3.Max(max,p);
            }
            offset+=2; // STL attribute byte count
        }

        Vector3 size=max-min;
        if(size.x<1e-5f||size.y<1e-5f||size.z<1e-5f)throw new InvalidDataException(name+" has degenerate bounds.");
        Vector3 center=(min+max)*.5f;

        Vector3[] vertices=new Vector3[vertexCount];
        Vector2[] uv=new Vector2[vertexCount];
        int[] triangles=new int[vertexCount];

        for(int i=0;i<vertexCount;i++)
        {
            Vector3 p=source[i]-center;
            p=new Vector3(p.x/size.x,p.y/size.y,p.z/size.z);
            vertices[i]=p;
            triangles[i]=i;
        }

        for(int i=0;i<vertexCount;i+=3)
        {
            Vector3 a=vertices[i],b=vertices[i+1],c=vertices[i+2];
            Vector3 n=Vector3.Cross(b-a,c-a).normalized;
            Vector3 an=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));
            for(int j=0;j<3;j++)
            {
                Vector3 p=vertices[i+j];
                if(an.y>=an.x && an.y>=an.z)uv[i+j]=new Vector2(p.x+.5f,p.z+.5f);
                else if(an.x>=an.z)uv[i+j]=new Vector2(p.z+.5f,p.y+.5f);
                else uv[i+j]=new Vector2(p.x+.5f,p.y+.5f);
            }
        }

        Mesh mesh=new Mesh{name=name};
        if(vertexCount>65000)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        return mesh;
    }

    private static void SaveOrReplaceMesh(string path,Mesh source)
    {
        Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null)
        {
            AssetDatabase.CreateAsset(source,path);
            return;
        }
        EditorUtility.CopySerialized(source,existing);
        existing.name=source.name;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(source);
    }

    private static byte[] ReadRequired(ZipArchive archive,string member)
    {
        ZipArchiveEntry entry=archive.Entries.FirstOrDefault(e=>string.Equals(e.FullName,member,StringComparison.OrdinalIgnoreCase));
        if(entry==null)entry=archive.Entries.FirstOrDefault(e=>string.Equals(Path.GetFileName(e.FullName),member,StringComparison.OrdinalIgnoreCase));
        if(entry==null)throw new InvalidDataException("Missing '"+member+"' in supplied marina ZIP.");
        using(Stream stream=entry.Open())using(MemoryStream memory=new MemoryStream()){stream.CopyTo(memory);return memory.ToArray();}
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
