using UnityEngine;

/// <summary>One looping ocean track, faded by distance to exposed water.</summary>
[DisallowMultipleComponent]
public sealed class OceanAmbienceRuntime : MonoBehaviour
{
    private const float FullVolumeDistance = 5f;
    private const float SilentDistance = 40f;
    private const float MaximumVolume = 0.55f;
    private const float SampleInterval = 0.2f;
    private static OceanAmbienceRuntime instance;
    private AudioSource source;
    private OceanWater ocean;
    private Transform listener;
    private Terrain ground;
    private float sampleTimer, referenceTimer, targetVolume;

    public static void EnsureInstalled()
    {
        if (instance != null) return;
        new GameObject("Ocean Ambience").AddComponent<OceanAmbienceRuntime>();
    }

    private void Awake()
    {
        // Claim ownership before creating any source, including additive scenes.
        if (instance != null && instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.clip = Resources.Load<AudioClip>("Fishing/Audio/AmbientOceanWaves");
        if (source.clip != null) source.Play();
        else Debug.LogWarning("Ocean ambience clip is missing.");
    }

    private void Update()
    {
        if (instance != this || source == null) return;
        referenceTimer -= Time.unscaledDeltaTime;
        if (referenceTimer <= 0f)
        {
            referenceTimer = 1f;
            if (ocean == null || !ocean.isActiveAndEnabled)
                ocean = FindFirstObjectByType<OceanWater>();
            if (listener == null || !listener.gameObject.activeInHierarchy)
            {
                AudioListener ears = FindFirstObjectByType<AudioListener>();
                listener = ears != null ? ears.transform : Camera.main != null ? Camera.main.transform : null;
            }
            IslandExpansionWorld expansion = IslandExpansionWorld.Active;
            ground = expansion != null && expansion.Ready ? expansion.Terrain : Terrain.activeTerrain;
        }

        sampleTimer -= Time.unscaledDeltaTime;
        if (sampleTimer <= 0f)
        {
            sampleTimer = SampleInterval;
            ShopDimensionManager travel = ShopDimensionManager.Instance;
            bool audibleWorld = ocean != null && ocean.isActiveAndEnabled && listener != null &&
                (travel == null || (!travel.InDimension && !travel.Traveling));
            float distance = audibleWorld ? DistanceToWater(listener.position) : SilentDistance;
            float fade = Mathf.InverseLerp(FullVolumeDistance, SilentDistance, distance);
            targetVolume = MaximumVolume * (1f - Mathf.SmoothStep(0f, 1f, fade));
        }

        source.volume = Mathf.MoveTowards(source.volume, targetVolume, Time.unscaledDeltaTime * 0.7f);
        // Keep the same silent loop running inland. Returning to water never
        // spawns a second source or restarts the recording on every distance check.
    }

    private float DistanceToWater(Vector3 position)
    {
        float best = SilentDistance;
        ConsiderWater(position, position, ref best);
        if (best <= FullVolumeDistance) return best;

        // Sample the actual, final Terrain (including Snapper Island), not the
        // infinite ocean renderer bounds or a pre-sculpt island height formula.
        // 32 directions / 2 m steps, only five times a second, with early exits.
        for (float radius = 2f; radius < best; radius += 2f)
        {
            for (int direction = 0; direction < 32; direction++)
            {
                float angle = direction * (Mathf.PI * 2f / 32f);
                Vector3 point = position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                ConsiderWater(position, point, ref best);
            }
            if (best <= FullVolumeDistance) break;
        }
        return best;
    }

    private void ConsiderWater(Vector3 listenerPosition, Vector3 point, ref float best)
    {
        // Use mean water height so individual waves cannot make the volume pump.
        float surface = ocean.BaseWaterLevel;
        PondWater pond = PondWater.Active;
        if (pond != null && pond.Contains(point)) surface = pond.transform.position.y;

        if (ground != null && ground.terrainData != null)
        {
            Vector3 local = point - ground.transform.position;
            Vector3 size = ground.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z &&
                ground.SampleHeight(point) + ground.transform.position.y >= surface - 0.05f)
                return;
        }
        point.y = surface;
        best = Mathf.Min(best, Vector3.Distance(listenerPosition, point));
    }

    private void OnDisable()
    {
        if (source != null) source.Stop();
    }

    private void OnEnable()
    {
        if (instance == this && source != null && source.clip != null && !source.isPlaying)
        {
            source.volume = 0f;
            sampleTimer = referenceTimer = 0f;
            source.Play();
        }
    }

    private void OnDestroy()
    {
        if (source != null) source.Stop();
        if (instance == this) instance = null;
    }
}
