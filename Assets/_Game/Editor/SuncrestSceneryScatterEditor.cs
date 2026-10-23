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
/// Extra palms are added BEFORE bushes/grass can occupy otherwise empty land.
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
    private const string VersionMarker = "SUNCREST_FOLIAGE_FILL_INTERIOR_V7";
    private const string PrefabFolder = "Assets/_Game/Reef/Generated/";
    // Preserve the 150 m tree line that now follows the mountains.
    private const float AreaRadius = 150f;
    // The added planting pass can extend past that radius, but only on dry
    // land belonging to NEW Suncrest Beach around the main rock.
    private const float ExpandedRadius = 195f;
    private const int ExtraPalms = 210;
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
                if(tree.name.StartsWith("Palm_Fill_",StringComparison.Ordinal))
                    extra++;
                else if(tree.name.StartsWith("Palm_",StringComparison.Ordinal))
                    baseline++;
            }
        bool current=group.transform.Find(VersionMarker)!=null;
        Debug.Log("[SUNCREST FOLIAGE INSPECT] V7 marker="+current+
            ", original palms="+baseline+", new inland palms="+extra+
            ". Selected group="+group.name+
            ". Expand Palms - Individually Editable in the Hierarchy to "+
            "locate each Palm_Fill_#### object.");
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
            if(candidateCells.Count==0)throw new InvalidOperationException(
                "Suncrest foliage grid is empty.");
            if(nextCell==candidateCells.Count)ShuffleGrid();
            Vector2 cell=candidateCells[nextCell++];
            return center+new Vector3(
                cell.x+Rand(-GridCellSize*.48f,GridCellSize*.48f),0f,
                cell.y+Rand(-GridCellSize*.48f,GridCellSize*.48f));
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
                    s.rockColliders.Add(c);

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

    /// <summary>
    /// Fill the remaining inland clearings. Plant the additional palms
    /// immediately AFTER the successful original palms but BEFORE shrubs
    /// and grass. In V6 this pass ran after 1,350 shrubs/grass patches,
    /// whose occupied-radius checks rejected new trees almost everywhere.
    /// The independent, seeded 7 m stratified grid covers the whole dry
    /// Suncrest island, not only points within 58 m of existing palms.
    /// </summary>
    private static int FillRemainingPalmCoverage(Scatter s,GameObject prefab,
        Transform palmGroup,out int usableCells)
    {
        var cells=new List<Vector2>();
        int range=Mathf.CeilToInt(ExpandedRadius/FillGridStep);
        for(int x=-range;x<=range;x++)
        for(int z=-range;z<=range;z++)
        {
            Vector2 p=new Vector2((x+.5f)*FillGridStep,
                (z+.5f)*FillGridStep);
            if(p.sqrMagnitude<ExpandedRadius*ExpandedRadius)
                cells.Add(p);
        }

        // Separate RNG keeps the first 180 palm placements exactly as
        // before, and new random positions are repeatable between rebuilds.
        var rng=new System.Random(77317);
        for(int i=cells.Count-1;i>0;i--)
        {
            int j=rng.Next(i+1);
            Vector2 v=cells[i];cells[i]=cells[j];cells[j]=v;
        }

        usableCells=0;
        int placed=0;
        // Use up to three attempts per cell to cover patches rejected only
        // because their first jitter landed too close to rock/water.
        for(int pass=0;pass<3 && placed<ExtraPalms;pass++)
        for(int i=0;i<cells.Count && placed<ExtraPalms;i++)
        {
            Vector2 cell=cells[(i+pass*73)%cells.Count];
            float jitter=FillGridStep*.47f;
            Vector3 point=new Vector3(
                s.center.x+cell.x+Mathf.Lerp(-jitter,jitter,(float)rng.NextDouble()),
                0f,
                s.center.z+cell.y+Mathf.Lerp(-jitter,jitter,(float)rng.NextDouble()));
            float scale=Mathf.Lerp(.72f,1.12f,(float)rng.NextDouble());
            float canopyRadius=1.9f*scale;
            float trunkRadius=.30f*scale;
            s.totalCandidates++;
            if(!s.DryLand(point,trunkRadius))
            {
                s.noLandRejects++;
                continue;
            }
            usableCells++;
            if(!s.Available(point,canopyRadius,trunkRadius,3.5f*scale))
                continue;

            GameObject tree=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if(tree==null)
                throw new InvalidOperationException("Original Suncrest Palm prefab failed to instantiate.");
            tree.name="Palm_Fill_"+placed.ToString("D4");
            tree.transform.SetParent(palmGroup,false);
            tree.transform.SetPositionAndRotation(
                new Vector3(point.x,s.Height(point),point.z),
                Quaternion.Euler(0f,(float)rng.NextDouble()*360f,0f));
            tree.transform.localScale=Vector3.one*scale;
            s.Claim(point,Mathf.Max(.25f,canopyRadius*.7f));
            placed++;
        }
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

            int a=PlacePrefabs(scatter,palm,palms.transform,
                "Palm",TargetPalms,30000,.7f,1.2f,1.9f,false,.30f,3.5f);
            // Trees first! Reserve the open land for the new fill trees
            // before bushes and grass claim thousands of ground patches.
            int possibleFillSites;
            int extra=FillRemainingPalmCoverage(scatter,palm,palms.transform,
                out possibleFillSites);
            int b=PlacePrefabs(scatter,shrub,bushes.transform,
                "SeaGrape",TargetBushes,60000,.7f,1.45f,.8f,true,.23f,1.6f);
            int c=PlacePrefabs(scatter,grass,grasses.transform,
                "Grass",TargetGrass,90000,.6f,1.6f,.34f,false,.10f,.8f);
            if(a+b+c+extra==0)throw new InvalidOperationException(
                "No valid dry island terrain was found near the main Suncrest rock. "+
                "Check the terrain heights relative to the ocean.");

            if(extra<25)
                Debug.LogWarning("[SUNCREST FOLIAGE] Only "+extra+
                    " of "+ExtraPalms+" interior palms were planted. "+
                    "This usually means dry terrain or rock collider checks "+
                    "are rejecting the remaining cells. Check the V7 "+
                    "rejection counts instead of assuming they were placed.");
            var marker=new GameObject(VersionMarker);
            marker.transform.SetParent(staged.transform,false);
            if(old!=null)Undo.DestroyObjectImmediate(old);
            if(previous!=null)Undo.DestroyObjectImmediate(previous);
            staged.name=GroupName;
            EditorSceneManager.MarkSceneDirty(scene);
            Physics.SyncTransforms();
            Debug.Log("[SUNCREST FOLIAGE] V7: "+(a+extra)+" palms ("+
                a+" rock-edge + "+extra+" full-island fill, "+
                possibleFillSites+" dry fill candidates), "+
                b+" SeaGrape bushes / "+c+" grass clumps. Candidate attempts "+
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
