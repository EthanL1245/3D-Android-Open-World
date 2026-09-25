using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Replaces the visible bobber with the authored lipless crankbait whenever a
/// permanent lure is being cast/retrieved. FishingSystem remains authoritative
/// for casting, bites and fights. The lure's authored transform and animation
/// are never rotated, flipped, rescaled or procedurally wobbled here.
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

        if (!ShouldShowLure(state, bobber))
        {
            StopShowingLure();
            return;
        }

        if (!EnsureLure())
            return;

        if (!showingLure)
            showingLure = true;

        SetBobberRenderers(false);
        lureRoot.SetActive(true);

        bool waiting = string.Equals(state, "Waiting", StringComparison.Ordinal);
        bool reeling = waiting && IsReeling();

        Vector3 logicalPosition = waiting && castPointField != null
            ? (Vector3)castPointField.GetValue(fishing)
            : bobber.transform.position;

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

        // Translation is required to put the lure at the cast/retrieve point.
        // Deliberately do NOT write rotation or scale. The prefab keeps the exact
        // orientation and size produced by the authored import.
        lureRoot.transform.position = position;

        bobber.transform.position = position;

        // Use the supplied animation exactly. No hook/body bones are touched by
        // this presentation component and no extra procedural wobble is added.
        if (lureAnimator != null)
            lureAnimator.speed = reeling ? 1f : 0f;

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

    private bool EnsureLure()
    {
        if (lureRoot != null)
            return true;

        GameObject prefab = Resources.Load<GameObject>(PrefabResource);
        if (prefab == null)
            return false;

        lureRoot = Instantiate(prefab);
        lureRoot.name = "ActiveLiplessCrankbait";

        // Keep the imported LineAttach exactly where the importer put it. Do not
        // reparent or reset it, because doing so would shift the string away from
        // the authored lure head.
        lineAttach = FindDeepChild(lureRoot.transform, "LineAttach");
        if (lineAttach == null)
        {
            GameObject attach = new GameObject("LineAttach");
            lineAttach = attach.transform;
            lineAttach.SetParent(lureRoot.transform, false);
        }

        lureAnimator = lureRoot.GetComponentInChildren<Animator>(true);
        if (lureAnimator != null)
        {
            lureAnimator.applyRootMotion = false;
            lureAnimator.speed = 0f;
            lureAnimator.Update(0f);
        }

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
