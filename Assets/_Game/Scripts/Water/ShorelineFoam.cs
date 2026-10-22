using UnityEngine;

/// <summary>Compatibility for previously saved baked-foam roots. The live ocean
/// shader replaces these strips. Keep serialized fields/GUID so existing scenes
/// load without missing scripts or broken asset references.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class ShorelineFoam : MonoBehaviour
{
    public OceanWater ocean;
    public Material foamMaterial;
    public float width = .65f;
    public float cycleSeconds = 2.8f;
    public float fadeStart = 85f;
    public float fadeEnd = 140f;
    [HideInInspector] public MeshRenderer[] chunks;
    [HideInInspector] public float bakedWaterLevel;

    private void OnEnable()
    {
        // Also mute the old strips in Scene view. No Update, material instance,
        // CullingGroup, collision, or mesh allocation remains in this component.
        var oldChunks = chunks != null && chunks.Length > 0
            ? chunks : GetComponentsInChildren<MeshRenderer>(true);
        foreach (var chunk in oldChunks)
            if (chunk != null) chunk.enabled = false;
    }
}
