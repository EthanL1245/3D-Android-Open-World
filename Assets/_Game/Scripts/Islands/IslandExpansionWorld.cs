using UnityEngine;

// Scene-authored procedural expansion: generates once before gameplay Start methods.
// Keeps the SAME Terrain component, so existing fishing and boat references stay valid.
[DefaultExecutionOrder(-600)]
public sealed class IslandExpansionWorld : MonoBehaviour
{
    public IslandExpansionConfig Config;
    public static IslandExpansionWorld Active {get;private set;}
    public Terrain Terrain {get;private set;}
    public OceanWater Water {get;private set;}
    public Vector3 NewCenter {get;private set;}
    public Vector3 ShelfCenter {get;private set;}
    public Vector2 ShelfRadii {get;private set;}
    public Transform Arrival {get;private set;}
    public bool Ready {get;private set;}
    private TerrainData originalData,generatedData;
    private Vector3 originalPosition;
    private ReefZone reef;
    private float sea;
    private Texture2D rockTexture;
    private TerrainLayer rockLayer;
    private Material rock,wood,grass;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForExistingScenes()
    {
        if(FindFirstObjectByType<IslandExpansionWorld>()!=null || ReefZone.Active==null)return;
        var go=new GameObject("Island Expansion");go.AddComponent<IslandExpansionWorld>();
    }
    private void Start(){Build();}
    public void Build()
    {
        if(Ready)return;
        reef=ReefZone.Active;Water=FindFirstObjectByType<OceanWater>();
        if(reef==null || Water==null){Debug.LogWarning("Island expansion needs the installed Suncrest Reef scene. Run Tools/Setup Island Expansion after installing Suncrest Reef.");return;}
        Terrain=reef.GetComponentInChildren<Terrain>();if(Terrain==null)Terrain=UnityEngine.Terrain.activeTerrain;
        if(Terrain==null)return;
        if(Config==null)Config=Resources.Load<IslandExpansionConfig>("Islands/BrinebreakExpansion");
        if(Config==null){Debug.LogError("Brinebreak expansion configuration missing.");return;}
        Active=this;sea=Water.BaseWaterLevel;originalData=Terrain.terrainData;originalPosition=Terrain.transform.position;
        NewCenter=reef.center+new Vector3(reef.islandRadiusX+Config.OffsetBeyondSuncrest,0,35);
        float left=reef.center.x-reef.islandRadiusX-reef.reefWidth-60;
        float right=NewCenter.x+Config.IslandRadii.x+150;
        ShelfCenter=new Vector3((left+right)*.5f,sea,reef.center.z+17.5f);
        // Oversize the outer ellipse so both complete coastal shelves fit inside.
        ShelfRadii=new Vector2((right-left)*.5f,Mathf.Max(reef.islandRadiusZ+reef.reefWidth+95,Config.IslandRadii.y+190));
        BuildTerrain();BuildScenery();ResizeWaterQuery();Ready=true;
    }
    private float SourceHeight(Vector3 p)
    {
        Vector3 q=p-originalPosition;var size=originalData.size;
        if(q.x<0 || q.z<0 || q.x>size.x || q.z>size.z)return sea-Config.OceanDepth;
        return originalPosition.y+originalData.GetInterpolatedHeight(q.x/size.x,q.z/size.z);
    }
    public float Height(Vector3 p)
    {
        float beyond=IslandGeometry.Beyond(p,ShelfCenter,ShelfRadii);
        float shelf=IslandGeometry.ShelfFloor(sea,Config.ShelfDepth,Config.OceanDepth,beyond,Config.DropoffWidth);
        float sunDistance=IslandGeometry.Beyond(p,reef.center,new Vector2(reef.islandRadiusX,reef.islandRadiusZ));
        float retain=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(reef.reefWidth-40,reef.reefWidth+50,sunDistance));
        shelf=Mathf.Max(shelf,Mathf.Lerp(shelf,SourceHeight(p),retain));
        float q=IslandGeometry.Ellipse(p,NewCenter,Config.IslandRadii);
        if(q<1)
        {
            float inland=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,.63f,q));
            float hills=4+7*Mathf.PerlinNoise(p.x*.022f+13,p.z*.025f+71)+3*Mathf.PerlinNoise(p.x*.07f,p.z*.07f);
            float h=sea+.06f+inland*hills;
            return Mathf.Max(shelf,h);
        }
        float coast=(q-1)*Mathf.Min(Config.IslandRadii.x,Config.IslandRadii.y);
        return IslandGeometry.CoastalFloor(shelf,sea,coast,Config.ShelfDepth);
    }
    private void BuildTerrain()
    {
        float extent=Mathf.Max(ShelfRadii.x,ShelfRadii.y)+Config.DropoffWidth+150;
        float size=extent*2;
        Vector3 origin=new Vector3(ShelfCenter.x-extent,sea-Config.OceanDepth-10,ShelfCenter.z-extent);
        int n=Mathf.ClosestPowerOfTwo(Mathf.Clamp(Config.HeightResolution-1,256,2048))+1;
        generatedData=new TerrainData{name="Suncrest + Brinebreak Shared Shelf",heightmapResolution=n,alphamapResolution=512,baseMapResolution=1024};
        generatedData.size=new Vector3(size,Config.OceanDepth+40,size);
        var heights=new float[n,n];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {var p=origin+new Vector3(x/(float)(n-1)*size,0,z/(float)(n-1)*size);heights[z,x]=Mathf.Clamp01((Height(p)-origin.y)/generatedData.size.y);}
        generatedData.SetHeights(0,0,heights);
        var sourceLayers=originalData.terrainLayers;
        rockTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);rockTexture.wrapMode=TextureWrapMode.Repeat;
        var pixels=new Color[1024];for(int i=0;i<pixels.Length;i++){float noise=Mathf.PerlinNoise(i%32*.3f,i/32*.3f);pixels[i]=Color.Lerp(new Color(.23f,.25f,.26f),new Color(.49f,.47f,.41f),noise);}rockTexture.SetPixels(pixels);rockTexture.Apply();
        rockLayer=new TerrainLayer{diffuseTexture=rockTexture,tileSize=new Vector2(7,7)};
        var layers=new TerrainLayer[sourceLayers.Length+1];sourceLayers.CopyTo(layers,0);layers[layers.Length-1]=rockLayer;generatedData.terrainLayers=layers;
        var oldAlpha=originalData.GetAlphamaps(0,0,originalData.alphamapWidth,originalData.alphamapHeight);
        var alpha=new float[512,512,layers.Length];
        for(int z=0;z<512;z++)for(int x=0;x<512;x++)
        {
            Vector3 p=origin+new Vector3(x/511f*size,0,z/511f*size);
            float q=IslandGeometry.Ellipse(p,NewCenter,Config.IslandRadii);
            if(q<1.2f)
            {
                float rocky=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.96f,.75f,q));
                alpha[z,x,0]=1-rocky;alpha[z,x,layers.Length-1]=rocky;
            }
            else
            {
                Vector3 local=p-originalPosition;int ox=Mathf.Clamp(Mathf.RoundToInt(local.x/originalData.size.x*(originalData.alphamapWidth-1)),0,originalData.alphamapWidth-1);int oz=Mathf.Clamp(Mathf.RoundToInt(local.z/originalData.size.z*(originalData.alphamapHeight-1)),0,originalData.alphamapHeight-1);
                for(int l=0;l<sourceLayers.Length;l++)alpha[z,x,l]=oldAlpha[oz,ox,l];
                if(sourceLayers.Length==0)alpha[z,x,0]=1;
            }
        }
        generatedData.SetAlphamaps(0,0,alpha);
        Terrain.transform.position=origin;Terrain.terrainData=generatedData;
        var collider=Terrain.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=generatedData;
        Terrain.heightmapPixelError=6;Terrain.drawInstanced=true;Terrain.basemapDistance=350;
        Physics.SyncTransforms();
    }
    private Material Surface(Color color)
    {
        var shader=Resources.Load<Shader>("Fishing/ShopSurface");var m=new Material(shader);m.color=color;return m;
    }
    private void BuildScenery()
    {
        rock=Surface(new Color(.32f,.34f,.35f));wood=Surface(new Color(.32f,.20f,.10f));grass=Surface(new Color(.25f,.31f,.15f));
        Arrival=new GameObject("BrinebreakArrival").transform;Arrival.SetParent(transform,false);
        Vector3 approach=NewCenter+new Vector3(-Config.IslandRadii.x*.82f,0,-8);
        approach.y=Terrain.SampleHeight(approach)+Terrain.transform.position.y+.12f;
        Arrival.SetPositionAndRotation(approach,Quaternion.Euler(0,90,0));
        var random=new System.Random(Config.ScenerySeed);
        for(int i=0;i<145;i++)
        {
            float angle=(float)random.NextDouble()*Mathf.PI*2;float radius=Mathf.Sqrt((float)random.NextDouble())*.94f;
            Vector3 p=NewCenter+new Vector3(Mathf.Cos(angle)*radius*Config.IslandRadii.x,0,Mathf.Sin(angle)*radius*Config.IslandRadii.y);
            if(Vector3.ProjectOnPlane(p-approach,Vector3.up).magnitude<12)continue;
            bool bush=i%4==0;float scale=bush?1.3f:1.5f+(float)random.NextDouble()*4.5f;
            var prop=GameObject.CreatePrimitive(bush?PrimitiveType.Sphere:PrimitiveType.Cube);prop.name=bush?"Wind scrub":"Weathered rock";prop.transform.SetParent(transform,false);
            prop.transform.localScale=new Vector3(scale,bush?.8f:scale*.6f,scale*.8f);prop.transform.rotation=Quaternion.Euler(bush?0:random.Next(-18,19),random.Next(360),bush?0:random.Next(-18,19));
            p.y=Terrain.SampleHeight(p)+Terrain.transform.position.y;prop.transform.position=p;
            var renderer=prop.GetComponent<Renderer>();renderer.sharedMaterial=bush?grass:rock;
            prop.transform.position+=Vector3.up*(renderer.bounds.extents.y*.45f); // bury lower half in slope
            if(bush)Destroy(prop.GetComponent<Collider>());
            prop.isStatic=true;
        }
        Vector3 signPoint=approach+new Vector3(4,0,4);signPoint.y=Terrain.SampleHeight(signPoint)+Terrain.transform.position.y;
        IslandWelcomeSign.Create(transform,"BRINEBREAK ISLE",signPoint,Quaternion.Euler(0,90,0),wood);
        var trigger=new GameObject("Brinebreak Land Discovery");trigger.transform.SetParent(transform,false);trigger.transform.position=NewCenter+Vector3.up*(sea+12-NewCenter.y);
        var box=trigger.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(Config.IslandRadii.x*2,30,Config.IslandRadii.y*2);
        var rb=trigger.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;trigger.AddComponent<IslandDiscovery>();
    }
    private void ResizeWaterQuery()
    {
        var query=GameObject.Find("BoatPlacementWater");if(query==null)return;
        var box=query.GetComponent<BoxCollider>();if(box==null)return;
        query.transform.position=new Vector3(Terrain.transform.position.x+Terrain.terrainData.size.x*.5f,sea-.025f,Terrain.transform.position.z+Terrain.terrainData.size.z*.5f);
        box.size=new Vector3(Terrain.terrainData.size.x,.05f,Terrain.terrainData.size.z);
    }
    public int BiomeAt(Vector3 p)=>IslandGeometry.Biome(p,reef.center,NewCenter);
    public float OffshoreAt(Vector3 p)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,180,IslandGeometry.Beyond(p,ShelfCenter,ShelfRadii)));
    public static int FishingBiome(Vector3 p)=>Active!=null && Active.Ready?Active.BiomeAt(p):0;
    private void OnDestroy()
    {
        if(Active==this)Active=null;
        if(Terrain!=null && originalData!=null){Terrain.terrainData=originalData;Terrain.transform.position=originalPosition;var c=Terrain.GetComponent<TerrainCollider>();if(c!=null)c.terrainData=originalData;}
        if(generatedData!=null)Destroy(generatedData);if(rockLayer!=null)Destroy(rockLayer);if(rockTexture!=null)Destroy(rockTexture);
        if(rock!=null)Destroy(rock);if(wood!=null)Destroy(wood);if(grass!=null)Destroy(grass);
    }
}
