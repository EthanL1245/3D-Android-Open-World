#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Restores the original Suncrest Palm/SeaGrape/Grass prefabs with the original
/// reproducible angle + square-root-radius sampling, filtered by the *current*
/// painted grass TerrainLayers. Scatters each imported Low Poly rock mesh as
/// an INDEPENDENT GameObject across sand throughout the 150 m Suncrest area,
/// plus up to 10 m offshore where water is at most 2 m deep.
/// Protects all authored rocks, the entire marina/dock, and existing trees.
/// No changes to terrain heights/alphamaps or any hand-placed object transforms.
/// </summary>
[InitializeOnLoad]
public static class SuncrestSceneryScatterEditor
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    private const string GroupName = SuncrestPlacedRockCollision.GeneratedName;
    private const string Base = "Assets/_Game/Reef/Generated/";
    private const string LowPoly = "Assets/Manually Added Assets/Suncrest Beach Rock Pack Low Poly.fbx";
    private const string BrownMaterial = "Assets/Manually Added Assets/Suncrest Beach Main Rock - Editable.mat";
    private const float SuncrestRadius = 150f;
    private const string VersionMarker = "SUNCREST_SCENERY_150M_V2";
    private static readonly Collider[] Probe = new Collider[256];

    static SuncrestSceneryScatterEditor()
    {
        EditorApplication.delayCall += PopulateOnOpen;
        EditorSceneManager.sceneOpened += (scene,mode) => EditorApplication.delayCall += PopulateOnOpen;
    }

    private static bool Ready()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling &&
            SceneManager.GetActiveScene().path == ScenePath;
    }

    private static void PopulateOnOpen()
    {
        if (!Ready()) return;
        GameObject old=GameObject.Find(GroupName);
        if (old!=null && old.transform.Find(VersionMarker)!=null) return;
        try { Generate(old!=null); }
        catch(Exception e) { Debug.LogError("[SUNCREST SCENERY] "+e); }
    }

    [MenuItem("Tools/Open World/Manual Scenery/Rebuild Suncrest 150m Foliage and Sand Rocks")]
    public static void Rebuild()
    {
        if (!Ready())
        {
            Debug.LogWarning("[SUNCREST SCENERY] Open PrototypeWorld in Edit Mode first.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Rebuild Suncrest auto scenery",
            "Replace ONLY the auto-generated Suncrest scenery in the main rock's 150 m area?\n\n" +
            "Hand-placed rocks, existing trees, dock, marina and terrain remain untouched.\n" +
            "This will replace any manual edits made inside the generated group.", "Regenerate", "Cancel"))
            return;
        try { Generate(true); }
        catch(Exception e) { Debug.LogError("[SUNCREST SCENERY] "+e); }
    }

    private sealed class Builder
    {
        public Terrain terrain;
        public TerrainData data;
        public ReefZone reef;
        public float sea;
        public float[,,] alpha;
        public readonly List<int> grassLayers = new List<int>();
        public readonly List<int> sandLayers = new List<int>();
        public Vector3 origin, size, center;
        public float radius; 
        public Transform marina;
        public readonly List<Collider> protectedRocks = new List<Collider>();
        public readonly List<Bounds> dockBounds = new List<Bounds>();
        public readonly List<Vector2> occupied = new List<Vector2>();
        public readonly List<float> spacing = new List<float>();
        public readonly List<Vector2> existingTreePositions = new List<Vector2>();
        public readonly System.Random rng = new System.Random(73191);

        public float Rand(float min,float max)
        { return Mathf.Lerp(min,max,(float)rng.NextDouble()); }

        public Vector3 Candidate(bool rocks)
        {
            // Reproduce the original fixed-seed polar/sqrt-radius distribution.
            // Unlike the old elliptical sampling, this is truly within 150 m
            // of the rendered center of the newly placed Suncrest main rock.
            float angle=Rand(0f,Mathf.PI*2f);
            float distance=radius*Mathf.Sqrt(Rand(rocks ? 0f : .015f,
                                                     rocks ? 1f : .98f));
            return new Vector3(center.x+Mathf.Cos(angle)*distance,0f,
                center.z+Mathf.Sin(angle)*distance);
        }

        public bool InsideSuncrest(Vector3 p,float footprint)
        {
            Vector2 delta=new Vector2(p.x-center.x,p.z-center.z);
            float edge=Mathf.Max(0f,radius-footprint);
            return delta.sqrMagnitude<=edge*edge;
        }

        public bool InsideTerrain(Vector3 p,float padding)
        {
            return p.x>=origin.x+padding && p.z>=origin.z+padding &&
                   p.x<=origin.x+size.x-padding && p.z<=origin.z+size.z-padding;
        }

        public float Height(Vector3 p)
        {
            return terrain.SampleHeight(p)+origin.y;
        }

        private float Paint(Vector3 p,bool isGrass)
        {
            if(!InsideTerrain(p,0f))return 0f;
            List<int> indices=isGrass?grassLayers:sandLayers;
            float u=(p.x-origin.x)/size.x, v=(p.z-origin.z)/size.z;
            int x=Mathf.Clamp(Mathf.RoundToInt(u*(data.alphamapWidth-1)),0,data.alphamapWidth-1);
            int z=Mathf.Clamp(Mathf.RoundToInt(v*(data.alphamapHeight-1)),0,data.alphamapHeight-1);
            float weight=0f;
            for(int i=0;i<indices.Count;i++)weight+=alpha[z,x,indices[i]];
            return Mathf.Clamp01(weight);
        }

        private bool SafeSurface(Vector3 p,float footprint,float slopeMax)
        {
            if(!InsideTerrain(p,footprint+1f))return false;
            float u=(p.x-origin.x)/size.x, v=(p.z-origin.z)/size.z;
            if(data.GetSteepness(u,v)>slopeMax)return false;
            float h=Height(p);
            // The old bush/rock grounding used eight slope-footprint probes.
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;
                Vector3 sample=p+new Vector3(Mathf.Cos(a)*footprint,0f,
                                               Mathf.Sin(a)*footprint);
                if(!InsideTerrain(sample,.1f) ||
                   Mathf.Abs(Height(sample)-h)>footprint*.48f+.2f)return false;
            }
            return true;
        }

        public bool Grass(Vector3 p,float footprint)
        {
            if(!InsideSuncrest(p,footprint) || !SafeSurface(p,footprint,34f) || Paint(p,true)<.52f)return false;
            if(Height(p)<sea+.20f)return false;
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f;
                if(Paint(p+new Vector3(Mathf.Cos(a)*footprint,0f,
                   Mathf.Sin(a)*footprint),true)<.37f) return false;
            }
            return true;
        }

        private bool Within10mOfDryLand(Vector3 p)
        {
            // Applies ONLY to underwater/offshore sand. Dry sand may appear
            // anywhere on the island, not just its outer shoreline.
            for(int distance=1;distance<=10;distance++)
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8f;
                Vector3 sample=p+new Vector3(Mathf.Cos(a)*distance,0f,
                    Mathf.Sin(a)*distance);
                if(InsideTerrain(sample,.1f) && Height(sample)>=sea+.05f)
                    return true;
            }
            return false;
        }

        public bool SandArea(Vector3 p,float footprint)
        {
            if(!InsideSuncrest(p,footprint) ||
               !SafeSurface(p,footprint,27f) || Paint(p,false)<.55f)
                return false;
            float elevation=Height(p)-sea;
            if(elevation < -2f) return false;
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;
                Vector3 sample=p+new Vector3(Mathf.Cos(a)*footprint,0f,
                    Mathf.Sin(a)*footprint);
                if(Paint(sample,false)<.40f || Height(sample)<sea-2f)
                    return false;
            }
            return elevation>=0f || Within10mOfDryLand(p);
        }

        public bool Available(Vector3 p,float radius)
        {
            Vector2 at=new Vector2(p.x,p.z);
            for(int i=0;i<occupied.Count;i++)
            {
                float min=radius+spacing[i];
                if((at-occupied[i]).sqrMagnitude<min*min)return false;
            }
            for(int i=0;i<existingTreePositions.Count;i++)
                if((at-existingTreePositions[i]).sqrMagnitude<
                    (radius+2.2f)*(radius+2.2f))return false;
            if(marina!=null)
            {
                // The authored pier extends at runtime (up to 42 m from
                // MarinaShop), so reserve its entire future approach as well.
                Vector3 local=marina.InverseTransformPoint(p);
                if(Mathf.Abs(local.x)<radius+9f &&
                   local.z> -16f-radius && local.z<51f+radius)
                    return false;
            }
            for(int i=0;i<dockBounds.Count;i++)
            {
                Bounds b=dockBounds[i];
                if(p.x>=b.min.x-radius-4f && p.x<=b.max.x+radius+4f &&
                   p.z>=b.min.z-radius-4f && p.z<=b.max.z+radius+4f) return false;
            }
            // Test the true collider shapes, not only enormous AABBs from
            // imported overhangs/caves. Include side probes for wide plants.
            for(int i=0;i<9;i++)
            {
                float a=(i-1)*Mathf.PI*.25f;
                Vector3 s=p+(i==0?Vector3.zero:new Vector3(
                    Mathf.Cos(a)*radius*.9f,0,Mathf.Sin(a)*radius*.9f));
                Vector3 test=new Vector3(s.x,Height(s)+.8f,s.z);
                for(int k=0;k<protectedRocks.Count;k++)
                {
                    Collider collider=protectedRocks[k];
                    if(collider==null || !collider.enabled)continue;
                    Bounds box=collider.bounds;
                    if(box.min.x>test.x+radius || box.max.x<test.x-radius ||
                       box.min.z>test.z+radius || box.max.z<test.z-radius ||
                       box.min.y>test.y+radius || box.max.y<test.y-radius)continue;
                    if((collider.ClosestPoint(test)-test).sqrMagnitude<
                        (radius*.78f)*(radius*.78f))return false;
                    RaycastHit hit;
                    if(collider.Raycast(new Ray(new Vector3(s.x,box.max.y+2f,s.z),
                        Vector3.down),out hit,Mathf.Max(4f,box.size.y+4f)) &&
                        hit.point.y>Height(s)+.20f)return false;
                }
            }
            return true;
        }

        public void Claim(Vector3 p,float radius)
        {
            occupied.Add(new Vector2(p.x,p.z));
            spacing.Add(radius);
        }

        public float LowerFootprint(Vector3 p,float footprint)
        {
            float min=Height(p);
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI*.25f;
                Vector3 s=p+new Vector3(Mathf.Cos(angle)*footprint,0f,
                    Mathf.Sin(angle)*footprint);
                min=Mathf.Min(min,Height(s));
            }
            return min;
        }
    }

    private static void AddExisting(Builder b,Scene scene)
    {
        SuncrestMainRockCollision.EnsureColliders(scene);
        SuncrestPlacedRockCollision.EnsureColliders(scene);
        Physics.SyncTransforms();
        var main=SuncrestMainRockCollision.FindRock(scene);
        if(main!=null)foreach(Collider c in main.GetComponentsInChildren<Collider>(true))
            if(c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy)
                b.protectedRocks.Add(c);
        foreach(GameObject piece in SuncrestPlacedRockCollision.PlacedRocks(scene))
        foreach(Collider c in piece.GetComponentsInChildren<Collider>(true))
            if(c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy)
                b.protectedRocks.Add(c);

        GameObject marina=GameObject.Find("MarinaShop");
        b.marina=marina!=null?marina.transform:null;
        if(marina!=null)foreach(Renderer ren in marina.GetComponentsInChildren<Renderer>(true))
            if(ren.enabled) b.dockBounds.Add(ren.bounds);
        GameObject worldDecor=GameObject.Find("WorldDecor");
        if(worldDecor!=null)
        {
            Transform trees=worldDecor.transform.Find("Trees");
            if(trees!=null)foreach(Transform tree in trees)
                b.existingTreePositions.Add(new Vector2(tree.position.x,tree.position.z));
        }
    }

    private static int ScatterPrefab(Builder b,GameObject prefab,Transform parent,
        string prefix,int target,int maxAttempts,float minScale,float maxScale,
        float radius,bool shrub)
    {
        if(prefab==null)return 0;
        int added=0;
        for(int attempt=0;attempt<maxAttempts && added<target;attempt++)
        {
            Vector3 p=b.Candidate(false);
            float scale=b.Rand(minScale,maxScale);
            float footprint=radius*scale;
            if(!b.Grass(p,footprint) || !b.Available(p,footprint))continue;
            GameObject plant=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            plant.name=prefix+"_"+added.ToString("D3");
            plant.transform.SetParent(parent,true);
            plant.transform.SetPositionAndRotation(
                new Vector3(p.x,shrub?b.LowerFootprint(p,footprint)-.10f*scale:b.Height(p),
                    p.z),
                Quaternion.Euler(0f,b.Rand(0f,360f),0f));
            plant.transform.localScale=Vector3.one*scale;
            b.Claim(p,shrub?footprint:Mathf.Max(.32f,footprint*.65f));
            added++;
        }
        return added;
    }

    private static int ScatterIndividualLowPolyRocks(Builder b,Transform parent)
    {
        GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(LowPoly);
        if(model==null)throw new InvalidOperationException("Missing low-poly rock pack: "+LowPoly);
        MeshFilter[] parts=model.GetComponentsInChildren<MeshFilter>(true);
        var meshes=new List<MeshFilter>();
        foreach(MeshFilter filter in parts)
            if(filter!=null && filter.sharedMesh!=null && filter.sharedMesh.vertexCount>=3)
                meshes.Add(filter);
        if(meshes.Count==0)throw new InvalidOperationException(
            "Rock pack has no individually selectable meshes. No sand rocks were placed.");
        Material brown=AssetDatabase.LoadAssetAtPath<Material>(BrownMaterial);

        int added=0;
        for(int attempt=0;attempt<24000 && added<160;attempt++)
        {
            Vector3 p=b.Candidate(true);
            MeshFilter piece=meshes[b.rng.Next(meshes.Count)];
            Mesh mesh=piece.sharedMesh;
            Bounds meshBox=mesh.bounds;
            float width=Mathf.Max(meshBox.size.x,meshBox.size.z);
            if(width<.0001f)continue;

            // Each MeshFilter is an independent rock object, never the whole FBX.
            float diameter=b.Rand(.85f,2.5f);
            float scale=diameter/width;
            float angle=b.Rand(0f,360f);
            Quaternion rotation=Quaternion.Euler(0f,0f,angle);
            float rad=angle*Mathf.Deg2Rad;
            // Z rotation changes X and Y extents. Account for both when
            // excluding protected authored geometry and grounding the mesh.
            float halfX=(Mathf.Abs(Mathf.Cos(rad))*meshBox.extents.x+
                         Mathf.Abs(Mathf.Sin(rad))*meshBox.extents.y)*scale;
            float halfZ=meshBox.extents.z*scale;
            float radius=Mathf.Sqrt(halfX*halfX+halfZ*halfZ);
            if(!b.SandArea(p,radius) || !b.Available(p,radius+.35f))continue;
            float halfY=(Mathf.Abs(Mathf.Sin(rad))*meshBox.extents.x+
                         Mathf.Abs(Mathf.Cos(rad))*meshBox.extents.y)*scale;

            GameObject rock=new GameObject("LowPolyBeachRock_"+added.ToString("D3")+
                "_"+piece.gameObject.name);
            rock.transform.SetParent(parent,false);
            rock.transform.position=new Vector3(p.x,
                b.LowerFootprint(p,radius)+halfY-.04f,p.z);
            rock.transform.rotation=rotation;
            rock.transform.localScale=Vector3.one*scale;

            GameObject visual=new GameObject("Mesh");
            visual.transform.SetParent(rock.transform,false);
            // Rotate around each source mesh's center. The full rotated
            // bottom now lies against the sand, even at large Z angles.
            visual.transform.localPosition=-meshBox.center;
            MeshFilter mf=visual.AddComponent<MeshFilter>();mf.sharedMesh=mesh;
            MeshRenderer mr=visual.AddComponent<MeshRenderer>();
            MeshRenderer sourceRenderer=piece.GetComponent<MeshRenderer>();
            if(sourceRenderer!=null)mr.sharedMaterials=sourceRenderer.sharedMaterials;
            if(mr.sharedMaterial==null && brown!=null)mr.sharedMaterial=brown;
            mr.shadowCastingMode=ShadowCastingMode.Off;
            MeshCollider collider=visual.AddComponent<MeshCollider>();
            collider.sharedMesh=mesh;collider.convex=false;collider.isTrigger=false;
            b.Claim(p,radius+.35f);
            added++;
        }
        return added;
    }

    private static void Generate(bool replace)
    {
        Scene scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)return;
        GameObject existing=GameObject.Find(GroupName);
        if(existing!=null && !replace)return;

        ReefZone reef=UnityEngine.Object.FindFirstObjectByType<ReefZone>();
        Terrain terrain=reef!=null?reef.GetComponentInChildren<Terrain>(true):null;
        if(terrain==null)terrain=Terrain.activeTerrain;
        OceanWater ocean=UnityEngine.Object.FindFirstObjectByType<OceanWater>();
        if(reef==null || terrain==null || terrain.terrainData==null || ocean==null)
            throw new InvalidOperationException("Suncrest terrain, ReefZone or water is missing.");

        GameObject palm=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Palm.prefab");
        GameObject shrub=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"SeaGrape.prefab");
        GameObject grass=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Grass.prefab");
        if(palm==null || shrub==null || grass==null ||
           AssetDatabase.LoadAssetAtPath<GameObject>(LowPoly)==null)
            throw new InvalidOperationException("Missing original foliage prefabs or low-poly rock pack.");

        Builder b=new Builder();
        b.terrain=terrain;b.data=terrain.terrainData;b.reef=reef;b.sea=ocean.BaseWaterLevel;
        b.origin=terrain.transform.position;b.size=b.data.size;
        GameObject mainRock=SuncrestMainRockCollision.FindRock(scene);
        if(mainRock==null)throw new InvalidOperationException("Suncrest Beach Main Rock is missing.");
        Renderer[] mainRenderers=mainRock.GetComponentsInChildren<Renderer>(true);
        bool hasBounds=false;
        Bounds mainBounds=new Bounds(mainRock.transform.position,Vector3.zero);
        foreach(Renderer renderer in mainRenderers)
        {
            if(renderer==null || !renderer.enabled)continue;
            if(!hasBounds){mainBounds=renderer.bounds;hasBounds=true;}
            else mainBounds.Encapsulate(renderer.bounds);
        }
        if(!hasBounds)throw new InvalidOperationException("Suncrest main rock has no renderable mesh; cannot determine its center.");
        b.center=mainBounds.center;
        b.radius=SuncrestRadius;
        int layers=b.data.alphamapLayers;
        for(int i=0;i<layers;i++)
        {
            TerrainLayer layer=b.data.terrainLayers[i];
            string label=layer!=null?layer.name.ToLowerInvariant():"";
            if(label.Contains("grass")||label.Contains("meadow")) b.grassLayers.Add(i);
            if(label.Contains("sand")||label.Contains("beach")) b.sandLayers.Add(i);
        }
        if(b.grassLayers.Count==0 || b.sandLayers.Count==0)
            throw new InvalidOperationException("Couldn't locate named Grass/Meadow and Sand/Beach terrain paint layers. Nothing changed.");
        b.alpha=b.data.GetAlphamaps(0,0,b.data.alphamapWidth,b.data.alphamapHeight);

        // Stage the replacement before deleting the old generated scenery.
        // Disabling the previous group excludes its colliders from placement.
        if(existing!=null)existing.SetActive(false);
        GameObject group=new GameObject(GroupName+" - BUILDING");
        Undo.RegisterCreatedObjectUndo(group,"Generate Suncrest vegetation and sand rocks");
        try
        {
            AddExisting(b,scene);
        }
        catch
        {
            Undo.DestroyObjectImmediate(group);
            if(existing!=null)existing.SetActive(true);
            throw;
        }
        var palms=new GameObject("Palms");palms.transform.SetParent(group.transform,false);
        var shrubs=new GameObject("SeaGrape Bushes");shrubs.transform.SetParent(group.transform,false);
        var grasses=new GameObject("Grass");grasses.transform.SetParent(group.transform,false);
        var rocks=new GameObject("Shore Rocks");rocks.transform.SetParent(group.transform,false);
        try
        {
            int a=ScatterPrefab(b,palm,palms.transform,"Palm",70,2600,.7f,1.2f,1.9f,false);
            int c=ScatterPrefab(b,shrub,shrubs.transform,"SeaGrape",95,2800,.7f,1.45f,.8f,true);
            int d=ScatterPrefab(b,grass,grasses.transform,"Grass",220,3300,.6f,1.6f,.34f,false);
            int e=ScatterIndividualLowPolyRocks(b,rocks.transform);
            if(a+c+d+e==0)throw new InvalidOperationException(
                "No matching coastal meadow or sand paint found within 150 m of the main rock.");
            var marker=new GameObject(VersionMarker);
            marker.transform.SetParent(group.transform,false);
            if(existing!=null)Undo.DestroyObjectImmediate(existing);
            group.name=GroupName;
            EditorSceneManager.MarkSceneDirty(scene);
            Physics.SyncTransforms();
            Debug.Log("[SUNCREST SCENERY] Created "+a+" palms, "+c+" SeaGrape bushes, "+
                d+" grass clumps, "+e+" individually movable low-poly sand rocks. "+
                "Protected "+b.protectedRocks.Count+" authored-rock colliders and "+
                b.dockBounds.Count+" marina renderers. Save PrototypeWorld (Ctrl+S).");
        }
        catch
        {
            Undo.DestroyObjectImmediate(group);
            if(existing!=null)existing.SetActive(true);
            throw;
        }
    }
}
#endif
