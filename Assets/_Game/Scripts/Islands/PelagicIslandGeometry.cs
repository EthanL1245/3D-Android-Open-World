using UnityEngine;

public static class PelagicIslandGeometry
{
    public const int BiomeId=3; // Keep existing Deep Ocean ID 2 stable.
    public const string Id="bluewater-cay";
    public const string Name="Bluewater Cay";
    public const float Radius=58f;
    public const float FishingMargin=75f;
    public const float ShoreGap=200f;
    public static Vector3 Center(Vector3 brinebreak,Vector2 radii)
    {
        // Southwest of Brinebreak, southeast of Suncrest. +Z is map north.
        const float dx=-.55f,dz=-.83516465f;
        float coast=1f/Mathf.Sqrt(dx*dx/(radii.x*radii.x)+dz*dz/(radii.y*radii.y));
        float distance=coast+ShoreGap+Radius;
        return brinebreak+new Vector3(dx*distance,0,dz*distance);
    }
    public static float DistanceFromShore(Vector3 p,Vector3 center)
    {float x=p.x-center.x,z=p.z-center.z;return Mathf.Max(0,Mathf.Sqrt(x*x+z*z)-Radius);}
    public static bool Contains(Vector3 p,Vector3 center)=>DistanceFromShore(p,center)<=FishingMargin;
    public static float Height(Vector3 p,Vector3 center,float sea,float seabed)
    {
        float x=p.x-center.x,z=p.z-center.z;
        float distance=Mathf.Sqrt(x*x+z*z);
        if(distance<=Radius)
        {
            float inland=Mathf.SmoothStep(0,1,Mathf.InverseLerp(Radius,Radius*.48f,distance));
            float hills=4f+5f*Mathf.PerlinNoise(p.x*.035f+47,p.z*.035f+19);
            return Mathf.Max(seabed,sea+.06f+inland*hills);
        }
        // A short coastal shelf merges back into the existing cliff/valley floor.
        float blend=Mathf.SmoothStep(0,1,(distance-Radius)/90f);
        return Mathf.Max(seabed,Mathf.Lerp(sea,seabed,blend));
    }
}
