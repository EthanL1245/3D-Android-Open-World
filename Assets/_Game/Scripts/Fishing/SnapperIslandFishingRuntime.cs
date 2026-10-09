using UnityEngine;

/// <summary>
/// Supplies Snapper Island's shoreline depth reference. Species, weight and health
/// are selected by the normal catch pipeline, never replaced as a fish moves.
/// </summary>
public sealed class SnapperIslandFishingRuntime : MonoBehaviour
{
    private Terrain terrain;
    private OceanWater ocean;
    private float snapperReferenceDepth=-1f;

    internal float ReferenceDepth()
    {
        if(terrain==null)terrain=Terrain.activeTerrain;
        if(ocean==null)ocean=FindFirstObjectByType<OceanWater>();
        if(terrain==null || ocean==null)return 0f;
        if(snapperReferenceDepth>0.001f)return snapperReferenceDepth;
        float best=0f;
        const int directions=96;
        for(int i=0;i<directions;i++)
        {
            float angle=i*Mathf.PI*2f/directions;
            Vector3 edge=SnapperIslandRuntime.Center+new Vector3(
                Mathf.Cos(angle)*SnapperIslandGeometry.RadiusX,0f,
                Mathf.Sin(angle)*SnapperIslandGeometry.RadiusZ);
            Vector3 outward=edge-SnapperIslandRuntime.Center;
            outward.y=0f;
            outward.Normalize();
            for(float d=0f;d<=30.01f;d+=1f)
            {
                Vector3 sample=edge+outward*d;
                best=Mathf.Max(best,StableWaterDepth(sample));
            }
        }
        snapperReferenceDepth=Mathf.Max(1f,best);
        return snapperReferenceDepth;
    }

    private float StableWaterDepth(Vector3 position)
    {
        if(terrain==null || ocean==null)return 0f;
        TerrainData data=terrain.terrainData;
        Vector3 local=position-terrain.transform.position;
        if(local.x<0f || local.z<0f || local.x>data.size.x || local.z>data.size.z)return 0f;
        float ground=terrain.SampleHeight(position)+terrain.transform.position.y;
        return Mathf.Max(0f,ocean.BaseWaterLevel-ground);
    }
}

// Retained for compatibility with existing scene components. Cast quality now runs
// once in FishingCastQualityRuntime, with the correct reference from the outset.
public sealed class SnapperIslandCastQualityRuntime : MonoBehaviour { }
