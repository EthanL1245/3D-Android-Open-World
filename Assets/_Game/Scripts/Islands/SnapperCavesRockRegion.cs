using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Snapper fishing water extends 100 metres from the horizontal bounds of
/// each authored Snapper Cave mesh. Cache mesh bounds once per loaded scene.
/// </summary>
public static class SnapperCavesRockRegion
{
    public const float FishingRadius=100f;
    private static readonly List<Bounds> Rocks=new List<Bounds>(8);
    private static int sceneHandle=-1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Rocks.Clear(); sceneHandle=-1; }

    public static bool IsSnapperRock(Transform part)
    {
        for(Transform t=part;t!=null;t=t.parent)
            if(t.name=="Snapper Caves Main Rock" ||
               t.name=="Snapper Caves Supporting Rocks")return true;
        return false;
    }

    private static void Cache()
    {
        Scene scene=SceneManager.GetSceneByName("PrototypeWorld");
        if(!scene.IsValid() || !scene.isLoaded)
        {
            Rocks.Clear();sceneHandle=-1;return;
        }
        if(scene.handle==sceneHandle)return;
        sceneHandle=scene.handle;
        Rocks.Clear();
        foreach(GameObject root in scene.GetRootGameObjects())
        {
            if(root.name!="Snapper Caves Main Rock" &&
               root.name!="Snapper Caves Supporting Rocks")continue;
            if(!root.activeInHierarchy)continue;
            foreach(MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer!=null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    Rocks.Add(renderer.bounds);
        }
        if(Rocks.Count==0)Debug.LogWarning("[SNAPPER CAVES] Cave rock renderers missing.");
    }

    public static bool HasRocks {get{Cache();return Rocks.Count!=0;}}
    public static int RockMeshCount {get{Cache();return Rocks.Count;}}

    private static float DistanceSquaredXZ(Vector3 p,Bounds b)
    {
        float dx=Mathf.Max(0f,Mathf.Max(b.min.x-p.x,p.x-b.max.x));
        float dz=Mathf.Max(0f,Mathf.Max(b.min.z-p.z,p.z-b.max.z));
        return dx*dx+dz*dz;
    }

    public static bool ContainsBiome(Vector3 p,float clearance=0f)
    {
        Cache();
        float r=Mathf.Max(0f,FishingRadius-clearance);
        for(int i=0;i<Rocks.Count;i++)
            if(DistanceSquaredXZ(p,Rocks[i])<=r*r)return true;
        return false;
    }

    public static bool NearBounds(Bounds candidate,float extraDistance=0f)
    {
        Cache();
        float r=Mathf.Max(0f,FishingRadius+extraDistance);
        for(int i=0;i<Rocks.Count;i++)
        {
            Bounds b=Rocks[i];
            float dx=Mathf.Max(0f,Mathf.Max(b.min.x-candidate.max.x,candidate.min.x-b.max.x));
            float dz=Mathf.Max(0f,Mathf.Max(b.min.z-candidate.max.z,candidate.min.z-b.max.z));
            if(dx*dx+dz*dz<=r*r)return true;
        }
        return false;
    }
}
