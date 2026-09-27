using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Fight-movement companion.
/// - Plans 20 m ahead through the terrain/water-depth field before a fish reaches shore.
/// - Chooses a safe heading fan and remembers which side of an island it is rounding,
///   so boat-to-shore casts do not make the fish repeatedly run straight onto land.
/// - Uses stable base-water depth (not the moving wave crest) for routing decisions.
/// - Keeps a small emergency recovery only for an already critically-shallow fish.
/// - Runs the existing unconscious retrieve step one extra time while REEL is held,
///   preserving the requested 2x unconscious retrieve speed.
/// </summary>
[DefaultExecutionOrder(2400)]
public sealed class FishingFightMovementRuntime : MonoBehaviour
{
    private const float PlannerInterval = 0.11f;
    private const float RouteLookAheadMetres = 20f;
    private const float SafeRouteDepth = 1.10f;
    private const float MinimumEscapeDepth = 0.72f;
    private const float EmergencyDepth = 0.46f;
    private const float RouteSideHoldSeconds = 2.8f;
    private const float StallGraceSeconds = 0.65f;
    private const float MinimumProgressMetres = 0.004f;

    private static readonly float[] RouteDistances = { 2f, 4f, 7f, 10f, 14f, 17f, RouteLookAheadMetres };
    private static readonly float[] CandidateAngles = { 0f, 25f, -25f, 45f, -45f, 65f, -65f, 85f, -85f, 105f, -105f, 125f, -125f };

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
    private float plannerCooldown;
    private float nextPhaseShuffle;
    private float routeSideTimer;
    private int routeSideSign;
    private float previousDistance = -1f;
    private float stalledSeconds;

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
                UpdateUnconsciousBobberMethod.Invoke(fishing, null);
                UpdateUnconsciousFishVisualMethod.Invoke(fishing, null);
            }
            return;
        }

        // Palm Pond is intentionally separate from island/ocean fight routing.
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

        if (routeSideTimer > 0f)
        {
            routeSideTimer -= Time.deltaTime;
            if (routeSideTimer <= 0f) routeSideSign = 0;
        }

        Vector3 position = bobber.transform.position;
        Vector3 away = HorizontalDirection(position - fishing.transform.position, fishing.transform.forward);
        Vector3 currentDirection = FightTravelDirectionField.GetValue(fishing) is Vector3 value
            ? HorizontalDirection(value, away)
            : away;

        float currentDepth = StableWaterDepth(position);
        bool fishAlreadyOnShore = (bool)FishOnShoreField.GetValue(fishing);

        // Only physically nudge when the fish is already in critically shallow water.
        // Normal avoidance is direction planning only, so it does not add free speed.
        if (fishAlreadyOnShore || currentDepth < EmergencyDepth)
            EmergencyRecover(bobber, away);

        plannerCooldown -= Time.deltaTime;
        if (plannerCooldown <= 0f)
        {
            float currentRouteMinimum = RouteMinimumDepth(position, currentDirection);
            bool routeThreatened = currentRouteMinimum < SafeRouteDepth;

            // While rounding an island, keep planning on the chosen side even if the
            // next metre happens to be safe. This prevents left/right oscillation.
            if (routeThreatened || routeSideSign != 0)
                PlanRoute(position, away, currentDirection, routeThreatened);

            plannerCooldown = PlannerInterval;
        }

        float distance = HorizontalDistance(bobber.transform.position, fishing.transform.position);
        if (previousDistance < 0f || distance > previousDistance + MinimumProgressMetres || routeSideSign != 0)
            stalledSeconds = 0f;
        else
            stalledSeconds += Time.deltaTime;
        previousDistance = distance;

        // Stall detection is now only a fallback that requests another route plan;
        // it is no longer the primary shoreline-avoidance trigger.
        if (stalledSeconds >= StallGraceSeconds)
        {
            PlanRoute(bobber.transform.position, away, currentDirection, true);
            stalledSeconds = 0f;
            plannerCooldown = PlannerInterval;
        }
    }

    private void PlanRoute(Vector3 position, Vector3 away, Vector3 currentDirection, bool threatened)
    {
        Vector3 bestDirection = currentDirection;
        float bestScore = float.NegativeInfinity;
        int bestSign = routeSideSign;

        for (int i = 0; i < CandidateAngles.Length; i++)
        {
            float angle = CandidateAngles[i];
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * away;
            direction = HorizontalDirection(direction, away);

            // Rounding an island sometimes requires temporarily moving partly back
            // toward the player. More than ~125 degrees inward is never considered.
            float outward = Vector3.Dot(direction, away);
            if (outward < -0.58f) continue;

            float minimumDepth;
            float averageDepth;
            float endDepth;
            EvaluateRoute(position, direction, out minimumDepth, out averageDepth, out endDepth);

            float continuity = Vector3.Dot(direction, currentDirection);
            int sign = SignedSide(away, direction);
            float score = minimumDepth * 5.2f + averageDepth * 1.35f + endDepth * 0.75f;
            score += outward * 1.0f + continuity * 0.70f;

            if (minimumDepth < MinimumEscapeDepth) score -= 80f;
            else if (minimumDepth < SafeRouteDepth) score -= (SafeRouteDepth - minimumDepth) * 18f;

            if (routeSideSign != 0 && sign != 0)
                score += sign == routeSideSign ? 2.8f : -3.6f;

            // A tiny random term prevents two otherwise-identical routes from always
            // choosing the exact same side while remaining dominated by water depth.
            score += UnityEngine.Random.Range(-0.08f, 0.08f);

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = direction;
                bestSign = sign;
            }
        }

        FightTravelDirectionField.SetValue(fishing, bestDirection);
        FightMovePhaseField.SetValue(fishing, UnityEngine.Random.Range(0f, Mathf.PI * 2f));
        nextPhaseShuffle = UnityEngine.Random.Range(0.55f, 1.35f);

        if (bestSign != 0 && (threatened || routeSideSign != 0))
        {
            routeSideSign = bestSign;
            routeSideTimer = RouteSideHoldSeconds;
        }
        else if (!threatened)
        {
            routeSideSign = 0;
            routeSideTimer = 0f;
        }
    }

    private void EvaluateRoute(Vector3 origin, Vector3 direction, out float minimumDepth, out float averageDepth, out float endDepth)
    {
        minimumDepth = float.PositiveInfinity;
        averageDepth = 0f;
        endDepth = StableWaterDepth(origin);
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        int samples = 0;

        for (int i = 0; i < RouteDistances.Length; i++)
        {
            float distance = RouteDistances[i];
            Vector3 center = origin + direction * distance;

            // Three-point corridor means a route is rejected before the fish's weave
            // or body width can clip a shallow sandbar beside the centerline.
            float centerDepth = StableWaterDepth(center);
            float leftDepth = StableWaterDepth(center + side * 0.70f);
            float rightDepth = StableWaterDepth(center - side * 0.70f);
            float localMinimum = Mathf.Min(centerDepth, Mathf.Min(leftDepth, rightDepth));

            minimumDepth = Mathf.Min(minimumDepth, localMinimum);
            averageDepth += (centerDepth + leftDepth + rightDepth) / 3f;
            samples++;
            if (i == RouteDistances.Length - 1) endDepth = centerDepth;
        }

        if (samples > 0) averageDepth /= samples;
        if (float.IsPositiveInfinity(minimumDepth)) minimumDepth = endDepth;
    }

    private float RouteMinimumDepth(Vector3 origin, Vector3 direction)
    {
        float minimum, average, end;
        EvaluateRoute(origin, direction, out minimum, out average, out end);
        return minimum;
    }

    private void EmergencyRecover(GameObject bobber, Vector3 away)
    {
        Vector3 position = bobber.transform.position;
        Vector3 bestDirection = away;
        float bestDepth = StableWaterDepth(position);

        for (int i = 0; i < CandidateAngles.Length; i++)
        {
            Vector3 direction = HorizontalDirection(Quaternion.AngleAxis(CandidateAngles[i], Vector3.up) * away, away);
            Vector3 candidate = position + direction * 0.45f;
            float depth = StableWaterDepth(candidate);
            if (depth > bestDepth)
            {
                bestDepth = depth;
                bestDirection = direction;
            }
        }

        if (bestDepth <= StableWaterDepth(position) + 0.02f) return;

        Vector3 recovered = position + bestDirection * 0.32f;
        if (ocean != null) recovered.y = ocean.GetSurfaceHeight(recovered) - 0.08f;
        bobber.transform.position = recovered;
        CastPointField.SetValue(fishing, recovered);
        FightTravelDirectionField.SetValue(fishing, bestDirection);
        FishOnShoreField.SetValue(fishing, false);
    }

    private float StableWaterDepth(Vector3 position)
    {
        if (terrain == null || ocean == null) return 1000f;
        TerrainData data = terrain.terrainData;
        Vector3 local = position - terrain.transform.position;
        if (local.x < 0f || local.z < 0f || local.x > data.size.x || local.z > data.size.z)
            return 1000f;
        float ground = terrain.SampleHeight(position) + terrain.transform.position.y;
        return ocean.BaseWaterLevel - ground;
    }

    private static Vector3 HorizontalDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            fallback.y = 0f;
            direction = fallback.sqrMagnitude < 0.0001f ? Vector3.forward : fallback;
        }
        return direction.normalized;
    }

    private static int SignedSide(Vector3 away, Vector3 direction)
    {
        float angle = Vector3.SignedAngle(away, direction, Vector3.up);
        if (Mathf.Abs(angle) < 8f) return 0;
        return angle > 0f ? 1 : -1;
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
        plannerCooldown = 0f;
        routeSideTimer = 0f;
        routeSideSign = 0;
        previousDistance = -1f;
        stalledSeconds = 0f;
    }
}
