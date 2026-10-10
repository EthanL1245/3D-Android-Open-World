using System;
using UnityEngine;

/// <summary>Restores paint weights without rebuilding any terrain outside Snapper.</summary>
public static class IslandTerrainSurfaceRepair
{
    private static int FindLayer(TerrainLayer[] layers, string first, string second, int fallback)
    {
        for(int i=0;i<layers.Length;i++)
        {
            if(layers[i]==null)continue;
            string name=layers[i].name.ToLowerInvariant();
            if(name.Contains(first) || name.Contains(second))return i;
        }
        return Mathf.Clamp(fallback,0,layers.Length-1);
    }

    private static void Roles(TerrainData data,out int sand,out int grass,out int stone,out int seabed)
    {
        var layers=data.terrainLayers;
        if(layers.Length<3)throw new InvalidOperationException("Fishing terrain needs its sand, grass and stone layers.");
        sand=FindLayer(layers,"sand","sand",0);
        grass=FindLayer(layers,"grass","meadow",1);
        stone=FindLayer(layers,"brinebreak","island stone",layers.Length>=5?3:2);
        seabed=FindLayer(layers,"underwater","submerged",layers.Length>=5?4:2);
    }

    public static void PaintSnapper(Terrain terrain,Vector3 center)
    {
        TerrainData data=terrain.terrainData;
        Roles(data,out int sand,out int grass,out _,out _);
        int w=data.alphamapWidth,h=data.alphamapHeight;
        float[,,] alpha=data.GetAlphamaps(0,0,w,h);
        for(int z=0;z<h;z++)for(int x=0;x<w;x++)
        {
            Vector3 p=terrain.transform.position+new Vector3(x/(float)(w-1)*data.size.x,0,z/(float)(h-1)*data.size.z);
            float q=SnapperIslandGeometry.Ellipse(p,center);
            if(q>1f)continue;
            float green=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.80f,.38f,q));
            for(int l=0;l<data.alphamapLayers;l++)alpha[z,x,l]=0f;
            alpha[z,x,sand]=1f-green;
            alpha[z,x,grass]+=green;
        }
        // Existing layer order, texture references and every outside pixel survive.
        data.SetAlphamaps(0,0,alpha);
    }

    public static void RestoreWorldPaint(Terrain terrain,ReefZone reef,IslandExpansionWorld world,float sea)
    {
        // A saved starter-grass restoration is explicit user-owned scene paint.
        // Never regenerate multi-island alphamaps over it through this older API.
        if(world!=null && world.GetComponent<StarterIslandGrassLock>()?.IsSaved==true)
        {
            Debug.LogWarning("[SUNCREST GRASS] Refusing legacy full-world terrain repaint: the owner locked the saved starter grass. Request explicit authorization before changing it.");
            return;
        }
        TerrainData data=terrain.terrainData;
        Roles(data,out int sand,out int grass,out int stone,out int seabed);
        int w=data.alphamapWidth,h=data.alphamapHeight;
        var alpha=new float[h,w,data.alphamapLayers];
        for(int z=0;z<h;z++)for(int x=0;x<w;x++)
        {
            float u=x/(float)(w-1),v=z/(float)(h-1);
            Vector3 p=terrain.transform.position+new Vector3(u*data.size.x,0,v*data.size.z);
            float height=terrain.transform.position.y+data.GetInterpolatedHeight(u,v)-sea;
            float green=0f,rock=0f;
            float brine=world!=null && world.Config!=null?IslandGeometry.Ellipse(p,world.NewCenter,world.Config.IslandRadii):float.PositiveInfinity;
            float pelagic=world!=null?Vector2.Distance(new Vector2(p.x,p.z),new Vector2(world.PelagicCenter.x,world.PelagicCenter.z)):float.PositiveInfinity;
            if(pelagic<=PelagicIslandGeometry.Radius)
                green=Mathf.SmoothStep(0,1,Mathf.InverseLerp(PelagicIslandGeometry.Radius*.88f,PelagicIslandGeometry.Radius*.45f,pelagic));
            else if(brine<1.2f)
                rock=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.96f,.75f,brine));
            else if(IslandGeometry.Ellipse(p,reef.center,new Vector2(reef.islandRadiusX,reef.islandRadiusZ))<1.2f)
            {
                // Match the starter's existing height-based meadow and sandy path.
                float dx=p.x-reef.center.x,dz=p.z-reef.center.z;
                green=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,3.7f,height));
                float pathCenter=-reef.islandRadiusX*.12f*Mathf.Clamp01((dz+reef.islandRadiusZ*.76f)/(reef.islandRadiusZ*.84f));
                float path=dz>-reef.islandRadiusZ*.78f && dz<reef.islandRadiusZ*.12f?
                    1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,3f,Mathf.Abs(dx-pathCenter))):0f;
                float pondRadius=Mathf.Clamp(reef.islandRadiusX*.18f,8f,18f);
                float px=(dx+reef.islandRadiusX*.12f)/pondRadius;
                float pz=(dz-reef.islandRadiusZ*.08f)/(pondRadius*.72f);
                float pondBank=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,1.65f,Mathf.Sqrt(px*px+pz*pz)));
                green*=1f-Mathf.Max(path,pondBank);
            }
            // Preserve the original seabed policy: sandy flats, stone on cliffs.
            float underwater=SeabedRelief.RockWeight(-height,90f);
            float cliff=SeabedRelief.RockWeight(-height,data.GetSteepness(u,v));
            alpha[z,x,sand]=(1f-green-rock)*(1f-underwater)+underwater-cliff;
            alpha[z,x,grass]+=green*(1f-underwater);
            alpha[z,x,stone]+=rock*(1f-underwater);
            alpha[z,x,seabed]+=cliff;
        }
        data.SetAlphamaps(0,0,alpha);
    }
}
