using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Collision and safe landing for the artist-authored Main Blue Water Cay Rock.
/// Never changes any authored position, scale, rotation, mesh, or material.
/// Mesh colliders are added only where an imported rock part has none.
/// Used by walking, physical objects, ambient fish and fighting fish.
/// </summary>
public static class BluewaterCayRockCollision
{
    private const string RockName = "Main Blue Water Cay Rock";
    private static readonly List<Collider> Colliders = new List<Collider>(24);
    private static readonly HashSet<Collider> ColliderSet = new HashSet<Collider>();
    private static readonly Collider[] Overlaps = new Collider[128];

    private static int sceneHandle = -1;
    private static GameObject mainRock;
    private static Bounds rockBounds;
    private static bool hasBounds;
    private static bool arrivalResolved;
    private static Vector3 arrivalPosition;
    private static Quaternion arrivalRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        sceneHandle = -1;
        mainRock = null;
        Colliders.Clear();
        ColliderSet.Clear();
        hasBounds = false;
        arrivalResolved = false;
    }

    private static GameObject FindRock(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, RockName, StringComparison.OrdinalIgnoreCase))
                return root;
            if (root.name != UserPlacedScenery.RootName) continue;
            foreach (Transform child in root.transform)
                if (string.Equals(child.name, RockName, StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;
        }
        return null;
    }

    private static bool Ensure()
    {
        Scene scene = SceneManager.GetSceneByName("PrototypeWorld");
        if (!scene.IsValid() || !scene.isLoaded) return false;
        if (sceneHandle == scene.handle && mainRock != null && hasBounds)
            return true;

        sceneHandle = scene.handle;
        mainRock = FindRock(scene);
        Colliders.Clear();
        ColliderSet.Clear();
        hasBounds = false;
        arrivalResolved = false;
        if (mainRock == null || !mainRock.activeInHierarchy) return false;

        // These are static, non-convex meshes. This also covers meshes extracted
        // from an FBX which did not inherit its importer-generated collider.
        foreach (MeshFilter filter in mainRock.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
            if (filter.GetComponent<Collider>() != null) continue;
            Rigidbody body = filter.GetComponentInParent<Rigidbody>();
            if (body != null && !body.isKinematic) continue;

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
        }

        Physics.SyncTransforms();
        foreach (Collider collider in mainRock.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy) continue;
            // Ignore tiny unrelated physics fixtures; actual imported mesh
            // collider shapes are used for surface and fish queries.
            Bounds b = collider.bounds;
            if (b.size.sqrMagnitude < 0.000001f) continue;
            Colliders.Add(collider);
            ColliderSet.Add(collider);
            if (!hasBounds) { rockBounds = b; hasBounds = true; }
            else rockBounds.Encapsulate(b);
        }

        if (!hasBounds)
            Debug.LogWarning("[BLUEWATER ROCK] No solid collider found on the main rock.");
        return hasBounds;
    }

    private static bool TopHit(float x, float z, out RaycastHit hit)
    {
        hit = default;
        Ray ray = new Ray(new Vector3(x, rockBounds.max.y + 12f, z), Vector3.down);
        float distance = rockBounds.size.y + 24f;
        bool found = false;
        float highest = float.NegativeInfinity;
        for (int i = 0; i < Colliders.Count; i++)
        {
            Collider collider = Colliders[i];
            if (collider == null || !collider.enabled) continue;
            if (!collider.bounds.Contains(new Vector3(x,
                Mathf.Clamp(collider.bounds.center.y, collider.bounds.min.y, collider.bounds.max.y), z)))
                continue;
            RaycastHit candidate;
            if (!collider.Raycast(ray, out candidate, distance) ||
                candidate.point.y <= highest) continue;
            highest = candidate.point.y;
            hit = candidate;
            found = true;
        }
        return found;
    }

    private static bool HasFooting(Vector3 point, float radius)
    {
        float offset = Mathf.Max(0.42f, radius * 0.85f);
        Vector2[] directions = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
        for (int i = 0; i < directions.Length; i++)
        {
            RaycastHit around;
            if (!TopHit(point.x + directions[i].x * offset,
                        point.z + directions[i].y * offset, out around) ||
                around.normal.y < 0.74f ||
                Mathf.Abs(around.point.y - point.y) > 0.38f)
                return false;
        }
        return true;
    }

    private static bool IsOpenWater(float x, float z, float sea)
    {
        RaycastHit next;
        return !TopHit(x, z, out next) || next.point.y < sea + 0.15f;
    }

    /// <summary>
    /// Finds the northwest near-water WALKABLE rock surface, not the terrain.
    /// Returns a CharacterController-transform position, with the capsule entirely
    /// above the mesh. If none is safe the teleport caller must abort, not drop
    /// the player into the sea or use the obsolete BluewaterArrival coordinates.
    /// </summary>
    public static bool TryGetArrival(CharacterController capsule,
        out Vector3 position, out Quaternion facing)
    {
        position = Vector3.zero;
        facing = Quaternion.identity;
        if (capsule == null || !Ensure()) return false;

        if (arrivalResolved)
        {
            position = arrivalPosition;
            facing = arrivalRotation;
            return true;
        }

        IslandExpansionWorld expansion = IslandExpansionWorld.Active;
        OceanWater water = expansion != null ? expansion.Water : null;
        if (water == null) water = UnityEngine.Object.FindFirstObjectByType<OceanWater>();
        if (water == null) return false;
        float sea = water.BaseWaterLevel;

        Vector3 scale = capsule.transform.lossyScale;
        float radius = Mathf.Max(0.15f, capsule.radius *
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float halfHeight = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * 0.5f);
        float centerY = capsule.center.y * scale.y;
        float feetOffset = centerY - halfHeight;
        float cylinderHalf = Mathf.Max(0f, halfHeight - radius);

        Physics.SyncTransforms();
        int countX = Mathf.Clamp(Mathf.CeilToInt(rockBounds.size.x / 1.15f), 16, 64);
        int countZ = Mathf.Clamp(Mathf.CeilToInt(rockBounds.size.z / 1.15f), 16, 64);
        float bestScore = float.NegativeInfinity;
        bool found = false;
        bool foundShoreHeight = false;
        Vector3 best = Vector3.zero;

        for (int ix = 0; ix <= countX; ix++)
        for (int iz = 0; iz <= countZ; iz++)
        {
            // Unity world north = +Z, west = -X.
            float x = Mathf.Lerp(rockBounds.min.x, rockBounds.max.x,
                (ix + 0.5f) / (countX + 1f));
            float z = Mathf.Lerp(rockBounds.max.z, rockBounds.min.z,
                (iz + 0.5f) / (countZ + 1f));
            RaycastHit hit;
            if (!TopHit(x, z, out hit) || hit.normal.y < 0.78f) continue;
            float heightAboveSea = hit.point.y - sea;
            if (heightAboveSea < 0.55f) continue;
            if (!HasFooting(hit.point, radius)) continue;

            Vector3 proposed = hit.point;
            // Clearance for a horizontal step immediately after arrival.
            proposed.y += 0.32f - feetOffset;
            Vector3 center = proposed + Vector3.up * centerY;
            Vector3 bottom = center - Vector3.up * cylinderHalf;
            Vector3 top = center + Vector3.up * cylinderHalf;
            if (Physics.CheckCapsule(bottom, top, Mathf.Max(0.05f, radius - 0.02f),
                    ~0, QueryTriggerInteraction.Ignore))
                continue;

            bool shoreHeight = heightAboveSea <= 4f;
            float west = (rockBounds.max.x - x) / Mathf.Max(1f, rockBounds.size.x);
            float north = (z - rockBounds.min.z) / Mathf.Max(1f, rockBounds.size.z);
            float shoreline = IsOpenWater(x - 3f, z, sea) ||
                              IsOpenWater(x, z + 3f, sea) ||
                              IsOpenWater(x - 5f, z + 2f, sea) ? 2.4f : 0f;
            float score = 4.0f * (west + north) + shoreline -
                          1.05f * Mathf.Abs(heightAboveSea - 1.35f);

            // Prefer nearby sea-level rock even over a higher NW ledge.
            if (found && foundShoreHeight && !shoreHeight) continue;
            if (!found || (shoreHeight && !foundShoreHeight) || score > bestScore)
            {
                found = true;
                foundShoreHeight = shoreHeight;
                bestScore = score;
                best = proposed;
            }
        }

        if (!found)
        {
            Debug.LogError("[BLUEWATER ROCK] No safe near-northwest landing was found on the main rock. Teleport cancelled.");
            return false;
        }

        Vector3 towardRock = rockBounds.center - best;
        towardRock.y = 0f;
        facing = towardRock.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(towardRock.normalized, Vector3.up)
            : Quaternion.identity;
        position = best;
        arrivalPosition = best;
        arrivalRotation = facing;
        arrivalResolved = true;
        Debug.Log("[BLUEWATER ROCK] Safe rock arrival: " + best + ", water level " + sea);
        return true;
    }

    public static bool AmbientPointClear(Vector3 position, float radius)
    {
        if (!Ensure()) return true;
        radius = Mathf.Max(0.15f, radius);
        if (rockBounds.SqrDistance(position) > radius * radius) return true;
        int count = Physics.OverlapSphereNonAlloc(position, radius, Overlaps,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (ColliderSet.Contains(Overlaps[i])) return false;
        return count < Overlaps.Length;
    }

    public static bool SurfacePointClear(Vector3 position, float radius)
    {
        if (!Ensure()) return true;
        if (position.x < rockBounds.min.x - radius || position.x > rockBounds.max.x + radius ||
            position.z < rockBounds.min.z - radius || position.z > rockBounds.max.z + radius)
            return true;
        OceanWater water = IslandExpansionWorld.Active != null ? IslandExpansionWorld.Active.Water : null;
        if (water == null) water = UnityEngine.Object.FindFirstObjectByType<OceanWater>();
        if (water == null) return true;
        float sea = water.BaseWaterLevel;
        position.y = sea - 0.30f;
        if (!AmbientPointClear(position, radius)) return false;
        position.y = sea - 0.95f;
        if (!AmbientPointClear(position, radius)) return false;
        position.y = sea - 1.60f;
        return AmbientPointClear(position, radius);
    }

    public static bool SurfaceSegmentClear(Vector3 from, Vector3 to, float radius)
    {
        if (!Ensure()) return true;
        if (Mathf.Max(from.x, to.x) + radius < rockBounds.min.x ||
            Mathf.Min(from.x, to.x) - radius > rockBounds.max.x ||
            Mathf.Max(from.z, to.z) + radius < rockBounds.min.z ||
            Mathf.Min(from.z, to.z) - radius > rockBounds.max.z)
            return true;
        Vector3 horizontal = to - from;
        horizontal.y = 0f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(horizontal.magnitude / 0.55f));
        for (int i = 1; i <= steps; i++)
            if (!SurfacePointClear(Vector3.Lerp(from, to, i / (float)steps), radius))
                return false;
        return true;
    }
}
