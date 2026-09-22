using UnityEngine;

// A finite water surface. Shares the ocean shader and wave equations, never follows the player.
[ExecuteAlways, RequireComponent(typeof(MeshRenderer))]
public sealed class PondWater : MonoBehaviour
{
    public static PondWater Active { get; private set; }
    public Vector2 radii = new Vector2(10,7.2f);
    private MaterialPropertyBlock properties;
    private MeshRenderer surface;
    private static readonly float[] Amplitudes = {.025f,.013f,.007f};
    private static readonly float[] Lengths = {3.5f,2f,1.2f};
    private static readonly float[] Speeds = {.75f,1.05f,1.4f};
    private static readonly Vector2[] Directions = {new Vector2(1,.35f),new Vector2(-.4f,1),new Vector2(.8f,-.65f)};
    private void OnEnable(){Active=this;surface=GetComponent<MeshRenderer>();Apply();}
    private void OnDisable(){if(Active==this)Active=null;}
    public bool Contains(Vector3 world)
    {
        Vector3 d=world-transform.position;
        return d.x*d.x/(radii.x*radii.x)+d.z*d.z/(radii.y*radii.y)<=1f;
    }
    public float Height(Vector3 world)
    {
        float h=transform.position.y;
        for(int i=0;i<3;i++)h+=Mathf.Sin(Vector2.Dot(new Vector2(world.x,world.z),Directions[i].normalized)*Mathf.PI*2/Lengths[i]+Time.time*Speeds[i])*Amplitudes[i];
        return h;
    }
    public static bool TrySurface(Vector3 world,out float height)
    {
        height=0;if(Active==null || !Active.Contains(world))return false;
        height=Active.Height(world);return true;
    }
    public bool Raycast(Ray ray,float maxDistance,out Vector3 point)
    {
        point=Vector3.zero;
        if(!new Plane(Vector3.up,transform.position).Raycast(ray,out float distance) || distance>maxDistance)return false;
        point=ray.GetPoint(distance);if(!Contains(point))return false;
        point.y=Height(point)+.06f;return true;
    }
    private void Update(){Apply();}
    private void Apply()
    {
        if(surface==null)return;
        if(properties==null)properties=new MaterialPropertyBlock();
        properties.SetFloat("_OceanTime",Time.time);
        for(int i=0;i<3;i++)
        {
            int n=i+1;Vector2 d=Directions[i].normalized;
            properties.SetFloat("_WaveAmplitude"+n,Amplitudes[i]);
            properties.SetFloat("_WaveLength"+n,Lengths[i]);
            properties.SetFloat("_WaveSpeed"+n,Speeds[i]);
            properties.SetVector("_WaveDirection"+n,new Vector4(d.x,d.y,0,0));
        }
        surface.SetPropertyBlock(properties);
    }
}
