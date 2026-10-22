using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One controller for all editor-baked shoreline chunks. Never samples
/// terrain, runs physics, or creates/updates meshes in the player.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1100)]
public sealed class ShorelineFoam : MonoBehaviour
{
    public OceanWater ocean;
    public Material foamMaterial;
    [Range(.2f, 2f)] public float width = .65f;
    [Range(1f, 8f)] public float cycleSeconds = 2.8f;
    public float fadeStart = 85f;
    public float fadeEnd = 140f;
    [HideInInspector] public MeshRenderer[] chunks;
    [HideInInspector] public float bakedWaterLevel;
    private Material instance;
    private MaterialPropertyBlock waves;
    private CullingGroup culling;
    private Camera view;
    private float nextViewCheck;
    private static readonly string[] Floats = {
        "_WaveAmplitude1", "_WaveLength1", "_WaveAmplitude2", "_WaveLength2",
        "_WaveAmplitude3", "_WaveLength3", "_GerstnerStrength",
        "_IslandWaveEnabled", "_IslandWaveAmplitude"
    };
    private static readonly string[] Vectors = {
        "_WaveDirection1", "_WaveDirection2", "_WaveDirection3",
        "_WavePhases", "_IslandWavePhases", "_IslandWaveCenter"
    };

    private void OnEnable()
    {
        if (ocean == null || foamMaterial == null || chunks == null) return;
        instance = new Material(foamMaterial) { name = "Shared shoreline foam (runtime)" };
        waves = new MaterialPropertyBlock();
        var spheres = new BoundingSphere[chunks.Length];
        for (int i = 0; i < chunks.Length; i++)
        {
            if (chunks[i] == null) continue;
            chunks[i].sharedMaterial = instance;
            Bounds b = chunks[i].bounds;
            spheres[i] = new BoundingSphere(b.center, b.extents.magnitude);
        }
        culling = new CullingGroup();
        culling.SetBoundingSpheres(spheres);
        culling.SetBoundingSphereCount(spheres.Length);
        culling.SetBoundingDistances(new[] { Mathf.Max(1f, fadeEnd) });
        culling.onStateChanged = OnCulling;
        nextViewCheck = 0;
    }

    private void OnCulling(CullingGroupEvent change)
    {
        if (chunks[change.index] != null)
            chunks[change.index].enabled = change.currentDistance == 0;
    }

    private void LateUpdate()
    {
        if (instance == null || ocean == null) return;
        if (Time.unscaledTime >= nextViewCheck)
        {
            nextViewCheck = Time.unscaledTime + 1f;
            var player = FindFirstObjectByType<FirstPersonController>();
            Camera candidate = player != null ? player.ViewCamera : Camera.main;
            if (candidate != null && candidate != view)
            {
                view = candidate;
                culling.targetCamera = view;
                culling.SetDistanceReferencePoint(view.transform);
            }
        }
        ocean.CopyWaveProperties(waves);
        foreach (string property in Floats) instance.SetFloat(property, waves.GetFloat(property));
        foreach (string property in Vectors) instance.SetVector(property, waves.GetVector(property));
        instance.SetFloat("_BaseLevel", ocean.BaseWaterLevel);
        instance.SetFloat("_FoamWidth", width);
        instance.SetFloat("_CycleSeconds", Mathf.Max(1f, cycleSeconds));
        instance.SetVector("_FadeRange", new Vector4(fadeStart, Mathf.Max(fadeStart + 1, fadeEnd), 0, 0));
    }

    private void OnDisable()
    {
        culling?.Dispose();
        culling = null;
        if (chunks != null) foreach (var chunk in chunks)
        {
            if (chunk == null) continue;
            chunk.sharedMaterial = foamMaterial;
            chunk.enabled = true;
        }
        if (instance != null) Destroy(instance);
        instance = null;
    }
}
