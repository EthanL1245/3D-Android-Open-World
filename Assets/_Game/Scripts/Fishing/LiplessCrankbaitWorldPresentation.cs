using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Replaces the visible bobber with the authored lipless crankbait whenever a
/// permanent lure is being cast/retrieved. FishingSystem remains authoritative
/// for casting, bites and fights; this component is presentation-only.
/// </summary>
[DefaultExecutionOrder(900)]
public sealed class LiplessCrankbaitWorldPresentation : MonoBehaviour
{
    private const string PrefabResource = "Fishing/LiplessCrankbaitGreenStriped";
    private const float RetrieveDepth = 0.20f;
    private const float FloatDepth = 0.015f;

    private FishingSystem fishing;
    private ShopProgress shopProgress;
    private FishingHUD hud;
    private OceanWater oceanWater;

    private FieldInfo stateField;
    private FieldInfo activeBaitField;
    private FieldInfo bobberField;
    private FieldInfo fishingLineField;
    private FieldInfo castPointField;
    private FieldInfo oceanWaterField;

    private GameObject lureRoot;
    private Transform lineAttach;
    private Animator lureAnimator;
    private Renderer[] bobberRenderers;

    private bool showingLure;
    private float sinkDepth;
    private Vector3 previousLogicalPosition;
    private Vector3 travelDirection = Vector3.forward;
    private Quaternion smoothRotation = Quaternion.identity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<LiplessCrankbaitWorldPresentation>() == null)
                system.gameObject.AddComponent<LiplessCrankbaitWorldPresentation>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        shopProgress = GetComponent<ShopProgress>();
        CacheMembers();
    }

    private void LateUpdate()
    {
        if (fishing == null || stateField == null || bobberField == null)
            return;

        string state = GetStateName();
        GameObject bobber = bobberField.GetValue(fishing) as GameObject;
        if (bobber == null)
            return;

        bool lureState = ShouldShowLure(state, bobber);
        if (!lureState)
        {
            StopShowingLure();
            return;
        }

        if (!EnsureLure())
            return;

        if (!showingLure)
        {
            showingLure = true;
            previousLogicalPosition = bobber.transform.position;
            travelDirection = Vector3.ProjectOnPlane(fishing.transform.position - previousLogicalPosition, Vector3.up);
            if (travelDirection.sqrMagnitude < 0.0001f) travelDirection = Vector3.forward;
            travelDirection.Normalize();
            smoothRotation = Quaternion.LookRotation(travelDirection, Vector3.up);
        }

        SetBobberRenderers(false);
        lureRoot.SetActive(true);

        bool waiting = string.Equals(state, "Waiting", StringComparison.Ordinal);
        bool reeling = waiting && IsReeling();

        Vector3 logicalPosition = waiting && castPointField != null
            ? (Vector3)castPointField.GetValue(fishing)
            : bobber.transform.position;

        Vector3 frameTravel = logicalPosition - previousLogicalPosition;
        frameTravel.y = 0f;
        if (frameTravel.sqrMagnitude > 0.000005f)
            travelDirection = frameTravel.normalized;
        else if (reeling)
        {
            Vector3 towardPlayer = fishing.transform.position - logicalPosition;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude > 0.0001f)
                travelDirection = towardPlayer.normalized;
        }
        previousLogicalPosition = logicalPosition;

        float targetDepth = reeling ? RetrieveDepth : FloatDepth;
        float depthSpeed = reeling ? 0.70f : 0.38f;
        sinkDepth = Mathf.MoveTowards(sinkDepth, targetDepth, depthSpeed * Time.deltaTime);

        Vector3 position = logicalPosition;
        if (waiting && oceanWater != null)
        {
            float surface = oceanWater.GetSurfaceHeight(logicalPosition);
            position.y = surface - sinkDepth;
            if (!reeling)
                position.y += Mathf.Sin(Time.time * 2.6f) * 0.012f;
        }

        Vector3 up = waiting ? GetWaterNormal(logicalPosition) : Vector3.up;
        Quaternion heading = Quaternion.LookRotation(travelDirection, up);
        float response = reeling ? 12f : 5f;
        smoothRotation = Quaternion.Slerp(
            smoothRotation,
            heading,
            1f - Mathf.Exp(-response * Time.deltaTime));

        // Lipless crankbaits vibrate rapidly while being worked. Because the
        // prefab origin is the nose/line tie, this wobble makes the rear and
        // hooks visibly trail behind rather than leading the motion.
        float vibration = reeling ? Mathf.Sin(Time.time * 31f) : 0f;
        Quaternion wobble = Quaternion.Euler(
            reeling ? 5f + Mathf.Sin(Time.time * 17f) * 1.5f : -1.5f,
            reeling ? vibration * 4.0f : 0f,
            reeling ? Mathf.Sin(Time.time * 27f + 0.8f) * 7.0f : Mathf.Sin(Time.time * 2.2f) * 1.5f);

        lureRoot.transform.position = position;
        lureRoot.transform.rotation = smoothRotation * wobble;

        // Keep FishingSystem's invisible logical bobber at the lure nose for
        // line-distance/fight handoff consistency. Next Update will still do
        // its normal authoritative surface/path calculations.
        bobber.transform.position = position;

        if (lureAnimator != null)
            lureAnimator.speed = reeling ? 1.35f : 0f;

        LineRenderer line = fishingLineField != null
            ? fishingLineField.GetValue(fishing) as LineRenderer
            : null;
        if (line != null && line.enabled && line.positionCount >= 2)
            line.SetPosition(1, lineAttach != null ? lineAttach.position : lureRoot.transform.position);
    }

    private bool ShouldShowLure(string state, GameObject bobber)
    {
        if (bobber == null || !bobber.activeSelf)
            return false;

        if (string.Equals(state, "Waiting", StringComparison.Ordinal))
        {
            if (activeBaitField == null) return false;
            return (int)activeBaitField.GetValue(fishing) == ShopCatalog.StarterLure;
        }

        // activeBait is committed only after landing, so while the cast is in
        // flight look at the currently equipped tackle selection instead.
        if (string.Equals(state, "Casting", StringComparison.Ordinal))
            return shopProgress != null && shopProgress.Data.baitEquipped == ShopCatalog.StarterLure;

        return false;
    }

    private bool IsReeling()
    {
        if (hud == null)
            hud = FindFirstObjectByType<FishingHUD>();
        return hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
    }

    private Vector3 GetWaterNormal(Vector3 position)
    {
        if (oceanWater == null) return Vector3.up;
        const float sample = 0.20f;
        float left = oceanWater.GetSurfaceHeight(position - Vector3.right * sample);
        float right = oceanWater.GetSurfaceHeight(position + Vector3.right * sample);
        float back = oceanWater.GetSurfaceHeight(position - Vector3.forward * sample);
        float front = oceanWater.GetSurfaceHeight(position + Vector3.forward * sample);
        Vector3 tangentX = new Vector3(sample * 2f, right - left, 0f);
        Vector3 tangentZ = new Vector3(0f, front - back, sample * 2f);
        Vector3 normal = Vector3.Cross(tangentZ, tangentX).normalized;
        return normal.y < 0f ? -normal : normal;
    }

    private bool EnsureLure()
    {
        if (lureRoot != null)
            return true;

        GameObject prefab = Resources.Load<GameObject>(PrefabResource);
        if (prefab == null)
            return false;

        lureRoot = Instantiate(prefab);
        lureRoot.name = "ActiveLiplessCrankbait";
        lineAttach = FindDeepChild(lureRoot.transform, "LineAttach");
        if (lineAttach == null) lineAttach = lureRoot.transform;
        lureAnimator = lureRoot.GetComponentInChildren<Animator>(true);
        lureRoot.SetActive(false);
        return true;
    }

    private void StopShowingLure()
    {
        if (!showingLure)
            return;

        showingLure = false;
        sinkDepth = 0f;
        if (lureRoot != null) lureRoot.SetActive(false);
        if (lureAnimator != null) lureAnimator.speed = 0f;
        SetBobberRenderers(true);
    }

    private void SetBobberRenderers(bool visible)
    {
        GameObject bobber = bobberField != null ? bobberField.GetValue(fishing) as GameObject : null;
        if (bobber == null) return;
        if (bobberRenderers == null || bobberRenderers.Length == 0)
            bobberRenderers = bobber.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < bobberRenderers.Length; i++)
            if (bobberRenderers[i] != null) bobberRenderers[i].enabled = visible;
    }

    private string GetStateName()
    {
        object value = stateField.GetValue(fishing);
        return value != null ? value.ToString() : string.Empty;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private void CacheMembers()
    {
        Type type = typeof(FishingSystem);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        stateField = type.GetField("state", flags);
        activeBaitField = type.GetField("activeBait", flags);
        bobberField = type.GetField("bobber", flags);
        fishingLineField = type.GetField("fishingLine", flags);
        castPointField = type.GetField("castPoint", flags);
        oceanWaterField = type.GetField("oceanWater", flags);
        if (oceanWaterField != null && fishing != null)
            oceanWater = oceanWaterField.GetValue(fishing) as OceanWater;

        if (stateField == null || activeBaitField == null || bobberField == null ||
            fishingLineField == null || castPointField == null)
        {
            Debug.LogError("LiplessCrankbaitWorldPresentation could not bind to FishingSystem.");
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (lureRoot != null) lureRoot.SetActive(false);
        if (fishing != null && bobberField != null) SetBobberRenderers(true);
    }

    private void OnDestroy()
    {
        if (lureRoot != null) Destroy(lureRoot);
    }
}
