using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Applies one cast-quality multiplier to island/ocean catches after the species and
/// biome-normal weight have already been rolled.
///
/// Quality uses both real cast distance and the cast point's stable water depth
/// compared with the deepest stable water found in a 30 m scan around that landing.
/// The distance endpoints are deliberate gameplay anchors:
///   5 m cast  -> 20% potential (80% size/HP penalty)
///   30 m cast -> 100% potential (no penalty)
/// Between those endpoints, locally deeper water earns substantially more of the
/// remaining potential than a shallow/sandbar cast.
///
/// The same multiplier is applied to caught weight and starting fight HP. Because
/// every later length/value calculation reads the reduced caught weight, the game's
/// existing biological length table and sale-value parabola stay authoritative.
/// Palm Pond is exempt and retains its dedicated tiny-fish rules.
/// </summary>
[DefaultExecutionOrder(2000)]
public sealed class FishingCastQualityRuntime : MonoBehaviour
{
    private const float MinimumGameplayCastMetres = 5f;
    private const float FullQualityCastMetres = 30f;
    private const float ScanRadiusMetres = 30f;
    private const float ScanRingStepMetres = 5f;
    private const int ScanDirections = 16;
    private const float MinimumQuality = 0.20f;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo StateField = typeof(FishingSystem).GetField("state", Flags);
    private static readonly FieldInfo CastPointField = typeof(FishingSystem).GetField("castPoint", Flags);
    private static readonly FieldInfo PondCastField = typeof(FishingSystem).GetField("pondCast", Flags);
    private static readonly FieldInfo OriginalCastDistanceField = typeof(FishingSystem).GetField("originalCastDistance", Flags);
    private static readonly FieldInfo HookedWeightKgField = typeof(FishingSystem).GetField("hookedWeightKg", Flags);
    private static readonly FieldInfo FishMaxHealthField = typeof(FishingSystem).GetField("fishMaxHealth", Flags);
    private static readonly FieldInfo FishHealthPointsField = typeof(FishingSystem).GetField("fishHealthPoints", Flags);
    private static readonly FieldInfo FishHealthField = typeof(FishingSystem).GetField("fishHealth", Flags);
    private static readonly FieldInfo OceanWaterField = typeof(FishingSystem).GetField("oceanWater", Flags);

    private FishingSystem fishing;
    private OceanWater ocean;
    private Terrain terrain;
    private string previousState = string.Empty;
    private bool measured;
    private bool applied;
    private bool pond;
    private float castDistance;
    private float castDepth;
    private float deepestDepth;
    private float depthRatio = 1f;
    private float quality = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if (system != null && system.GetComponent<FishingCastQualityRuntime>() == null)
                system.gameObject.AddComponent<FishingCastQualityRuntime>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        ocean = fishing != null && OceanWaterField != null ? OceanWaterField.GetValue(fishing) as OceanWater : null;
        terrain = Terrain.activeTerrain;

