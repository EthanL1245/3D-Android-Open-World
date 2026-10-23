#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Populates the NEW Suncrest Beach, up to 195 metres from the world-space centre
/// of the artist's main Suncrest rock. Uses the ORIGINAL Palm / SeaGrape / Grass
/// prefabs and seeded, stratified placement across the entire island.
/// Seeds only near the main mountain and grows successive outward waves.
/// Beach-tip palms from the prior layout are not retained.
/// No terrain-paint dependency: any DRY, sufficiently level island terrain
/// may receive vegetation, right up to the shoreline. The artist's rocks,
/// their undersides, the marina/dock and existing vegetation are protected.
/// Never scatters limestone, beach stones or any other rocks.
/// </summary>
[InitializeOnLoad]
public static class SuncrestSceneryScatterEditor
{
    private const string ScenePath = "Assets/Scenes/PrototypeWorld.unity";
    private const string GroupName = SuncrestPlacedRockCollision.GeneratedName;
    private const string LegacyGroupName = SuncrestPlacedRockCollision.LegacyGeneratedName;
    private const string VersionMarker = "SUNCREST_FOLIAGE_MOUNTAIN_GROWTH_V8";
    private const string PrefabFolder = "Assets/_Game/Reef/Generated/";
    // Preserve the 150 m tree line that now follows the mountains.
    private const float AreaRadius = 150f;
    // The added planting pass can extend past that radius, but only on dry
    // land belonging to NEW Suncrest Beach around the main rock.
    private const float ExpandedRadius = 195f;
    private const int ExtraPalms = 260;
    private const float TreeSeparation = 6.2f;
    private const float BaseDistance = 12f;
    private const float FillGridStep = 7f;
    private const float MinimumRockClearance = .5f;
    private const float GridCellSize = 5f;
    // Only the visible trunk/base needs stable terrain. A full palm crown
    // does NOT have to sit inside a nearly-flat, dry 4m disk.
    private const float GroundProbeRadius = .35f;
    private const int TargetPalms = 180;
    private const int TargetBushes = 400;
    private const int TargetGrass = 950;
    private static bool generating;

    static SuncrestSceneryScatterEditor()
    {
        EditorApplication.delayCall += PopulateOnOpen;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += PopulateOnOpen;
    }

    private static bool Ready()
    {
        return !generating && !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling &&
            SceneManager.GetActiveScene().path == ScenePath;
    }

    private static void PopulateOnOpen()
    {
        if (!Ready()) return;
        GameObject existing = GameObject.Find(GroupName);
        GameObject legacy = GameObject.Find(LegacyGroupName);
        if (legacy == null && existing != null &&
            existing.transform.Find(VersionMarker) != null) return;
        try { Generate(); }
        catch(Exception error) { Debug.LogError("[SUNCREST FOLIAGE] " + error); }
    }

    [MenuItem("Tools/Open World/Manual Scenery/Inspect Generated Suncrest Foliage")]
    public static void InspectGenerated()
    {
        if(!Ready())
        {
            Debug.LogWarning("[SUNCREST FOLIAGE] Open PrototypeWorld in Edit Mode.");
            return;
        }
        GameObject group=GameObject.Find(GroupName);
        if(group==null)
        {
            Debug.LogWarning("[SUNCREST FOLIAGE] No generated foliage group "+
                "in the scene. Run Rebuild NEW Suncrest Beach Foliage Only.");
            return;
        }
        Transform palms=group.transform.Find("Palms - Individually Editable");
        int baseline=0,extra=0;
        if(palms!=null)
            foreach(Transform tree in palms)
            {
                if(tree.name.StartsWith("Palm_Grow_",StringComparison.Ordinal))
                    extra++;
                else if(tree.name.StartsWith("Palm_Base_",StringComparison.Ordinal))
                    baseline++;
            }
        bool current=group.transform.Find(VersionMarker)!=null;
        Debug.Log("[SUNCREST FOLIAGE INSPECT] V8 marker="+current+
            ", original palms="+baseline+", new inland palms="+extra+
            ". Selected group="+group.name+
            ". Expand Palms - Individually Editable in the Hierarchy to "+
            "locate each Palm_Base_#### and Palm_Grow_#### object.");
        Selection.activeGameObject=group;
        EditorGUIUtility.PingObject(group);
    }

