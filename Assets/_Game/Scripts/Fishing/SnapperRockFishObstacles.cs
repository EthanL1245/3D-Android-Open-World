using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Snapper-only fish obstacle queries against the real, unchanged rock colliders.
/// Terrain.SampleHeight does not include separate imported Blender rock meshes.
/// A one-time cache and cheap bounds rejection keep queries inexpensive on phones.
/// No GameObjects, colliders, visual assets or transforms are modified here.
/// </summary>
public static class SnapperRockFishObstacles
{
    private const float IslandMargin = 56f;
    private static readonly List<Collider> Rocks = new List<Collider>(32);
    private static readonly HashSet<Collider> RockSet = new HashSet<Collider>();
    private static readonly Collider[] Overlaps = new Collider[64];
    private static int cachedScene = -1;
    private static bool cacheBuilt;
    private static float baseWaterLevel;
    private static bool waterCached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        Rocks.Clear();
        RockSet.Clear();
        cachedScene = -1;
        cacheBuilt = false;
        waterCached = false;
    }

    private static bool NearIsland(Vector3 position)
    {
        return SnapperIslandRuntime.Ready &&
            SnapperIslandGeometry.DistanceFromShore(position, SnapperIslandRuntime.Center) < IslandMargin;
    }

    private static bool IsRockName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("cliff", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("boulder", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("outcrop", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsRockHierarchy(Transform transform)
    {
        for (Transform part = transform; part != null; part = part.parent)
            if (IsRockName(part.name)) return true;
        return false;
    }

    private static void CacheRocks()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (cacheBuilt && cachedScene == scene.handle) return;

        Rocks.Clear();
        RockSet.Clear();
        cachedScene = scene.handle;
        cacheBuilt = true;
        waterCached = false;

        // Only run this scan once per loaded fishing scene. All rocks in Snapper,
        // including single instances, old Blender formations and protected scenery,
        // are considered when their hierarchy identifies them as rocks.
        Vector3 center = SnapperIslandRuntime.Center;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || collider.isTrigger || !collider.enabled ||
                !collider.gameObject.activeInHierarchy || !IsRockHierarchy(collider.transform))
                continue;

            Bounds b = collider.bounds;
            if (Mathf.Abs(b.center.x - center.x) > SnapperIslandGeometry.RadiusX + IslandMargin + b.extents.x ||
                Mathf.Abs(b.center.z - center.z) > SnapperIslandGeometry.RadiusZ + IslandMargin + b.extents.z)
                continue;
            Rocks.Add(collider);
            RockSet.Add(collider);
        }
    }

    private static float WaterLevel()
    {
        if (!waterCached)
        {
            OceanWater water = UnityEngine.Object.FindFirstObjectByType<OceanWater>();
            if (water != null)
            {
                baseWaterLevel = water.BaseWaterLevel;
                waterCached = true;
            }
        }
        return baseWaterLevel;
    }

    private static bool PointClear(Vector3 position, float radius)
    {
        if (!NearIsland(position)) return true;
        CacheRocks();
        radius = Mathf.Max(.15f, radius);
        float radiusSq = radius * radius;
        bool possible = false;
        for (int i = 0; i < Rocks.Count; i++)
        {
            Collider rock = Rocks[i];
            if (rock == null || !rock.enabled || !rock.gameObject.activeInHierarchy) continue;
            if (rock.bounds.SqrDistance(position) > radiusSq) continue;
            possible = true;
            break;
        }
        if (!possible) return true;

        // Actual collider shape is used after the AABB broadphase. In particular,
        // irregular cliff bounds must not turn the surrounding water into a box.
        int count = Physics.OverlapSphereNonAlloc(
            position, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (RockSet.Contains(Overlaps[i])) return false;
        // Do not accidentally swim through rocks if the shared result buffer fills.
        return count < Overlaps.Length;
    }

    private static bool SegmentMayMeetRock(Vector3 a, Vector3 b, float radius,
        float lowY, float highY)
    {
        CacheRocks();
        float minX = Mathf.Min(a.x, b.x) - radius, maxX = Mathf.Max(a.x, b.x) + radius;
        float minZ = Mathf.Min(a.z, b.z) - radius, maxZ = Mathf.Max(a.z, b.z) + radius;
        for (int i = 0; i < Rocks.Count; i++)
        {
            Collider rock = Rocks[i];
            if (rock == null || !rock.enabled || !rock.gameObject.activeInHierarchy) continue;
            Bounds bounds = rock.bounds;
            if (bounds.max.x < minX || bounds.min.x > maxX ||
                bounds.max.z < minZ || bounds.min.z > maxZ ||
                bounds.max.y < lowY - radius || bounds.min.y > highY + radius)
                continue;
            return true;
        }
        return false;
    }

    public static bool AmbientPointClear(Vector3 position, float clearance)
    {
        return PointClear(position, clearance);
    }

    public static bool AmbientSegmentClear(Vector3 from, Vector3 to, float clearance)
    {
        if (!NearIsland(to) && !NearIsland(from)) return true;
        if (!SegmentMayMeetRock(from, to, clearance,
                Mathf.Min(from.y, to.y), Mathf.Max(from.y, to.y))) return true;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from,to) / .35f));
        for (int i = 1; i <= steps; i++)
            if (!AmbientPointClear(Vector3.Lerp(from,to,i/(float)steps),clearance))
                return false;
        return true;
    }

    // A fighting fish has its bobber at the surface, but its rendered body and
    // nose are submerged. Probe this vertical corridor using stable base water
    // rather than moving wave crests, so navigation does not jitter with waves.
    public static bool SurfacePointClear(Vector3 position, float radius)
    {
        if (!NearIsland(position)) return true;
        float sea = WaterLevel();
        Vector3 p = position;
        p.y = sea - .30f;
        if (!PointClear(p, radius)) return false;
        p.y = sea - .95f;
        if (!PointClear(p, radius)) return false;
        p.y = sea - 1.60f;
        return PointClear(p, radius);
    }

    public static bool SurfaceSegmentClear(Vector3 from, Vector3 to, float radius)
    {
        if (!NearIsland(from) && !NearIsland(to)) return true;
        float sea = WaterLevel();
        if (!SegmentMayMeetRock(from,to,radius,sea - 1.60f,sea - .30f))
            return true;
        Vector3 flat = to - from;
        flat.y = 0f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(flat.magnitude / .55f));
        for (int i = 1; i <= steps; i++)
            if (!SurfacePointClear(Vector3.Lerp(from,to,i/(float)steps),radius))
                return false;
        return true;
    }
}
