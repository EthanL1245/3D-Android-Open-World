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
        return d.x*d.x/Mathf.Pow(islandRadiusX+reefWidth,2)+d.z*d.z/Mathf.Pow(islandRadiusZ+reefWidth,2)<=1f;
    }
}
