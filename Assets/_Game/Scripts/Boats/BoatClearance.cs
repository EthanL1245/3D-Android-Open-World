using UnityEngine;

public static class BoatClearance
{
    // Measured from the stationary authored raft body in the exact v20 diagnostic.
    // Do NOT include the oars in the hull footprint: they visually swing outside the
    // raft but should never make the collision/shore-clearance box wider.
    private static readonly Vector3 AuthoredRaftHullSize = new Vector3(2.6749f,1.254236f,4.5634f);
    private const float AuthoredRaftDraft = .45f;

    public static bool Valid(BoatData data, Vector3 position, Quaternion rotation, OceanWater water, Transform ignored=null, Transform player=null)
    {
        if(data==null || water==null || PondWater.TrySurface(position,out _))return false;

        bool authoredRaft=data.ID=="raft";
        Vector3 hullSize=authoredRaft?AuthoredRaftHullSize:data.HullSize;
        float draft=authoredRaft?AuthoredRaftDraft:data.Draft;
        if(hullSize.x<=0 || hullSize.z<=0 || draft<0)return false;

        // The old raft value effectively used the oars as hull width and then added
        // another full metre on each side. That made the boat stop well before the
        // visible left/right edges. Keep a small safety margin on the authored raft;
        // preserve the original larger buffer for the other boats.
        Vector3 buffer=authoredRaft?new Vector3(.18f,0,.35f):new Vector3(1,0,1);
        Vector3 half=hullSize*.5f+buffer;
        var terrains=Terrain.activeTerrains;
        for(float x=-half.x;x<=half.x+.01f;x+=Mathf.Max(.08f,half.x/3f))
        for(float z=-half.z;z<=half.z+.01f;z+=Mathf.Max(.08f,half.z/4f))
        {
            Vector3 p=position+rotation*new Vector3(x,0,z);
            bool found=false;
            foreach(var terrain in terrains)
            {
                Vector3 local=p-terrain.transform.position;Vector3 size=terrain.terrainData.size;
                if(local.x<0 || local.z<0 || local.x>size.x || local.z>size.z)continue;
                found=true;
                float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                if(water.BaseWaterLevel-ground<draft+.8f)return false;
            }
            // No known bathymetry: fail closed instead of allowing an unseen shore.
            if(!found)return false;
        }
        half.y=(draft+2f)*.5f;
        foreach(var c in Physics.OverlapBox(position+Vector3.up*((2f-draft)*.5f),half,rotation,~0,QueryTriggerInteraction.Ignore))
        {
            var rider=c.GetComponentInParent<BoatPassenger>();
            if(rider!=null && rider.Boat!=null && ignored==rider.Boat.transform)continue;
            if(ignored!=null && c.transform.IsChildOf(ignored))continue;
            if(player!=null && c.transform.IsChildOf(player))continue;
            return false;
        }
        return true;
    }
}
