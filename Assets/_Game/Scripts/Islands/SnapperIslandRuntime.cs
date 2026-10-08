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
    [SerializeField, HideInInspector] private bool savedLayout;
    [SerializeField] private Vector3 savedCenter;
    [SerializeField] private Transform savedArrival;
    public bool HasSavedLayout => savedLayout;
    [SerializeField, HideInInspector] private int simpleIslandVersion;
    public bool NeedsSimpleIslandRepair => simpleIslandVersion < 1;

    private void Awake() { if(savedLayout) UseSavedLayout(); }
    private void UseSavedLayout()
    {
        Center=savedCenter;Arrival=savedArrival;Ready=true;
    }
#if UNITY_EDITOR
    public void SaveLayoutForEditing()
    {
        if(!Ready)throw new System.InvalidOperationException("Generate Snapper Island before saving it.");
        savedCenter=Center;savedArrival=Arrival;savedLayout=true;
    }
#endif

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

    public void Build()
    {
        if(savedLayout)
        {
            UseSavedLayout();
            if(NeedsSimpleIslandRepair) RestoreSimpleIsland(true);
            return;
        }
        if(Ready && Application.isPlaying)return;
        IslandExpansionWorld expansion=IslandExpansionWorld.Active;
        ReefZone reef=FindFirstObjectByType<ReefZone>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);
        OceanWater water=FindFirstObjectByType<OceanWater>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);

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
        RemoveRockscape();
        simpleIslandVersion=1;
        Physics.SyncTransforms();
        Ready=true;

        Debug.Log("[SNAPPER ISLAND] Built a low sand-and-grass island without rock objects.");
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
            // Replace the old raised cliff terrain inside Snapper; preserve all
            // other islands and the surrounding edited seabed.
            if(SnapperIslandGeometry.Ellipse(p,Center)<=1f || target>current)
                heights[z,x]=Mathf.Clamp01((target-origin.y)/size.y);
        }

        data.SetHeights(0,0,heights);
        TerrainCollider collider=terrain.GetComponent<TerrainCollider>();
        if(collider!=null)collider.terrainData=data;
    }

    private void PaintTerrain(Terrain terrain)
    {
        // Paint only Snapper in existing layers. NEVER append a layer or reset
        // the shared terrain's splat map while constructing this island.
        IslandTerrainSurfaceRepair.PaintSnapper(terrain,Center);
    }

    public void RestoreSimpleIsland(bool restoreWorldSurfaces)
    {
        if(!NeedsSimpleIslandRepair)return;
        var expansion=IslandExpansionWorld.Active;
        var reef=FindFirstObjectByType<ReefZone>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);
        var water=FindFirstObjectByType<OceanWater>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include);
        Terrain terrain=expansion!=null && expansion.Ready?expansion.Terrain:Terrain.activeTerrain;
        if(terrain==null || reef==null || water==null)
            throw new System.InvalidOperationException("Open the fishing map with terrain, reef and ocean before restoring Snapper.");
        Center=savedLayout?savedCenter:SnapperIslandGeometry.Center(reef);
        if(!InsideTerrain(terrain,Center,SnapperIslandGeometry.RadiusX+SnapperIslandGeometry.CoastalShelfWidth,
            SnapperIslandGeometry.RadiusZ+SnapperIslandGeometry.CoastalShelfWidth))
            throw new System.InvalidOperationException("Snapper lies outside this terrain. The map was not changed.");
        // Keep saved terrain assets intact in Play Mode, and keep the editor's
        // pre-repair scene backup tied to the old data until the new copy is saved.
        TerrainData data=Instantiate(terrain.terrainData);
        data.name="Restored sand grass and stone fishing terrain";
        terrain.terrainData=data;
        var collider=terrain.GetComponent<TerrainCollider>();
        if(collider!=null)collider.terrainData=data;
        SculptTerrain(terrain,water.BaseWaterLevel);
        if(restoreWorldSurfaces)IslandTerrainSurfaceRepair.RestoreWorldPaint(terrain,reef,expansion,water.BaseWaterLevel);
        PaintTerrain(terrain);
        AuthoredTerrainSurfaceTextures.ApplyToTerrain(terrain);
        RemoveRockscape();
        if(savedArrival!=null)
        {
            Vector3 p=savedArrival.position;
            p.y=terrain.SampleHeight(p)+terrain.transform.position.y+.2f;
            savedArrival.position=p;
            Arrival=savedArrival;
        }
        simpleIslandVersion=1;
        Physics.SyncTransforms();
        terrain.Flush();
    }

    private void RemoveRockscape()
    {
        var old=transform.Find("Snapper Coastal Rockscape");
        if(old!=null)
        {
            old.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(old.gameObject);else DestroyImmediate(old.gameObject);
        }
        // Retire the component but keep its script GUID available for older
        // locally saved scenes until they have received this migration.
        foreach(var oldBuilder in GetComponents<SnapperIslandRockscape>())
        {
            if(Application.isPlaying)Destroy(oldBuilder);else DestroyImmediate(oldBuilder);
        }
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
        trigger.transform.position=Center+Vector3.up*(FindFirstObjectByType<OceanWater>(Application.isPlaying?FindObjectsInactive.Exclude:FindObjectsInactive.Include).BaseWaterLevel+12f*SnapperIslandGeometry.DesignScale-Center.y);
        var box=trigger.AddComponent<BoxCollider>();box.isTrigger=true;
        box.size=new Vector3(SnapperIslandGeometry.RadiusX*2,30f*SnapperIslandGeometry.DesignScale,SnapperIslandGeometry.RadiusZ*2);
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