    [MenuItem("Tools/Open World/Manual Scenery/Rebuild NEW Suncrest Beach Foliage Only")]
    public static void Rebuild()
    {
        if (!Ready())
        {
            Debug.LogWarning("[SUNCREST FOLIAGE] Open PrototypeWorld in Edit Mode.");
            return;
        }
        if(!EditorUtility.DisplayDialog("Rebuild New Suncrest Beach foliage",
            "Replace ONLY automatically generated Suncrest foliage, using dry terrain within " +
            "195 m of the main Suncrest rock? The original palms, SeaGrape bushes and grass " +
            "are distributed without regard to terrain texture. All hand-placed rocks, " +
            "terrain, marina and user-placed scenery stay intact.\n\n" +
            "Manually edited/deleted objects inside the generated foliage group will be replaced.",
            "Rebuild foliage","Cancel")) return;
        try { Generate(); }
        catch(Exception error) { Debug.LogError("[SUNCREST FOLIAGE] " + error); }
    }

    private sealed class Scatter
    {
        public Terrain terrain;
        public OceanWater water;
        public Vector3 center;
        public Vector3 terrainOrigin;
        public Vector3 terrainSize;
        public Transform marina;
        public readonly List<Collider> rockColliders = new List<Collider>();
        public readonly List<Bounds> marinaBounds = new List<Bounds>();
        public readonly List<Vector2> existingTrees = new List<Vector2>();
        public readonly List<Vector2> occupied = new List<Vector2>();
        public readonly List<float> radii = new List<float>();
        public readonly System.Random random = new System.Random(73191);
        private readonly List<Vector2> candidateCells = new List<Vector2>();
        private int nextCell;
        public readonly List<Vector3> treeSites = new List<Vector3>();
        public readonly List<Collider> mainRockColliders = new List<Collider>();

        public float Rand(float low, float high)
        {
            return Mathf.Lerp(low,high,(float)random.NextDouble());
        }

        public void PrepareGrid()
        {
            // Stratify the actual island: visit every 6m patch in randomized
            // order before revisiting any patch. Unlike random radial samples
            // this prevents narrow permitted land from getting all the foliage.
            int extent=Mathf.CeilToInt(AreaRadius/GridCellSize);
            for(int x=-extent;x<=extent;x++)
            for(int z=-extent;z<=extent;z++)
            {
                Vector2 offset=new Vector2(
                    (x+.5f)*GridCellSize,(z+.5f)*GridCellSize);
                if(offset.sqrMagnitude<AreaRadius*AreaRadius)
                    candidateCells.Add(offset);
            }
            ShuffleGrid();
        }

        private void ShuffleGrid()
        {
            for(int i=candidateCells.Count-1;i>0;i--)
            {
                int j=random.Next(i+1);
                Vector2 t=candidateCells[i];
                candidateCells[i]=candidateCells[j];
                candidateCells[j]=t;
            }
            nextCell=0;
        }

        public Vector3 Candidate()
        {
            // After trees have grown away from the mountain, grow the
            // shrubs and grass around those trees (NOT at the outer tips).
            if(treeSites.Count>0)
            {
                Vector3 t=treeSites[random.Next(treeSites.Count)];
                float angle=Rand(0f,Mathf.PI*2f);
                float d=Rand(1f,8f);
                return t+new Vector3(Mathf.Cos(angle)*d,0f,
                                     Mathf.Sin(angle)*d);
            }
            if(candidateCells.Count==0)throw new InvalidOperationException(
                "Suncrest foliage grid is empty.");
            if(nextCell==candidateCells.Count)ShuffleGrid();
            Vector2 cell=candidateCells[nextCell++];
            return center+new Vector3(
                cell.x+Rand(-GridCellSize*.48f,GridCellSize*.48f),0f,
                cell.y+Rand(-GridCellSize*.48f,GridCellSize*.48f));
        }

        public bool TreeSpace(Vector3 p)
        {
            Vector2 v=new Vector2(p.x,p.z);
            foreach(Vector3 q in treeSites)
                if((v-new Vector2(q.x,q.z)).sqrMagnitude<
                   TreeSeparation*TreeSeparation)return false;
            return true;
        }

