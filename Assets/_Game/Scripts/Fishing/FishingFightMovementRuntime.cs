using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Small runtime companion for fight movement.
/// - Changes the weave phase at irregular intervals so escaping fish do not repeat
///   the same predictable path forever.
/// - Detects a conscious, non-reeled fish whose player distance has stopped growing
///   near shore and gives it a mostly-sideways escape nudge through usable water.
/// - Runs the existing unconscious retrieve step one extra time while REEL is held,
///   making unconscious fish retrieval exactly 2x the normal base movement per frame.
/// </summary>
[DefaultExecutionOrder(2400)]
public sealed class FishingFightMovementRuntime : MonoBehaviour
{
    private const float StallGraceSeconds = 0.42f;
    private const float MinimumProgressMetres = 0.004f;
    private const float MinimumEscapeDepth = 0.12f;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo StateField = typeof(FishingSystem).GetField("state", Flags);
    private static readonly FieldInfo UnconsciousField = typeof(FishingSystem).GetField("fishUnconscious", Flags);
    private static readonly FieldInfo FishOnShoreField = typeof(FishingSystem).GetField("fishOnShore", Flags);
    private static readonly FieldInfo PondCastField = typeof(FishingSystem).GetField("pondCast", Flags);
    private static readonly FieldInfo BobberField = typeof(FishingSystem).GetField("bobber", Flags);
    private static readonly FieldInfo CastPointField = typeof(FishingSystem).GetField("castPoint", Flags);
    private static readonly FieldInfo FightMovePhaseField = typeof(FishingSystem).GetField("fightMovePhase", Flags);
    private static readonly FieldInfo FightTravelDirectionField = typeof(FishingSystem).GetField("fightTravelDirection", Flags);
    private static readonly FieldInfo OceanWaterField = typeof(FishingSystem).GetField("oceanWater", Flags);
    private static readonly MethodInfo UpdateUnconsciousBobberMethod = typeof(FishingSystem).GetMethod("UpdateUnconsciousBobber", Flags);
    private static readonly MethodInfo UpdateUnconsciousFishVisualMethod = typeof(FishingSystem).GetMethod("UpdateUnconsciousFishVisual", Flags);

    private FishingSystem fishing;
    private FishingHUD hud;
    private OceanWater ocean;
    private Terrain terrain;
    private float previousDistance = -1f;
    private float stalledSeconds;
    private float nextPhaseShuffle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if (system != null && system.GetComponent<FishingFightMovementRuntime>() == null)
                system.gameObject.AddComponent<FishingFightMovementRuntime>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        hud = FindFirstObjectByType<FishingHUD>();
        terrain = Terrain.activeTerrain;
        ocean = fishing != null && OceanWaterField != null ? OceanWaterField.GetValue(fishing) as OceanWater : null;
        nextPhaseShuffle = UnityEngine.Random.Range(0.55f, 1.35f);

        if (fishing == null || StateField == null || UnconsciousField == null ||
            FishOnShoreField == null || PondCastField == null || BobberField == null ||
            CastPointField == null || FightMovePhaseField == null || FightTravelDirectionField == null ||
            UpdateUnconsciousBobberMethod == null || UpdateUnconsciousFishVisualMethod == null)
        {
            Debug.LogError("FishingFightMovementRuntime could not bind FishingSystem movement fields and was disabled.");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!enabled || fishing == null || !IsFighting())
        {
            ResetTracking();
            return;
        }

        if (hud == null) hud = FindFirstObjectByType<FishingHUD>();
        if (ocean == null && OceanWaterField != null) ocean = OceanWaterField.GetValue(fishing) as OceanWater;
        if (terrain == null) terrain = Terrain.activeTerrain;

        bool reeling = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
        bool unconscious = (bool)UnconsciousField.GetValue(fishing);

        if (unconscious)
        {
            ResetTracking();
            if (reeling && !(bool)FishOnShoreField.GetValue(fishing))
            {
                // FishingSystem already performed one retrieve step in Update(). A
                // second identical step here makes unconscious retrieval 2x faster
                // while preserving all of the original shore/catch handling.
                UpdateUnconsciousBobberMethod.Invoke(fishing, null);
                UpdateUnconsciousFishVisualMethod.Invoke(fishing, null);
            }
            return;
        }

