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
        target.enabled=false; // A RawImage with no texture is a white rectangle.
        string key=(fitWholeFish?"index:":"")+(gear??(species+":"+kg.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        if(cache.TryGetValue(key,out var ready)) {target.texture=ready;target.enabled=ready.IsCreated();if(target.enabled)return;cache.Remove(key);Destroy(ready);}
        requests.Enqueue(()=> {if(target!=null)StartCoroutine(Render(target,key,species,kg,gear,fitWholeFish));else busy=false;});
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
        if(gear==null)model=FishWorldSize.Create("FishPreview",stage.transform,species,kg);
        else
        {
            GameObject prefab=null;
            if(gear=="Rod" || gear=="Reel" || gear=="RodAssembly")
                prefab=Resources.Load<GameObject>("Fishing/FishingRodReel");
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
        if(gear=="Rod" || gear=="Reel")
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                bool reel=false;for(var t=renderer.transform;t!=null && t!=model.transform;t=t.parent)if(t.name=="ReelMount")reel=true;
                renderer.enabled=gear=="Reel"?reel:!reel;
            }
        }
        model.transform.localRotation=gear=="Rod" || gear=="RodAssembly"
            ? Quaternion.Euler(0,-90,40)
            : gear=="Reel"
                ? Quaternion.Euler(12,35,-12)
                : lurePreview
                    ? Quaternion.Euler(0,-90,90)
                    : Quaternion.Euler(0,-90,0);
        var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
        if(renderers.Length==0){Destroy(model);busy=false;yield break;}
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(gear==null && !fitWholeFish)
        {

            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            // Common world-size frame: small fish fit; larger bodies extend off the right edge.
            studio.orthographicSize=0.26f;
            Vector3 head=new Vector3(bounds.min.x+0.18f,bounds.center.y,bounds.center.z);
            studio.transform.position=head+Vector3.back*5;
        }
        else
        {
            studio.orthographicSize=Mathf.Max(bounds.size.x,bounds.size.y)*0.58f;
            studio.transform.position=bounds.center+Vector3.back*(bounds.size.z+5);
        }
        studio.transform.rotation=Quaternion.identity;
        var texture=new RenderTexture(192,192,16){name="Inventory "+key};texture.Create();rendering=texture;
        studio.targetTexture=texture;studio.enabled=true;
        // Let the normal render pipeline draw this camera once (also works with URP).
        yield return new WaitForEndOfFrame();
        studio.enabled=false;studio.targetTexture=null;Destroy(model);model=null;
        cache[key]=texture;rendering=null;if(target!=null){target.texture=texture;target.enabled=true;}
        busy=false;
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
        Suspend();
        foreach(var t in cache.Values){t.Release();Destroy(t);}cache.Clear();
    }
    private void OnDestroy(){Clear();if(stage!=null)Destroy(stage);if(itemMaterial!=null)Destroy(itemMaterial);}
}