        public float MountainDistance(Vector3 point,out Vector2 away)
        {
            Vector3 here=new Vector3(point.x,Height(point)+1f,point.z);
            float best=float.MaxValue;
            Vector3 nearest=Vector3.zero;
            foreach(Collider c in mainRockColliders)
            {
                if(c==null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                Vector3 q=c.ClosestPoint(here);
                float d=Vector3.Distance(q,here);
                if(d<best){best=d;nearest=q;}
            }
            away=new Vector2(here.x-nearest.x,here.z-nearest.z);
            if(away.sqrMagnitude<.01f)
                away=new Vector2(point.x-center.x,point.z-center.z);
            if(away.sqrMagnitude<.01f)away=Vector2.right;
            away.Normalize();
            return best;
        }

        public bool OnTerrain(Vector3 point, float margin)
        {
            return point.x >= terrainOrigin.x+margin &&
                   point.z >= terrainOrigin.z+margin &&
                   point.x <= terrainOrigin.x+terrainSize.x-margin &&
                   point.z <= terrainOrigin.z+terrainSize.z-margin;
        }

        public float Height(Vector3 point)
        {
            return terrain.SampleHeight(point) + terrainOrigin.y;
        }

        public int noLandRejects,rockRejects,dockRejects,spacingRejects;
        public int totalCandidates;

        public bool DryLand(Vector3 point,float stemRadius)
        {
            // Test the ROOT, not the palm-crown footprint. The old 8-point
            // ring / steepness restrictions filtered out nearly every patch
            // around the mountain. These plants can be grounded onto slopes.
            float edge=Mathf.Max(.1f,stemRadius);
            Vector2 horizontal=new Vector2(point.x-center.x,point.z-center.z);
            if(horizontal.sqrMagnitude>(ExpandedRadius-edge)*(ExpandedRadius-edge) ||
               !OnTerrain(point,edge+.2f))
                return false;
            float sea=water.BaseWaterLevel;
            float height=Height(point);
            if(height<sea+.06f)return false;

            // Root-width probes keep stems on land, but canopies are free
            // to extend toward the shoreline or slope.
            float testRadius=Mathf.Min(GroundProbeRadius,
                Mathf.Max(.1f,stemRadius));
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*.5f;
                Vector3 sample=point+new Vector3(Mathf.Cos(angle)*testRadius,
                    0f,Mathf.Sin(angle)*testRadius);
                if(!OnTerrain(sample,.05f) ||
                   Height(sample)<sea+.03f) return false;
            }
            return true;
        }