        // Palm Pond is deliberately tiny and uses its own confined-water behavior;
        // the anti-beaching assist is for shoreline/ocean fights only.
        if ((bool)PondCastField.GetValue(fishing) || reeling)
        {
            ResetTracking();
            return;
        }

        GameObject bobber = BobberField.GetValue(fishing) as GameObject;
        if (bobber == null || !bobber.activeSelf)
        {
            ResetTracking();
            return;
        }

        nextPhaseShuffle -= Time.deltaTime;
        if (nextPhaseShuffle <= 0f)
        {
            FightMovePhaseField.SetValue(fishing, UnityEngine.Random.Range(0f, Mathf.PI * 2f));
            nextPhaseShuffle = UnityEngine.Random.Range(0.55f, 1.35f);
        }

        Vector3 delta = bobber.transform.position - fishing.transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;

        if (previousDistance < 0f || distance > previousDistance + MinimumProgressMetres)
            stalledSeconds = 0f;
        else
            stalledSeconds += Time.deltaTime;

        previousDistance = distance;

        if (stalledSeconds >= StallGraceSeconds)
        {
            if (TrySideEscape(bobber))
                previousDistance = HorizontalDistance(bobber.transform.position, fishing.transform.position);
            stalledSeconds = 0f;
        }
    }

    private bool TrySideEscape(GameObject bobber)
    {
        Vector3 current = bobber.transform.position;
        Vector3 away = current - fishing.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = fishing.transform.forward;
        away.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, away).normalized;
        float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;

        // Prefer a lateral route with a small outward component. If that side is
        // shallow/blocked, try the other side and progressively wider outward arcs.
        Vector3[] directions =
        {
            (side * sign + away * UnityEngine.Random.Range(0.18f, 0.38f)).normalized,
            (-side * sign + away * UnityEngine.Random.Range(0.18f, 0.38f)).normalized,
            Quaternion.AngleAxis(62f * sign, Vector3.up) * away,
            Quaternion.AngleAxis(-62f * sign, Vector3.up) * away,
            Quaternion.AngleAxis(38f * sign, Vector3.up) * away,
            Quaternion.AngleAxis(-38f * sign, Vector3.up) * away
        };
        float[] distances = { 0.52f, 0.34f, 0.20f };

        for (int d = 0; d < distances.Length; d++)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 direction = directions[i];
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f) continue;
                direction.Normalize();

                // Never use the recovery nudge to pull a free-swimming fish back
                // toward the player; tangent or outward motion only.
                if (Vector3.Dot(direction, away) < -0.01f) continue;

                Vector3 candidate = current + direction * distances[d];
                if (!IsEscapeWater(candidate)) continue;

                if (ocean != null)
                    candidate.y = ocean.GetSurfaceHeight(candidate) - 0.08f;

                bobber.transform.position = candidate;
                CastPointField.SetValue(fishing, candidate);
                FightTravelDirectionField.SetValue(fishing, direction);
                FightMovePhaseField.SetValue(fishing, UnityEngine.Random.Range(0f, Mathf.PI * 2f));
                nextPhaseShuffle = UnityEngine.Random.Range(0.55f, 1.35f);
                return true;
            }
        }

        return false;
    }

    private bool IsEscapeWater(Vector3 position)
    {
        if (terrain == null || ocean == null) return true;

        TerrainData data = terrain.terrainData;
        Vector3 local = position - terrain.transform.position;
        if (local.x < 0f || local.z < 0f || local.x > data.size.x || local.z > data.size.z)
            return true;

        float ground = terrain.SampleHeight(position) + terrain.transform.position.y;
        float water = ocean.GetSurfaceHeight(position);
        return water - ground > MinimumEscapeDepth;
    }

    private bool IsFighting()
    {
        object state = StateField.GetValue(fishing);
        return state != null && string.Equals(state.ToString(), "Fighting", StringComparison.Ordinal);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void ResetTracking()
    {
        previousDistance = -1f;
        stalledSeconds = 0f;
    }
}
