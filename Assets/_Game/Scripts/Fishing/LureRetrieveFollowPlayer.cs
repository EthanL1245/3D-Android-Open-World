using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Keeps the permanent retrieval lure aimed at the player's CURRENT position.
/// FishingSystem owns the normal lure speed, bite rolls and fight transition;
/// this controller only refreshes its retrieve path before each fishing update
/// and auto-picks the lure up when the player walks close enough to it.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class LureRetrieveFollowPlayer : MonoBehaviour
{
    private const float PickupDistance = 1.15f;
    private const float MinimumWaterDepth = 0.08f;
    private const float BlockedPathLength = 100000f;

    private FishingSystem fishing;
    private OceanWater oceanWater;

    private FieldInfo stateField;
    private FieldInfo activeBaitField;
    private FieldInfo castPointField;
    private FieldInfo originalCastDistanceField;
    private FieldInfo lureStartField;
    private FieldInfo lureEndField;
    private FieldInfo lureLengthField;
    private FieldInfo lureRetrievedField;
    private FieldInfo reelPowerField;
    private FieldInfo bobberField;
    private FieldInfo oceanWaterField;

    private MethodInfo failFishingMethod;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems =
            FindObjectsByType<FishingSystem>(
                FindObjectsSortMode.None
            );

        for (int i = 0;
             i < systems.Length;
             i++)
        {
            FishingSystem system =
                systems[i];

            if (system != null &&
                system.GetComponent<LureRetrieveFollowPlayer>() == null)
            {
                system.gameObject
                    .AddComponent<LureRetrieveFollowPlayer>();
            }
        }
    }

    private void Awake()
    {
        fishing =
            GetComponent<FishingSystem>();

        CacheMembers();
    }

    private void Update()
    {
        if (!IsActiveLureRetrieve())
            return;

        Vector3 current =
            (Vector3)castPointField.GetValue(
                fishing
            );

        Vector3 player =
            fishing.transform.position;

        Vector3 toPlayer =
            player -
            current;

        toPlayer.y = 0f;

        float horizontalDistance =
            toPlayer.magnitude;

        if (horizontalDistance <=
            PickupDistance)
        {
            PickUpLure();
            return;
        }

        Vector3 desiredDirection =
            toPlayer /
            horizontalDistance;

        float reelPower =
            Mathf.Max(
                0.01f,
                (float)reelPowerField.GetValue(
                    fishing
                )
            );

        float frameStep =
            2.4f *
            reelPower *
            Time.deltaTime;

        Vector3 direction =
            FindWaterDirection(
                current,
                desiredDirection,
                frameStep
            );

        float originalDistance =
            Mathf.Max(
                0.01f,
                (float)originalCastDistanceField.GetValue(
                    fishing
                )
            );

        lureStartField.SetValue(
            fishing,
            current
        );

        lureRetrievedField.SetValue(
            fishing,
            0f
        );

        if (direction.sqrMagnitude <
            0.0001f)
        {
            // The lure has reached a shoreline but the player is still too
            // far away to collect it. Keep it there until the player walks
            // closer. A huge path length also makes the normal lure bite
            // fraction effectively zero while it is physically stationary.
            lureLengthField.SetValue(
                fishing,
                BlockedPathLength
            );

            lureEndField.SetValue(
                fishing,
                current
            );

            return;
        }

        // FishingSystem moves by:
        // Lerp(lureStart, lureEnd, step / lureLength).
        // Give it a path whose geometric length equals lureLength so that its
        // existing 2.4 m/s reel speed is preserved exactly, while the heading
        // is refreshed toward the player's live position every frame.
        lureLengthField.SetValue(
            fishing,
            originalDistance
        );

        lureEndField.SetValue(
            fishing,
            current +
            direction.normalized *
            originalDistance
        );
    }

    private void LateUpdate()
    {
        if (!IsActiveLureRetrieve())
            return;

        Vector3 current =
            (Vector3)castPointField.GetValue(
                fishing
            );

        Vector3 delta =
            fishing.transform.position -
            current;

        delta.y = 0f;

        if (delta.magnitude <=
            PickupDistance)
        {
            PickUpLure();
        }
    }

    private Vector3 FindWaterDirection(
        Vector3 current,
        Vector3 desiredDirection,
        float step)
    {
        if (desiredDirection.sqrMagnitude <
            0.0001f)
        {
            return Vector3.zero;
        }

        desiredDirection.Normalize();

        // Prefer the direct route to the player's CURRENT position. Near a
        // shoreline, progressively fan out so the lure slides through water
        // instead of travelling across dry terrain toward an old cast point.
        float[] angles =
        {
            0f,
            12f,
            -12f,
            24f,
            -24f,
            38f,
            -38f,
            55f,
            -55f,
            72f,
            -72f
        };

        float testDistance =
            Mathf.Max(
                0.04f,
                step
            );

        for (int i = 0;
             i < angles.Length;
             i++)
        {
            Vector3 direction =
                Quaternion.AngleAxis(
                    angles[i],
                    Vector3.up
                ) *
                desiredDirection;

            Vector3 candidate =
                current +
                direction *
                testDistance;

            if (IsUsableWater(
                    candidate))
            {
                return direction.normalized;
            }
        }

        return Vector3.zero;
    }

    private bool IsUsableWater(
        Vector3 position)
    {
        Terrain terrain =
            Terrain.activeTerrain;

        if (terrain == null ||
            oceanWater == null)
        {
            return true;
        }

        TerrainData data =
            terrain.terrainData;

        Vector3 local =
            position -
            terrain.transform.position;

        // Outside the finite terrain, FishingSystem treats the infinite ocean
        // as deep water too.
        if (local.x < 0f ||
            local.z < 0f ||
            local.x > data.size.x ||
            local.z > data.size.z)
        {
            return true;
        }

        float ground =
            terrain.SampleHeight(
                position
            ) +
            terrain.transform.position.y;

        float water =
            oceanWater.GetSurfaceHeight(
                position
            );

        return
            water -
            ground >
            MinimumWaterDepth;
    }

    private bool IsActiveLureRetrieve()
    {
        if (fishing == null ||
            stateField == null ||
            activeBaitField == null ||
            castPointField == null ||
            bobberField == null)
        {
            return false;
        }

        object state =
            stateField.GetValue(
                fishing
            );

        if (state == null ||
            !string.Equals(
                state.ToString(),
                "Waiting",
                StringComparison.Ordinal))
        {
            return false;
        }

        int activeBait =
            (int)activeBaitField.GetValue(
                fishing
            );

        if (activeBait !=
            ShopCatalog.StarterLure)
        {
            return false;
        }

        GameObject bobber =
            bobberField.GetValue(
                fishing
            ) as GameObject;

        return
            bobber != null &&
            bobber.activeSelf;
    }

    private void PickUpLure()
    {
        if (!IsActiveLureRetrieve())
            return;

        failFishingMethod.Invoke(
            fishing,
            new object[]
            {
                "Lure retrieved. Cast again."
            }
        );
    }

    private void CacheMembers()
    {
        Type type =
            typeof(FishingSystem);

        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic;

        stateField =
            type.GetField(
                "state",
                flags
            );

        activeBaitField =
            type.GetField(
                "activeBait",
                flags
            );

        castPointField =
            type.GetField(
                "castPoint",
                flags
            );

        originalCastDistanceField =
            type.GetField(
                "originalCastDistance",
                flags
            );

        lureStartField =
            type.GetField(
                "lureStart",
                flags
            );

        lureEndField =
            type.GetField(
                "lureEnd",
                flags
            );

        lureLengthField =
            type.GetField(
                "lureLength",
                flags
            );

        lureRetrievedField =
            type.GetField(
                "lureRetrieved",
                flags
            );

        reelPowerField =
            type.GetField(
                "reelPower",
                flags
            );

        bobberField =
            type.GetField(
                "bobber",
                flags
            );

        oceanWaterField =
            type.GetField(
                "oceanWater",
                flags
            );

        failFishingMethod =
            type.GetMethod(
                "FailFishing",
                flags
            );

        if (oceanWaterField != null &&
            fishing != null)
        {
            oceanWater =
                oceanWaterField.GetValue(
                    fishing
                ) as OceanWater;
        }

        if (stateField == null ||
            activeBaitField == null ||
            castPointField == null ||
            originalCastDistanceField == null ||
            lureStartField == null ||
            lureEndField == null ||
            lureLengthField == null ||
            lureRetrievedField == null ||
            reelPowerField == null ||
            bobberField == null ||
            failFishingMethod == null)
        {
            Debug.LogError(
                "LureRetrieveFollowPlayer could not bind to FishingSystem. " +
                "The lure retrieve helper is disabled."
            );

            enabled = false;
        }
    }
}
