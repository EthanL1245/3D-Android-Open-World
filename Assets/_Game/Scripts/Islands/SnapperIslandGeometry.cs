using UnityEngine;

/// <summary>
/// Geometry contract for the dedicated snapper island south of Suncrest Reef.
/// One shared scale keeps terrain, formations, arrival and shore bounds aligned.
/// </summary>
public static class SnapperIslandGeometry
{
    public const string Id="snapper-island";
    public const string Name="Snapper Island";
    public const float DesignScale=1f/3f;
    public const float RadiusX=55f*DesignScale;
    public const float RadiusZ=42f*DesignScale;
    public const float ShoreGap=200f;
    public const float FishingMargin=50f;
    public const float CoastalShelfWidth=72f*DesignScale;
    // The level shelf surrounds all sides, fades into the existing ocean floor,
    // and never touches the dry island or any other island.
    public const float UniformSeabedOuterDistance=82f;

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

    /// <summary>
    /// A quiet, gently undulating apron matching the seabed depth north of Snapper.
    /// Retains the island/shoreline and smoothly blends into distant original relief.
    /// No artificial rock pillars, trenches, or uniform featureless plane.
    /// </summary>
    public static float UniformSeabedHeight(Vector3 p,Vector3 center,float sea,float originalFloor,float northDepth)
    {
        if(Ellipse(p,center)<=1f)return originalFloor;
        float distance=DistanceFromShore(p,center);
        if(distance>=UniformSeabedOuterDistance)return originalFloor;

        // The bottom begins at the existing beach edge (sea + 4 cm) to avoid a
        // crack between the existing island surface and the new seabed.
        float descent=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(distance/30f));
        float broad=(SeabedRelief.Noise(p.x*.045f+11f,p.z*.046f+71f)-.5f)*1.4f;
        float detail=(SeabedRelief.Noise(p.x*.13f+37f,p.z*.12f+22f)-.5f)*.40f;
        float variation=(broad+detail)*Mathf.SmoothStep(0f,1f,Mathf.Clamp01(distance/12f));
        float shelf=sea+.04f-Mathf.Max(1f,northDepth)*descent+variation;

        // Preserve all existing terrain outside the local apron. A wide falloff
        // avoids creating a harsh circular outer cliff where the deep sea resumes.
        float fade=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(48f,UniformSeabedOuterDistance,distance));
        return Mathf.Lerp(originalFloor,shelf,fade);
    }

    public static float Height(Vector3 p,Vector3 center,float sea,float existingFloor)
    {
        float q=Ellipse(p,center);
        if(q<=1f)
        {
            // A small, gently rounded beach island with a low grassy interior.
            // No cliff terraces, rock crowns or outcrop noise.
            float inland=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1f,.20f,q));
            return sea+.04f+inland*2.05f;
        }
        float coast=DistanceFromShore(p,center);
        if(coast>CoastalShelfWidth)return existingFloor;
        float blend=Mathf.SmoothStep(0f,1f,coast/CoastalShelfWidth);
        return Mathf.Max(existingFloor,Mathf.Lerp(sea-.12f*DesignScale,existingFloor,blend));
    }

}