        private bool TooCloseToRock(Vector3 point,float stemRadius,
            float stemHeight)
        {
            // Earlier versions traced DOWN from the top of every mountain
            // and excluded the ground below its entire horizontal projection.
            // Here, test only real rock surfaces within half a metre of
            // the vertical STEM near the terrain. A mountain rising several
            // metres overhead does not cast an artificial exclusion shadow.
            float ground=Height(point);
            float clearance=MinimumRockClearance+stemRadius;
            float maxHeight=Mathf.Min(stemHeight,2.2f);
            float distSq=clearance*clearance;
            for(int i=0;i<rockColliders.Count;i++)
            {
                Collider c=rockColliders[i];
                if(c==null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                Bounds bounds=c.bounds;
                if(bounds.min.x>point.x+clearance ||
                   bounds.max.x<point.x-clearance ||
                   bounds.min.z>point.z+clearance ||
                   bounds.max.z<point.z-clearance ||
                   bounds.max.y<ground-.3f ||
                   bounds.min.y>ground+maxHeight+clearance)
                    continue;

                // A single narrow trunk clearance allows growth directly
                // beside rock faces, instead of discarding whole vegetation
                // footprints. ClosestPoint uses the actual mesh collider.
                for(int j=0;j<4;j++)
                {
                    float h=j==0?.08f:j==1?.55f:
                        j==2?1.15f:maxHeight;
                    if(h>maxHeight)h=maxHeight;
                    Vector3 baseSample=new Vector3(point.x,ground+h,point.z);
                    Vector3 nearest=c.ClosestPoint(baseSample);
                    if((nearest-baseSample).sqrMagnitude<distSq)
                    {
                        // Some buried meshes intersect terrain internally.
                        // If the visible top of the rock at this XZ lies
                        // BELOW the terrain, it must not block vegetation.
                        RaycastHit topHit;
                        float startY=bounds.max.y+.5f;
                        bool hasTop=c.Raycast(new Ray(
                            new Vector3(point.x,startY,point.z),
                            Vector3.down),out topHit,
                            Mathf.Max(1f,startY-ground+2f));
                        if(hasTop && topHit.point.y<ground-.08f)
                            continue;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Available(Vector3 point,float radius,
            float stemRadius,float clearHeight)
        {
            Vector2 position=new Vector2(point.x,point.z);
            for(int i=0;i<occupied.Count;i++)
            {
                float required=radius+radii[i];
                if((position-occupied[i]).sqrMagnitude < required*required)
                {
                    spacingRejects++;
                    return false;
                }
            }
            for(int i=0;i<existingTrees.Count;i++)
            {
                float min=radius+2f;
                if((position-existingTrees[i]).sqrMagnitude<min*min)
                {
                    spacingRejects++;
                    return false;
                }
            }
            if(marina!=null)
            {
                // Extend the avoidance envelope to the dock built at runtime.
                Vector3 local=marina.InverseTransformPoint(point);
                if(Mathf.Abs(local.x)<radius+9f &&
                   local.z>-16f-radius && local.z<51f+radius)
                {
                    dockRejects++;
                    return false;
                }
            }
            for(int i=0;i<marinaBounds.Count;i++)
            {
                Bounds b=marinaBounds[i];
                if(point.x>=b.min.x-radius-3f &&
                   point.x<=b.max.x+radius+3f &&
                   point.z>=b.min.z-radius-3f &&
                   point.z<=b.max.z+radius+3f)
                {
                    dockRejects++;
                    return false;
                }
            }
            if(TooCloseToRock(point,stemRadius,clearHeight))
            {
                rockRejects++;
                return false;
            }
            return true;
        }

        public void Claim(Vector3 point,float radius)
        {
            occupied.Add(new Vector2(point.x,point.z));
            radii.Add(radius);
        }

        public float LowestGround(Vector3 point,float footprint)
        {
            float lowest=Height(point);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;
                lowest=Mathf.Min(lowest,Height(point+new Vector3(
                    Mathf.Cos(a)*footprint,0f,Mathf.Sin(a)*footprint)));
            }
            return lowest;
        }
    }

    private static void FindProtectedGeometry(Scatter s,Scene scene)
    {
        SuncrestMainRockCollision.EnsureColliders(scene);
        SuncrestPlacedRockCollision.EnsureColliders(scene);
        Physics.SyncTransforms();
        var unique=new HashSet<Collider>();

        GameObject main=SuncrestMainRockCollision.FindRock(scene);
        if(main!=null)
            foreach(Collider c in main.GetComponentsInChildren<Collider>(true))
                if(c!=null && c.enabled && !c.isTrigger &&
                    c.gameObject.activeInHierarchy && unique.Add(c))
                {
                    s.rockColliders.Add(c);
                    s.mainRockColliders.Add(c);
                }

        foreach(GameObject rock in SuncrestPlacedRockCollision.PlacedRocks(scene))
            foreach(Collider c in rock.GetComponentsInChildren<Collider>(true))
                if(c!=null && c.enabled && !c.isTrigger &&
                    c.gameObject.activeInHierarchy && unique.Add(c))
                    s.rockColliders.Add(c);

        // Also guard any other active meshes explicitly placed by the artist
        // under the "DO NOT MODIFY" group, even if their names omit "rock".
        GameObject authored=GameObject.Find(
            "USER PLACED SCENERY - DO NOT MODIFY BY CODE");
        if(authored!=null)
            foreach(Collider c in authored.GetComponentsInChildren<Collider>(true))
                if(c!=null && c.enabled && !c.isTrigger &&
                    c.gameObject.activeInHierarchy && unique.Add(c))
                    s.rockColliders.Add(c);

        GameObject dock=GameObject.Find("MarinaShop");
        s.marina=dock!=null?dock.transform:null;
        if(dock!=null)
            foreach(Renderer r in dock.GetComponentsInChildren<Renderer>(true))
                if(r!=null && r.enabled && r.gameObject.activeInHierarchy)
                    s.marinaBounds.Add(r.bounds);

        GameObject decor=GameObject.Find("WorldDecor");
        if(decor!=null && decor.activeInHierarchy)
        {
            Transform trees=decor.transform.Find("Trees");
            if(trees!=null)
                foreach(Transform tree in trees)
                    if(tree.gameObject.activeInHierarchy)
                        s.existingTrees.Add(new Vector2(tree.position.x,tree.position.z));
        }
    }

    private static int PlacePrefabs(Scatter s,GameObject prefab,Transform group,
        string prefix,int goal,int tries,float minScale,float maxScale,
        float footprintRadius,bool shrub,float stemRadius,float clearHeight)
    {
        int placed=0;
        for(int attempt=0;attempt<tries && placed<goal;attempt++)
        {
            Vector3 point=s.Candidate();
            float scale=s.Rand(minScale,maxScale);
            float radius=footprintRadius*scale;
            float stem=stemRadius*scale;
            s.totalCandidates++;
            if(!s.DryLand(point,stem))
            {
                s.noLandRejects++;
                continue;
            }
            if(!s.Available(point,radius,stem,clearHeight*scale))
                continue;

            GameObject plant=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if(plant==null)throw new InvalidOperationException(
                "Failed to instantiate original Suncrest prefab: "+prefab.name);
            plant.name=prefix+"_"+placed.ToString("D4");
            plant.transform.SetParent(group,false);
            plant.transform.SetPositionAndRotation(
                new Vector3(point.x,
                    shrub?s.LowestGround(point,radius)-.10f*scale:s.Height(point),
                    point.z),Quaternion.Euler(0f,s.Rand(0f,360f),0f));
            plant.transform.localScale=Vector3.one*scale;
            s.Claim(point,shrub?radius:Mathf.Max(.25f,radius*.7f));
            placed++;
        }
        return placed;
    }

    private struct GrowthTip
    {
        public Vector3 pos;
        public Vector2 outward;
        public GrowthTip(Vector3 p,Vector2 d){pos=p;outward=d;}
    }

    private static bool AddPalm(Scatter s,GameObject prefab,Transform parent,
        Vector3 position,float scale,string name)
    {
        float stem=.30f*scale,canopy=1.9f*scale;
        s.totalCandidates++;
        if(!s.DryLand(position,stem))
        {
            s.noLandRejects++;return false;
        }
        if(!s.TreeSpace(position))
        {
            s.spacingRejects++;return false;
        }
        if(!s.Available(position,canopy,stem,3.5f*scale))return false;
        GameObject tree=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if(tree==null)throw new InvalidOperationException("Palm prefab could not be instantiated.");
        tree.name=name;
        tree.transform.SetParent(parent,false);
        tree.transform.SetPositionAndRotation(
            new Vector3(position.x,s.Height(position),position.z),
            Quaternion.Euler(0f,s.Rand(0f,360f),0f));
        tree.transform.localScale=Vector3.one*scale;
        s.Claim(position,Mathf.Max(.25f,canopy*.7f));
        s.treeSites.Add(position);
        return true;
    }

    private static int StartAtMountain(Scatter s,GameObject prefab,
        Transform group,GameObject oldGroup)
    {
        int kept=0;
        if(oldGroup!=null)
        {
            Transform palms=oldGroup.transform.Find("Palms - Individually Editable");
            if(palms!=null)
            {
                var nearby=new List<Transform>();
                foreach(Transform child in palms)
                {
                    Vector2 dir;
                    if(s.MountainDistance(child.position,out dir)<=BaseDistance)
                        nearby.Add(child);
                }
                nearby.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));
                foreach(Transform t in nearby)
                {
                    float size=Mathf.Clamp(t.lossyScale.x,.7f,1.3f);
                    if(AddPalm(s,prefab,group,t.position,size,
                        "Palm_Base_"+kept.ToString("D4")))kept++;
                }
            }
        }
        // If Unity did not save the previous autogenerated foliage, seed
        // directly from suitable dry points NEXT TO the main mountain.
        if(kept<24)
        {
            var rng=new System.Random(84197);
            var sites=new List<Vector2>();
            const float cell=4.5f;
            int max=Mathf.CeilToInt(ExpandedRadius/cell);
            for(int x=-max;x<=max;x++)
            for(int z=-max;z<=max;z++)
            {
                Vector2 q=new Vector2((x+.5f)*cell,(z+.5f)*cell);
                if(q.sqrMagnitude<ExpandedRadius*ExpandedRadius)
                    sites.Add(q);
            }
            for(int i=sites.Count-1;i>0;i--)
            {
                int j=rng.Next(i+1);
                Vector2 tmp=sites[i];sites[i]=sites[j];sites[j]=tmp;
            }
            foreach(Vector2 q in sites)
            {
                if(kept>=52)break;
                Vector3 p=new Vector3(s.center.x+q.x,0f,s.center.z+q.y);
                Vector2 away;
                if(s.MountainDistance(p,out away)>BaseDistance)continue;
                float scale=Mathf.Lerp(.7f,1.15f,(float)rng.NextDouble());
                if(AddPalm(s,prefab,group,p,scale,
                    "Palm_Base_"+kept.ToString("D4")))kept++;
            }
        }
        return kept;
    }

    // Grow in breadth-first waves, each step 7-11m FROM the previous wave.
    // Unlike the V7 full-island scatter, this cannot jump to beach tips and
    // make dense outlying forests before the inner clearing is occupied.
    private static int GrowFromMountain(Scatter s,GameObject prefab,
        Transform group,out int waveCount)
    {
        var rng=new System.Random(100117);
        var frontier=new List<GrowthTip>();
        foreach(Vector3 p in s.treeSites)
        {
            Vector2 outward;
            s.MountainDistance(p,out outward);
            frontier.Add(new GrowthTip(p,outward));
        }
        int grown=0;waveCount=0;
        for(int wave=0;wave<14 && grown<ExtraPalms && frontier.Count>0;wave++)
        {
            // Shuffle growth tips before every wave to avoid favoring one
            // side of the mountain every time.
            for(int i=frontier.Count-1;i>0;i--)
            {
                int j=rng.Next(i+1);
                GrowthTip tmp=frontier[i];frontier[i]=frontier[j];frontier[j]=tmp;
            }
            var next=new List<GrowthTip>();
            foreach(GrowthTip tip in frontier)
            {
                if(next.Count>=30 || grown>=ExtraPalms)break;
                for(int attempt=0;attempt<16;attempt++)
                {
                    float angle=Mathf.Lerp(-105f,105f,
                        (float)rng.NextDouble())*Mathf.Deg2Rad;
                    float cs=Mathf.Cos(angle),sn=Mathf.Sin(angle);
                    Vector2 heading=new Vector2(
                        tip.outward.x*cs-tip.outward.y*sn,
                        tip.outward.x*sn+tip.outward.y*cs);
                    float step=Mathf.Lerp(7f,11f,
                        (float)rng.NextDouble());
                    Vector3 p=tip.pos+new Vector3(
                        heading.x*step,0f,heading.y*step);
                    float scale=Mathf.Lerp(.72f,1.16f,
                        (float)rng.NextDouble());
                    if(!AddPalm(s,prefab,group,p,scale,
                        "Palm_Grow_"+grown.ToString("D4")))continue;
                    next.Add(new GrowthTip(p,heading));
                    grown++;
                    break;
                }
            }
            if(next.Count==0)break;
            frontier=next;waveCount++;
        }
        return grown;
    }

    private static void Generate()
    {
        if(!Ready())return;
        generating=true;
        GameObject old=GameObject.Find(LegacyGroupName);
        GameObject previous=GameObject.Find(GroupName);
        GameObject staged=null;
        try
        {
            Scene scene=SceneManager.GetActiveScene();
            ReefZone reef=UnityEngine.Object.FindFirstObjectByType<ReefZone>();
            Terrain terrain=reef!=null?reef.GetComponentInChildren<Terrain>(true):null;
            if(terrain==null)terrain=Terrain.activeTerrain;
            OceanWater water=UnityEngine.Object.FindFirstObjectByType<OceanWater>();
            GameObject main=SuncrestMainRockCollision.FindRock(scene);
            if(terrain==null || terrain.terrainData==null || water==null || main==null)
                throw new InvalidOperationException(
                    "Missing active Suncrest Terrain, OceanWater or main Suncrest rock.");

            GameObject palm=AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabFolder+"Palm.prefab");
            GameObject shrub=AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabFolder+"SeaGrape.prefab");
            GameObject grass=AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabFolder+"Grass.prefab");
            if(palm==null || shrub==null || grass==null)
                throw new InvalidOperationException(
                    "Missing one of the three ORIGINAL Suncrest foliage prefabs.");

