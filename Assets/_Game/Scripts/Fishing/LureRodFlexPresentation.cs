using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Gives lure-only retrieval a small visual rod load. FishingSystem deliberately
/// relaxes the rod whenever no fish is hooked, so this runs afterward and applies
/// only the baseline line-pull bend (zero fight tension, no fight reel boost).
/// </summary>
[DefaultExecutionOrder(1200)]
public sealed class LureRodFlexPresentation : MonoBehaviour
{
    private FishingSystem fishing;
    private FishingHUD hud;

    private FieldInfo stateField;
    private FieldInfo activeBaitField;
    private FieldInfo castPointField;
    private FieldInfo bobberField;
    private FieldInfo rodViewField;
    private FieldInfo fishingLineField;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<LureRodFlexPresentation>() == null)
                system.gameObject.AddComponent<LureRodFlexPresentation>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        CacheMembers();
    }

    private void LateUpdate()
    {
        if (!IsRetrievingLure())
            return;

        FishingRodView rodView = rodViewField.GetValue(fishing) as FishingRodView;
        if (rodView == null)
            return;

        Vector3 lurePosition = (Vector3)castPointField.GetValue(fishing);

        // reeling=false is intentional here: SetLinePull's "reeling" flag adds
        // a large fight-style +26 degree load. Zero tension + baseline pull gives
        // the subtle bend of a calm/very-low-tension fish while the separate
        // FishingSystem code still spins the reel animation normally.
        rodView.SetLinePull(
            lurePosition,
            0f,
            false,
            Time.deltaTime);

        // FishingSystem positioned the line earlier in the frame, before this
        // LateUpdate-only lure bend moved the visual rod tip. Re-anchor the line
        // after the bend so it always begins at the current visible RodTip.
        LineRenderer fishingLine = fishingLineField.GetValue(fishing) as LineRenderer;
        if (fishingLine != null && fishingLine.enabled &&
            fishingLine.positionCount >= 2 && rodView.RodTip != null)
        {
            fishingLine.SetPosition(0, rodView.RodTip.position);
        }
    }

    private bool IsRetrievingLure()
    {
        if (fishing == null || stateField == null || activeBaitField == null ||
            castPointField == null || bobberField == null || rodViewField == null ||
            fishingLineField == null)
            return false;

        object state = stateField.GetValue(fishing);
        if (state == null || !string.Equals(state.ToString(), "Waiting", StringComparison.Ordinal))
            return false;

        if ((int)activeBaitField.GetValue(fishing) != ShopCatalog.StarterLure)
            return false;

        GameObject bobber = bobberField.GetValue(fishing) as GameObject;
        if (bobber == null || !bobber.activeSelf)
            return false;

        if (hud == null)
            hud = FindFirstObjectByType<FishingHUD>();

        return hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
    }

    private void CacheMembers()
    {
        Type type = typeof(FishingSystem);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        stateField = type.GetField("state", flags);
        activeBaitField = type.GetField("activeBait", flags);
        castPointField = type.GetField("castPoint", flags);
        bobberField = type.GetField("bobber", flags);
        rodViewField = type.GetField("rodView", flags);
        fishingLineField = type.GetField("fishingLine", flags);

        if (stateField == null || activeBaitField == null || castPointField == null ||
            bobberField == null || rodViewField == null || fishingLineField == null)
        {
            Debug.LogError(
                "LureRodFlexPresentation could not bind to FishingSystem. Lure rod flex is disabled.");
            enabled = false;
        }
    }
}
