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
    public void Travel(bool shop) => Travel(shop?1:0);
    public void Travel(int destination)
    {
        if(Traveling || destination==Destination || destination<0 || destination>2)return;
        StartCoroutine(TravelRoutine(destination));
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
        if(previous==0)
        {
            returnPosition=transform.position; returnRotation=transform.rotation; hasReturn=true;
            oldFog=RenderSettings.fog; oldFogColor=RenderSettings.fogColor; oldFogDensity=RenderSettings.fogDensity;
        }
        Destination=destination; World=target;
        if(destination==0)
        {
            Teleport(hasReturn?returnPosition:islandArrival.position,hasReturn?returnRotation:islandArrival.rotation);
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
    private void Teleport(Vector3 position,Quaternion rotation)
    {
        var capsule=GetComponent<CharacterController>();
        capsule.enabled=false; transform.SetPositionAndRotation(position,rotation); capsule.enabled=true;
        controller.ResetMotion(); controller.ResetViewPitch(); Physics.SyncTransforms();
    }
    private void LateUpdate()
    {
        if(InDimension && !Traveling && World!=null && transform.position.y<World.transform.position.y-8f)
            Teleport(World.spawn.position,World.spawn.rotation);
    }
}
