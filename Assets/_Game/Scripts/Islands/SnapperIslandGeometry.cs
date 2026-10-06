using UnityEngine;

/// <summary>
/// Geometry contract for the dedicated snapper island south of Suncrest Reef.
/// The 200 m spacing is measured coast-to-coast along due south, while snapper
/// fishing water extends exactly 50 m beyond this island's shoreline.
/// </summary>
public static class SnapperIslandGeometry
{
    public const string Id="snapper-island";
    public const string Name="Snapper Island";
    public const float RadiusX=55f;
    public const float RadiusZ=42f;
    public const float ShoreGap=200f;
    public const float FishingMargin=50f;
    public const float CoastalShelfWidth=72f;

    public static Vector3 Center(ReefZone reef)
    {
        if(reef==null)return Vector3.zero;
        // +Z is map north, so due south is -Z. Coast-to-coast gap is 200 m.
        return reef.center+new Vector3(0f,0f,-(reef.islandRadiusZ+ShoreGap+RadiusZ));
    }

    public static float Ellipse(Vector3 p,Vector3 center)
    {
        float x=(p.x-center.x)/RadiusX;
        float z=(p.z-center.z)/RadiusZ;
        return Mathf.Sqrt(x*x+z*z);
    }

    public static float DistanceFromShore(Vector3 p,Vector3 center)
    {
        float dx=p.x-center.x,dz=p.z-center.z;
        float distance=Mathf.Sqrt(dx*dx+dz*dz);
        if(distance<0.0001f)return 0f;
        float ux=dx/distance,uz=dz/distance;
        float edge=1f/Mathf.Sqrt(ux*ux/(RadiusX*RadiusX)+uz*uz/(RadiusZ*RadiusZ));
        return Mathf.Max(0f,distance-edge);
    }

    public static bool ContainsFishingWater(Vector3 p,Vector3 center)
    {
        float q=Ellipse(p,center);
        return q>=1f && DistanceFromShore(p,center)<=FishingMargin;
    }

    public static bool ContainsArea(Vector3 p,Vector3 center)
    {
        return Ellipse(p,center)<=1f || DistanceFromShore(p,center)<=FishingMargin;
    }

    public static int ResolveBiome(Vector3 p)
    {
        if(SnapperIslandRuntime.Ready && ContainsArea(p,SnapperIslandRuntime.Center))
            return ReefCatalog.SnapperBiomeId;
        return IslandExpansionWorld.FishingBiome(p);
    }

    public static float Height(Vector3 p,Vector3 center,float sea,float existingFloor)
    {
        float q=Ellipse(p,center);
        if(q<=1f)
        {
            float inland=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1f,.38f,q));
            float broad=1.7f+1.55f*Mathf.PerlinNoise(p.x*.028f+91f,p.z*.027f+37f);
            float detail=.55f*Mathf.PerlinNoise(p.x*.081f+12f,p.z*.074f+54f);
            float west=Shoulder(p,center+new Vector3(-17f,0f,-5f),21f,16f)*1.65f;
            float east=Shoulder(p,center+new Vector3(18f,0f,-9f),19f,17f)*1.45f;
            float back=Shoulder(p,center+new Vector3(3f,0f,-18f),24f,15f)*1.25f;
            return Mathf.Max(existingFloor,sea+.08f+inland*(broad+detail)+west+east+back);
        }

        float coast=DistanceFromShore(p,center);
        if(coast>CoastalShelfWidth)return existingFloor;
        float blend=Mathf.SmoothStep(0f,1f,coast/CoastalShelfWidth);
        return Mathf.Max(existingFloor,Mathf.Lerp(sea-.12f,existingFloor,blend));
    }

    private static float Shoulder(Vector3 p,Vector3 c,float radiusX,float radiusZ)
    {
        float x=(p.x-c.x)/radiusX;
        float z=(p.z-c.z)/radiusZ;
        float d=x*x+z*z;
        return Mathf.SmoothStep(1f,0f,Mathf.Clamp01(d));
    }
}
