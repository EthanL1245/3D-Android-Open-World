using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// One reusable off-screen studio. Static snapshots avoid one camera per inventory row.
public sealed class ShopPreview : MonoBehaviour
{
    private readonly Dictionary<string,RenderTexture> cache=new Dictionary<string,RenderTexture>();
    private readonly Queue<System.Action> requests=new Queue<System.Action>();
    private static int studioCount;
    private Camera studio;
    private GameObject stage, model;
    private bool busy;
    private Material itemMaterial;
    private RenderTexture rendering;
    public void Attach(RawImage target,int species,float kg,string gear=null,bool fitWholeFish=false)
    {
        target.enabled=false;
        string resolvedGear=ResolveGearKey(target,gear);
        string key=(fitWholeFish?"index:":"")+(resolvedGear??(species+":"+kg.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        if(cache.TryGetValue(key,out var ready)) {target.texture=ready;target.enabled=ready.IsCreated();if(target.enabled)return;cache.Remove(key);Destroy(ready);}
        requests.Enqueue(()=> {if(target!=null)StartCoroutine(Render(target,key,species,kg,resolvedGear,fitWholeFish));else busy=false;});
    }
    private void Update() { if(!busy && requests.Count>0) {busy=true;requests.Dequeue()();} }
    private IEnumerator Render(RawImage target,string key,int species,float kg,string gear,bool fitWholeFish)
    {
        if(cache.TryGetValue(key,out var existing)) {target.texture=existing;target.enabled=true;busy=false;yield break;}
        if(stage==null)
        {
            stage=new GameObject("InventoryPreviewStudio");stage.transform.position=new Vector3(10000+100*studioCount++,10000,10000);
            studio=new GameObject("PreviewCamera").AddComponent<Camera>();studio.transform.SetParent(stage.transform,false);
            studio.clearFlags=CameraClearFlags.SolidColor;studio.backgroundColor=new Color(0.08f,0.18f,0.20f);
            studio.orthographic=true;studio.nearClipPlane=0.01f;studio.farClipPlane=15;studio.cullingMask=1<<30;studio.enabled=false;
        }
        studio.backgroundColor=gear=="RodAssembly"?new Color(0,0,0,0):new Color(0.08f,0.18f,0.20f);
        bool lurePreview=gear!=null && gear.StartsWith("Lure",System.StringComparison.Ordinal);
        bool reelPreview=gear!=null && gear.StartsWith("Reel",System.StringComparison.Ordinal);
        if(gear==null)model=FishWorldSize.Create("FishPreview",stage.transform,species,kg);
        else
        {
            GameObject prefab=null;
            if(gear=="Rod" || gear=="RodAssembly")
                prefab=Resources.Load<GameObject>("Fishing/FishingRodReel");
            else if(reelPreview)
            {
                int tier=0;
                if(gear.Length>4)int.TryParse(gear.Substring(4),out tier);
                prefab=Resources.Load<GameObject>(ShopCatalog.ReelPrefabResource(tier));
                if(prefab==null && tier>0)prefab=Resources.Load<GameObject>(ShopCatalog.ReelPrefabResource(0));
            }
            else if(lurePreview)
            {
                int variant=0;
                if(gear.Length>4)int.TryParse(gear.Substring(4),out variant);
                prefab=Resources.Load<GameObject>(ShopCatalog.LurePrefabResource(variant));
            }
            if(prefab!=null)model=Instantiate(prefab,stage.transform);
            else model=BuildItem(gear);
            model.transform.SetParent(stage.transform,false);
        }
        foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
        foreach(var collider in model.GetComponentsInChildren<Collider>())collider.enabled=false;
        foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
        if(gear=="Rod" || reelPreview)
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                bool reel=false;for(var t=renderer.transform;t!=null && t!=model.transform;t=t.parent)if(t.name=="ReelMount")reel=true;
                renderer.enabled=reelPreview?reel:!reel;
            }
        }

        if(lurePreview)
        {
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion=false;animator.speed=0f;animator.Update(0f);animator.enabled=false;
            }
            OrientLureSideProfile(model);
        }
        else if(reelPreview)
        {
            // The user-provided Blender reference is authoritative for reel icons:
            // reel mount/foot above the spool, spool low-left/center, handle to the right.
            OrientReelLikeReference(model);
        }
        else
        {
            model.transform.localRotation=gear=="Rod" || gear=="RodAssembly"?Quaternion.Euler(0,-90,40):Quaternion.Euler(0,-90,0);
        }