            // A renderer-bounds centroid follows the new hand-positioned main
            // rock even when its imported prefab pivot is far from its mesh.
            Bounds box=new Bounds(main.transform.position,Vector3.zero);
            bool found=false;
            foreach(Renderer r in main.GetComponentsInChildren<Renderer>(true))
            {
                if(r==null || !r.enabled)continue;
                if(!found){box=r.bounds;found=true;}
                else box.Encapsulate(r.bounds);
            }
            if(!found)throw new InvalidOperationException(
                "Could not locate rendered bounds of Suncrest Beach Main Rock.");

            Scatter scatter=new Scatter();
            scatter.center=box.center;
            scatter.terrain=terrain;
            scatter.water=water;
            scatter.terrainOrigin=terrain.transform.position;
            scatter.terrainSize=terrain.terrainData.size;
            scatter.PrepareGrid();

            if(old!=null)old.SetActive(false);
            if(previous!=null)previous.SetActive(false);
            staged=new GameObject(GroupName+" - BUILDING");
            Undo.RegisterCreatedObjectUndo(staged,
                "Populate new Suncrest Beach with original foliage");

            FindProtectedGeometry(scatter,scene);
            var palms=new GameObject("Palms - Individually Editable");
            palms.transform.SetParent(staged.transform,false);
            var bushes=new GameObject("SeaGrape Bushes - Individually Editable");
            bushes.transform.SetParent(staged.transform,false);
            var grasses=new GameObject("Grass - Individually Editable");
            grasses.transform.SetParent(staged.transform,false);

