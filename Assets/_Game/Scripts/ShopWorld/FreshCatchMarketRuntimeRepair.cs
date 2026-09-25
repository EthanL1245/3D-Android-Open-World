using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authoritative runtime repair for the Fresh Catch Market.
///
/// The Tideglass scene is generated locally and is not stored in Git, so older
/// generated copies can be missing ShopMarketDisplay entirely. This repair finds
/// the market by its authored name and rebuilds the visible ice/fish display from
/// scratch. It therefore does not depend on which installer version created the
/// user's local TideglassShopWorld.unity scene.
/// </summary>
public sealed class FreshCatchMarketRuntimeRepair : MonoBehaviour
{
    private static FreshCatchMarketRuntimeRepair instance;
    private Material steel,ice,board;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if(instance!=null)return;
        var host=new GameObject("FreshCatchMarketRuntimeRepair");
        instance=host.AddComponent<FreshCatchMarketRuntimeRepair>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if(instance!=null && instance!=this){Destroy(gameObject);return;}
        instance=this;DontDestroyOnLoad(gameObject);
    }

    private void OnEnable(){SceneManager.sceneLoaded+=OnSceneLoaded;}
    private void OnDisable(){SceneManager.sceneLoaded-=OnSceneLoaded;}
    private void Start(){StartCoroutine(RepairLoadedScenes());}
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode){StartCoroutine(RepairLoadedScenes());}

    private IEnumerator RepairLoadedScenes()
    {
        // Additive sceneLoaded fires before every Start() in the loaded scene.
        yield return null;
        yield return null;

        var markets=new List<Transform>();
        for(int s=0;s<SceneManager.sceneCount;s++)
        {
            Scene scene=SceneManager.GetSceneAt(s);
            if(!scene.IsValid() || !scene.isLoaded)continue;
            foreach(GameObject root in scene.GetRootGameObjects())
            {
                Transform[] all=root.GetComponentsInChildren<Transform>(true);
                for(int i=0;i<all.Length;i++)
                    if(all[i]!=null && all[i].name=="FRESH CATCH MARKET")markets.Add(all[i]);
            }
        }

        for(int i=0;i<markets.Count;i++)
            yield return Rebuild(markets[i]);
    }

    private IEnumerator Rebuild(Transform market)
    {
        if(market==null)yield break;

        NormalizeCounter(market);

        // Older generated scenes may or may not contain this component. If it is
        // present, stop it from fighting with this authoritative replacement.
        var legacy=market.GetComponent<ShopMarketDisplay>();
        if(legacy!=null)legacy.enabled=false;

        // Remove every older generated/runtime display under this market only.
        var remove=new List<GameObject>();
        foreach(Transform child in market)
        {
            if(child==null)continue;
            if(child.name=="FreshCatchOnIce" || child.name=="FreshCatchRuntimeDisplay" ||
               child.name=="SeafoodTray" || child.name=="Ice" ||
               child.name=="HangingMarketFish" || child.name=="HangingCord")
                remove.Add(child.gameObject);
        }
        for(int i=0;i<remove.Count;i++)if(remove[i]!=null)Destroy(remove[i]);
        if(remove.Count>0)yield return null;

        EnsureMaterials();

        var display=new GameObject("FreshCatchRuntimeDisplay").transform;
        display.SetParent(market,false);
        display.localPosition=Vector3.zero;
        display.localRotation=Quaternion.identity;
        display.localScale=Vector3.one;

        int[] species={0,FishCatalog.RedSnapperId,3};
        var fish=new List<PendingFish>();

        // Countertop is normalized to y=0.80 with 0.12 m thickness, so its top
        // is y=0.86. Trays begin immediately above it and fish rest on the ice.
        for(int tray=0;tray<species.Length;tray++)
        {
            float x=(tray-1)*3.30f;
            Box(display,"Steel tray",new Vector3(x,.895f,-1.50f),new Vector3(2.95f,.07f,1.55f),steel);
            Box(display,"Ice bed",new Vector3(x,.965f,-1.50f),new Vector3(2.78f,.10f,1.38f),ice);
            BuildCrushedIce(display,x,tray);

            for(int n=0;n<3;n++)
            {
                GameObject f=FishVisualFactory.CreateFish("Market "+FishCatalog.Get(species[tray]).Name,display,species[tray],1f);
                if(f!=null)fish.Add(new PendingFish
                {
                    fish=f,
                    target=new Vector3(x+(n-1)*.86f,1.03f,-1.50f),
                    index=n
                });
            }

            Box(display,"Label board",new Vector3(x,.74f,-2.48f),new Vector3(2.45f,.30f,.055f),board);
            Label(display,FishCatalog.Get(species[tray]).Name.ToUpperInvariant(),new Vector3(x,.74f,-2.515f));
        }

        // Let imported fish Awake/Start/presentation setup complete BEFORE we
        // freeze them. Disabling every behaviour immediately after Instantiate
        // can prevent an imported model from ever making its visual child ready.
        yield return null;
        yield return null;

        for(int i=0;i<fish.Count;i++)FreezeAndPlace(display,fish[i]);
    }

    private static void NormalizeCounter(Transform market)
    {
        Transform[] all=market.GetComponentsInChildren<Transform>(true);
        for(int i=0;i<all.Length;i++)
        {
            Transform node=all[i];if(node==null)continue;
            if(node.name=="Counter")
            {
                Vector3 s=node.localScale;s.y=.75f;node.localScale=s;
                Vector3 p=node.localPosition;p.y=.375f;node.localPosition=p;
            }
            else if(node.name=="Countertop")
            {
                Vector3 p=node.localPosition;p.y=.80f;node.localPosition=p;
            }
            else if(node.name=="CounterInlay")
            {
                Vector3 p=node.localPosition;p.y=.45f;node.localPosition=p;
            }
        }
    }

    private void FreezeAndPlace(Transform parent,PendingFish pending)
    {
        GameObject fish=pending.fish;if(fish==null)return;

        // Size these as counter-display fish, not trophy/aquarium monsters.
        FishWorldSize.SetLength(fish,.50f+pending.index*.04f);
        fish.transform.localRotation=Quaternion.Euler(0f,pending.index%2==0?8f:-8f,90f);

        foreach(var animator in fish.GetComponentsInChildren<Animator>(true))
        {
            if(animator==null)continue;
            animator.applyRootMotion=false;
            animator.Update(0f);
            animator.speed=0f;
        }
        foreach(var animation in fish.GetComponentsInChildren<Animation>(true))
            if(animation!=null)animation.enabled=false;
        foreach(var behaviour in fish.GetComponentsInChildren<MonoBehaviour>(true))
            if(behaviour!=null)behaviour.enabled=false;
        foreach(var collider in fish.GetComponentsInChildren<Collider>(true))
            if(collider!=null)collider.enabled=false;
        foreach(var body in fish.GetComponentsInChildren<Rigidbody>(true))
            if(body!=null){body.isKinematic=true;body.useGravity=false;}

        Renderer[] renderers=fish.GetComponentsInChildren<Renderer>(true);
        bool found=false;Bounds bounds=default;
        for(int i=0;i<renderers.Length;i++)
        {
            Renderer r=renderers[i];
            if(r==null || !r.gameObject.activeInHierarchy)continue;
            r.enabled=true;
            if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
        }
        if(!found)return;

        Vector3 targetWorld=parent.TransformPoint(pending.target);
        fish.transform.position+=targetWorld-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
    }

    private void EnsureMaterials()
    {
        if(steel!=null)return;
        Shader surface=Resources.Load<Shader>("Fishing/ShopSurface");
        if(surface==null)surface=Shader.Find("Universal Render Pipeline/Lit");
        steel=new Material(surface){name="Runtime market steel"};
        ice=new Material(surface){name="Runtime crushed ice"};
        board=new Material(surface){name="Runtime market labels"};
        SetSurface(steel,new Color(.36f,.46f,.49f),.72f,.65f);
        SetSurface(ice,new Color(.82f,.94f,.98f),.78f,0f);
        SetSurface(board,new Color(.025f,.08f,.085f),.20f,0f);
    }

    private static void SetSurface(Material m,Color color,float smooth,float metallic)
    {
        if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
        if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);
        if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);
    }

    private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        GameObject node=GameObject.CreatePrimitive(PrimitiveType.Cube);node.name=name;node.transform.SetParent(parent,false);
        node.transform.localPosition=position;node.transform.localScale=size;node.GetComponent<Renderer>().sharedMaterial=material;
        Collider c=node.GetComponent<Collider>();if(c!=null)Destroy(c);return node;
    }

    private void BuildCrushedIce(Transform parent,float x,int seed)
    {
        var random=new System.Random(401+seed);
        for(int row=0;row<4;row++)for(int col=0;col<11;col++)
        {
            float px=x-1.22f+col*.244f;
            float pz=-2.02f+row*.35f;
            float sy=.045f+(float)random.NextDouble()*.045f;
            GameObject chip=Box(parent,"Crushed ice",new Vector3(px,1.02f+sy*.5f,pz),new Vector3(.16f,sy,.19f),ice);
            chip.transform.localRotation=Quaternion.Euler((float)random.NextDouble()*24f,(float)random.NextDouble()*180f,(float)random.NextDouble()*20f);
        }
    }

    private void Label(Transform parent,string caption,Vector3 position)
    {
        var node=new GameObject("Species label");node.transform.SetParent(parent,false);node.transform.localPosition=position;
        TextMesh text=node.AddComponent<TextMesh>();text.text=caption;text.fontSize=64;text.characterSize=.031f;
        text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color=new Color(.95f,.96f,.89f);
        Shader sign=Resources.Load<Shader>("Fishing/ShopSign");
        if(sign!=null)
        {
            var mat=new Material(sign);mat.mainTexture=text.font.material.mainTexture;node.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
    }

    private void OnDestroy()
    {
        if(instance==this)instance=null;
        if(steel!=null)Destroy(steel);if(ice!=null)Destroy(ice);if(board!=null)Destroy(board);
    }

    private sealed class PendingFish
    {
        public GameObject fish;
        public Vector3 target;
        public int index;
    }
}
