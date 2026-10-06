using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class SnapperIslandRockscape : MonoBehaviour
{
    const string Root="Islands/SnapperIsland/Rocks/";
    readonly GameObject[] rocks=new GameObject[6];
    Terrain terrain; float sea; Vector3 center,arrival; Transform holder; Material material; System.Random rng;

    public bool Build(Terrain t,float water,Vector3 c,Vector3 a)
    {
        terrain=t;sea=water;center=c;arrival=a;rng=new System.Random(77341);
        for(int i=0;i<6;i++)
        {
            rocks[i]=Resources.Load<GameObject>(Root+"Rock"+(i+1));
            if(rocks[i]==null){Debug.LogError("[SNAPPER ROCKS] Missing Rock"+(i+1));return false;}
        }
        material=Resources.Load<Material>(Root+"WorldRock");
        if(material==null){Debug.LogError("[SNAPPER ROCKS] Missing shared WorldRock material.");return false;}
        holder=new GameObject("Snapper Rock Formations").transform;
        holder.SetParent(transform,false);holder.gameObject.isStatic=true;
        Majors();Transitions();Shore();Scatter();Physics.SyncTransforms();return true;
    }

    void P(int asset,float x,float z,float s,float rx,float ry,float rz,float bury,bool collider,bool shadow,float submerged=-1)
    {Place(asset,new Vector2(x,z),s,new Vector3(rx,ry,rz),bury,collider,shadow,submerged);}

    GameObject Place(int asset,Vector2 o,float s,Vector3 e,float bury,bool collider,bool shadow,float submerged)
    {
        Vector3 p=center+new Vector3(o.x,0,o.y);
        GameObject go=Instantiate(rocks[Mathf.Clamp(asset,0,5)],holder);
        go.name="Snapper Rock "+(asset+1);go.transform.SetPositionAndRotation(p,Quaternion.Euler(e));
        go.transform.localScale=new Vector3(s*N(.94f,1.06f),s*N(.96f,1.05f),s*N(.94f,1.06f));
        Renderer[] rs=go.GetComponentsInChildren<Renderer>(true);
        if(rs.Length==0){Destroy(go);return null;}
        Bounds b=rs[0].bounds;
        for(int i=0;i<rs.Length;i++)
        {
            rs[i].sharedMaterial=material;
            rs[i].shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
            rs[i].receiveShadows=true;if(i>0)b.Encapsulate(rs[i].bounds);
        }
        float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
        float bottom=ground-b.size.y*bury;
        if(submerged>=0)bottom=Mathf.Max(bottom,sea-b.size.y*Mathf.Clamp(submerged,.2f,.5f));
        go.transform.position+=Vector3.up*(bottom-b.min.y);
        foreach(Collider c in go.GetComponentsInChildren<Collider>(true))Destroy(c);
        if(collider)SimpleCollider(go);
        Static(go.transform);return go;
    }

    static void SimpleCollider(GameObject go)
    {
        MeshFilter f=go.GetComponentInChildren<MeshFilter>();if(f==null||f.sharedMesh==null)return;
        BoxCollider c=f.gameObject.AddComponent<BoxCollider>();Bounds b=f.sharedMesh.bounds;
        c.center=b.center;c.size=new Vector3(b.size.x*.82f,b.size.y*.88f,b.size.z*.82f);
    }

    static void Static(Transform t){t.gameObject.isStatic=true;for(int i=0;i<t.childCount;i++)Static(t.GetChild(i));}
    bool Clear(Vector2 o,float r){Vector3 p=center+new Vector3(o.x,0,o.y);return Vector3.ProjectOnPlane(p-arrival,Vector3.up).sqrMagnitude<r*r;}
    float N(float min,float max){return Mathf.Lerp(min,max,(float)rng.NextDouble());}
}
