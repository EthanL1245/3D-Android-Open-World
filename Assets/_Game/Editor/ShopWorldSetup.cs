using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class ShopWorldSetup
{
    private const string Root="Assets/_Game/ShopWorld";
    private const string ScenePath=Root+"/TideglassShopWorld.unity";
    private static Material stone, wood, dark, gold, plaster, glass, water, green, sand, glow;
    private static int lightCount;
    private static readonly Vector3 DimensionOrigin=new Vector3(2000,200,2000);

    [MenuItem("Tools/Open World/Install Shop World (One Click)")]
    public static void Install()
    {
        Scene island=SceneManager.GetActiveScene(); Scene shop=default;
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if(string.IsNullOrEmpty(island.path) || island.name==ShopDimensionManager.SceneName) throw new InvalidOperationException("Open your saved fishing island scene first.");
            GameObject player=GameObject.Find("Player");
            if(player==null || player.GetComponent<FirstPersonController>()==null || player.GetComponent<FishingSystem>()==null || player.GetComponent<FishingInventory>()==null)
                throw new InvalidOperationException("This scene needs the existing Player, fishing system and inventory.");
            Canvas canvas=Object.FindFirstObjectByType<Canvas>();
            if(canvas==null) throw new InvalidOperationException("Gameplay canvas missing.");
            Folder(Root); Folder(Root+"/Materials"); Folder(Root+"/Textures"); Folder(Root+"/Backups");
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(Root+"/Backups/IslandBeforeShopWorld.unity"))
            {
                // Capture current scene edits before modifying the home-base objects.
                EditorSceneManager.SaveScene(island);
                if(!AssetDatabase.CopyAsset(island.path,Root+"/Backups/IslandBeforeShopWorld.unity"))
                    throw new InvalidOperationException("Could not back up the island scene.");
            }
            EditorUtility.DisplayProgressBar("Tideglass Quay","Creating stone, timber, glass and light materials...",0.12f);
            BuildMaterials();
            Scene loaded=SceneManager.GetSceneByPath(ScenePath);
            if(loaded.IsValid() && loaded.isLoaded) EditorSceneManager.CloseScene(loaded,true);
            shop=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(shop);
            BuildWorld();
            if(!EditorSceneManager.SaveScene(shop,ScenePath)) throw new InvalidOperationException("Could not save the shop world.");
            EditorSceneManager.CloseScene(shop,true); shop=default;
            SceneManager.SetActiveScene(island);
            EditorUtility.DisplayProgressBar("Tideglass Quay","Connecting travel, purchases, inventory and migration...",0.8f);
            Vector3 safeArrival=FindSafeIslandArrival(player.transform.position);
            RemoveLegacyObjects(island);
            Add<EconomySystem>(player); Add<ShopProgress>(player);
            var travel=Add<ShopDimensionManager>(player);
            var ui=Add<ShopWorldHUD>(canvas.gameObject);
            var oldHud=canvas.GetComponent<EconomyHUD>(); if(oldHud!=null) Undo.DestroyObjectImmediate(oldHud);
            var oldAquarium=player.GetComponent<AquariumSystem>(); if(oldAquarium!=null) Undo.DestroyObjectImmediate(oldAquarium);
            Transform arrival=player.transform.parent!=null ? player.transform.parent.Find("FishingIslandArrival") : null;
            if(arrival==null)
            {
                GameObject existing=GameObject.Find("FishingIslandArrival");
                arrival=existing!=null ? existing.transform : new GameObject("FishingIslandArrival").transform;
            }
            arrival.position=safeArrival;
            arrival.rotation=player.transform.rotation; travel.islandArrival=arrival;
            // The old platform is gone. Start on real land, not above its former surface.
            player.transform.position=arrival.position;
            EditorUtility.SetDirty(travel); EditorUtility.SetDirty(ui); EditorUtility.SetDirty(player);
            var scenes=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            AddBuildScene(scenes,island.path); AddBuildScene(scenes,ScenePath); EditorBuildSettings.scenes=scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(island); EditorSceneManager.SaveScene(island); AssetDatabase.SaveAssets();
            Selection.activeGameObject=player;
            EditorUtility.DisplayDialog("Tideglass Quay ready","Press Play and use MENU / TRAVEL.\n\nThe island's old shops/platform and aquarium system have been removed. Existing aquarium fish and refunds migrate into the new save on first Play.\n\nVisit counters or habitat signs to buy, sell and manage fish. Larger habitats have stairs, swimming access and EXIT WATER.\n\nYour pre-install island scene is backed up under _Game/ShopWorld/Backups.","OK");
        }
        catch(Exception error) { Debug.LogException(error); EditorUtility.DisplayDialog("Shop setup failed",error.Message,"OK"); }
        finally
        {
            if(shop.IsValid() && shop.isLoaded) EditorSceneManager.CloseScene(shop,true);
            if(island.IsValid() && island.isLoaded) SceneManager.SetActiveScene(island);
            EditorUtility.ClearProgressBar();
        }
    }
    private static void BuildWorld()
    {
        lightCount=0;
        GameObject root=new GameObject("TideglassQuay"); root.transform.position=DimensionOrigin;
        var world=root.AddComponent<ShopWorldEnvironment>(); Transform t=root.transform;
        Box("Foundation",t,new Vector3(0,-8.5f,48),new Vector3(142,1.3f,166),stone);
        BuildGround(t);
        for(int side=-1;side<=1;side+=2)
        {
            Box("BoundaryHedge",t,new Vector3(side*69,1.3f,48),new Vector3(1.2f,2.6f,166),green);
            for(int z=-26;z<130;z+=14) { Tree(t,new Vector3(side*63,0,z)); Lamp(t,new Vector3(side*55,0,z)); }
        }
        Box("RearHedge",t,new Vector3(0,1.3f,130),new Vector3(140,2.6f,1.2f),green);
        Box("FrontBoundary",t,new Vector3(0,0.65f,-34),new Vector3(140,1.3f,1),stone);
        Sign(t,"TIDEGLASS QUAY",new Vector3(0,5.7f,-16),0,0.17f);
        Sign(t,"TACKLE  /  AQUARIUMS  /  POND GARDENS",new Vector3(0,4.6f,-16),0,0.07f);
        // Timber colonnade and a skylit pavilion, with clear walkways between exhibits.
        for(int x=-1;x<=1;x+=2)
        {
            for(int z=-12;z<=40;z+=13)
            {
                Box("PavilionColumn",t,new Vector3(x*39,4.8f,z),new Vector3(0.55f,9.6f,0.55f),wood);
                Box("ColumnFoot",t,new Vector3(x*39,0.35f,z),new Vector3(0.8f,0.7f,0.8f),dark);
                Planter(t,new Vector3(x*35,0,z));
            }
            Box("RoofEdge",t,new Vector3(x*39,9.7f,14),new Vector3(0.5f,0.55f,58),wood);
            Box("SkylightWing",t,new Vector3(x*28,10f,14),new Vector3(23,0.2f,58),plaster,false);
        }
        for(int z=-14;z<=42;z+=7)
        {
            Box("RoofRafter",t,new Vector3(0,9.6f,z),new Vector3(78,0.24f,0.22f),wood,false);
            Box("Skylight",t,new Vector3(0,9.9f,z),new Vector3(32,0.06f,6.8f),glass,false);
            Box("WarmLightStrip",t,new Vector3(0,9.4f,z),new Vector3(20,0.07f,0.08f),glow,false);
        }
        world.spawn=Point("Arrival",t,new Vector3(0,0.25f,-23),0);
        world.gearPoint=Kiosk(t,new Vector3(-14,0,-3),"THE TACKLE ATELIER","RODS / REELS / LINE / BAIT",false);
        world.marketPoint=Kiosk(t,new Vector3(14,0,-3),"FRESH CATCH MARKET","FISH WEIGHED / FAIR PRICES",true);
        Sign(t,"AQUARIUM GALLERY",new Vector3(0,3.8f,13),0,0.11f);
        Vector3[] positions={new Vector3(-28,0,22),new Vector3(-17,0,22),new Vector3(-3,0,26),new Vector3(20,0,28),new Vector3(0,0,58),new Vector3(-35,0,60),new Vector3(35,0,67),new Vector3(0,0,106)};
        var habitats=new List<ShopHabitat>();
        for(int i=0;i<ShopCatalog.Habitats.Length;i++) habitats.Add(BuildHabitat(t,ShopCatalog.Habitats[i],positions[i]));
        world.habitats=habitats.ToArray();
        ValidateWorld(world);
        Sign(t,"POND GARDENS",new Vector3(0,4.6f,82),0,0.13f);
        for(int side=-1;side<=1;side+=2)
        {
            Bench(t,new Vector3(side*12,0,77)); Bench(t,new Vector3(side*25,0,8));
            for(int i=0;i<4;i++) Planter(t,new Vector3(side*(12+i*9),0,-22));
        }
        // One low-resolution ambient reflection probe; most lighting comes from
        // the island sun/sky plus emissive fixtures, keeping mobile cost bounded.
        var probe=new GameObject("GalleryReflection").AddComponent<ReflectionProbe>(); probe.transform.SetParent(t,false);
        probe.transform.localPosition=new Vector3(0,5,22); probe.size=new Vector3(85,18,100);
        probe.mode=ReflectionProbeMode.Realtime; probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce; probe.resolution=128; probe.cullingMask=~0;
    }
    private static void ValidateWorld(ShopWorldEnvironment world)
    {
        if(world.habitats.Length!=ShopCatalog.Habitats.Length || world.spawn==null || world.gearPoint==null || world.marketPoint==null)
            throw new InvalidOperationException("Shop navigation is incomplete.");
        Physics.SyncTransforms();
        MeshCollider ground=world.transform.Find("PavedGalleryAndPondGardens").GetComponent<MeshCollider>();
        if(!ground.Raycast(new Ray(world.spawn.position+Vector3.up*2,Vector3.down),out RaycastHit arrivalHit,4f))
            throw new InvalidOperationException("The shop arrival has no walkable ground.");
        var ids=new HashSet<string>();
        foreach(var habitat in world.habitats)
        {
            var d=ShopCatalog.Habitat(habitat.id);
            if(d==null || !ids.Add(habitat.id) || habitat.water==null || habitat.interaction==null || habitat.fishRoot==null || habitat.label==null)
                throw new InvalidOperationException("Incomplete habitat setup.");
            if(d.swimmable)
            {
                Vector3 entry=habitat.water.transform.InverseTransformPoint(habitat.entrance.position);
                if(Mathf.Abs(entry.x)>habitat.water.size.x/2 || Mathf.Abs(entry.z)>habitat.water.size.z/2 || entry.y<0 || entry.y>habitat.water.size.y)
                    throw new InvalidOperationException("Water entry lies outside "+d.name);
            }
            if(d.pond && ground.Raycast(new Ray(habitat.transform.position+Vector3.up*10,Vector3.down),out RaycastHit hit,20))
                throw new InvalidOperationException("The garden floor blocks swimming in "+d.name);
        }
    }

    private static void BuildGround(Transform parent)
    {
        // One shared-vertex mesh with actual openings for sunken ponds. A solid
        // floor below the water would otherwise prevent swimming to the bottom.
        const int width=142, depth=166;
        var vertices=new Vector3[(width+1)*(depth+1)];
        var uv=new Vector2[vertices.Length];
        var paving=new List<int>(); var lawn=new List<int>();
        for(int z=0;z<=depth;z++) for(int x=0;x<=width;x++)
        {int i=z*(width+1)+x;vertices[i]=new Vector3(x-71,0,z-35);uv[i]=new Vector2(x/8f,z/8f);}
        for(int z=0;z<depth;z++) for(int x=0;x<width;x++)
        {
            float px=x-70.5f,pz=z-34.5f;
            bool hole=(Mathf.Abs(px+35)<5 && Mathf.Abs(pz-60)<4) ||
                (Mathf.Abs(px-35)<9 && Mathf.Abs(pz-67)<7) || (Mathf.Abs(px)<16 && Mathf.Abs(pz-106)<12);
            if(hole) continue;
            bool path=(Mathf.Abs(px)<42 && pz<46) || Mathf.Abs(px)<4 || Mathf.Abs(pz-50)<3 || Mathf.Abs(pz-82)<3;
            var indices=path?paving:lawn;int a=z*(width+1)+x,b=a+width+1;
            indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});
        }
        var mesh=new Mesh{name="QuayGround",vertices=vertices,uv=uv,subMeshCount=2};
        mesh.SetTriangles(paving,0);mesh.SetTriangles(lawn,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string pathAsset=Root+"/QuayGround.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(pathAsset);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}
        else AssetDatabase.CreateAsset(mesh,pathAsset);
        var go=new GameObject("PavedGalleryAndPondGardens",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
        go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials=new[]{stone,green};go.GetComponent<MeshCollider>().sharedMesh=mesh;
    }

    private static ShopHabitat BuildHabitat(Transform parent,HabitatDefinition d,Vector3 location)
    {
        Transform root=Point(d.name,parent,location,0); var habitat=root.gameObject.AddComponent<ShopHabitat>(); habitat.id=d.id;
        float w=d.width,h=d.height,z=d.depth,bottom=d.pond ? 0.35f-d.height : 0.45f;
        Material wall=d.pond?stone:glass;
        Box("Plinth",root,new Vector3(0,bottom-0.22f,0),new Vector3(w+0.65f,0.44f,z+0.65f),dark);
        Box("SandBed",root,new Vector3(0,bottom-0.04f,0),new Vector3(w,0.08f,z),sand);
        Box("Front",root,new Vector3(0,bottom+h/2,-z/2),new Vector3(w, h+0.18f,0.13f),wall);
        Box("Back",root,new Vector3(0,bottom+h/2,z/2),new Vector3(w, h+0.18f,0.13f),wall);
        Box("Left",root,new Vector3(-w/2,bottom+h/2,0),new Vector3(0.13f,h+0.18f,z),wall);
        Box("Right",root,new Vector3(w/2,bottom+h/2,0),new Vector3(0.13f,h+0.18f,z),wall);
        for(int side=-1;side<=1;side+=2)
        {
            Box("TopRim",root,new Vector3(0,bottom+h+0.1f,side*z/2),new Vector3(w+0.25f,0.17f,0.23f),d.pond?stone:gold);
            Box("SideRim",root,new Vector3(side*w/2,bottom+h+0.1f,0),new Vector3(0.23f,0.17f,z+0.25f),d.pond?stone:gold);
            if(!d.pond)
            {
                Box("IlluminatedBase",root,new Vector3(0,bottom+0.035f,side*(z/2-0.12f)),new Vector3(w-0.3f,0.045f,0.055f),glow,false);
                for(int end=-1;end<=1;end+=2) Box("GlassMullion",root,new Vector3(side*w/2,bottom+h/2,end*z/2),new Vector3(0.08f,h,0.08f),gold,false);
            }
        }
        Transform volume=Point("SwimVolume",root,new Vector3(0,bottom,0),0);
        habitat.water=volume.gameObject.AddComponent<ShopWaterVolume>(); habitat.water.size=new Vector3(w-0.18f,h-0.18f,z-0.18f);
        habitat.fishRoot=Point("Residents",root,new Vector3(0,bottom,0),0);
        Box("WaterSurface",root,new Vector3(0,bottom+h-0.18f,0),new Vector3(w-0.16f,0.018f,z-0.16f),water,false);
        GameObject barrier=new GameObject("UnpurchasedAccessCover");barrier.transform.SetParent(root,false);barrier.transform.localPosition=new Vector3(0,bottom+h+0.24f,0);
        habitat.purchaseBarrier=barrier.AddComponent<BoxCollider>();habitat.purchaseBarrier.size=new Vector3(w,0.12f,z);
        habitat.interaction=Point("PurchasePoint",root,new Vector3(0,0.25f,-z/2-3.1f),0);
        habitat.exit=Point("Exit",root,new Vector3(w/2+2.4f,0.25f,-z/2-2f),0);
        habitat.entrance=Point("WaterEntry",root,new Vector3(w*0.24f,bottom+h-1.25f,0),0);
        Box("SignPedestal",root,new Vector3(0,0.85f,-z/2-1.5f),new Vector3(Mathf.Min(w+1,5),1.5f,0.22f),dark);
        habitat.label=Sign(root,d.name.ToUpperInvariant()+$"\n{d.price:N0} COINS\n{d.fishLimit} FISH / MAX {d.maxFishKg} KG EACH",new Vector3(0,1.32f,-z/2-1.64f),0,d.width<3?0.027f:0.04f);
        if(d.swimmable)
        {
            // Real stair access in addition to the accessible Enter/Exit controls.
            if(d.pond)
            {
                int submergedSteps=Mathf.CeilToInt((h+0.25f)/0.18f);
                for(int i=0;i<submergedSteps;i++)
                {
                    float top=h+0.25f-i*0.18f;
                    Box("PondExitStep",root,new Vector3(w/2-1.05f,bottom+top/2,-z/2+0.4f+i*0.32f),new Vector3(1.5f,top,0.34f),stone);
                }
            }
            int steps=Mathf.CeilToInt((bottom+h+0.3f)/0.18f);
            for(int i=0;i<steps;i++) Box("AccessStep",root,new Vector3(w/2+1.05f,(i+1)*0.09f,-z/2-steps*0.32f+i*0.32f),new Vector3(1.6f,(i+1)*0.18f,0.34f),wood);
            Box("AccessLanding",root,new Vector3(w/2+0.55f,bottom+h+0.23f,-z/2+0.6f),new Vector3(2.6f,0.18f,1.8f),wood);
            Sign(root,"SWIM ACCESS",new Vector3(w/2+1.1f,1.3f,-z/2-steps*0.32f-0.3f),0,0.028f);
        }
        UnityEngine.Random.State state=UnityEngine.Random.state; UnityEngine.Random.InitState(d.price);
        int decorations=d.pond?14:6;
        for(int i=0;i<decorations;i++)
        {
            // Decoration stays at the edge, leaving the swimming paths open.
            float x=UnityEngine.Random.Range(-w*0.4f,w*0.4f), zz=(i%2==0?-1:1)*z*0.39f;
            Vector3 p=new Vector3(x,bottom+0.1f,zz);
            var rock=Primitive(PrimitiveType.Sphere,"RiverStone",root,p,new Vector3(0.3f,0.17f,0.25f)*Mathf.Clamp(w/4,0.5f,2),stone,false);
            for(int j=0;j<3;j++)
            {
                float size=UnityEngine.Random.Range(0.15f,0.5f)*Mathf.Min(h,2);
                var leaf=Primitive(PrimitiveType.Capsule,"AquaticPlant",root,p+new Vector3(j*0.04f,size/2,0),new Vector3(0.045f,size/2,0.055f),green,false);
                leaf.transform.localRotation=Quaternion.Euler(10*j,0,15*(j-1));
            }
        }
        UnityEngine.Random.state=state;
        if(!d.pond) Light(root,new Vector3(0,h+1,0),new Color(0.35f,0.8f,1),Mathf.Min(1.6f,w*0.2f),Mathf.Max(w,z));
        return habitat;
    }
    private static Transform Kiosk(Transform parent,Vector3 position,string title,string subtitle,bool market)
    {
        Transform root=Point(title,parent,position,0);
        Box("TimberDeck",root,new Vector3(0,0.08f,0),new Vector3(15,0.16f,11),wood);
        Box("BackWall",root,new Vector3(0,2.9f,3.8f),new Vector3(15,5.8f,0.3f),plaster);
        Box("Canopy",root,new Vector3(0,6f,0),new Vector3(15.8f,0.4f,11.8f),dark);
        Box("Counter",root,new Vector3(0,0.65f,-1.5f),new Vector3(11,1.3f,1.6f),wood);
        Box("Countertop",root,new Vector3(0,1.35f,-1.5f),new Vector3(11.3f,0.12f,1.9f),stone);
        Box("CounterInlay",root,new Vector3(0,1f,-2.32f),new Vector3(10.8f,0.06f,0.04f),gold,false);
        Sign(root,title,new Vector3(0,4.6f,-3),0,0.08f); Sign(root,subtitle,new Vector3(0,3.85f,-3),0,0.035f);
        for(int i=-1;i<=1;i++)
        {
            Box("DisplayShelf",root,new Vector3(i*4,2.1f,3.35f),new Vector3(3.5f,0.12f,0.9f),wood);
            if(market)
            {
                Box("SeafoodTray",root,new Vector3(i*3.1f,1.48f,-1.5f),new Vector3(2.4f,0.13f,1.2f),dark);
                for(int j=0;j<3;j++) Primitive(PrimitiveType.Sphere,"Ice",root,new Vector3(i*3.1f+j*0.5f-0.5f,1.58f,-1.5f),new Vector3(0.55f,0.12f,0.7f),glass,false);
                Box("WeighingScale",root,new Vector3(i*4,2.45f,3.3f),new Vector3(0.7f,0.55f,0.55f),gold,false);
            }
            else
            {
                for(int j=0;j<3;j++)
                {
                    var rod=Primitive(PrimitiveType.Cylinder,"DisplayRod",root,new Vector3(i*4+j*0.55f-0.55f,3.55f,3.1f),new Vector3(0.03f,1.4f,0.03f),j==0?wood:dark,false);
                    Primitive(PrimitiveType.Cylinder,"Reel",root,new Vector3(i*4+j*0.55f-0.45f,2.5f,3),new Vector3(0.22f,0.09f,0.22f),gold,false).transform.localRotation=Quaternion.Euler(90,0,0);
                    Box("BaitTin",root,new Vector3(i*3.2f+j*0.6f-0.6f,1.62f,-1.4f),new Vector3(0.45f,0.4f,0.55f),j==0?gold:green,false);
                }
            }
        }
        Light(root,new Vector3(0,5,-1),new Color(1,0.85f,0.62f),1.5f,13);
        return Point("CounterInteraction",root,new Vector3(0,0.25f,-4.3f),0);
    }
    private static void BuildMaterials()
    {
        Shader lit=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Fishing/ShopSurface.shader");
        Shader transparent=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Fishing/ShopGlass.shader");
        if(lit==null || transparent==null || ShaderUtil.ShaderHasError(lit) || ShaderUtil.ShaderHasError(transparent)) throw new InvalidOperationException("Shop shaders are missing or have compile errors. Wait for Unity to finish importing.");
        stone=Mat("Limestone",lit,new Color(0.7f,0.68f,0.59f),Texture("Limestone",false),0.18f);
        wood=Mat("OiledOak",lit,new Color(0.52f,0.32f,0.15f),Texture("OiledOak",true),0.38f);
        dark=Mat("DeepTeal",lit,new Color(0.025f,0.085f,0.095f),null,0.35f);
        gold=Mat("BrushedBrass",lit,new Color(0.72f,0.51f,0.19f),null,0.7f);gold.SetFloat("_Metallic",0.6f);
        plaster=Mat("WarmPlaster",lit,new Color(0.85f,0.82f,0.73f),null,0.1f);
        green=Mat("BotanicalGreen",lit,new Color(0.09f,0.25f,0.12f),null,0.2f);
        sand=Mat("AquariumSand",lit,new Color(0.67f,0.61f,0.43f),Texture("Sand",false),0.1f);
        glass=Mat("AquariumGlass",transparent,new Color(0.22f,0.64f,0.68f,0.08f),null,0);
        water=Mat("HabitatWater",transparent,new Color(0.06f,0.42f,0.45f,0.19f),null,0);water.SetFloat("_Water",1);
        Shader unlit=Shader.Find("Universal Render Pipeline/Unlit"); if(unlit==null) unlit=lit;
        glow=Mat("WarmLight",unlit,new Color(1.5f,1.2f,0.65f),null,0);
        AssetDatabase.SaveAssets();
    }
    private static Texture2D Texture(string name,bool timber)
    {
        string path=Root+"/Textures/"+name+".asset";
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture!=null) return texture;
        texture=new Texture2D(256,256,TextureFormat.RGB24,true) { name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear };
        var pixels=new Color[256*256];
        for(int y=0;y<256;y++) for(int x=0;x<256;x++)
        {
            float noise=Mathf.PerlinNoise(x*(timber?0.08f:0.15f),y*(timber?0.006f:0.15f));
            float seam=timber ? (x%64<2?0.62f:1f) : (x%64<2 || y%64<2?0.67f:1f);
            float value=(0.72f+noise*0.28f)*seam; pixels[y*256+x]=new Color(value,value,value,1);
        }
        texture.SetPixels(pixels);texture.Apply(true,false);AssetDatabase.CreateAsset(texture,path);return texture;
    }
    private static Material Mat(string name,Shader shader,Color color,Texture texture,float smoothness)
    {
        string path=Root+"/Materials/"+name+".mat"; Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) {m=new Material(shader);AssetDatabase.CreateAsset(m,path);} m.shader=shader;m.SetColor("_BaseColor",color);
        if(m.HasProperty("_BaseMap")) {m.SetTexture("_BaseMap",texture);m.SetTextureScale("_BaseMap",texture!=null?new Vector2(6,6):Vector2.one);m.SetTextureOffset("_BaseMap",Vector2.zero);}
        if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(m);return m;
    }
    private static void Tree(Transform p,Vector3 v)
    {
        Primitive(PrimitiveType.Cylinder,"TreeTrunk",p,v+Vector3.up*1.7f,new Vector3(0.4f,1.7f,0.4f),wood);
        Primitive(PrimitiveType.Sphere,"TreeCrown",p,v+Vector3.up*4,new Vector3(3.5f,3.8f,3.5f),green,false);
        Primitive(PrimitiveType.Sphere,"TreeCrown",p,v+new Vector3(0.8f,5.4f,0.4f),new Vector3(2.7f,2.8f,2.7f),green,false);
    }
    private static void Planter(Transform p,Vector3 v)
    {Box("Planter",p,v+Vector3.up*0.4f,new Vector3(1.4f,0.8f,1.4f),plaster); Primitive(PrimitiveType.Sphere,"Foliage",p,v+Vector3.up*1.1f,new Vector3(1.35f,1.2f,1.35f),green,false);}
    private static void Bench(Transform p,Vector3 v)
    {Box("BenchSeat",p,v+Vector3.up*0.6f,new Vector3(3.6f,0.15f,0.8f),wood);Box("BenchBack",p,v+new Vector3(0,1.05f,0.35f),new Vector3(3.6f,0.8f,0.12f),wood);for(int side=-1;side<=1;side+=2)Box("BenchLeg",p,v+new Vector3(side*1.3f,0.3f,0),new Vector3(0.12f,0.6f,0.65f),dark);}
    private static void Lamp(Transform p,Vector3 v)
    {Box("PathLamp",p,v+Vector3.up*1.3f,new Vector3(0.12f,2.6f,0.12f),dark);Box("Lantern",p,v+Vector3.up*2.7f,new Vector3(0.32f,0.36f,0.32f),glow,false);}
    private static void Light(Transform p,Vector3 position,Color color,float intensity,float range)
    {
        if(lightCount++>=6)return;var go=new GameObject("AccentLight");go.transform.SetParent(p,false);go.transform.localPosition=position;
        var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;l.renderMode=LightRenderMode.Auto;
    }
    private static TextMesh Sign(Transform parent,string text,Vector3 position,float yaw,float size)
    {
        var node=Point("Sign",parent,position,yaw); var label=node.gameObject.AddComponent<TextMesh>();
        label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=64;label.characterSize=size;
        label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(0.96f,0.87f,0.65f);
        node.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;return label;
    }
    private static Transform Point(string name,Transform parent,Vector3 position,float yaw)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go.transform;}
    private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool collide=true) => Primitive(PrimitiveType.Cube,name,parent,position,scale,material,collide);
    private static GameObject Primitive(PrimitiveType kind,string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool collide=true)
    {
        var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=material;
        if(!collide)Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);return go;
    }
    private static T Add<T>(GameObject go) where T:Component => go.GetComponent<T>()??Undo.AddComponent<T>(go);
    private static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    private static void AddBuildScene(List<EditorBuildSettingsScene> scenes,string path)
    {var found=scenes.Find(s=>s.path==path);if(found!=null)found.enabled=true;else scenes.Add(new EditorBuildSettingsScene(path,true));}
    private static void RemoveLegacyObjects(Scene scene)
    {
        var remove=new HashSet<GameObject>();
        foreach(var root in scene.GetRootGameObjects())
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(t.name=="HomeBase" || t.GetComponent<HomeBaseSystem>()!=null || t.name=="FishMarket" || t.name=="TankShop" || t.name=="SpawnPlatform" || t.name=="FishTankPreview" || t.name.StartsWith("FishTank_") || t.GetComponent<PlacedFishTank>()!=null)remove.Add(t.gameObject);
        }
        // Parent deletion also deletes descendants; skip Unity-null objects safely.
        foreach(var go in remove)if(go!=null)Undo.DestroyObjectImmediate(go);
    }
    private static Vector3 FindSafeIslandArrival(Vector3 from)
    {
        Terrain terrain=Object.FindFirstObjectByType<Terrain>();OceanWater ocean=Object.FindFirstObjectByType<OceanWater>();
        if(terrain==null)return from;
        Vector3 best=from;float distance=float.MaxValue;var data=terrain.terrainData;Vector3 origin=terrain.transform.position;
        for(int x=1;x<80;x++)for(int z=1;z<80;z++)
        {
            float nx=x/80f,nz=z/80f;float y=data.GetInterpolatedHeight(nx,nz)+origin.y;
            if(ocean!=null && y<ocean.BaseWaterLevel+1.2f)continue;
            if(data.GetSteepness(nx,nz)>25)continue;
            Vector3 p=new Vector3(origin.x+nx*data.size.x,y+0.15f,origin.z+nz*data.size.z);
            float d=(p-from).sqrMagnitude;if(d<distance){distance=d;best=p;}
        }
        if(distance==float.MaxValue)throw new InvalidOperationException("No safe dry arrival spot found on the island terrain.");
        return best;
    }
}
