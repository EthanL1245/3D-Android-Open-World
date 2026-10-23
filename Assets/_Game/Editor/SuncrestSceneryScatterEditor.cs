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
/// Seeds only near the main mountain and expands into complete sorted rings.
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
    private const string VersionMarker = "SUNCREST_FOLIAGE_VISIBLE_ROCK_SURFACES_V10";
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
        // The generated group has been extensively hand-edited. Never
        // silently overwrite saved trees/grass on Unity scene open, even
        // if a generation marker changes in a future update.
        if (existing != null || legacy != null) return;
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
        Debug.Log("[SUNCREST FOLIAGE INSPECT] V10 marker="+current+
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
        public Vector3 playerSpawn;
        public bool hasPlayerSpawn;
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
            // Respect the 10 m clear spawn zone even on an explicit manual
            // rebuild. Only new generated placements are affected.
            if(hasPlayerSpawn)
            {
                Vector2 spawnOffset=new Vector2(
                    point.x-playerSpawn.x,point.z-playerSpawn.z);
                if(spawnOffset.sqrMagnitude<100f)return false;
            }
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

        private static readonly Vector2[] RadialTests =
        {
            new Vector2(1f,0f),new Vector2(-1f,0f),
            new Vector2(0f,1f),new Vector2(0f,-1f),
            new Vector2(.707107f,.707107f),
            new Vector2(-.707107f,.707107f),
            new Vector2(.707107f,-.707107f),
            new Vector2(-.707107f,-.707107f)
        };

        public readonly Dictionary<string,int> rockRejectionsByCollider =
            new Dictionary<string,int>();

        private bool TooCloseToRock(Vector3 point,float stemRadius,
            float stemHeight)
        {
            // Rock meshes such as the massive Suncrest main mountain can
            // overlap/bury themselves beneath the Terrain. ClosestPoint()
            // returns THE POINT ITSELF when inside a solid MeshCollider:
            // that is not evidence of a rock SURFACE near a visible tree.
            //
            // Instead, use actual triangle raycast contacts at the exposed
            // trunk: no rocks within 0.5m and no low overhang intersecting
            // the tree's base. Ignore distant/buried mountain geometry that
            // made 432/485 otherwise-dry V9 candidates appear 'blocked'.
            float ground=Height(point);
            float clearance=MinimumRockClearance+stemRadius;
            float upper=Mathf.Min(stemHeight,2.2f);
            float scanRadius=clearance+.85f;
            for(int i=0;i<rockColliders.Count;i++)
            {
                Collider c=rockColliders[i];
                if(c==null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                Bounds b=c.bounds;
                if(b.min.x>point.x+scanRadius ||
                   b.max.x<point.x-scanRadius ||
                   b.min.z>point.z+scanRadius ||
                   b.max.z<point.z-scanRadius ||
                   b.max.y<ground-.2f ||
                   b.min.y>ground+upper+.5f)
                    continue;

                bool contact=false;
                RaycastHit hit;
                // Check visible surface triangles along the stem's first
                // 2.2m, where a solid rock wall could intersect the palm.
                for(int h=0;h<4 && !contact;h++)
                {
                    float y=ground+Mathf.Min(upper,h==0?.15f:
                        h==1?.7f:h==2?1.45f:upper);
                    for(int k=0;k<RadialTests.Length;k++)
                    {
                        Vector2 direction=RadialTests[k];
                        Vector3 from=new Vector3(
                            point.x+direction.x*scanRadius,y,
                            point.z+direction.y*scanRadius);
                        Vector3 toward=new Vector3(-direction.x,0f,-direction.y);
                        if(!c.Raycast(new Ray(from,toward),out hit,
                            scanRadius*2f))continue;
                        Vector2 hitFromStem=new Vector2(
                            hit.point.x-point.x,hit.point.z-point.z);
                        if(hitFromStem.sqrMagnitude<=clearance*clearance)
                        {
                            contact=true;
                            break;
                        }
                    }
                }

                // Detect truly LOW overhead mesh (inside the plant height),
                // not an entire mountain far above it. A rock triangle
                // crossing the trunk cannot be accepted as terrain.
                if(!contact)
                {
                    Vector3 from=new Vector3(point.x,
                        ground+upper+.05f,point.z);
                    if(c.Raycast(new Ray(from,Vector3.down),out hit,
                        upper+.35f) &&
                        hit.point.y>=ground-.15f)
                        contact=true;
                }

                // A nonzero ClosestPoint offset is evidence of a nearby
                // exterior surface. Zero offset can mean the point lies
                // DEEP INSIDE a collider (common with intersecting FBX
                // mountains) and must not veto a visible dry terrain patch.
                if(!contact)
                {
                    for(int h=0;h<3;h++)
                    {
                        Vector3 test=new Vector3(point.x,
                            ground+Mathf.Min(upper,h==0?.2f:h==1?1f:2f),
                            point.z);
                        Vector3 nearest=c.ClosestPoint(test);
                        float dist=(nearest-test).sqrMagnitude;
                        if(dist>1e-6f && dist<clearance*clearance)
                        {
                            contact=true;
                            break;
                        }
                    }
                }

                if(contact)
                {
                    string name=c.name;
                    int n;
                    rockRejectionsByCollider.TryGetValue(name,out n);
                    rockRejectionsByCollider[name]=n+1;
                    return true;
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

    private struct GrowthCandidate
    {
        public Vector3 point;
        public float distanceFromSeed;
        public float sortKey;
        public GrowthCandidate(Vector3 p,float d,float key)
        {
            point=p;distanceFromSeed=d;sortKey=key;
        }
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

    // Expand outward from the preserved mountain-base palms using complete
    // concentric 5 m sampling cells ordered by distance from ANY mountain seed.
    // Do not depend on each previous growth wave having a viable child.
    // The old V8 frontier died after 3 palms because it ran out of accepted
    // tips, even though accessible terrain remained beyond them.
    private static int GrowFromMountain(Scatter s,GameObject prefab,
        Transform group,out int ringCount,out int dryCandidates)
    {
        var baseTrees=new List<Vector3>(s.treeSites);
        if(baseTrees.Count==0)
        {
            ringCount=0;dryCandidates=0;return 0;
        }

        const float grid=5f;
        const float minSeedDistance=5.0f;
        const float maxSeedDistance=95f;
        var rng=new System.Random(100117);
        var candidates=new List<GrowthCandidate>();
        int extent=Mathf.CeilToInt(ExpandedRadius/grid);
        float maxAreaSq=ExpandedRadius*ExpandedRadius;
        float minSq=minSeedDistance*minSeedDistance;
        float maxSq=maxSeedDistance*maxSeedDistance;

        for(int x=-extent;x<=extent;x++)
        for(int z=-extent;z<=extent;z++)
        {
            // Stratified jitter prevents grid-looking tree rows and gives
            // each part of the usable island an opportunity to be sampled.
            Vector3 point=new Vector3(
                s.center.x+(x+.5f)*grid+s.Rand(-2f,2f),0f,
                s.center.z+(z+.5f)*grid+s.Rand(-2f,2f));
            Vector2 centerOffset=new Vector2(point.x-s.center.x,
                point.z-s.center.z);
            if(centerOffset.sqrMagnitude>maxAreaSq)continue;

            float nearSq=float.MaxValue;
            foreach(Vector3 baseTree in baseTrees)
            {
                float dx=point.x-baseTree.x,dz=point.z-baseTree.z;
                float d=dx*dx+dz*dz;
                if(d<nearSq)nearSq=d;
            }
            if(nearSq<minSq || nearSq>maxSq)continue;
            float near=Mathf.Sqrt(nearSq);
            // Complete each band before moving substantially farther from
            // the mountain. A little noise avoids concentric-looking rows.
            float ranking=near+Mathf.Lerp(-1f,1f,(float)rng.NextDouble());
            candidates.Add(new GrowthCandidate(point,near,ranking));
        }
        candidates.Sort((a,b)=>a.sortKey.CompareTo(b.sortKey));

        int placed=0;
        dryCandidates=0;
        float furthest=0f;
        foreach(GrowthCandidate candidate in candidates)
        {
            if(placed>=ExtraPalms)break;
            // Use stable randomized scales, not the direction of a mesh
            // collider normal, to decide where the next tree should go.
            float scale=Mathf.Lerp(.72f,1.16f,(float)rng.NextDouble());
            float stem=.3f*scale;
            s.totalCandidates++;
            if(!s.DryLand(candidate.point,stem))
            {
                s.noLandRejects++;continue;
            }
            dryCandidates++;
            // Do NOT reject an entire neighborhood when one candidate fails.
            // Keep evaluating independent cells farther from the mountain.
            if(!s.TreeSpace(candidate.point))
            {
                s.spacingRejects++;continue;
            }
            if(!s.Available(candidate.point,1.9f*scale,stem,3.5f*scale))
                continue;

            GameObject tree=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if(tree==null)throw new InvalidOperationException(
                "Original Suncrest Palm prefab could not be instantiated.");
            tree.name="Palm_Grow_"+placed.ToString("D4");
            tree.transform.SetParent(group,false);
            tree.transform.SetPositionAndRotation(
                new Vector3(candidate.point.x,s.Height(candidate.point),
                    candidate.point.z),
                Quaternion.Euler(0f,(float)rng.NextDouble()*360f,0f));
            tree.transform.localScale=Vector3.one*scale;
            s.Claim(candidate.point,Mathf.Max(.25f,1.9f*scale*.7f));
            s.treeSites.Add(candidate.point);
            furthest=Mathf.Max(furthest,candidate.distanceFromSeed);
            placed++;
        }
        // The diagnostic counts distance bands reached, not BFS waves.
        ringCount=Mathf.CeilToInt(furthest/5f);
        Debug.Log("[SUNCREST FOLIAGE] Mountain-grid candidate diagnostics: "+
            candidates.Count+" sites within 5-95m of "+baseTrees.Count+
            " mountain seeds, "+dryCandidates+" dry candidates scanned, "+
            placed+" new palms, farthest planted "+
            furthest.ToString("F1")+"m from nearest seed.");
        return placed;
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
            GameObject spawn=GameObject.Find("SuncrestArrival");
            if(spawn==null)spawn=GameObject.Find("Player");
            if(spawn!=null)
            {
                scatter.playerSpawn=spawn.transform.position;
                scatter.hasPlayerSpawn=true;
            }
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

            // Old beach-tip clumps are NOT copied. Mountain palms are
            // retained as seeds for sorted inland candidate rings; a
            // blocked trial no longer terminates the entire expansion.
            int baseCount=StartAtMountain(scatter,palm,palms.transform,
                previous!=null?previous:old);
            int waves;
            int drySites;
            int grown=GrowFromMountain(scatter,palm,palms.transform,
                out waves,out drySites);
            // Never silently replace the beach with sparse shoreline clumps
            // again if mountain seeding / expansion fails in Unity.
            if(baseCount<6 || grown<12)
            {
                var rejectionTypes=new List<KeyValuePair<string,int>>(
                    scatter.rockRejectionsByCollider);
                rejectionTypes.Sort((a,b)=>b.Value.CompareTo(a.Value));
                var topRocks=new System.Text.StringBuilder();
                for(int i=0;i<Mathf.Min(6,rejectionTypes.Count);i++)
                    topRocks.Append(" [").Append(rejectionTypes[i].Key)
                        .Append(": ").Append(rejectionTypes[i].Value).Append("]");
                Debug.LogWarning("[SUNCREST FOLIAGE] Rock blockers "+
                    topRocks.ToString());
                throw new InvalidOperationException(
                    "Mountain rings generated only "+baseCount+
                    " mountain-base palms and "+grown+" expanded palms from "+
                    drySites+" dry sampled candidate sites. Existing foliage "+
                    "was restored. Candidates rejected: dry land "+
                    scatter.noLandRejects+", existing plant spacing "+
                    scatter.spacingRejects+", rock colliders "+
                    scatter.rockRejects+", dock/marina "+
                    scatter.dockRejects+".");
            }
            int b=PlacePrefabs(scatter,shrub,bushes.transform,
                "SeaGrape",TargetBushes,60000,.7f,1.45f,.8f,true,.23f,1.6f);
            int c=PlacePrefabs(scatter,grass,grasses.transform,
                "Grass",TargetGrass,90000,.6f,1.6f,.34f,false,.10f,.8f);
            if(baseCount+b+c+grown==0)throw new InvalidOperationException(
                "No valid dry island terrain was found near the main Suncrest rock. "+
                "Check the terrain heights relative to the ocean.");

            if(baseCount==0 || grown<40)
                Debug.LogWarning("[SUNCREST FOLIAGE] V10 produced "+
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
            Debug.Log("[SUNCREST FOLIAGE] V10: "+(baseCount+grown)+" palms ("+
                baseCount+" mountain-base + "+grown+" outward-grown through "+
                waves+" ordered 5m distance rings, "+
                drySites+" dry growth sites), "+interior+
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
