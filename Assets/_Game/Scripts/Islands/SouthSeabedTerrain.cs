using UnityEngine;

/// <summary>Scene-owned southern terrain. Generation happens in the Editor, never per frame.</summary>
[DisallowMultipleComponent]
public sealed class SouthSeabedTerrain : MonoBehaviour
{
    public Terrain sourceTerrain;
    public OceanWater water;
    public Terrain terrain;
    private static SouthSeabedTerrain active;

    private void OnEnable()
    {
        active = this;
        ConnectNeighbors();
    }

    public void ConnectNeighbors()
    {
        if (terrain == null || sourceTerrain == null) return;
        Vector3 expected = sourceTerrain.transform.position - new Vector3(0, 0, sourceTerrain.terrainData.size.z);
        if ((terrain.transform.position - expected).sqrMagnitude > .0001f) return;
        // Never replace an unrelated neighbor authored later by the user.
        if (sourceTerrain.bottomNeighbor != null && sourceTerrain.bottomNeighbor != terrain) return;
        sourceTerrain.SetNeighbors(sourceTerrain.leftNeighbor, sourceTerrain.topNeighbor,
            sourceTerrain.rightNeighbor, terrain);
        terrain.SetNeighbors(terrain.leftNeighbor, sourceTerrain, terrain.rightNeighbor, terrain.bottomNeighbor);
    }

    private void OnDisable()
    {
        if (active == this) active = null;
        if (sourceTerrain != null && sourceTerrain.bottomNeighbor == terrain && terrain != null)
            sourceTerrain.SetNeighbors(sourceTerrain.leftNeighbor, sourceTerrain.topNeighbor,
                sourceTerrain.rightNeighbor, null);
    }

    public static bool TryGetHeight(Vector3 position, out float height)
    {
        height = 0f;
        if (active == null || active.terrain == null || active.terrain.terrainData == null) return false;
        Terrain tile = active.terrain;
        Vector3 local = position - tile.transform.position;
        Vector3 size = tile.terrainData.size;
        if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) return false;
        height = tile.SampleHeight(position) + tile.transform.position.y;
        return true;
    }
}
