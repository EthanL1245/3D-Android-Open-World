using UnityEngine;

// Scene geometry bounds, used by ambient habitat queries and the island index.
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
        Vector3 d=point-center;
        float rx=Mathf.Max(1f,islandRadiusX+reefWidth);
        float rz=Mathf.Max(1f,islandRadiusZ+reefWidth);
        bool insideReef=d.x*d.x/(rx*rx)+d.z*d.z/(rz*rz)<=1f;
        if(insideReef)return true;

        // The old implementation made the edge of the installed reef an invisible
        // fishing wall. Keep rejecting finite terrain outside the authored reef,
        // but once the player/cast is beyond every terrain tile it is genuine open
        // ocean and is valid fishing water.
        foreach(Terrain terrain in Terrain.activeTerrains)
        {
            if(terrain==null || terrain.terrainData==null)continue;
            Vector3 local=point-terrain.transform.position;
            Vector3 size=terrain.terrainData.size;
            if(local.x>=0f && local.z>=0f && local.x<=size.x && local.z<=size.z)
                return false;
        }
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
