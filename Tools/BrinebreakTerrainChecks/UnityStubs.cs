using System;
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) {} }
    public static class Time { public static int frameCount; }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 up => new Vector3(0,1,0);
        public float magnitude => MathF.Sqrt(x*x+y*y+z*z);
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t) => a+(b-a)*t;
    }
    public struct Quaternion { public static Quaternion identity => new Quaternion(); public static Vector3 operator *(Quaternion q,Vector3 v) => v; }
    public class Transform { public Vector3 position; public bool IsChildOf(Transform t) => this==t; }
    public class TerrainData { public Vector3 size=new Vector3(100,140,100); }
    public class Terrain
    {
        public static Terrain[] activeTerrains=Array.Empty<Terrain>();
        public TerrainData terrainData=new TerrainData();
        public Transform transform=new Transform();
        public bool isActiveAndEnabled=true;
        public Func<Vector3,float> sample = p=>0;
        public float SampleHeight(Vector3 p) => sample(p);
    }
    public static class Mathf
    {
        public static float Max(float a,float b)=>MathF.Max(a,b);
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static int CeilToInt(float x)=>(int)MathF.Ceiling(x);
    }
    public struct Ray { public Vector3 origin,direction; public Ray(Vector3 o,Vector3 d){origin=o;direction=d;} }
    public struct Plane
    {
        float y;
        public Plane(Vector3 normal,Vector3 point){y=point.y;}
        public bool Raycast(Ray ray,out float d){d=(y-ray.origin.y)/ray.direction.y;return d>0;}
    }
    public class Collider { public Transform transform=new Transform(); public T GetComponentInParent<T>() where T:class => null; }
    public class TerrainCollider : Collider {}
    public enum QueryTriggerInteraction { Ignore }
    public static class Physics
    {
        public static Collider[] overlaps=Array.Empty<Collider>();
        public static Collider[] OverlapBox(Vector3 p,Vector3 half,Quaternion q,int mask,QueryTriggerInteraction trigger)=>overlaps;
    }
}
public class OceanWater { public float BaseWaterLevel=2.5f; public float GetSurfaceHeight(UnityEngine.Vector3 p)=>BaseWaterLevel; }
public class BoatData { public string ID="raft"; public UnityEngine.Vector3 HullSize=new UnityEngine.Vector3(2,1,4); public float Draft=.45f; }
public class BoatPassenger { public BoatController Boat; }
public class BoatController { public UnityEngine.Transform transform; }
public static class PondWater { public static bool TrySurface(UnityEngine.Vector3 p,out float height){height=0;return false;} }