        var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        if(renderers.Length==0){Destroy(model);busy=false;yield break;}
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(gear==null && !fitWholeFish)
        {
            studio.orthographicSize=0.26f;
            Vector3 head=new Vector3(bounds.min.x+0.18f,bounds.center.y,bounds.center.z);
            studio.transform.position=head+Vector3.back*5;
        }
        else
        {
            float margin=lurePreview?0.66f:reelPreview?0.72f:0.58f;
            studio.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.y)*margin;
            studio.transform.position=bounds.center+Vector3.back*(bounds.size.z+5);
        }
        studio.transform.rotation=Quaternion.identity;
        var texture=new RenderTexture(192,192,16){name="Inventory "+key};texture.Create();rendering=texture;
        studio.targetTexture=texture;studio.enabled=true;
        yield return new WaitForEndOfFrame();
        studio.enabled=false;studio.targetTexture=null;Destroy(model);model=null;
        cache[key]=texture;rendering=null;if(target!=null){target.texture=texture;target.enabled=true;}
        busy=false;
    }

    private static string ResolveGearKey(RawImage target,string gear)
    {
        if(gear!="Reel" || target==null)return gear;
        Transform row=target.transform;
        for(int i=0;i<8 && row!=null;i++,row=row.parent)
        {
            Text[] labels=row.GetComponentsInChildren<Text>(true);
            if(labels.Any(t=>t!=null && t.text!=null && t.text.IndexOf("Level 2 Fishing Reel",System.StringComparison.OrdinalIgnoreCase)>=0))return "Reel1";
            if(row.GetComponent<LayoutElement>()!=null)break;
        }
        return "Reel0";
    }

    private static void OrientReelLikeReference(GameObject reel)
    {
        Transform mount=FindDeepChild(reel.transform,"ReelMount");
        Transform spool=FindDeepChild(reel.transform,"Spool");
        Transform handle=FindDeepChild(reel.transform,"Handle");
        Renderer spoolRenderer=spool!=null?spool.GetComponent<Renderer>():null;
        Renderer handleRenderer=handle!=null?handle.GetComponent<Renderer>():null;
        if(mount==null || spoolRenderer==null || handleRenderer==null)
        {reel.transform.localRotation=Quaternion.Euler(8,35,0);return;}

        Vector3 centre=spoolRenderer.bounds.center;
        Vector3 sourceUp=mount.position-centre;
        if(sourceUp.sqrMagnitude<0.000001f){reel.transform.localRotation=Quaternion.Euler(8,35,0);return;}
        sourceUp.Normalize();
        Vector3 sourceRight=Vector3.ProjectOnPlane(handleRenderer.bounds.center-centre,sourceUp);
        if(sourceRight.sqrMagnitude<0.000001f){reel.transform.localRotation=Quaternion.Euler(8,35,0);return;}
        sourceRight.Normalize();
        Vector3 sourceForward=Vector3.Cross(sourceRight,sourceUp).normalized;
        Quaternion sourceBasis=Quaternion.LookRotation(sourceForward,sourceUp);
        Quaternion targetBasis=Quaternion.LookRotation(Vector3.forward,Vector3.up);
        reel.transform.rotation=(targetBasis*Quaternion.Inverse(sourceBasis))*reel.transform.rotation;
    }

    private static void OrientLureSideProfile(GameObject lure)
    {
        if(lure==null)return;
        Transform lineAttach=FindDeepChild(lure.transform,"LineAttach");
        Renderer[] renderers=lure.GetComponentsInChildren<Renderer>(true)
            .Where(r=>r!=null && r.enabled && !IsUnderLightingRig(r.transform,lure.transform)).ToArray();
        if(lineAttach==null || renderers.Length<2)return;

        Renderer body=renderers[0];float bodyScore=-1f;
        for(int i=0;i<renderers.Length;i++)
        {
            Vector3 size=renderers[i].bounds.size;
            float score=Mathf.Max(size.x,Mathf.Max(size.y,size.z))*Mathf.Max(0.000001f,size.x*size.y*size.z);
            if(score>bodyScore){bodyScore=score;body=renderers[i];}
        }

        Vector3 centre=body.bounds.center;Vector3 head=lineAttach.position-centre;
        if(head.sqrMagnitude<0.000001f)return;head.Normalize();
        Renderer bellyHook=null;Vector3 belly=Vector3.zero;float bestPerpendicular=-1f;
        for(int i=0;i<renderers.Length;i++)
        {
            Renderer candidate=renderers[i];if(candidate==body)continue;
            Vector3 fromBody=candidate.bounds.center-centre;
            Vector3 perpendicular=fromBody-Vector3.Dot(fromBody,head)*head;
            float score=perpendicular.sqrMagnitude;
            if(score>bestPerpendicular){bestPerpendicular=score;bellyHook=candidate;belly=perpendicular;}
        }
        if(bellyHook==null || belly.sqrMagnitude<0.000001f)return;belly.Normalize();
        Quaternion sourceBasis=Quaternion.LookRotation(head,-belly);
        Quaternion targetBasis=Quaternion.LookRotation(Vector3.left,Vector3.up);
        lure.transform.rotation=(targetBasis*Quaternion.Inverse(sourceBasis))*lure.transform.rotation;
    }

    private static bool IsUnderLightingRig(Transform transform,Transform root)
    {
        for(Transform current=transform;current!=null && current!=root;current=current.parent)
            if(current.name=="AuthoredLightingRig")return true;
        return false;
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        Transform[] all=root.GetComponentsInChildren<Transform>(true);
        for(int i=0;i<all.Length;i++)if(all[i]!=null && all[i].name==name)return all[i];
        return null;
    }

    private GameObject BuildItem(string kind)
    {
        var root=new GameObject(kind);root.transform.SetParent(stage.transform,false);
        if(itemMaterial==null)itemMaterial=new Material(Resources.Load<Shader>("Fishing/ShopSurface"));
        System.Action<PrimitiveType,Vector3,Vector3,Color> part=(type,pos,size,color)=>
        {
            var go=GameObject.CreatePrimitive(type);go.transform.SetParent(root.transform,false);go.transform.localPosition=pos;go.transform.localScale=size;
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=itemMaterial;
            var props=new MaterialPropertyBlock();props.SetColor("_BaseColor",color);renderer.SetPropertyBlock(props);
        };
        if(kind=="Bait4")
        {
            part(PrimitiveType.Capsule,Vector3.zero,new Vector3(.2f,.42f,.16f),new Color(.82f,.89f,.94f));
            part(PrimitiveType.Sphere,new Vector3(0,.28f,-.04f),new Vector3(.19f,.2f,.17f),new Color(.85f,.15f,.08f));
            part(PrimitiveType.Cylinder,new Vector3(0,-.5f,0),new Vector3(.025f,.12f,.025f),Color.gray);
            for(int i=-1;i<=1;i++)part(PrimitiveType.Sphere,new Vector3(i*.065f,-.59f,0),Vector3.one*.07f,Color.gray);
        }
        else if(kind=="Line")
        {
            part(PrimitiveType.Cylinder,Vector3.zero,new Vector3(0.65f,0.2f,0.65f),new Color(0.65f,0.83f,0.86f));
            foreach(int side in new[]{-1,1})part(PrimitiveType.Cylinder,Vector3.up*side*0.21f,new Vector3(0.85f,0.025f,0.85f),Color.gray);
        }
        else if(kind=="Bait3")
        {
            part(PrimitiveType.Capsule,Vector3.up*0.2f,new Vector3(0.24f,0.25f,0.24f),new Color(0.8f,0.6f,0.65f));
            for(int i=0;i<6;i++)part(PrimitiveType.Capsule,new Vector3((i-2.5f)*0.055f,-0.22f,0),new Vector3(0.035f,0.18f,0.035f),new Color(0.9f,0.7f,0.75f));
        }
        else if(kind=="Bait0" || kind=="Bait1" || kind=="Bait2")
        {
            for(int i=0;i<8;i++)part(PrimitiveType.Sphere,new Vector3(Mathf.Sin(i*0.4f)*0.23f,(i-4)*0.075f,0),Vector3.one*(kind!="Bait2"?0.10f:0.14f),kind!="Bait2"?new Color(0.5f,0.21f,0.16f):new Color(1,0.55f,0.4f));
        }
        else part(PrimitiveType.Capsule,Vector3.zero,new Vector3(0.2f,0.35f,0.2f),new Color(0.9f,0.65f,0.15f));
        root.transform.localRotation=Quaternion.Euler(20,0,20);return root;
    }
    public void Suspend()
    {
        StopAllCoroutines(); requests.Clear();busy=false;
        if(rendering!=null){rendering.Release();Destroy(rendering);rendering=null;}
        if(studio!=null){studio.enabled=false;studio.targetTexture=null;}
        if(model!=null)Destroy(model);
        if(cache.Count>96){foreach(var t in cache.Values){t.Release();Destroy(t);}cache.Clear();}
    }
    public void Clear()
    {
        Suspend();foreach(var t in cache.Values){t.Release();Destroy(t);}cache.Clear();
    }
    private void OnDestroy(){Clear();if(stage!=null)Destroy(stage);if(itemMaterial!=null)Destroy(itemMaterial);}
}
