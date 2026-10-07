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
            // Low irregular sand/stone foundation.
            float inland=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1f,.63f,q));
            float floorNoise=Mathf.PerlinNoise(p.x*.043f+41f,p.z*.041f+73f);
            float baseLand=sea+.10f+inland*(.48f+.62f*floorNoise);

            // Connected rocky shoulders, not a single smooth dome.
            float main=Mound(p,center+new Vector3(2f,0f,-10f),27f,20f)*5.35f;
            float crown=Mound(p,center+new Vector3(2f,0f,-14f),12f,10f)*2.25f;
            float west=Mound(p,center+new Vector3(-26f,0f,-6f),18f,15f)*3.55f;
            float east=Mound(p,center+new Vector3(28f,0f,-9f),18f,15f)*3.35f;
            float westStep=Mound(p,center+new Vector3(-19f,0f,7f),17f,12f)*1.65f;
            float eastStep=Mound(p,center+new Vector3(20f,0f,7f),17f,12f)*1.55f;
            float southStep=Mound(p,center+new Vector3(-10f,0f,-28f),15f,11f)*1.20f;

            // Keep the teleport/landing approach as a usable sandy corridor.
            float arrivalCut=Mound(p,center+new Vector3(0f,0f,28f),14f,12f);
            float rocky=(main+crown+west+east+westStep+eastStep+southStep)*(1f-.88f*arrivalCut);

            // Small-scale breakup prevents the terrain under the rocks reading as
            // perfectly smooth where it peeks through gaps.
            float rough=(Mathf.PerlinNoise(p.x*.095f+17f,p.z*.091f+29f)-.5f)*.72f*RockMask(p,center);
            return Mathf.Max(existingFloor,baseLand+rocky+rough);
        }

        float coast=DistanceFromShore(p,center);
        if(coast>CoastalShelfWidth)return existingFloor;
        float blend=Mathf.SmoothStep(0f,1f,coast/CoastalShelfWidth);
        return Mathf.Max(existingFloor,Mathf.Lerp(sea-.12f,existingFloor,blend));
    }

    /// <summary>
    /// Art mask used by terrain painting. High around the authored cliff masses,
    /// low in the sandy channels and the north arrival corridor.
    /// </summary>
    public static float RockMask(Vector3 p,Vector3 center)
    {
        if(Ellipse(p,center)>1f)return 0f;

        float main=Mound(p,center+new Vector3(2f,0f,-10f),30f,22f);
        float west=Mound(p,center+new Vector3(-26f,0f,-6f),20f,17f);
        float east=Mound(p,center+new Vector3(28f,0f,-9f),20f,17f);
        float westStep=Mound(p,center+new Vector3(-19f,0f,7f),18f,13f)*.72f;
        float eastStep=Mound(p,center+new Vector3(20f,0f,7f),18f,13f)*.72f;
        float south=Mound(p,center+new Vector3(-10f,0f,-28f),17f,12f)*.66f;

        float mask=Mathf.Max(main,Mathf.Max(west,east));
        mask=Mathf.Max(mask,Mathf.Max(westStep,Mathf.Max(eastStep,south)));

        // Preserve visible sand pockets between rock groups.
        float pocketA=Mound(p,center+new Vector3(-9f,0f,10f),9f,7f);
        float pocketB=Mound(p,center+new Vector3(12f,0f,15f),10f,7f);
        float pocketC=Mound(p,center+new Vector3(-35f,0f,19f),8f,7f);
        float arrival=Mound(p,center+new Vector3(0f,0f,28f),15f,12f);
        mask*=1f-.78f*Mathf.Max(pocketA,Mathf.Max(pocketB,pocketC));
        mask*=1f-.94f*arrival;

        float breakup=Mathf.Lerp(.72f,1f,Mathf.PerlinNoise(p.x*.071f+19f,p.z*.067f+43f));
        return Mathf.Clamp01(mask*breakup);
    }

    private static float Mound(Vector3 p,Vector3 c,float radiusX,float radiusZ)
    {
        float x=(p.x-c.x)/radiusX;
        float z=(p.z-c.z)/radiusZ;
        float d=x*x+z*z;
        return Mathf.SmoothStep(1f,0f,Mathf.Clamp01(d));
    }
}
