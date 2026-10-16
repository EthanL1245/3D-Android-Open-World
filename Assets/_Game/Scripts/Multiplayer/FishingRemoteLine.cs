using UnityEngine;

// Observer-only geometry. No fishing simulation, collisions, or network requests.
public sealed class FishingRemoteLine : MonoBehaviour
{
    private LineRenderer line;
    private Material lineMaterial;
    private GameObject tackle;
    private Transform tie;
    private Animator animator;
    private Quaternion retrieveFrame=Quaternion.identity;
    private Vector3 target,position;
    private int kind,variant=-1,sequence;
    private float receivedAt;
    private bool reeling,positionReady;

    public void Receive(FishingMultiplayer.Pose pose)
    {
        int next=pose.rodEquipped && !pose.swimming && pose.world==0?pose.lineKind:0;
        receivedAt=Time.realtimeSinceStartup;
        reeling=pose.reeling;
        Vector3 end=new Vector3(pose.lineX,pose.lineY,pose.lineZ);
        if(next!=kind || pose.castSequence!=sequence || !positionReady || (end-position).sqrMagnitude>400f)
        {position=end;positionReady=true;}
        if(next!=kind || (next==1 && variant!=pose.lureVariant))ClearTackle();
        kind=next;variant=pose.lureVariant;sequence=pose.castSequence;target=end;
        if(kind==0)Hide();
    }

    // Called after the avatar pose and rod flex, so the line starts at the bent tip.
    public void Present(Transform tip)
    {
        if(kind==0 || tip==null || Time.realtimeSinceStartup-receivedAt>1f){Hide();return;}
        if(line==null)
        {
            var shader=Resources.Load<Shader>("Fishing/FishingUnlit");
            if(shader==null)return;
            lineMaterial=new Material(shader);
            SetColor(lineMaterial,new Color(.88f,.94f,.96f,.92f));
            var go=new GameObject("Observer Fishing Line");go.transform.SetParent(transform,false);
            line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;
            line.useWorldSpace=true;line.positionCount=2;line.startWidth=.008f;line.endWidth=.004f;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        }
        position=Vector3.Lerp(position,target,1f-Mathf.Exp(-18f*Time.unscaledDeltaTime));
        if(tackle==null && kind==1)CreateTackle();
        if(tackle!=null)
        {
            tackle.SetActive(true);
            if(kind==1)
            {
                Vector3 direction=tip.position-position;direction.y=0f;
                if(direction.sqrMagnitude>.0001f)
                    tackle.transform.rotation=Quaternion.LookRotation(direction,Vector3.up)*
                        Quaternion.AngleAxis(variant==ShopCatalog.MetalSpoonLureVariant?0f:10f,Vector3.forward)*Quaternion.Inverse(retrieveFrame);
                if(animator!=null)animator.speed=reeling?2f:0f;
            }
            tackle.transform.position=position;
            if(tie!=null)tackle.transform.position+=position-tie.position;
        }
        line.enabled=true;line.SetPosition(0,tip.position);line.SetPosition(1,position);
    }

    private void CreateTackle()
    {
        var prefab=Resources.Load<GameObject>(ShopCatalog.LurePrefabResource(variant));
        if(prefab==null)return;
        tackle=Instantiate(prefab,transform,false);tackle.name="Observer Retrieval Lure";
        foreach(var c in tackle.GetComponentsInChildren<Collider>(true))c.enabled=false;
        foreach(var b in tackle.GetComponentsInChildren<Rigidbody>(true)){b.isKinematic=true;b.detectCollisions=false;}
        foreach(var script in tackle.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
        tie=Find(tackle.transform,"LineAttach");
        animator=tackle.GetComponentInChildren<Animator>(true);
        if(animator!=null){animator.applyRootMotion=false;animator.speed=0f;animator.Update(0f);}
        Transform frame=Find(tackle.transform,"RetrieveFrame");
        if(frame!=null)
            retrieveFrame=Quaternion.LookRotation(tackle.transform.InverseTransformDirection(frame.forward),tackle.transform.InverseTransformDirection(frame.up));
        else if(tie!=null && tie.parent!=tackle.transform)
            retrieveFrame=Quaternion.LookRotation(tackle.transform.InverseTransformDirection(tie.parent.TransformVector(Vector3.down)),
                tackle.transform.InverseTransformDirection(tie.parent.TransformVector(Vector3.forward)));
        else retrieveFrame=Quaternion.identity;
    }
    private static Transform Find(Transform root,string name)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
        return null;
    }
    private static void SetColor(Material material,Color color)
    {
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Color"))material.SetColor("_Color",color);
    }
    private void Hide(){if(line!=null)line.enabled=false;if(tackle!=null)tackle.SetActive(false);}
    private void ClearTackle()
    {
        if(tackle!=null){tackle.SetActive(false);Destroy(tackle);}
        tackle=null;tie=null;animator=null;
    }
    private void OnDisable(){Hide();positionReady=false;}
    private void OnDestroy(){ClearTackle();if(lineMaterial!=null)Destroy(lineMaterial);}
}

