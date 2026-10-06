using System.Collections;
using UnityEngine;

/// <summary>
/// Builds the dedicated snapper island into the already-expanded shared Terrain.
/// Runs immediately after IslandExpansionWorld, before normal gameplay Start methods.
/// Existing terrain, colliders, seabed relief and authored surface layers are preserved.
/// </summary>
[DefaultExecutionOrder(-550)]
public sealed class SnapperIslandRuntime : MonoBehaviour
{
    public static Vector3 Center {get;private set;}
    public static Transform Arrival {get;private set;}
    public static bool Ready {get;private set;}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if(FindFirstObjectByType<SnapperIslandRuntime>()!=null)return;
        new GameObject("Snapper Island Runtime").AddComponent<SnapperIslandRuntime>();
    }

    private IEnumerator Start()
    {
        // Normally IslandExpansionWorld (-600) completes before this component (-550).
        // Still allow a few frames for domain/scene timing differences so we never
        // sculpt the old pre-expansion Terrain only to have it replaced a frame later.
        for(int frame=0;frame<120;frame++)
        {
            IslandExpansionWorld expansion=IslandExpansionWorld.Active;
            if(expansion==null || expansion.Ready)break;
            yield return null;
        }
        Build();
    }

    private void Build()
    {
        if(Ready)return;
        IslandExpansionWorld expansion=IslandExpansionWorld.Active;
        ReefZone reef=ReefZone.Active;
        OceanWater water=FindFirstObjectByType<OceanWater>();

        if(expansion!=null && !expansion.Ready)
        {
            Debug.LogWarning("[SNAPPER ISLAND] Island expansion was still rebuilding; Snapper Island was not sculpted this frame.");
            return;
        }

        Terrain terrain=expansion!=null && expansion.Ready?expansion.Terrain:Terrain.activeTerrain;
        if(reef==null || water==null || terrain==null)
        {
            Debug.LogWarning("[SNAPPER ISLAND] Could not build because Suncrest terrain/water is not ready.");
            return;
        }

        Center=SnapperIslandGeometry.Center(reef);
        if(!InsideTerrain(terrain,Center,SnapperIslandGeometry.RadiusX+SnapperIslandGeometry.CoastalShelfWidth,
            SnapperIslandGeometry.RadiusZ+SnapperIslandGeometry.CoastalShelfWidth))
        {
            Debug.LogError("[SNAPPER ISLAND] Requested south island falls outside the generated terrain bounds.");
            return;
        }

        SculptTerrain(terrain,water.BaseWaterLevel);
        PaintTerrain(terrain);
        CreateMarker();
        SnapperIslandRockscape rockscape=gameObject.AddComponent<SnapperIslandRockscape>();
        if(!rockscape.Build(terrain,water.BaseWaterLevel,Center,Arrival.position))
            Debug.LogWarning("[SNAPPER ISLAND] Terrain built, but the modular rockscape could not be loaded.");
        Physics.SyncTransforms();
        Ready=true;

        Debug.Log("[SNAPPER ISLAND] Built rugged modular-rock Snapper Island directly south of Suncrest; gameplay bounds and 50 m snapper fishing water are unchanged.");
    }

    private static bool InsideTerrain(Terrain terrain,Vector3 center,float radiusX,float radiusZ)
    {
        TerrainData data=terrain.terrainData;
        Vector3 local=center-terrain.transform.position;
        return local.x-radiusX>=0f && local.z-radiusZ>=0f &&
               local.x+radiusX<=data.size.x && local.z+radiusZ<=data.size.z;
    }

    private static void SculptTerrain(Terrain terrain,float sea)
    {
        TerrainData data=terrain.terrainData;
        int n=data.heightmapResolution;
        float[,] heights=data.GetHeights(0,0,n,n);
        Vector3 origin=terrain.transform.position;
        Vector3 size=data.size;
        float influenceX=SnapperIslandGeometry.RadiusX+SnapperIslandGeometry.CoastalShelfWidth;
        float influenceZ=SnapperIslandGeometry.RadiusZ+SnapperIslandGeometry.CoastalShelfWidth;

        int minX=Mathf.Clamp(Mathf.FloorToInt((Center.x-influenceX-origin.x)/size.x*(n-1)),0,n-1);
        int maxX=Mathf.Clamp(Mathf.CeilToInt((Center.x+influenceX-origin.x)/size.x*(n-1)),0,n-1);
        int minZ=Mathf.Clamp(Mathf.FloorToInt((Center.z-influenceZ-origin.z)/size.z*(n-1)),0,n-1);
        int maxZ=Mathf.Clamp(Mathf.CeilToInt((Center.z+influenceZ-origin.z)/size.z*(n-1)),0,n-1);

        for(int z=minZ;z<=maxZ;z++)
        for(int x=minX;x<=maxX;x++)
        {
            Vector3 p=origin+new Vector3(x/(float)(n-1)*size.x,0f,z/(float)(n-1)*size.z);
            float current=origin.y+heights[z,x]*size.y;
            float target=SnapperIslandGeometry.Height(p,Center,sea,current);
            if(target>current)
                heights[z,x]=Mathf.Clamp01((target-origin.y)/size.y);
        }

        data.SetHeights(0,0,heights);
        TerrainCollider collider=terrain.GetComponent<TerrainCollider>();
        if(collider!=null)collider.terrainData=data;
    }

    private static void PaintTerrain(Terrain terrain)
    {
        TerrainData data=terrain.terrainData;
        int width=data.alphamapWidth,height=data.alphamapHeight,layers=data.alphamapLayers;
        if(layers<1)return;
        float[,,] alpha=data.GetAlphamaps(0,0,width,height);
        Vector3 origin=terrain.transform.position;
        Vector3 size=data.size;
        int rockLayer=layers>=2?layers-2:0;

        for(int z=0;z<height;z++)
        for(int x=0;x<width;x++)
        {
            Vector3 p=origin+new Vector3(x/(float)(width-1)*size.x,0f,z/(float)(height-1)*size.z);
            float q=SnapperIslandGeometry.Ellipse(p,Center);
            if(q>1f)continue;

            float inland=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.94f,.48f,q));
            float breakup=Mathf.PerlinNoise(p.x*.065f+19f,p.z*.061f+43f);
            float stone=Mathf.Clamp01(inland*Mathf.Lerp(.28f,.72f,breakup));
            if(Mathf.PerlinNoise(p.x*.035f+81f,p.z*.039f+12f)>.68f)stone*=.42f;

            for(int layer=0;layer<layers;layer++)alpha[z,x,layer]=0f;
            if(rockLayer==0)alpha[z,x,0]=1f;
            else
            {
                alpha[z,x,0]=1f-stone;
                alpha[z,x,rockLayer]=stone;
            }
        }

        data.SetAlphamaps(0,0,alpha);
    }

    private void CreateMarker()
    {
        GameObject marker=new GameObject("Snapper Island");
        marker.transform.SetParent(transform,false);
        marker.transform.position=Center;
        Terrain terrain=IslandExpansionWorld.Active!=null?IslandExpansionWorld.Active.Terrain:Terrain.activeTerrain;
        GameObject arrival=new GameObject("SnapperArrival");
        arrival.transform.SetParent(transform,false);
        Vector3 p=Center+Vector3.forward*(SnapperIslandGeometry.RadiusZ*.65f);
        p.y=terrain.SampleHeight(p)+terrain.transform.position.y+.2f;
        arrival.transform.SetPositionAndRotation(p,Quaternion.identity);
        Arrival=arrival.transform;
        GameObject trigger=new GameObject("Snapper Land Discovery");
        trigger.transform.SetParent(transform,false);
        trigger.transform.position=Center+Vector3.up*(FindFirstObjectByType<OceanWater>().BaseWaterLevel+12f-Center.y);
        var box=trigger.AddComponent<BoxCollider>();box.isTrigger=true;
        box.size=new Vector3(SnapperIslandGeometry.RadiusX*2,30,SnapperIslandGeometry.RadiusZ*2);
        var body=trigger.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
        trigger.AddComponent<IslandDiscovery>().Biome=ReefCatalog.SnapperBiomeId;
    }

    private void OnDestroy()
    {
        // Static state must not survive into a newly loaded gameplay scene where a
        // fresh Terrain still needs to be sculpted.
        Arrival=null;
        Ready=false;
        Center=Vector3.zero;
    }
}

