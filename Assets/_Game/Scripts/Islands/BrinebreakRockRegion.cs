using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Uses the artist-authored Brinebreak Isle Stalagmite Rocks model and its individual
/// mesh pieces as the coast. Preserves authored transforms, mesh geometry and terrain.
/// Static mesh colliders serve player/boat/object physics and fish spawn/fight checks.
/// </summary>
public static class BrinebreakRockRegion
{
    public const float BiomeRadius = 200f;
    private const string MainName = "Brinebreak Isle Stalagmite Rocks";
    private static readonly List<Collider> Rocks = new List<Collider>(96);
    private static readonly HashSet<Collider> RockSet = new HashSet<Collider>();
    private static readonly List<Vector2> Anchors = new List<Vector2>(48);
    private static readonly Collider[] Overlaps = new Collider[128];
    private static int sceneHandle = -1;
    private static bool cached;
    private static OceanWater water;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Rocks.Clear(); RockSet.Clear(); Anchors.Clear();
        sceneHandle = -1; cached = false; water = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install() { Ensure(); }

    private static GameObject FindMain(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, MainName, StringComparison.OrdinalIgnoreCase)) return root;
            if (root.name != UserPlacedScenery.RootName) continue;
            foreach (Transform child in root.transform)
                if (string.Equals(child.name, MainName, StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;
        }
        return null;
    }

    private static bool Ensure()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded) return false;
        if (cached && sceneHandle == scene.handle) return Rocks.Count != 0 && Anchors.Count != 0;
        sceneHandle = scene.handle; cached = true;
        Rocks.Clear(); RockSet.Clear(); Anchors.Clear(); water = null;
        GameObject main = FindMain(scene);
        if (main == null || !main.activeInHierarchy) return false;

        // Mesh importers with Generate Colliders enabled already have these.
        // Fallback protects scenes containing manually extracted mesh children.
        foreach (MeshFilter filter in main.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || !filter.gameObject.activeInHierarchy)
                continue;
            Renderer renderer = filter.GetComponent<Renderer>();
            Vector3 center = renderer != null && renderer.enabled
                ? renderer.bounds.center : filter.transform.position;
            Anchors.Add(new Vector2(center.x, center.z));

            bool hasSolidMesh = false;
            foreach (MeshCollider existing in filter.GetComponents<MeshCollider>())
                if (existing.enabled && !existing.isTrigger && existing.sharedMesh == filter.sharedMesh)
                    hasSolidMesh = true;
            if (hasSolidMesh) continue;
            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic) continue;
            MeshCollider mesh = filter.gameObject.AddComponent<MeshCollider>();
            mesh.sharedMesh = filter.sharedMesh;
            mesh.convex = false;
            mesh.isTrigger = false;
        }

        Physics.SyncTransforms();
        foreach (Collider collider in main.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy ||
                collider.bounds.size.sqrMagnitude < .000001f || !RockSet.Add(collider))
                continue;
            Rocks.Add(collider);
        }
        if (Rocks.Count == 0 || Anchors.Count == 0)
            Debug.LogError("[BRINEBREAK ROCKS] The stalagmite rock meshes have no usable solid colliders.");
        else
            Debug.Log("[BRINEBREAK ROCKS] " + Rocks.Count + " solid colliders, " +
                Anchors.Count + " fishing-biome mesh centres; radius 200m each.");
        return Rocks.Count > 0;
    }

    public static bool HasRocks { get { return Ensure(); } }

    // Fits the discovery trigger to the imported mesh cluster at Play without
    // changing any artist-placed rock, terrain height, material or scale.
    public static bool TryGetWorldBounds(out Bounds bounds)
    {
        bounds = default;
        if (!Ensure()) return false;
        bounds = Rocks[0].bounds;
        for (int i = 1; i < Rocks.Count; i++)
            if (Rocks[i] != null) bounds.Encapsulate(Rocks[i].bounds);
        return true;
    }

    /// <summary>Union of 200m horizontal circles around all authored stalagmite mesh pieces.</summary>
    public static bool ContainsBiome(Vector3 point, float edgeInset = 0f)
    {
        if (!Ensure()) return false;
        float radius = Mathf.Max(0f, BiomeRadius - Mathf.Max(0f, edgeInset));
        float sq = radius * radius;
        Vector2 pos = new Vector2(point.x, point.z);
        for (int i = 0; i < Anchors.Count; i++)
            if ((Anchors[i] - pos).sqrMagnitude <= sq) return true;
        return false;
    }

    public static bool IsRock(Collider collider)
    {
        return Ensure() && collider != null && RockSet.Contains(collider);
    }

    private static bool MayHit(Vector3 a, Vector3 b, float radius, float low, float high)
    {
        if (!Ensure()) return false;
        float minX = Mathf.Min(a.x,b.x)-radius, maxX = Mathf.Max(a.x,b.x)+radius;
        float minZ = Mathf.Min(a.z,b.z)-radius, maxZ = Mathf.Max(a.z,b.z)+radius;
        for (int i = 0; i < Rocks.Count; i++)
        {
            Collider c = Rocks[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
            Bounds v = c.bounds;
            if (v.min.x <= maxX && v.max.x >= minX &&
                v.min.z <= maxZ && v.max.z >= minZ &&
                v.min.y <= high+radius && v.max.y >= low-radius) return true;
        }
        return false;
    }

    private static bool PointClear(Vector3 point, float radius)
    {
        radius = Mathf.Max(.15f, radius);
        if (!MayHit(point,point,radius,point.y,point.y)) return true;
        int count = Physics.OverlapSphereNonAlloc(point,radius,Overlaps,~0,QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (RockSet.Contains(Overlaps[i])) return false;
        return count < Overlaps.Length;
    }

    public static bool AmbientPointClear(Vector3 point, float radius) => PointClear(point,radius);

    public static bool AmbientSegmentClear(Vector3 a, Vector3 b, float radius)
    {
        if (!MayHit(a,b,radius,Mathf.Min(a.y,b.y),Mathf.Max(a.y,b.y))) return true;
        int steps = Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.35f));
        for (int i = 1; i <= steps; i++)
            if (!PointClear(Vector3.Lerp(a,b,i/(float)steps),radius)) return false;
        return true;
    }

    private static float SeaLevel()
    {
        if (water == null) water = UnityEngine.Object.FindFirstObjectByType<OceanWater>();
        return water != null ? water.BaseWaterLevel : 0f;
    }

    public static bool SurfacePointClear(Vector3 point, float radius)
    {
        float sea = SeaLevel();
        point.y = sea - .30f;
        if (!PointClear(point,radius)) return false;
        point.y = sea - .95f;
        if (!PointClear(point,radius)) return false;
        point.y = sea - 1.60f;
        return PointClear(point,radius);
    }

    public static bool SurfaceSegmentClear(Vector3 a, Vector3 b, float radius)
    {
        float sea = SeaLevel();
        if (!MayHit(a,b,radius,sea-1.60f,sea-.30f)) return true;
        Vector3 flat = b-a; flat.y = 0f;
        int steps = Mathf.Max(1,Mathf.CeilToInt(flat.magnitude/.55f));
        for (int i = 1; i <= steps; i++)
            if (!SurfacePointClear(Vector3.Lerp(a,b,i/(float)steps),radius)) return false;
        return true;
    }

    public static bool StandingOnRock(Vector3 feet)
    {
        if (!Ensure()) return false;
        Ray ray = new Ray(feet+Vector3.up*.8f,Vector3.down);
        for (int i = 0; i < Rocks.Count; i++)
            if (Rocks[i].Raycast(ray,out RaycastHit hit,2.2f) &&
                hit.normal.y >= .35f && Mathf.Abs(hit.point.y-feet.y) < .9f)
                return true;
        return false;
    }

    /// <summary>Find clear footing near the saved SeaGrape_0194 arrival point.</summary>
    public static bool TryGetArrival(Vector3 marker, CharacterController capsule, out Vector3 position)
    {
        position = marker;
        if (capsule == null || !Ensure()) return false;
        Vector3 scale = capsule.transform.lossyScale;
        float radius = Mathf.Max(.15f,capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)));
        float half = Mathf.Max(radius,capsule.height*Mathf.Abs(scale.y)*.5f);
        float centerY = capsule.center.y*scale.y;
        float feetOffset = centerY-half;
        float cylinderHalf = Mathf.Max(0f,half-radius);
        float sea = SeaLevel();
        float[] offsets = {0f,.7f,-.7f,1.5f,-1.5f,3f,-3f,5f,-5f,8f,-8f,12f,-12f,18f,-18f,24f,-24f};
        // Start outside every terrain/rock volume. An overlap query alone cannot
        // detect a capsule fully enclosed by a non-convex rock mesh.
        float rayTop = marker.y+100f;
        foreach (Terrain tile in WorldTerrain.Tiles)
            if (tile != null && tile.terrainData != null)
                rayTop = Mathf.Max(rayTop,tile.transform.position.y+tile.terrainData.size.y+10f);
        foreach (Collider rock in Rocks)
            if (rock != null) rayTop = Mathf.Max(rayTop,rock.bounds.max.y+10f);
        bool found = false;
        float best = float.PositiveInfinity;
        Physics.SyncTransforms();
        for (int ix = 0; ix < offsets.Length; ix++)
        for (int iz = 0; iz < offsets.Length; iz++)
        {
            float x = marker.x+offsets[ix], z = marker.z+offsets[iz];
            float horizontal = Vector2.Distance(new Vector2(x,z),new Vector2(marker.x,marker.z));
            if (horizontal > 24f) continue;
            // Take the TOPMOST solid hit, never the terrain closest in elevation
            // to the marker when another rock encloses that terrain surface.
            if (!TryArrivalSurface(x,z,rayTop,sea,out float y)) continue;
            float difference = Mathf.Abs(marker.y-y);
            if (difference > 30f) continue;
            float highest = y;
            bool supported = true;
            for (int sample = 0; sample < 8; sample++)
            {
                float angle = sample*Mathf.PI*.25f;
                if (!TryArrivalSurface(x+Mathf.Cos(angle)*radius,z+Mathf.Sin(angle)*radius,
                    rayTop,sea,out float edge) || Mathf.Abs(edge-y) > .4f)
                { supported = false; break; }
                highest = Mathf.Max(highest,edge);
            }
            if (!supported) continue;
            y = highest;
            Vector3 candidate = new Vector3(x,y+.36f-feetOffset,z);
            Vector3 center = candidate+Vector3.up*centerY;
            if (Physics.CheckCapsule(center-Vector3.up*cylinderHalf,
                center+Vector3.up*cylinderHalf,radius+.05f,
                ~0,QueryTriggerInteraction.Ignore)) continue;
            float score = horizontal*2f +
                difference*.3f;
            if (score >= best) continue;
            best = score; position = candidate; found = true;
        }
        if (!found) Debug.LogError("[BRINEBREAK TRAVEL] No safe footing near the new SeaGrape_0194 arrival.");
        return found;
    }

    private static bool TryArrivalSurface(float x, float z, float rayTop, float sea, out float height)
    {
        height = 0f;
        if (!Physics.Raycast(new Ray(new Vector3(x,rayTop,z),Vector3.down),out RaycastHit hit,
            rayTop-sea,~0,QueryTriggerInteraction.Ignore)) return false;
        // Foliage/boats/props may obstruct a candidate but cannot become its floor.
        if (!(hit.collider is TerrainCollider) && !RockSet.Contains(hit.collider)) return false;
        if (hit.normal.y < .75f || hit.point.y < sea+.3f) return false;
        height = hit.point.y;
        return true;
    }

}
