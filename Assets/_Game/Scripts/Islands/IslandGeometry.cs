using UnityEngine;

// Shared world-space boundaries: terrain, loot, offshore size and water all use these.
public static class IslandGeometry
{
    public static float Ellipse(Vector3 p,Vector3 center,Vector2 radii)
    {float x=(p.x-center.x)/Mathf.Max(1,radii.x),z=(p.z-center.z)/Mathf.Max(1,radii.y);return Mathf.Sqrt(x*x+z*z);}
    public static float Beyond(Vector3 p,Vector3 center,Vector2 radii)
        => Mathf.Max(0,Ellipse(p,center,radii)-1)*Mathf.Min(radii.x,radii.y);
    public static float StormBlend(Vector3 p,Vector3 center,float inner,float outer)
    {float dx=p.x-center.x,dz=p.z-center.z;return Mathf.SmoothStep(0,1,Mathf.InverseLerp(outer,inner,Mathf.Sqrt(dx*dx+dz*dz)));}
    public static float ShelfFloor(float sea,float depth,float oceanDepth,float beyond,float width)
        => sea-Mathf.Lerp(depth,oceanDepth,Mathf.SmoothStep(0,1,beyond/width));
    public static float CoastalFloor(float shelf,float sea,float coastDistance,float depth)
        => coastDistance>=65?shelf:Mathf.Max(shelf,sea-Mathf.Lerp(0,depth,Mathf.SmoothStep(0,1,coastDistance/65)));
    public static int Biome(Vector3 p,Vector3 newCenter,Vector2 islandRadii,Vector3 shelfCenter,Vector2 shelfRadii)
    {
        if(Ellipse(p,shelfCenter,shelfRadii)>1)return 2;
        return Ellipse(p,newCenter,islandRadii+new Vector2(95,95))<=1?1:0;
    }
}
