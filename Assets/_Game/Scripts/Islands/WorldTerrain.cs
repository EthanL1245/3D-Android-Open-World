using UnityEngine;

/// <summary>Bounds-aware queries across the editable main map and added terrain tiles.</summary>
public static class WorldTerrain
{
    private static Terrain[] tiles;
    private static int cachedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { tiles = null; cachedFrame = -1; }

    public static Terrain[] Tiles
    {
        get
        {
            // Terrain.activeTerrains returns an array. Share it across the many
            // route/footprint samples this frame rather than allocate per sample.
            if (tiles == null || cachedFrame != Time.frameCount)
            {
                tiles = Terrain.activeTerrains;
                cachedFrame = Time.frameCount;
            }
            return tiles;
        }
    }

    public static bool TryGetHeight(Vector3 position, out float height)
    {
        height = float.NegativeInfinity;
        bool found = false;
        foreach (Terrain tile in Tiles)
        {
            if (tile == null || !tile.isActiveAndEnabled || tile.terrainData == null) continue;
            Vector3 local = position - tile.transform.position;
            Vector3 size = tile.terrainData.size;
            // SampleHeight clamps to the edge outside a tile: never sample there.
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) continue;
            height = Mathf.Max(height, tile.SampleHeight(position) + tile.transform.position.y);
            found = true;
        }
        return found;
    }

    public static bool SurfaceSegmentClear(Vector3 from, Vector3 to, OceanWater water, float clearance)
    {
        if (water == null) return true;
        Vector3 delta = to - from;
        delta.y = 0f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .4f));
        // Include the origin: a falling wave must not leave a subdued fish under
        // a newly exposed bank. Intermediate samples also prevent fast retrieves
        // from skipping a narrow ridge between otherwise-deep endpoints.
        for (int i = 0; i <= steps; i++)
        {
            Vector3 sample = Vector3.Lerp(from, to, i / (float)steps);
            if (TryGetHeight(sample, out float floor) &&
                water.GetSurfaceHeight(sample) - floor <= clearance) return false;
        }
        return true;
    }
}
