using System.Collections.Generic;
using UnityEngine;

/// <summary>Replaces old primitive dock geometry without changing the user's saved scene.</summary>
[DefaultExecutionOrder(-500)]
public sealed class MarinaDockExtensionRuntime : MonoBehaviour
{
    private const string ExtensionName="Authored Marina Installation";
    private const float MinimumDockEnd=22f,MaximumDockEnd=42f,PlacementGap=8f;
    private const float WideEnd=2f,SourceEnd=21.942202f;
    private static MarinaDockExtensionRuntime active;
    private BoatSystem boats;
    private Collider[] solids;
    private static readonly Collider[] waterHits=new Collider[64];
    private Bounds worldBounds;
    private readonly List<Mesh> stretchedMeshes=new List<Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        active=null;
        foreach(var system in FindObjectsByType<BoatSystem>(FindObjectsSortMode.None))
            if(system.GetComponent<MarinaDockExtensionRuntime>()==null)system.gameObject.AddComponent<MarinaDockExtensionRuntime>();
    }
    private void Start(){boats=GetComponent<BoatSystem>();BuildExtension();}

    private void BuildExtension()
    {
        GameObject marina=GameObject.Find("MarinaShop");
        var dockPrefab=Resources.Load<GameObject>("Boats/AuthoredMarinaDock");
        var postPrefab=Resources.Load<GameObject>("Boats/AuthoredDockPost");
        if(boats==null || marina==null || dockPrefab==null || postPrefab==null)return;
        Transform root=marina.transform;
        if(root.Find(ExtensionName)!=null)return;
        OceanWater water=boats.Water!=null?boats.Water:FindFirstObjectByType<OceanWater>();
        if(water==null)return;
        float end=FindSafeDockEnd(root,water);
        var installation=new GameObject(ExtensionName);installation.transform.SetParent(root,false);
        var dock=Instantiate(dockPrefab,installation.transform,false);
        foreach(var filter in dock.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh=Instantiate(filter.sharedMesh);mesh.name=filter.name+" fitted pier";
            Vector3[] vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
                if(vertices[i].z>WideEnd)vertices[i].z=WideEnd+(vertices[i].z-WideEnd)*(end-WideEnd)/(SourceEnd-WideEnd);
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();mesh.RecalculateTangents();
            filter.sharedMesh=mesh;filter.GetComponent<MeshCollider>().sharedMesh=mesh;stretchedMeshes.Add(mesh);
        }

        // Shop stays over the wide authored platform. The ramp meets its landward edge.
        // Check the actual triangle surfaces, not a box across the surrounding water.
        if(!FindClearSite(root,installation.transform))
        {
            installation.SetActive(false);Destroy(installation);
            Debug.LogError("Marina replacement found an obstructed dock footprint. Clear the shoreline around MarinaShop, then restart Play. The existing marina was kept.");
            return;
        }
        foreach(Transform child in root)
        {
            if(child==installation.transform)continue;
            string n=child.name;
            if(n=="Boardwalk" || n=="Dock piling" || n=="Deep Water Dock Extension" ||
               n.StartsWith("UserDock") || n.StartsWith("DockVisual") || n.StartsWith("ImportedDock"))
            {child.gameObject.SetActive(false);Destroy(child.gameObject);}
        }
        FitRamp(root);
        // The supplied post has its own UVs. Only height is fitted to the seabed;
        // its small individual collider leaves all the water between posts open.
        foreach(float x in new[]{-4.65f,4.65f})foreach(float z in new[]{-4.5f,1.4f})
            AddPost(installation.transform,postPrefab,new Vector3(x,0,z));
        for(float z=4f;z<end-5f;z+=4f)
            foreach(float x in new[]{-.49f,.49f})AddPost(installation.transform,postPrefab,new Vector3(x,0,z));
        foreach(float x in new[]{-1.4f,1.4f})
            AddPost(installation.transform,postPrefab,new Vector3(x,0,end-1f));
        solids=installation.GetComponentsInChildren<Collider>();active=this;
        Physics.SyncTransforms();
        worldBounds=new Bounds(installation.transform.position,Vector3.zero);
        foreach(var solid in solids)worldBounds.Encapsulate(solid.bounds);
        Physics.SyncTransforms();
    }

    private static void AddPost(Transform parent,GameObject prefab,Vector3 local)
    {
        Vector3 top=parent.TransformPoint(new Vector3(local.x,.015f,local.z));
        float bottom=Ground(top)-.30f;
        float height=Mathf.Max(.4f,top.y-bottom);
        var post=Instantiate(prefab,parent,false);post.name="Authored dock post";
        post.transform.position=new Vector3(top.x,top.y-height*.5f,top.z);
        post.transform.localScale=new Vector3(.24f,height/Mathf.Max(.001f,parent.lossyScale.y),.24f);
    }

    private static float Ground(Vector3 point)
    {
        foreach(var terrain in Terrain.activeTerrains)
        {
            Vector3 p=point-terrain.transform.position,s=terrain.terrainData.size;
            if(p.x>=0 && p.z>=0 && p.x<=s.x && p.z<=s.z)return terrain.SampleHeight(point)+terrain.transform.position.y;
        }
        return point.y-8f;
    }

    private static bool FindClearSite(Transform marina,Transform dock)
    {
        Vector3 origin=marina.position;
        // Include visible scenery without colliders (e.g. foliage), and cache it once.
        var scenery=new List<Renderer>();
        foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if(!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.transform.IsChildOf(marina) ||
               renderer.GetComponentInParent<OceanWater>()!=null || renderer.GetComponentInParent<FirstPersonController>()!=null ||
               renderer.GetComponentInParent<AmbientFishAgent>()!=null || renderer.GetComponentInParent<BoatController>()!=null)continue;
            if((renderer.bounds.center-origin).sqrMagnitude<100f*100f)scenery.Add(renderer);
        }
        var nearby=new List<Bounds>();
        var placementHits=new Collider[64];
        foreach(float seaward in new[]{0f,2f,4f,6f})
        foreach(float lateral in new[]{0f,-2f,2f,-4f,4f,-6f,6f})
        {
            marina.position=origin+marina.rotation*new Vector3(lateral,0,seaward);
            Physics.SyncTransforms();
            bool clear=true;
            Bounds footprint=new Bounds(dock.position,Vector3.zero);
            foreach(var renderer in dock.GetComponentsInChildren<Renderer>())footprint.Encapsulate(renderer.bounds);
            footprint.Expand(new Vector3(1f,5f,1f));
            nearby.Clear();
            foreach(var renderer in scenery)
                if(renderer!=null && footprint.Intersects(renderer.bounds))nearby.Add(renderer.bounds);
            foreach(var filter in dock.GetComponentsInChildren<MeshFilter>())
            {
                Vector3[] v=filter.sharedMesh.vertices;int[] indices=filter.sharedMesh.triangles;
                for(int i=0;i<indices.Length && clear;i+=3)
                {
                    Vector3 a=filter.transform.TransformPoint(v[indices[i]]),b=filter.transform.TransformPoint(v[indices[i+1]]),c=filter.transform.TransformPoint(v[indices[i+2]]);
                    if(Vector3.Cross(b-a,c-a).y<=0.001f)continue;
                    // Sample every ~0.5 m over each top triangle for scenery/terrain intersections.
                    int steps=Mathf.CeilToInt(Mathf.Max((b-a).magnitude,(c-a).magnitude)*2f);
                    for(int u=0;u<=steps && clear;u++)for(int w=0;w<=steps-u && clear;w++)
                    {
                        Vector3 p=a+(b-a)*(u/(float)steps)+(c-a)*(w/(float)steps);
                        if(Ground(p)>p.y+.03f){clear=false;break;}
                        var walkingSpace=new Bounds(p+Vector3.up*1f,new Vector3(.5f,2f,.5f));
                        foreach(var obstacle in nearby)if(obstacle.Intersects(walkingSpace)){clear=false;break;}
                        if(!clear)break;
                        int hitCount=Physics.OverlapSphereNonAlloc(p+Vector3.up*.65f,.65f,placementHits,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                        if(hitCount==placementHits.Length){clear=false;break;}
                        for(int hitIndex=0;hitIndex<hitCount;hitIndex++)
                        {
                            var hit=placementHits[hitIndex];
                            if(hit is TerrainCollider || hit.transform.IsChildOf(marina) || hit.GetComponentInParent<FirstPersonController>()!=null || hit.GetComponentInParent<BoatController>()!=null)continue;
                            clear=false;break;
                        }
                    }
                }
                if(!clear)break;
            }
            if(clear)return true;
        }
        marina.position=origin;Physics.SyncTransforms();return false;
    }

    private static void FitRamp(Transform root)
    {
        Transform ramp=root.Find("Beach access ramp");if(ramp==null)return;
        Vector3 top=root.TransformPoint(new Vector3(0,.15f,-5f));
        Vector3 bottom=root.TransformPoint(new Vector3(0,0,-10f));bottom.y=Ground(bottom)+.08f;
        Vector3 delta=top-bottom;
        ramp.position=(top+bottom)*.5f;ramp.rotation=Quaternion.LookRotation(delta,Vector3.up);
        // End exactly at the authored deck edge; do not overlap its top faces.
        ramp.localScale=new Vector3(4f,.15f,delta.magnitude);
    }

    public static bool WaterClear(Vector3 point,float radius)
    {
        if(active==null || active.solids==null)return true;
        Bounds region=active.worldBounds;region.Expand(radius*2f);
        if(!region.Contains(point))return true;
        foreach(var solid in active.solids)
        {
            if(solid==null || !solid.enabled)continue;
            Bounds b=solid.bounds;b.Expand(radius*2f);
            if(!b.Contains(point))continue;
            // Non-convex deck's AABB must NOT close the open water beside the pier.
            // Posts are exact cuboids. Deck overlap uses a small sphere query below.
            if(solid.name=="Authored dock post")return false;
        }
        int layer=LayerMask.NameToLayer("Terrain");
        int count=Physics.OverlapSphereNonAlloc(point,radius,waterHits,layer>=0?1<<layer:Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)if(System.Array.IndexOf(active.solids,waterHits[i])>=0)return false;
        if(count==waterHits.Length)return false;
        return true;
    }
    public static bool WaterSegmentClear(Vector3 from,Vector3 to,float radius)
    {
        if(active==null || active.solids==null)return true;
        float distance=Vector3.Distance(from,to);int count=Mathf.Max(1,Mathf.CeilToInt(distance/Mathf.Max(.1f,radius)));
        for(int i=0;i<=count;i++)if(!WaterClear(Vector3.Lerp(from,to,i/(float)count),radius))return false;
        return true;
    }
    private void OnDestroy()
    {
        if(active==this)active=null;
        foreach(var mesh in stretchedMeshes)if(mesh!=null)Destroy(mesh);
    }
    private float FindSafeDockEnd(Transform marina,OceanWater water)
    {
        float fallback=MinimumDockEnd;
        for(float end=MinimumDockEnd;end<=MaximumDockEnd;end+=1f)
        {
            Vector3 target=marina.TransformPoint(new Vector3(0f,0f,end+PlacementGap));
            target.y=water.GetSurfaceHeight(target);
            Quaternion heading=Quaternion.Euler(0f,marina.eulerAngles.y,0f);

            bool depthSafe=HasCatalogDepth(target,heading,water);
            if(depthSafe)fallback=end;
            if(!depthSafe)continue;

            bool allClear=true;
            if(boats.Catalog!=null)
            {
                foreach(BoatData data in boats.Catalog)
                {
                    if(data==null)continue;
                    if(!BoatClearance.Valid(data,target,heading,water,null,transform))
                    { allClear=false;break; }
                }
            }
            if(allClear)return end;
        }
        return Mathf.Clamp(fallback,MinimumDockEnd,MaximumDockEnd);
    }

    private bool HasCatalogDepth(Vector3 center,Quaternion rotation,OceanWater water)
    {
        float halfX=2.5f,halfZ=3.5f,requiredDepth=1.25f;
        if(boats.Catalog!=null)
        {
            foreach(BoatData data in boats.Catalog)
            {
                if(data==null)continue;
                halfX=Mathf.Max(halfX,data.HullSize.x*.5f+1f);
                halfZ=Mathf.Max(halfZ,data.HullSize.z*.5f+1f);
                requiredDepth=Mathf.Max(requiredDepth,data.Draft+.85f);
            }
        }

        for(int ix=-2;ix<=2;ix++)
        for(int iz=-3;iz<=3;iz++)
        {
            Vector3 sample=center+rotation*new Vector3(halfX*ix/2f,0f,halfZ*iz/3f);
            bool found=false;
            foreach(Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 local=sample-terrain.transform.position;
                Vector3 size=terrain.terrainData.size;
                if(local.x<0f || local.z<0f || local.x>size.x || local.z>size.z)continue;
                found=true;
                float ground=terrain.SampleHeight(sample)+terrain.transform.position.y;
                if(water.BaseWaterLevel-ground<requiredDepth)return false;
                break;
            }
            if(!found)return false;
        }
        return true;
    }


}
