using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class SuncrestReefSetup
{
    private const string Root="Assets/_Game/Reef/Generated";
    private static Material bark,leaf,stone,coral,wood;
    private static System.Random random;
    private static float sea,rx,rz;
    private static Vector3 center;
    private static Terrain terrain;

    [MenuItem("Tools/Open World/Install Suncrest Reef (One Click)")]
    public static void Install()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        var old=Object.FindFirstObjectByType<Terrain>();
        var player=Object.FindFirstObjectByType<FirstPersonController>();
        var water=Object.FindFirstObjectByType<OceanWater>();
        if(old==null||player==null||water==null)throw new InvalidOperationException("Open your fishing island scene first (Terrain, Player and OceanSystem are required).");
        if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("Save the island scene before installing.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EnsureFolder(Root);
        EnsureFolder("Assets/_Game/Reef/Backups");
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        string backupPath="Assets/_Game/Reef/Backups/Island-"+stamp+".unity";
        string originalScenePath=scene.path;
        // The backup owns a copy of TerrainData, so repeat installation cannot
        // mutate the old terrain through a shared asset reference.
        var originalData=old.terrainData;
        var backupData=Object.Instantiate(originalData);
        AssetDatabase.CreateAsset(backupData,"Assets/_Game/Reef/Backups/Terrain-"+stamp+".asset");
        var oldCollider=old.GetComponent<TerrainCollider>();
        old.terrainData=backupData;if(oldCollider!=null)oldCollider.terrainData=backupData;
        try { if(!EditorSceneManager.SaveScene(scene,backupPath,true))throw new IOException("Could not save the island backup."); }
        finally { old.terrainData=originalData;if(oldCollider!=null)oldCollider.terrainData=originalData; }
        try
        {
            EditorUtility.DisplayProgressBar("Suncrest Reef","Importing authored sea bass...",0.05f);
            SeaBassImporter.Install();
            var terrainMaterial=old.materialTemplate;
            var previous=Object.FindFirstObjectByType<ReefZone>();
            sea=water.BaseWaterLevel;
            float originalArea;
            if(previous!=null){originalArea=previous.originalLandArea;center=previous.center;}
            else originalArea=MeasureLand(old,out center);
            if(originalArea<100f)throw new InvalidOperationException("Could not measure enough dry land to resize. Check the island terrain and water level.");
            float target=originalArea*0.5f;
            rx=Mathf.Sqrt(target/(Mathf.PI*0.78f));rz=rx*0.78f;
            float size=Mathf.Max(600f,rx*2f+520f);
            if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            else old.gameObject.SetActive(false); // Old TerrainData stays untouched.
            var decor=GameObject.Find("WorldDecor");if(decor!=null)decor.SetActive(false);
            var root=new GameObject("SuncrestReef");
            var zone=root.AddComponent<ReefZone>();zone.center=center;zone.originalLandArea=originalArea;zone.reefWidth=160;
            EditorUtility.DisplayProgressBar("Suncrest Reef","Sculpting beach and wide reef shelf...",0.18f);
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(Root+"/SuncrestTerrain.asset");
            if(data==null){data=new TerrainData();AssetDatabase.CreateAsset(data,Root+"/SuncrestTerrain.asset");}
            data.heightmapResolution=513;data.alphamapResolution=256;data.baseMapResolution=1024;
            data.size=new Vector3(size,90f,size);
            // Calibrate against the generated heightmap, not only an ellipse formula.
            float low=rx*0.65f,high=rx*1.35f;
            for(int i=0;i<12;i++){rx=(low+high)*0.5f;rz=rx*0.78f;float area=CountLand(size,257);if(area<target)low=rx;else high=rx;}
            zone.islandRadiusX=rx;zone.islandRadiusZ=rz;
            var heights=new float[513,513];
            for(int z=0;z<513;z++)for(int x=0;x<513;x++)
                heights[z,x]=(Height((x/512f-.5f)*size,(z/512f-.5f)*size)-(sea-45f))/90f;
            data.SetHeights(0,0,heights);
            var terrainObject=Terrain.CreateTerrainGameObject(data);terrainObject.name="Suncrest Beach and Reef";terrainObject.transform.SetParent(root.transform);
            terrainObject.transform.position=new Vector3(center.x-size/2,sea-45f,center.z-size/2);
            terrain=terrainObject.GetComponent<Terrain>();terrain.materialTemplate=terrainMaterial;
            terrain.heightmapPixelError=6;terrain.basemapDistance=220;terrain.drawInstanced=true;
            zone.landArea=CountLand(size,513);
            Paint(data,size);
            BuildMaterials();random=new System.Random(73191);
            EditorUtility.DisplayProgressBar("Suncrest Reef","Planting palms and furnishing reef habitats...",0.42f);
            BuildScenery(root.transform);
            Vector3 arrival=new Vector3(center.x,0,center.z-rz*0.72f);arrival.y=terrain.SampleHeight(arrival)+terrain.transform.position.y+0.2f;
            var cc=player.GetComponent<CharacterController>();bool enabled=cc.enabled;cc.enabled=false;
            player.transform.SetPositionAndRotation(arrival,Quaternion.Euler(0,180,0));cc.enabled=enabled;
            var arrivalObject=new GameObject("SuncrestArrival");arrivalObject.transform.SetParent(root.transform);arrivalObject.transform.SetPositionAndRotation(arrival,player.transform.rotation);
            var travel=player.GetComponent<ShopDimensionManager>();if(travel!=null){travel.islandArrival=arrivalObject.transform;EditorUtility.SetDirty(travel);}
            var ambient=Object.FindFirstObjectByType<AmbientFishManager>();
            if(ambient==null){var a=new GameObject("AmbientFishSystem");ambient=a.AddComponent<AmbientFishManager>();}
            ambient.Configure(water,player.transform);EditorUtility.SetDirty(ambient);
            // Gentle reef chop, while the existing underwater/water systems remain active.
            var waterSettings=new SerializedObject(water);
            waterSettings.FindProperty("waveAmplitude1").floatValue=0.14f;
            waterSettings.FindProperty("waveAmplitude2").floatValue=0.07f;
            waterSettings.FindProperty("waveAmplitude3").floatValue=0.035f;
            waterSettings.ApplyModifiedPropertiesWithoutUndo();
            var waterRenderer=water.GetComponent<MeshRenderer>();
            if(waterRenderer.sharedMaterial!=null)
            {
                string waterPath=Root+"/SuncrestWater.mat";
                var reefWater=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
                if(reefWater==null){reefWater=new Material(waterRenderer.sharedMaterial);AssetDatabase.CreateAsset(reefWater,waterPath);}
                reefWater.SetColor("_ShallowColor",new Color(.09f,.66f,.59f,1));
                reefWater.SetColor("_DeepColor",new Color(.015f,.22f,.32f,1));
                reefWater.SetFloat("_Alpha",.48f);reefWater.SetFloat("_Smoothness",.84f);
                waterRenderer.sharedMaterial=reefWater;EditorUtility.SetDirty(reefWater);
            }

            var fishing=player.GetComponent<FishingSystem>();
            if(fishing!=null){var settings=new SerializedObject(fishing);settings.FindProperty("minimumFishingDepth").floatValue=1.2f;settings.ApplyModifiedPropertiesWithoutUndo();}
            EditorUtility.SetDirty(zone);EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root;
            Debug.Log($"Suncrest Reef installed. Original land {originalArea:N0} m²; new land {zone.landArea:N0} m² ({zone.landArea/originalArea:P1}). Reef shelf extends 160m beyond shore. Scene backup: {backupPath}");
            EditorUtility.DisplayDialog("Suncrest Reef ready","Beach island, broad reef shelf, sea bass and animated reef fish installed.\n\nPlay → MENU / TRAVEL → VIEW INDEX for species and rarity.\n\nScene backup saved in Assets/_Game/Reef/Backups. Your Home and Quay are preserved.","OK");
        }
        catch(Exception)
        {
            // Restore the saved scene if generation fails part-way through.
            var restored=EditorSceneManager.OpenScene(backupPath,OpenSceneMode.Single);
            EditorSceneManager.SaveScene(restored,originalScenePath);
            throw;
        }
        finally{EditorUtility.ClearProgressBar();}
    }
    private static float MeasureLand(Terrain t,out Vector3 centroid)
    {
        var d=t.terrainData;int n=d.heightmapResolution;var h=d.GetHeights(0,0,n,n);int count=0;Vector3 sum=Vector3.zero;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(t.transform.position.y+h[z,x]*d.size.y>sea)
        {count++;sum+=new Vector3(t.transform.position.x+x/(float)(n-1)*d.size.x,sea,t.transform.position.z+z/(float)(n-1)*d.size.z);}
        centroid=count>0?sum/count:t.transform.position+d.size*.5f;
        return count/(float)(n*n)*d.size.x*d.size.z;
    }
    private static float Height(float x,float z)
    {
        float angle=Mathf.Atan2(z/rz,x/rx);
        float coast=1f+.065f*Mathf.Sin(angle*3f)+.045f*Mathf.Cos(angle*5f);
        float q=Mathf.Sqrt(x*x/(rx*rx)+z*z/(rz*rz))/coast;
        if(q<=1f)
        {
            float inland=Mathf.SmoothStep(0,1,Mathf.Clamp01((1f-q)/.43f));
            return sea+0.04f+inland*(5.2f+1.6f*Mathf.PerlinNoise(x*.019f+80,z*.019f+90));
        }
        float distance=(q-1f)*Mathf.Min(rx,rz);
        float depth=distance<30 ? Mathf.Lerp(0,2f,Mathf.SmoothStep(0,1,distance/30f)) :
            distance<160 ? Mathf.Lerp(2,6f,(distance-30)/130f) :
            distance<260 ? Mathf.Lerp(6,18f,Mathf.SmoothStep(0,1,(distance-160)/100f)) :
            Mathf.Lerp(18,35f,Mathf.Clamp01((distance-260)/180f));
        return sea-depth;
    }
    private static float CountLand(float size,int n)
    {
        int count=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++)if(Height((x/(float)(n-1)-.5f)*size,(z/(float)(n-1)-.5f)*size)>sea)count++;
        return count/(float)(n*n)*size*size;
    }
    private static void Paint(TerrainData data,float size)
    {
        var sand=Layer("Warm sand",new Color(.72f,.65f,.45f),new Color(.93f,.85f,.63f),5);
        var grass=Layer("Coastal meadow",new Color(.19f,.29f,.10f),new Color(.36f,.46f,.19f),6);
        var reef=Layer("Reef limestone",new Color(.43f,.53f,.43f),new Color(.7f,.75f,.62f),9);
        data.terrainLayers=new[]{sand,grass,reef};var map=new float[256,256,3];
        for(int z=0;z<256;z++)for(int x=0;x<256;x++)
        {
            float wx=(x/255f-.5f)*size,wz=(z/255f-.5f)*size,h=Height(wx,wz)-sea;
            float green=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,3.7f,h));
            float rock=h<-.8f ? Mathf.PerlinNoise(wx*.037f+90,wz*.037f+80)*.55f:0;
            map[z,x,0]=1-green-rock;map[z,x,1]=green;map[z,x,2]=rock;
        }
        data.SetAlphamaps(0,0,map);
    }
    private static TerrainLayer Layer(string name,Color a,Color b,float tile)
    {
        string path=Root+"/"+name+".asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null){texture=new Texture2D(256,256,TextureFormat.RGB24,true);AssetDatabase.CreateAsset(texture,path);}
        var pixels=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float broad=Mathf.PerlinNoise(x/35f,y/35f),fine=Mathf.PerlinNoise(x*.7f+42,y*.7f+42);
            pixels[y*256+x]=Color.Lerp(a,b,broad*.7f+fine*.3f);
        }
        texture.SetPixels(pixels);texture.Apply();texture.wrapMode=TextureWrapMode.Repeat;EditorUtility.SetDirty(texture);
        string layerPath=Root+"/"+name+".terrainlayer";var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if(layer==null){layer=new TerrainLayer();AssetDatabase.CreateAsset(layer,layerPath);}
        layer.diffuseTexture=texture;layer.tileSize=Vector2.one*tile;layer.smoothness=.05f;EditorUtility.SetDirty(layer);return layer;
    }
    private static void EnsureFolder(string folder)
    {if(AssetDatabase.IsValidFolder(folder))return;EnsureFolder(Path.GetDirectoryName(folder).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\','/'),Path.GetFileName(folder));}
    private static Material Mat(string name,Color color,float smoothness=0.2f)
    {
        string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    private static void BuildMaterials(){bark=Mat("Palm bark",new Color(.38f,.29f,.16f));leaf=Mat("Palm fronds",new Color(.16f,.34f,.08f));stone=Mat("Limestone",new Color(.63f,.61f,.49f));coral=Mat("Reef coral",new Color(.52f,.30f,.23f));wood=Mat("Driftwood",new Color(.45f,.40f,.31f));}
    private static float Rand(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    private static void BuildScenery(Transform parent)
    {
        var palm=BuildPalm();var rock=BuildRock();var grasses=BuildGrass();var reefCoral=BuildCoral();
        int palms=Mathf.Clamp(Mathf.RoundToInt(rx*rz/230f),24,64);
        for(int i=0;i<palms;i++)
        {
            float a=Rand(0,Mathf.PI*2),r=Rand(.48f,.85f);
            Place(palm,parent,new Vector3(Mathf.Cos(a)*rx*r,0,Mathf.Sin(a)*rz*r),Rand(.7f,1.2f));
        }
        for(int i=0;i<100;i++)
        {
            float a=Rand(0,Mathf.PI*2),r=Rand(.52f,.96f);
            Place(grasses,parent,new Vector3(Mathf.Cos(a)*rx*r,0,Mathf.Sin(a)*rz*r),Rand(.6f,1.6f));
        }
        for(int i=0;i<45;i++)
        {
            float a=Rand(0,Mathf.PI*2),r=Rand(.65f,1.04f);
            Place(rock,parent,new Vector3(Mathf.Cos(a)*rx*r,0,Mathf.Sin(a)*rz*r),Rand(.5f,1.9f));
        }
        for(int i=0;i<150;i++)
        {
            float a=Rand(0,Mathf.PI*2),extension=Rand(15,145);
            var local=new Vector3(Mathf.Cos(a)*(rx+extension),0,Mathf.Sin(a)*(rz+extension));
            float depth=sea-Height(local.x,local.z);if(depth<1.2f||depth>8)continue;
            Place(i%4==0?rock:i%3==0?grasses:reefCoral,parent,local,Rand(.6f,1.7f));
        }
        // Driftwood and shell banks decorate the sandy shore without buildings.
        for(int i=0;i<12;i++)
        {
            float a=Rand(0,Mathf.PI*2);var p=new Vector3(Mathf.Cos(a)*rx*.97f,0,Mathf.Sin(a)*rz*.97f);
            var log=new GameObject("Tide-worn driftwood");log.transform.SetParent(parent);log.transform.position=Ground(p)+Vector3.up*.16f;
            log.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);
            var trunk=Primitive(PrimitiveType.Capsule,log.transform,new Vector3(0,0,0),new Vector3(.24f,1.6f,.3f),wood);
            trunk.transform.localRotation=Quaternion.Euler(0,0,88);
            var branch=Primitive(PrimitiveType.Capsule,log.transform,new Vector3(.4f,.1f,0),new Vector3(.13f,.6f,.14f),wood);
            branch.transform.localRotation=Quaternion.Euler(35,0,65);
            var shells=new GameObject("Shell bank");shells.transform.SetParent(parent);shells.transform.position=Ground(p+Vector3.forward*2f);
            for(int j=0;j<7;j++)
            {
                var shell=Primitive(PrimitiveType.Sphere,shells.transform,new Vector3(Rand(-.9f,.9f),.06f,Rand(-.7f,.7f)),new Vector3(.14f,.08f,.2f),stone);
                shell.transform.localRotation=Quaternion.Euler(0,Rand(0,360),Rand(-15,15));
            }
        }
        // A walk-through natural limestone arch is an exploration landmark.
        var archPosition=new Vector3(rx*.68f,0,rz*.57f);
        Vector3 ground=Ground(archPosition);
        for(int i=0;i<7;i++)
        {
            float a=Mathf.PI*i/6;
            var instance=PrefabUtility.InstantiatePrefab(rock) as GameObject;instance.name="Shell Cove limestone arch";
            instance.transform.SetParent(parent);instance.transform.position=ground+new Vector3(Mathf.Cos(a)*3.2f,Mathf.Sin(a)*3.5f,0);
            instance.transform.localScale=new Vector3(1.4f,1.1f,1.3f);
        }
        Sign(parent,"SUNCREST REEF",new Vector3(0,0,-rz*.76f));
        Sign(parent,"SHELL COVE",archPosition+new Vector3(-5,0,-4));
        Sign(parent,"PALM GROVE",new Vector3(-rx*.55f,0,-rz*.22f));
        Sign(parent,"SHALLOW REEF\nExplore gently. Leave the coral.",new Vector3(rx*.52f,0,-rz*.66f));
    }
    private static Vector3 Ground(Vector3 local)
    {
        Vector3 p=center+local;p.y=terrain.SampleHeight(p)+terrain.transform.position.y;return p;
    }
    private static void Place(GameObject prefab,Transform parent,Vector3 local,float scale)
    {
        var instance=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        instance.transform.SetParent(parent);instance.transform.position=Ground(local);
        instance.transform.rotation=Quaternion.Euler(0,Rand(0,360),0);instance.transform.localScale=Vector3.one*scale;
    }
    private static GameObject Primitive(PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        var collider=go.GetComponent<Collider>();if(collider!=null)Object.DestroyImmediate(collider);
        go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    private static GameObject BuildPalm()
    {
        var root=new GameObject("Leaning coastal palm");
        for(int i=0;i<9;i++)
        {
            float y=i*.8f;var segment=Primitive(PrimitiveType.Cylinder,root.transform,new Vector3(.016f*y*y,y+.4f,0),new Vector3(.44f-i*.023f,.43f,.44f-i*.023f),bark);
            segment.transform.localRotation=Quaternion.Euler(0,0,-Mathf.Atan(.032f*y)*Mathf.Rad2Deg);
        }
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
        for(int frond=0;frond<10;frond++)
        {
            float angle=frond*Mathf.PI*.2f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),side=Vector3.Cross(Vector3.up,direction);
            for(int step=0;step<20;step++)
            {
                float t=step/20f;
                Vector3 p=new Vector3(.82f,7.3f,0)+direction*(t*4.4f)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.7f-t*t*2.1f);
                float width=Mathf.Sin((t*.85f+.08f)*Mathf.PI)*.85f*(1-t*.65f);
                for(int sign=-1;sign<=1;sign+=2)
                {
                    int n=vertices.Count;
                    vertices.Add(p);vertices.Add(p+direction*.27f);
                    vertices.Add(p+side*(sign*width)-direction*.20f-Vector3.up*.20f);
                    triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n});
                }
            }
        }
        var leaves=new GameObject("Pinnate fronds");leaves.transform.SetParent(root.transform,false);
        leaves.AddComponent<MeshFilter>().sharedMesh=MeshAsset("PalmFronds",vertices,triangles);leaves.AddComponent<MeshRenderer>().sharedMaterial=leaf;
        for(int i=0;i<3;i++)Primitive(PrimitiveType.Sphere,root.transform,new Vector3(.7f+i*.16f,7.13f,(i-1)*.16f),Vector3.one*.28f,bark);
        var result=SaveProp(root,"Palm",true);
        return result;
    }
    private static GameObject BuildRock()
    {
        var root=new GameObject("Weathered reef limestone");
        var a=Primitive(PrimitiveType.Sphere,root.transform,new Vector3(0,.36f,0),new Vector3(1.8f,.9f,1.4f),stone);a.transform.localRotation=Quaternion.Euler(7,12,11);
        Primitive(PrimitiveType.Sphere,root.transform,new Vector3(.45f,.28f,.35f),new Vector3(1.1f,.7f,.8f),stone);
        var col=root.AddComponent<SphereCollider>();col.center=new Vector3(0,.35f,0);col.radius=.72f;
        return SaveProp(root,"Limestone",false);
    }
    private static GameObject BuildGrass()
    {
        var root=new GameObject("Coastal grass and seagrass");
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
        for(int blade=0;blade<24;blade++)
        {
            float a=blade*2.4f,h=.32f+(blade%7)*.075f;
            var p=new Vector3(Mathf.Cos(a)*.22f,0,Mathf.Sin(a)*.22f);var side=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.04f;
            int n=vertices.Count;vertices.Add(p-side);vertices.Add(p+side);vertices.Add(p+Vector3.up*h+new Vector3(p.x,0,p.z)*1.6f);
            triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n});
        }
        root.AddComponent<MeshFilter>().sharedMesh=MeshAsset("CoastalGrass",vertices,triangles);root.AddComponent<MeshRenderer>().sharedMaterial=leaf;
        return SaveProp(root,"Grass",false);
    }
    private static GameObject BuildCoral()
    {
        var root=new GameObject("Reef coral garden");
        Primitive(PrimitiveType.Sphere,root.transform,new Vector3(0,.14f,0),new Vector3(1.8f,.32f,1.5f),stone);
        for(int i=0;i<5;i++)
        {
            float a=i*2.4f;var pos=new Vector3(Mathf.Cos(a)*.5f,.3f,Mathf.Sin(a)*.5f);
            var branch=Primitive(PrimitiveType.Cylinder,root.transform,pos+Vector3.up*.2f,new Vector3(.12f,.35f,.12f),coral);branch.transform.localRotation=Quaternion.Euler(20,0,25);
            Primitive(PrimitiveType.Sphere,root.transform,pos+Vector3.up*.5f,new Vector3(.75f,.1f,.60f),coral);
        }
        return SaveProp(root,"Coral",false);
    }
    private static Mesh MeshAsset(string name,System.Collections.Generic.List<Vector3> vertices,System.Collections.Generic.List<int> triangles)
    {
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        return StoreMesh(mesh,name);
    }
    private static Mesh StoreMesh(Mesh mesh,string name)
    {
        string path=Root+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
    }
    private static GameObject SaveProp(GameObject root,string name,bool palm)
    {
        // Merge material groups once; all instances share the resulting meshes.
        var sources=root.GetComponentsInChildren<MeshFilter>();
        foreach(var material in sources.Select(s=>s.GetComponent<MeshRenderer>().sharedMaterial).Distinct().ToArray())
        {
            var selected=sources.Where(s=>s.GetComponent<MeshRenderer>().sharedMaterial==material).ToArray();
            var combine=selected.Select(s=>new CombineInstance{mesh=s.sharedMesh,transform=root.transform.worldToLocalMatrix*s.transform.localToWorldMatrix}).ToArray();
            var mesh=new Mesh();mesh.CombineMeshes(combine,true,true);
            var node=new GameObject(material.name);node.transform.SetParent(root.transform,false);
            node.AddComponent<MeshFilter>().sharedMesh=StoreMesh(mesh,name+"-"+material.name);
            var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=palm?ShadowCastingMode.On:ShadowCastingMode.Off;
        }
        foreach(var source in sources)
        {
            if(source.gameObject==root){Object.DestroyImmediate(source.GetComponent<MeshRenderer>());Object.DestroyImmediate(source);}
            else Object.DestroyImmediate(source.gameObject);
        }
        if(palm){var collider=root.AddComponent<CapsuleCollider>();collider.center=new Vector3(.4f,3.6f,0);collider.height=7.2f;collider.radius=.32f;}
        var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(palm ? 0.022f : 0.018f,root.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+name+".prefab");Object.DestroyImmediate(root);return prefab;
    }
    private static void Sign(Transform parent,string words,Vector3 local)
    {
        var root=new GameObject(words.Replace('\n',' '));root.transform.SetParent(parent);root.transform.position=Ground(local);
        Primitive(PrimitiveType.Cylinder,root.transform,new Vector3(0,.7f,0),new Vector3(.12f,.7f,.12f),wood);
        Primitive(PrimitiveType.Cube,root.transform,new Vector3(0,1.5f,0),new Vector3(3.5f,.72f,.10f),wood);
        var label=new GameObject("Carved trail sign");label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,1.5f,-.06f);
        var text=label.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=words;text.fontSize=64;text.characterSize=.042f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.96f,.91f,.74f);
        string path=Root+"/TrailSign.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Resources.Load<Shader>("Fishing/ShopSign"));material.mainTexture=text.font.material.mainTexture;AssetDatabase.CreateAsset(material,path);}
        label.GetComponent<MeshRenderer>().sharedMaterial=material;
        var fit=label.AddComponent<ShopSign>();fit.area=new Vector2(3.2f,.58f);fit.Fit();
    }
}