            // Old beach-tip clumps are NOT copied. Only palms physically
            // near the main mountain survive, then successive waves grow
            // outward across connected dry ground.
            int baseCount=StartAtMountain(scatter,palm,palms.transform,
                previous!=null?previous:old);
            int waves;
            int grown=GrowFromMountain(scatter,palm,palms.transform,out waves);
            // Never silently replace the beach with sparse shoreline clumps
            // again if mountain seeding / expansion fails in Unity.
            if(baseCount<6 || grown<12)
                throw new InvalidOperationException(
                    "Mountain-growth failed: only "+baseCount+" base palms and "+
                    grown+" outward palms. The previous generated foliage "+
                    "was restored. Check that the active terrain and main "+
                    "rock colliders match the visible Suncrest island.");
            int b=PlacePrefabs(scatter,shrub,bushes.transform,
                "SeaGrape",TargetBushes,60000,.7f,1.45f,.8f,true,.23f,1.6f);
            int c=PlacePrefabs(scatter,grass,grasses.transform,
                "Grass",TargetGrass,90000,.6f,1.6f,.34f,false,.10f,.8f);
            if(baseCount+b+c+grown==0)throw new InvalidOperationException(
                "No valid dry island terrain was found near the main Suncrest rock. "+
                "Check the terrain heights relative to the ocean.");

