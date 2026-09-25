using UnityEngine;

// Scene geometry reference used by habitat queries, offshore-distance tuning and the island index.
public sealed class ReefZone : MonoBehaviour
{
    public Vector3 center;
    public float islandRadiusX, islandRadiusZ, reefWidth=160f;
    public float originalLandArea, landArea;
    public static ReefZone Active {get;private set;}
    private void OnEnable(){Active=this;}
    private void OnDisable(){if(Active==this)Active=null;}

    public bool Contains(Vector3 point)
    {
        // FishingSystem and AmbientFishManager both perform their own actual-water
        // depth/terrain validation after this call. The previous ellipse therefore
        // acted only as an invisible wall that disabled otherwise-valid ocean once
        // a boat travelled beyond Suncrest's reef. Suncrest now intentionally has
        // continuous fishable ocean; OpenOceanDistance still preserves the reef
        // boundary for coastal-vs-offshore size tuning.
        return true;
    }

    public float OpenOceanDistance(Vector3 point)
    {
        Vector3 d=point-center;
        float rx=Mathf.Max(1f,islandRadiusX+reefWidth);
        float rz=Mathf.Max(1f,islandRadiusZ+reefWidth);
        float normalized=Mathf.Sqrt(d.x*d.x/(rx*rx)+d.z*d.z/(rz*rz));
        if(normalized<=1f)return 0f;
        return (normalized-1f)*(rx+rz)*.5f;
    }
}
