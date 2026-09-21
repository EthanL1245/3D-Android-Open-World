using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class ShopDimensionManager : MonoBehaviour
{
    public const string SceneName="TideglassShopWorld";
    public static ShopDimensionManager Instance { get; private set; }
    public Transform islandArrival;
    public bool InShop { get; private set; }
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
        if(!InShop || World==null || Traveling) return false;
        Transform point=World.Point(id);
        return point!=null && Vector3.Distance(transform.position,point.position)<=5.5f;
    }
    public string Nearest()
    {
        if(!InShop || World==null) return null;
        if(Near("gear")) return "gear"; if(Near("market")) return "market";
        foreach(var h in World.habitats) if(Near(h.id)) return h.id;
        return null;
    }
    public void Travel(bool shop)
    {
        if(Traveling || shop==InShop) return;
        StartCoroutine(TravelRoutine(shop));
    }
    private IEnumerator TravelRoutine(bool shop)
    {
        TravelError=null;
        if(shop && !Application.CanStreamedLevelBeLoaded(SceneName))
        { TravelError="Shop scene is missing from the build. Run Install Shop World (One Click)."; yield break; }
        Traveling=true;
        controller.SetUIBlocked(true);
        fishing.PrepareForWorldTravel();
        if(shop)
        {
            returnPosition=transform.position; returnRotation=transform.rotation; hasReturn=true;
            AsyncOperation operation=null;
            try { operation=SceneManager.LoadSceneAsync(SceneName,LoadSceneMode.Additive); }
            catch(System.Exception ex) { TravelError=ex.Message; }
            if(operation==null) { Traveling=false; yield break; }
            yield return operation;
            World=FindFirstObjectByType<ShopWorldEnvironment>();
            if(World==null || World.spawn==null)
            {
                TravelError="The shop scene has no arrival point. Reinstall Shop World.";
                yield return SceneManager.UnloadSceneAsync(SceneName);
                Traveling=false; yield break;
            }
            oldFog=RenderSettings.fog; oldFogColor=RenderSettings.fogColor; oldFogDensity=RenderSettings.fogDensity;
            InShop=true;
            Teleport(World.spawn.position,World.spawn.rotation);
        }
        else
        {
            Teleport(hasReturn ? returnPosition : islandArrival.position,hasReturn ? returnRotation : islandArrival.rotation);
            InShop=false;
            RenderSettings.fog=oldFog; RenderSettings.fogColor=oldFogColor; RenderSettings.fogDensity=oldFogDensity;
            World=null;
            yield return SceneManager.UnloadSceneAsync(SceneName);
        }
        fishing.SetShopWorld(InShop);
        Traveling=false;
        var ui=FindFirstObjectByType<ShopWorldHUD>();
        if(ui!=null) ui.Close(); else controller.SetUIBlocked(false);
    }
    public void Visit(string id)
    {
        if(World==null || Traveling) return;
        Transform p=World.Point(id); if(p==null) return;
        Teleport(p.position,p.rotation);
    }
    public void EnterHabitat(string id)
    {
        if(!Near(id)) return;
        var h=World.Habitat(id); var progress=GetComponent<ShopProgress>();
        if(h==null || progress.Data.Habitat(id)==null || !ShopCatalog.Habitat(id).swimmable) return;
        Teleport(h.entrance.position,h.entrance.rotation);
    }
    public void ExitWater()
    {
        if(!InShop || World==null) return;
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
        if(InShop && !Traveling && World!=null && transform.position.y<World.transform.position.y-8f)
            Teleport(World.spawn.position,World.spawn.rotation);
    }
}
