using UnityEngine;

public static class BoatClearance
{
    // Full footprint plus a one-metre buffer, not only the centre ray.
    public static bool Valid(BoatData data, Vector3 position, Quaternion rotation, OceanWater water, Transform ignored=null, Transform player=null)
    {
        if(data==null || water==null || PondWater.TrySurface(position,out _))return false;
        if(data.HullSize.x<=0 || data.HullSize.z<=0 || data.Draft<0)return false;
        var terrains=Terrain.activeTerrains;
        Vector3 half=data.HullSize*.5f+new Vector3(1,0,1);
        for(float x=-half.x;x<=half.x+.01f;x+=half.x/3f)
        for(float z=-half.z;z<=half.z+.01f;z+=half.z/4f)
        {
            Vector3 p=position+rotation*new Vector3(x,0,z);
            bool found=false;
            foreach(var terrain in terrains)
            {
                Vector3 local=p-terrain.transform.position;Vector3 size=terrain.terrainData.size;
                if(local.x<0 || local.z<0 || local.x>size.x || local.z>size.z)continue;
                found=true;
                float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                if(water.BaseWaterLevel-ground<data.Draft+.8f)return false;
            }
            // No known bathymetry: fail closed instead of allowing an unseen shore.
            if(!found)return false;
        }
        half.y=(data.Draft+2f)*.5f;
        foreach(var c in Physics.OverlapBox(position+Vector3.up*((2f-data.Draft)*.5f),half,rotation,~0,QueryTriggerInteraction.Ignore))
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