        if (fishing == null || StateField == null || CastPointField == null ||
            PondCastField == null || OriginalCastDistanceField == null || HookedWeightKgField == null ||
            FishMaxHealthField == null || FishHealthPointsField == null || FishHealthField == null)
        {
            Debug.LogError("FishingCastQualityRuntime could not bind FishingSystem fields and was disabled.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (!enabled || fishing == null) return;
        if (ocean == null && OceanWaterField != null) ocean = OceanWaterField.GetValue(fishing) as OceanWater;
        if (terrain == null) terrain = Terrain.activeTerrain;

        string state = StateName();

        // A new cast begins here. Waiting is deliberately not used for reset because
        // permanent lures continuously move castPoint while they are retrieved.
        if ((state == "Charging" || state == "Casting") && state != previousState)
            ResetForNewCast();

        // Capture the landing exactly once, before a retrieval lure can move inland.
        if (state == "Waiting" && !measured)
            MeasureLandingQuality();

        // Normal bait may transition through Bite quickly, so Fighting has a safe
        // fallback measurement. Apply after FishingSystem.StartFight has created HP.
        if (state == "Fighting" && !applied)
        {
            if (!measured) MeasureLandingQuality();
            ApplyToHookedFish();
        }

        if (state == "Idle" && previousState == "Fighting")
            ResetForNewCast();

        previousState = state;
    }

    private void ResetForNewCast()
    {
        measured = false;
        applied = false;
        pond = false;
        castDistance = 0f;
        castDepth = 0f;
        deepestDepth = 0f;
        depthRatio = 1f;
        quality = 1f;
    }

    private void MeasureLandingQuality()
    {
        measured = true;
        pond = PondCastField != null && (bool)PondCastField.GetValue(fishing);
        castDistance = OriginalCastDistanceField != null
            ? Mathf.Clamp((float)OriginalCastDistanceField.GetValue(fishing), MinimumGameplayCastMetres, FullQualityCastMetres)
            : FullQualityCastMetres;

        if (pond)
        {
            quality = 1f;
            depthRatio = 1f;
            return;
        }

        Vector3 landing = (Vector3)CastPointField.GetValue(fishing);
        castDepth = StableWaterDepth(landing);
        deepestDepth = DeepestDepthWithin30Metres(landing);
        depthRatio = deepestDepth > 0.001f ? Mathf.Clamp01(castDepth / deepestDepth) : 1f;
        quality = QualityFromDistanceAndDepth(castDistance, depthRatio);
    }

    /// <summary>
    /// Distance owns the exact endpoints. At 5 m the catch has 20% potential; at
    /// 30 m it has 100%. Between them, deeper local water bends the curve upward.
    /// </summary>
    public static float QualityFromDistanceAndDepth(float distanceMetres, float localDepthRatio)
    {
        float distance01 = Mathf.InverseLerp(MinimumGameplayCastMetres, FullQualityCastMetres, distanceMetres);
        float depth01 = Mathf.Clamp01(localDepthRatio);

        // Shallow intermediate casts rise slowly; a cast into the deepest nearby
        // water rises much faster. Both converge exactly at 20% / 100% endpoints.
        float exponent = Mathf.Lerp(2.0f, 0.65f, Mathf.Sqrt(depth01));
        float progress = Mathf.Pow(distance01, exponent);
        return Mathf.Lerp(MinimumQuality, 1f, progress);
    }

    private void ApplyToHookedFish()
    {
        applied = true;
        if (pond) return;

        float oldWeight = Mathf.Max(0.001f, (float)HookedWeightKgField.GetValue(fishing));
        int oldMaxHealth = Mathf.Max(1, (int)FishMaxHealthField.GetValue(fishing));

        float newWeight = Mathf.Max(0.001f, oldWeight * quality);
        int newMaxHealth = Mathf.Max(1, Mathf.RoundToInt(oldMaxHealth * quality));

        HookedWeightKgField.SetValue(fishing, newWeight);
        FishMaxHealthField.SetValue(fishing, newMaxHealth);
        FishHealthPointsField.SetValue(fishing, newMaxHealth);
        FishHealthField.SetValue(fishing, 1f);

        Debug.Log(
            "[FISH CAST QUALITY] distance=" + castDistance.ToString("0.0") + "m" +
            " depth=" + castDepth.ToString("0.00") + "m" +
            " deepest30m=" + deepestDepth.ToString("0.00") + "m" +
            " depthRatio=" + (depthRatio * 100f).ToString("0") + "%" +
            " quality=" + (quality * 100f).ToString("0") + "%" +
            " weight=" + oldWeight.ToString("0.###") + "->" + newWeight.ToString("0.###") + "kg" +
            " HP=" + oldMaxHealth + "->" + newMaxHealth);
    }

    private float DeepestDepthWithin30Metres(Vector3 center)
    {
        float deepest = StableWaterDepth(center);
        for (float radius = ScanRingStepMetres; radius <= ScanRadiusMetres + 0.01f; radius += ScanRingStepMetres)
        {
            for (int i = 0; i < ScanDirections; i++)
            {
                float angle = i * (360f / ScanDirections);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
                deepest = Mathf.Max(deepest, StableWaterDepth(center + direction * radius));
            }
        }
        return Mathf.Max(0f, deepest);
    }

    private float StableWaterDepth(Vector3 position)
    {
        if (terrain == null || ocean == null) return 0f;
        TerrainData data = terrain.terrainData;
        Vector3 local = position - terrain.transform.position;
        if (local.x < 0f || local.z < 0f || local.x > data.size.x || local.z > data.size.z)
            return 0f;

        float ground = terrain.SampleHeight(position) + terrain.transform.position.y;
        return Mathf.Max(0f, ocean.BaseWaterLevel - ground);
    }

    private string StateName()
    {
        object value = StateField.GetValue(fishing);
        return value != null ? value.ToString() : string.Empty;
    }
}
