using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

// Self-contained prepared assets: no external Blender or ZIP lookup on the user's PC.
public static class FishingContentUpdateSetup
{
    public const string Source="Assets/_Game/ContentUpdate/Source";
    private const string Generated="Assets/_Game/ContentUpdate/Generated";
    private const string Stamp="Library/FishingContentUpdate-v2.txt";
    private static bool running;
    private static readonly string[] Fish={"Albacore","GreaterAmberjack"};
    private static readonly string[] Prefabs={
        "Assets/Resources/Fishing/Albacore.prefab",
        "Assets/Resources/Fishing/GreaterAmberjack.prefab",
        "Assets/Resources/Fishing/FishingRodReelLevel4Rod.prefab",
        "Assets/Resources/Fishing/FishingRodReelLevel4.prefab",
        "Assets/Resources/Boats/AuthoredMarinaDock.prefab",
        "Assets/Resources/Boats/AuthoredDockPost.prefab"
    };
    [Serializable] private sealed class Geometry { public Part[] parts; }
    [Serializable] private sealed class Part { public string name; public float[] vertices,uv; public int[] triangles; }

    [InitializeOnLoadMethod]
    private static void AutoInstall()
    {
        EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try { EnsureInstalled(); }
            catch(Exception e){Debug.LogError("Fishing content setup: "+e+"\nRetry Tools > Open World > Install Fish, Level 4 Gear and Dock.");}
        };
    }

    public static void EnsureInstalled()
    {
        if(File.Exists(Stamp) && Prefabs.All(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p)!=null))return;
        Install();
    }

    [MenuItem("Tools/Open World/Install Fish, Level 4 Gear and Dock")]
    public static void Install()
    {
        if(running || EditorApplication.isPlayingOrWillChangePlaymode)return;
        running=true;
        try
        {
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory("Assets/Resources/Boats");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string fish in Fish)SeaBassImporter.InstallModel(fish,"Assets/Resources/Fishing/"+fish+".prefab");
            FishingRodLevel3Importer.InstallPrepared(Source+"/Level4Rod.json",Source+"/Level4RodTexture.jpg",Prefabs[2],Generated+"/Level4Rod");
            FishingReelLevel3Importer.InstallPrepared(Source+"/Level4Reel.json",Source+"/Level4ReelTexture.jpg",Prefabs[3],Generated+"/Level4Reel");
            InstallDock();
            FishingTuning.Reload();
            if(!FishingTuning.IsValid)throw new InvalidDataException(FishingTuning.ValidationError);
            AssetDatabase.SaveAssets();
            if(Prefabs.Any(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p)==null))throw new InvalidOperationException("A prepared prefab was not saved.");
            File.WriteAllText(Stamp,"Installed prepared fish/gear/dock v2");
            Debug.Log("Fishing content ready: Albacore, Greater Amberjack, Level 4 rod/reel and authored marina dock. The existing scene is preserved; the dock replaces its old parts on Play/build startup.");
        }
        finally { running=false; EditorUtility.ClearProgressBar(); }
    }

    private static void InstallDock()
    {
        var geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText(Source+"/Dock.json"));
        if(geometry?.parts==null || geometry.parts.Length!=3)throw new InvalidDataException("Expected authored Deck, EdgeTrim and Post.");
        var shader=Resources.Load<Shader>("Fishing/FishingEquipment");
        if(shader==null)throw new InvalidOperationException("FishingEquipment shader missing.");
        string materialPath=Generated+"/Dock.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        material.shader=shader;material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_BaseMap",AuthoredTextureAsset.Load(Source+"/DockTexture.jpg",Generated+"/DockTexture.asset"));
        material.SetFloat("_Smoothness",.16f);material.SetFloat("_Metallic",0f);
        EditorUtility.SetDirty(material);
        var dock=new GameObject("Authored Marina Dock");var post=new GameObject("Authored Dock Post");
        try
        {
            foreach(var part in geometry.parts)
            {
                int count=part.vertices.Length/3;
                if(count==0 || part.uv.Length!=count*2)throw new InvalidDataException("Dock UV or geometry missing: "+part.name);
                var vertices=new Vector3[count];var uv=new Vector2[count];
                for(int i=0;i<count;i++){vertices[i]=new Vector3(part.vertices[i*3],part.vertices[i*3+1],part.vertices[i*3+2]);uv[i]=new Vector2(part.uv[i*2],part.uv[i*2+1]);}
                var mesh=new Mesh{name=part.name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=part.triangles;
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
                string meshPath=Generated+"/Dock"+part.name+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(saved==null){AssetDatabase.CreateAsset(mesh,meshPath);saved=mesh;}
                else {EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                bool isPost=part.name=="Post";
                var go=isPost?post:new GameObject(part.name);
                if(!isPost)go.transform.SetParent(dock.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=saved;
                go.AddComponent<MeshRenderer>().sharedMaterial=material;
                // Exact static mesh collider, not one box covering the water beside the pier.
                go.AddComponent<MeshCollider>().sharedMesh=saved;
                int layer=LayerMask.NameToLayer("Terrain");if(layer>=0)go.layer=layer;
            }
            PrefabUtility.SaveAsPrefabAsset(dock,Prefabs[4]);PrefabUtility.SaveAsPrefabAsset(post,Prefabs[5]);
        }
        finally {Object.DestroyImmediate(dock);Object.DestroyImmediate(post);}
    }
}

public sealed class FishingContentBuildGate : IPreprocessBuildWithReport
{
    public int callbackOrder=>-1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        try { FishingContentUpdateSetup.EnsureInstalled(); }
        catch(Exception e){throw new BuildFailedException("Fishing content is incomplete: "+e.Message);}
    }
}
