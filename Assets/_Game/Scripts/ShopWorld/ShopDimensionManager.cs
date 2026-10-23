using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class ShopDimensionManager : MonoBehaviour
{
    public const string SceneName="TideglassShopWorld", HomeSceneName="FishingHomeWorld";
    public static ShopDimensionManager Instance { get; private set; }
    public Transform islandArrival;
    public int Destination { get; private set; }
    public bool InShop => Destination==1;
    public bool InHome => Destination==2;
    public bool InDimension => Destination!=0;
    public bool Traveling { get; private set; }
    public ShopWorldEnvironment World { get; private set; }
    public string TravelError { get; private set; }
    private FirstPersonController controller;
    private FishingSystem fishing;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private bool hasReturn;
    private float oldFogDensity;
    private bool oldFog;
    private Color oldFogColor;
    private void Awake() { Instance=this; controller=GetComponent<FirstPersonController>(); fishing=GetComponent<FishingSystem>(); }
    private void OnDestroy() { if(Instance==this) Instance=null; }
    public bool Near(string id)
    {
        if(!InDimension || World==null || Traveling) return false;
        Transform point=World.Point(id);
        return point!=null && Vector3.Distance(transform.position,point.position)<=5.5f;
    }
    public string Nearest()
    {
        if(!InDimension || World==null) return null;
        if(Near("gear")) return "gear"; if(Near("market")) return "market";
        foreach(var h in World.habitats) if(h.gameObject.activeInHierarchy && Near(h.id)) return h.id;
        return null;
    }
    // The total-weight tournament restricts teleport destinations, not movement
    // or access to the Tackle/Market shop dimension. This is the authoritative
    // guard for BOTH the old travel cards and the newer island index.
    public bool TournamentTravelRestricted =>
        GetComponent<FishingTournament>()?.SuncrestTravelRestricted==true;
    public bool TournamentAllowsIsland(int biome) => !TournamentTravelRestricted || biome==0;
    public bool TournamentAllowsDimension(int destination) =>
        !TournamentTravelRestricted || destination==0 || destination==1;
    private int travelIslandBiome;
    public void TravelIsland(bool brinebreak)=>TravelIsland(brinebreak?1:0);
    public void TravelIsland(int biome)
    {
        if(Traveling || !TournamentAllowsIsland(biome) || (biome!=0 && biome!=1 && biome!=3 && biome!=4))return;
        if(!ReefCatalog.Zones[biome].Unlocked)return;
        var expansion=IslandExpansionWorld.Active;
        if(biome!=0 && (expansion==null || !expansion.Ready))return;
        if(biome==4 && !SnapperIslandRuntime.Ready)return;
        Transform point=biome==4?SnapperIslandRuntime.Arrival:biome==3?expansion.PelagicArrival:biome==1?expansion.Arrival:GetComponent<BoatSystem>()?.IslandDock;
        if(point==null && biome==0)point=islandArrival;
        if(point==null)return;
        travelIslandBiome=biome;
        if(Destination!=0){StartCoroutine(TravelRoutine(0));return;}
        fishing.PrepareForWorldTravel();Teleport(point.position,point.rotation,biome==4,biome==3);
        var ui=FindFirstObjectByType<ShopWorldHUD>();if(ui!=null)ui.Close();
        travelIslandBiome=0;
    }
    public void Travel(bool shop) => Travel(shop?1:0);
    public void Travel(int destination)
    {
        if(Traveling || !TournamentAllowsDimension(destination) || destination==Destination || destination<0 || destination>2)return;
        travelIslandBiome=0;StartCoroutine(TravelRoutine(destination));
    }
    private IEnumerator TravelRoutine(int destination)
    {
        TravelError=null;
        string next=destination==1?SceneName:HomeSceneName;
        if(destination!=0 && !Application.CanStreamedLevelBeLoaded(next))
        { TravelError="Run Install Shop World (One Click) to create the updated worlds."; yield break; }
        Traveling=true; controller.SetUIBlocked(true); fishing.PrepareForWorldTravel();
        int previous=Destination;
        ShopWorldEnvironment target=null;
        if(destination!=0)
        {
            AsyncOperation operation=null;
            try { operation=SceneManager.LoadSceneAsync(next,LoadSceneMode.Additive); }
            catch(System.Exception ex) { TravelError=ex.Message; }
            if(operation==null) { Traveling=false; controller.SetUIBlocked(false); yield break; }
            yield return operation;
            var scene=SceneManager.GetSceneByName(next);
            foreach(var root in scene.GetRootGameObjects())
            { target=root.GetComponentInChildren<ShopWorldEnvironment>(); if(target!=null)break; }
            if(target==null || target.spawn==null)
            { TravelError="Arrival point missing. Reinstall Shop World."; yield return SceneManager.UnloadSceneAsync(next); Traveling=false; controller.SetUIBlocked(false); yield break; }
        }
        // An async Home load may have begun before the tournament countdown.
        // If the travel lock activated during loading, unload that destination
        // before moving the player, leaving them available for the forced
        // Suncrest arrival on the next multiplayer tournament update.
        if(destination!=0 && !TournamentAllowsDimension(destination))
        {
            yield return SceneManager.UnloadSceneAsync(next);
            Traveling=false;
            controller.SetUIBlocked(false);
            var interruptedMenu=FindFirstObjectByType<ShopWorldHUD>();
            if(interruptedMenu!=null && ShopWorldHUD.MenuOpen)interruptedMenu.Close();
            yield break;
        }
        if(previous==0)
        {
            returnPosition=transform.position; returnRotation=transform.rotation; hasReturn=true;
            oldFog=RenderSettings.fog; oldFogColor=RenderSettings.fogColor; oldFogDensity=RenderSettings.fogDensity;
        }
        Destination=destination; World=target;
        if(destination==0)
        {
            var boats=GetComponent<BoatSystem>();
            Transform dock=boats!=null?boats.IslandDock:null;
            var expansion=IslandExpansionWorld.Active;
            if(expansion!=null && expansion.Ready)
            {
                if(travelIslandBiome==1 && ReefCatalog.BrinebreakDiscovered)dock=expansion.Arrival;
                if(travelIslandBiome==4 && ReefCatalog.SnapperDiscovered && SnapperIslandRuntime.Ready)dock=SnapperIslandRuntime.Arrival;
                if(travelIslandBiome==3 && ReefCatalog.BluewaterDiscovered)dock=expansion.PelagicArrival;
            }
            bool returningToSnapper=travelIslandBiome==4 && dock==SnapperIslandRuntime.Arrival &&
                SnapperIslandRuntime.Ready;
            bool returningToBluewater=travelIslandBiome==3 && expansion!=null &&
                dock==expansion.PelagicArrival;
            travelIslandBiome=0;
            Teleport(dock!=null?dock.position:hasReturn?returnPosition:islandArrival.position,
                dock!=null?dock.rotation:hasReturn?returnRotation:islandArrival.rotation,
                returningToSnapper,returningToBluewater);
            RenderSettings.fog=oldFog; RenderSettings.fogColor=oldFogColor; RenderSettings.fogDensity=oldFogDensity;
        }
        else Teleport(target.spawn.position,target.spawn.rotation);
        if(previous!=0)yield return SceneManager.UnloadSceneAsync(previous==1?SceneName:HomeSceneName);
        fishing.SetShopWorld(InDimension); Traveling=false; controller.SetUIBlocked(false);
        var ui=FindFirstObjectByType<ShopWorldHUD>();
        if(ui!=null)ui.Close(); else controller.SetUIBlocked(false);
    }
    public void Visit(string id)
    {
        if(World==null || Traveling) return;
        Transform p=World.Point(id); if(p==null) return;
        Teleport(p.position,p.rotation);
    }
    public void EnterHabitat(string id)
    {
        if(!InHome || !Near(id)) return;
        var h=World.Habitat(id); var progress=GetComponent<ShopProgress>();
        if(h==null || progress.Data.Habitat(id)==null || !ShopCatalog.Habitat(id).swimmable) return;
        Teleport(h.entrance.position,h.entrance.rotation);
    }
    public void ExitWater()
    {
        if(!InDimension || World==null) return;
        ShopHabitat nearest=null; float distance=float.MaxValue;
        foreach(var h in World.habitats)
        { float d=(h.transform.position-transform.position).sqrMagnitude; if(d<distance) { distance=d; nearest=h; } }
        if(nearest!=null) Teleport(nearest.exit.position,nearest.exit.rotation);
    }
    // Keep the authored SnapperArrival marker and every placed rock untouched.
    // On teleport only, move 1 m east in world space and find terrain under the
    // PLAYER rather than using the marker's stale Y after terrain sculpting.
    private bool TrySnapperArrival(Vector3 marker,CharacterController capsule,out Vector3 arrival)
    {
        arrival=marker;
        var expansion=IslandExpansionWorld.Active;
        Terrain terrain=expansion!=null && expansion.Terrain!=null
            ? expansion.Terrain : Terrain.activeTerrain;
        if(terrain==null || terrain.terrainData==null)
        {
            Debug.LogWarning("[SNAPPER TRAVEL] Cannot find terrain for safe arrival.");
            return false;
        }

        // Search the desired +1m east landing first. If rocks now block it,
        // try small offsets nearby without ever moving the marker or scenery.
        float[] east={1f,2f,3f,0f,4f,-1f,-2f};
        float[] north={0f,1f,-1f,2f,-2f,3f,-3f};
        Vector3 scale=transform.lossyScale;
        float radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
        float halfHeight=Mathf.Max(capsule.height*Mathf.Abs(scale.y)*.5f,radius);
        float centerOffset=capsule.center.y*scale.y;
        float feetOffset=centerOffset-halfHeight;
        float cylinderHalf=Mathf.Max(0f,halfHeight-radius);
        const float footClearance=.55f;

        Physics.SyncTransforms();
        for(int z=0;z<north.Length;z++)
        for(int x=0;x<east.Length;x++)
        {
            Vector3 proposed=marker+new Vector3(east[x],0f,north[z]);
            // Avoid spilling onto the shoreline if Snapper's layout changes.
            if(SnapperIslandGeometry.Ellipse(proposed,SnapperIslandRuntime.Center)>.92f)
                continue;

            float terrainY=terrain.SampleHeight(proposed)+terrain.transform.position.y;
            if(float.IsNaN(terrainY) || float.IsInfinity(terrainY))continue;
            proposed.y=terrainY+footClearance-feetOffset;

            Vector3 capsuleCenter=proposed+Vector3.up*centerOffset;
            Vector3 bottom=capsuleCenter-Vector3.up*cylinderHalf;
            Vector3 top=capsuleCenter+Vector3.up*cylinderHalf;
            // Include rocks and TerrainCollider, ignore all triggers (water,
            // island discovery etc). The player's controller is disabled here,
            // so its own capsule cannot block the query.
            if(Physics.CheckCapsule(bottom,top,Mathf.Max(.05f,radius-.01f),
                ~0,QueryTriggerInteraction.Ignore))continue;
            arrival=proposed;
            return true;
        }

        // If there is no clear terrain-level location, prefer arriving over
        // the geometry to spawning inside a solid mesh. The player can fall
        // normally onto whatever surface is underneath.
        Vector3 raised=marker+Vector3.right;
        raised.y=terrain.SampleHeight(raised)+terrain.transform.position.y+footClearance-feetOffset;
        float[] elevations={2f,4f,8f,16f,32f,64f};
        for(int i=0;i<elevations.Length;i++)
        {
            Vector3 proposed=raised+Vector3.up*elevations[i];
            Vector3 capsuleCenter=proposed+Vector3.up*centerOffset;
            Vector3 bottom=capsuleCenter-Vector3.up*cylinderHalf;
            Vector3 top=capsuleCenter+Vector3.up*cylinderHalf;
            if(Physics.CheckCapsule(bottom,top,Mathf.Max(.05f,radius-.01f),
                ~0,QueryTriggerInteraction.Ignore))continue;
            arrival=proposed;
            Debug.LogWarning("[SNAPPER TRAVEL] Nearby landing blocked by scenery; using clear elevated arrival.");
            return true;
        }
        Debug.LogError("[SNAPPER TRAVEL] No safe arrival found. Teleport aborted.");
        return false;
    }

    private void Teleport(Vector3 position,Quaternion rotation,bool snapperArrival=false,bool bluewaterArrival=false)
    {
        var capsule=GetComponent<CharacterController>();
        // Disable first so the overlap query cannot mistake our own collider
        // for a rock. Preserve the player's old location on an aborted travel.
        GetComponent<BoatSystem>()?.BeforeTeleport();
        bool wasEnabled=capsule!=null && capsule.enabled;
        if(capsule!=null)capsule.enabled=false;
        try
        {
            if(snapperArrival && capsule!=null &&
                !TrySnapperArrival(position,capsule,out position))
                return;
            // Bluewater is now a single imported rock, not the old terrain island.
            // Resolve a safe near-water NW rock surface each loaded world session.
            if(bluewaterArrival && (capsule==null ||
                !BluewaterCayRockCollision.TryGetArrival(capsule,out position,out rotation)))
                return;
            transform.SetPositionAndRotation(position,rotation);
            controller.ResetMotion(); controller.ResetViewPitch();
            Physics.SyncTransforms();
        }
        finally
        {
            if(capsule!=null)capsule.enabled=wasEnabled;
        }
    }
    private void LateUpdate()
    {
        if(InDimension && !Traveling && World!=null && transform.position.y<World.transform.position.y-8f)
            Teleport(World.spawn.position,World.spawn.rotation);
    }
}




