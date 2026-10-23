#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Populates the NEW Suncrest Beach, within 150 metres of the world-space centre
/// of the artist's main Suncrest rock. Uses the ORIGINAL Palm / SeaGrape / Grass
/// prefabs and seeded, stratified placement across the entire island.
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
    private const string VersionMarker = "SUNCREST_FOLIAGE_EVEN_COVERAGE_V4";
    private const string PrefabFolder = "Assets/_Game/Reef/Generated/";
    private const float AreaRadius = 150f;
    private const float MinimumRockClearance = .5f;
    private const float GridCellSize = 6f;
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
            "150 m of the main Suncrest rock? The original palms, SeaGrape bushes and grass " +
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

        public bool DryLand(Vector3 point,float radius)
        {
            // The painted grass mask is deliberately NOT used. The old code's
            // layer-name/alphamap test left the new beach entirely bare.
            Vector2 offset = new Vector2(point.x-center.x,point.z-center.z);
            float max = AreaRadius-radius;
            if(max<=0f || offset.sqrMagnitude > max*max ||
               !OnTerrain(point,radius+1f)) return false;
            float u=(point.x-terrainOrigin.x)/terrainSize.x;
            float v=(point.z-terrainOrigin.z)/terrainSize.z;
            if(terrain.terrainData.GetSteepness(u,v)>55f) return false;
            float ground=Height(point);
            if(ground<water.BaseWaterLevel+.10f)return false;

            // Probes ensure none of the foliage footprint crosses into water.
            // Trees, bushes and grass therefore stop at the shoreline.
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI*.25f;
                Vector3 p=point+new Vector3(Mathf.Cos(angle)*radius,0f,
                                               Mathf.Sin(angle)*radius);
                if(!OnTerrain(p,.1f) ||
                   Height(p)<water.BaseWaterLevel+.08f ||
                   Mathf.Abs(Height(p)-ground)>radius*.65f+.45f) return false;
            }
            return true;
        }

        private bool TooCloseToRock(Vector3 point,float stemRadius,
            float clearHeight)
        {
            // Check only real rock geometry near the planting surface. The
            // old ray from the TOP of an entire mountain projected every
            // overhang onto the terrain, rejecting most of the island.
            float ground=Height(point);
            float minDistance=MinimumRockClearance+stemRadius;
            float minSq=minDistance*minDistance;
            Vector3 foot=new Vector3(point.x,ground,point.z);

            for(int i=0;i<rockColliders.Count;i++)
            {
                Collider c=rockColliders[i];
                if(c==null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                Bounds b=c.bounds;
                if(b.min.x>point.x+minDistance ||
                   b.max.x<point.x-minDistance ||
                   b.min.z>point.z+minDistance ||
                   b.max.z<point.z-minDistance ||
                   b.max.y<ground-.35f ||
                   b.min.y>ground+clearHeight+minDistance)
                    continue;

                // True collider distance, allowing stems to within 0.5m
                // of the rock surface rather than a radius-sized exclusion.
                for(int h=0;h<4;h++)
                {
                    float height=h==0?.2f:h==1?.8f:
                        h==2?Mathf.Min(2f,clearHeight):clearHeight;
                    Vector3 at=foot+Vector3.up*height;
                    if((c.ClosestPoint(at)-at).sqrMagnitude<minSq)
                        return true;
                }

                // Only reject a low overhead rock within the height of
                // this plant, not distant high overhangs and cliff faces.
                RaycastHit hit;
                Vector3 from=foot+Vector3.up*(clearHeight+.1f);
                if(c.Raycast(new Ray(from,Vector3.down),out hit,
                             clearHeight+.45f) &&
                   hit.point.y>=ground-.35f)
                    return true;
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
                    return false;
            }
            for(int i=0;i<existingTrees.Count;i++)
            {
                float min=radius+2f;
                if((position-existingTrees[i]).sqrMagnitude<min*min)
                    return false;
            }
            if(marina!=null)
            {
                // Extend the avoidance envelope to the dock built at runtime.
                Vector3 local=marina.InverseTransformPoint(point);
                if(Mathf.Abs(local.x)<radius+9f &&
                   local.z>-16f-radius && local.z<51f+radius)
                    return false;
            }
            for(int i=0;i<marinaBounds.Count;i++)
            {
                Bounds b=marinaBounds[i];
                if(point.x>=b.min.x-radius-3f &&
                   point.x<=b.max.x+radius+3f &&
                   point.z>=b.min.z-radius-3f &&
                   point.z<=b.max.z+radius+3f) return false;
            }
            return !TooCloseToRock(point,stemRadius,clearHeight);
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
            if(!s.DryLand(point,radius) ||
               !s.Available(point,radius,stemRadius*scale,clearHeight*scale))
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
            int b=PlacePrefabs(scatter,shrub,bushes.transform,
                "SeaGrape",TargetBushes,60000,.7f,1.45f,.8f,true,.23f,1.6f);
            int c=PlacePrefabs(scatter,grass,grasses.transform,
                "Grass",TargetGrass,90000,.6f,1.6f,.34f,false,.10f,.8f);
            if(a+b+c==0)throw new InvalidOperationException(
                "No valid dry island terrain was found within 150 m of the main rock. "+
                "Check the terrain heights relative to the ocean.");

            var marker=new GameObject(VersionMarker);
            marker.transform.SetParent(staged.transform,false);
            if(old!=null)Undo.DestroyObjectImmediate(old);
            if(previous!=null)Undo.DestroyObjectImmediate(previous);
            staged.name=GroupName;
            EditorSceneManager.MarkSceneDirty(scene);
            Physics.SyncTransforms();
            Debug.Log("[SUNCREST FOLIAGE] Even 6m grid and 0.5m rock clearance: "+a+" palms / "+
                b+" SeaGrape bushes / "+c+" grass clumps. NO beach rocks or "+
                "limestone added. Saved rock transforms untouched. "+
                "Group objects are individually editable; press Ctrl+S to save.");
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
