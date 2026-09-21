using System.Collections.Generic;
using UnityEngine;

public sealed class ShopWaterVolume : MonoBehaviour
{
    private static readonly List<ShopWaterVolume> Active = new List<ShopWaterVolume>();
    public Vector3 size;
    public bool available;
    private void OnEnable() { if(!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);
    public static bool Surface(Vector3 position,out float height)
    {
        foreach(var volume in Active)
        {
            if(volume==null || !volume.available) continue;
            Vector3 p=volume.transform.InverseTransformPoint(position);
            if(Mathf.Abs(p.x)<=volume.size.x*0.5f && Mathf.Abs(p.z)<=volume.size.z*0.5f && p.y>=-0.25f && p.y<=volume.size.y+1f)
            { height=volume.transform.position.y+volume.size.y; return true; }
        }
        height=0; return false;
    }
    public static bool TrySurface(Vector3 position,OceanWater ocean,out float height)
    {
        if(Surface(position,out height)) return true;
        if(ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.InShop) return false;
        if(ocean==null) return false;
        height=ocean.GetSurfaceHeight(position); return true;
    }
}