            if(baseCount==0 || grown<40)
                Debug.LogWarning("[SUNCREST FOLIAGE] V8 produced "+
                    baseCount+" mountain seeds / "+grown+" outward palms. "+
                    "Check whether the terrain and main rock MeshColliders "+
                    "match the visible island; no beach-tip clumps were retained.");
            var marker=new GameObject(VersionMarker);
            marker.transform.SetParent(staged.transform,false);
            if(old!=null)Undo.DestroyObjectImmediate(old);
            if(previous!=null)Undo.DestroyObjectImmediate(previous);
            staged.name=GroupName;
            EditorSceneManager.MarkSceneDirty(scene);
            Physics.SyncTransforms();
            int interior=0;
            float maxReach=0f;
            foreach(Vector3 position in scatter.treeSites)
            {
                Vector2 direction;
                float d=scatter.MountainDistance(position,out direction);
                if(d>20f)interior++;
                maxReach=Mathf.Max(maxReach,d);
            }
            Debug.Log("[SUNCREST FOLIAGE] V8: "+(baseCount+grown)+" palms ("+
                baseCount+" mountain-base + "+grown+" outward-grown through "+
                waves+" waves), "+interior+
                " palms >20m from the main mountain, farthest reach "+
                maxReach.ToString("F1")+"m, "+
                b+" SeaGrape bushes / "+c+
                " grass clumps. Candidate attempts "+
                scatter.totalCandidates+": unsuitable land "+scatter.noLandRejects+
                ", rock clearance "+scatter.rockRejects+
                ", marina/dock "+scatter.dockRejects+
                ", plant spacing "+scatter.spacingRejects+
                ". Used actual stem clearance of 0.5m from rock meshes, "+
                "not crowns / mountain-height columns. No rocks generated. "+
                "Save the scene with Ctrl+S.");
        }
        catch
        {
            if(staged!=null)Undo.DestroyObjectImmediate(staged);
            if(old!=null)old.SetActive(true);
            if(previous!=null)previous.SetActive(true);
            throw;
        }
        finally { generating=false; }
    }
}
#endif
