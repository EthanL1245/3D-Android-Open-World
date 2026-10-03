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
            float inland=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1f,.42f,q));
            float broad=4.2f+3.8f*Mathf.PerlinNoise(p.x*.026f+91f,p.z*.026f+37f);
            float detail=1.1f*Mathf.PerlinNoise(p.x*.073f+12f,p.z*.069f+54f);
            return Mathf.Max(existingFloor,sea+.08f+inland*(broad+detail));
        }

        float coast=DistanceFromShore(p,center);
        if(coast>CoastalShelfWidth)return existingFloor;
        float blend=Mathf.SmoothStep(0f,1f,coast/CoastalShelfWidth);
        return Mathf.Max(existingFloor,Mathf.Lerp(sea-.12f,existingFloor,blend));
    }
}
