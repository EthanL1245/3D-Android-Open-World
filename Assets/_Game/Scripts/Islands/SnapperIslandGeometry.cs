using UnityEngine;

/// <summary>
/// Geometry contract for the dedicated snapper island south of Suncrest Reef.
/// Gameplay bounds stay unchanged; only the landform inside them is art-directed.
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
            Vector3 local=p-center;
            float beach=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1f,.67f,q));
            float foundation=sea+.10f+beach*1.05f;
            // Broad asymmetric ridge: high rear-left crown, stepped western
            // shoulder, lower eastern buttress separated by a sandy saddle.
            float ridge=Mound(p,center+new Vector3(-7f,0f,-13f),34f,27f)*11.8f;
            float crown=Mound(p,center+new Vector3(-8f,0f,-16f),17f,15f)*3.8f;
            float west=Mound(p,center+new Vector3(-30f,0f,-5f),19f,23f)*5.3f;
            float east=Mound(p,center+new Vector3(33f,0f,-10f),17f,23f)*7.3f;
            float front=Mound(p,center+new Vector3(-23f,0f,10f),19f,14f)*2.4f;
            float rear=Mound(p,center+new Vector3(6f,0f,-28f),27f,12f)*3.4f;
            float land=Mathf.Max(ridge+crown,Mathf.Max(west,Mathf.Max(east,rear)))+front;
            float arrival=Mound(p,center+new Vector3(0f,0f,28f),13f,14f);
            // Irregular erosion becomes weaker at the waterline and on the
            // arrival beach. The rock meshes form the vertical cliff faces.
            float rough=(Mathf.PerlinNoise(local.x*.14f+117f,local.z*.13f+91f)-.5f)*1.3f;
            float rock=RockMask(p,center);
            return Mathf.Max(existingFloor,foundation+land*(1f-.96f*arrival)+rough*rock);
        }
        float coast=DistanceFromShore(p,center);
        if(coast>CoastalShelfWidth)return existingFloor;
        float blend=Mathf.SmoothStep(0f,1f,coast/CoastalShelfWidth);
        return Mathf.Max(existingFloor,Mathf.Lerp(sea-.12f,existingFloor,blend));
    }

    /// <summary>Exposed stone on the ridges; sand in the beach and saddle.</summary>
    public static float RockMask(Vector3 p,Vector3 center)
    {
        if(Ellipse(p,center)>1f)return 0f;
        float ridge=Mound(p,center+new Vector3(-7f,0f,-13f),36f,28f);
        float west=Mound(p,center+new Vector3(-30f,0f,-5f),21f,24f);
        float east=Mound(p,center+new Vector3(33f,0f,-10f),19f,24f);
        float front=Mound(p,center+new Vector3(-23f,0f,10f),20f,15f)*.80f;
        float mask=Mathf.Max(Mathf.Max(ridge,west),Mathf.Max(east,front));
        float arrival=Mound(p,center+new Vector3(0f,0f,28f),15f,15f);
        return Mathf.Clamp01(mask*1.55f)*(1f-arrival);
    }

    private static float Mound(Vector3 p,Vector3 c,float radiusX,float radiusZ)
    {
        float x=(p.x-c.x)/radiusX;
        float z=(p.z-c.z)/radiusZ;
        return Mathf.SmoothStep(1f,0f,Mathf.Clamp01(x*x+z*z));
    }
}
