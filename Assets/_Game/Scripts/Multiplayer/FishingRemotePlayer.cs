using UnityEngine;

// Lightweight visual avatar: no controller, fishing scripts, collision, or saved data.
public sealed class FishingRemotePlayer : MonoBehaviour
{
    public int World { get; private set; }
    private Vector3 target, previous;
    private Quaternion facing;
    private bool initialized,swimming;
    private float speed,phase,lastPacket;
    private Transform leftArm,rightArm,leftLeg,rightLeg,label;
    private Material material;
    public void Initialize(string displayName,string identity)
    {
        int seed=0;foreach(char c in identity)seed=(seed*31+c)&0x7fffffff;
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)shader=Shader.Find("Standard");
        if(shader!=null){material=new Material(shader);material.color=Color.HSVToRGB((seed%360)/360f,.65f,.85f);}
        Part("Body",PrimitiveType.Capsule,new Vector3(0,1.05f,0),new Vector3(.48f,.4f,.32f));
        Part("Head",PrimitiveType.Sphere,new Vector3(0,1.7f,0),Vector3.one*.35f);
        Part("Nose",PrimitiveType.Cube,new Vector3(0,1.68f,.18f),new Vector3(.1f,.1f,.12f));
        leftArm=Limb("LeftArm",new Vector3(-.34f,1.38f,0),.65f,.14f);
        rightArm=Limb("RightArm",new Vector3(.34f,1.38f,0),.65f,.14f);
        leftLeg=Limb("LeftLeg",new Vector3(-.14f,.7f,0),.7f,.18f);
        rightLeg=Limb("RightLeg",new Vector3(.14f,.7f,0),.7f,.18f);
        var text=new GameObject("Name").AddComponent<TextMesh>();
        label=text.transform;label.SetParent(transform,false);label.localPosition=new Vector3(0,2.05f,0);
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if(text.font!=null)text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
        text.text=displayName;text.anchor=TextAnchor.MiddleCenter;text.characterSize=.045f;text.fontSize=48;text.color=Color.white;text.richText=false;
    }
    private Transform Part(string title,PrimitiveType type,Vector3 position,Vector3 scale)
    {
        var part=GameObject.CreatePrimitive(type);part.name=title;part.transform.SetParent(transform,false);
        part.transform.localPosition=position;part.transform.localScale=scale;
        var collider=part.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}
        if(material!=null)part.GetComponent<Renderer>().sharedMaterial=material;
        return part.transform;
    }
    private Transform Limb(string title,Vector3 position,float length,float width)
    {
        var pivot=new GameObject(title).transform;pivot.SetParent(transform,false);pivot.localPosition=position;
        var mesh=Part(title+"Mesh",PrimitiveType.Capsule,Vector3.zero,new Vector3(width,length*.5f,width));
        mesh.SetParent(pivot,false);mesh.localPosition=new Vector3(0,-length*.5f,0);return pivot;
    }
    public void Receive(FishingMultiplayer.Peer peer)
    {
        World=peer.world;swimming=peer.swimming;
        target=new Vector3(peer.x,peer.y,peer.z);facing=Quaternion.Euler(0,peer.yaw,0);lastPacket=Time.realtimeSinceStartup;
        if(!initialized || (target-transform.position).sqrMagnitude>225f)
        {transform.SetPositionAndRotation(target,facing);previous=target;initialized=true;}
    }
    private void Update()
    {
        float dt=Time.unscaledDeltaTime;
        if(!initialized || dt<=0)return;
        float blend=1-Mathf.Exp(-12*dt);
        transform.position=Vector3.Lerp(transform.position,target,blend);
        transform.rotation=Quaternion.Slerp(transform.rotation,facing,blend);
        float measured=Vector3.ProjectOnPlane(transform.position-previous,Vector3.up).magnitude/dt;previous=transform.position;
        speed=Mathf.Lerp(speed,Time.realtimeSinceStartup-lastPacket>1?0:measured,blend);
        phase+=dt*Mathf.Clamp(speed*2.8f,0,18);
        float swing=Mathf.Sin(phase)*Mathf.Clamp01(speed/2)*35;
        leftArm.localRotation=Quaternion.Euler(swimming?-70+swing:swing,0,0);
        rightArm.localRotation=Quaternion.Euler(swimming?-70-swing:-swing,0,0);
        leftLeg.localRotation=Quaternion.Euler(-swing,0,0);rightLeg.localRotation=Quaternion.Euler(swing,0,0);
        if(label!=null && Camera.main!=null)label.rotation=Camera.main.transform.rotation;
    }
    private void OnDestroy(){if(material!=null)Destroy(material);}
}
