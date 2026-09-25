using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Prevents permanent-lure strikes once the lure reaches water shallower than
/// one metre. Runs before FishingSystem so the bite roll for the SAME retrieve
/// frame already knows the depth of the lure's next position.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class LureShallowWaterBiteGuard : MonoBehaviour
{
    private const float MinimumLureBiteDepth = 1.0f;

    private FishingSystem fishing;
    private FishingHUD hud;

    private FieldInfo stateField;
    private FieldInfo activeBaitField;
    private FieldInfo castPointField;
    private FieldInfo lureStartField;
    private FieldInfo lureEndField;
    private FieldInfo lureLengthField;
    private FieldInfo lureRetrievedField;
    private FieldInfo reelPowerField;
    private MethodInfo depthMethod;

    private readonly object[] depthArguments = new object[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<LureShallowWaterBiteGuard>() == null)
                system.gameObject.AddComponent<LureShallowWaterBiteGuard>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();

        Type type = typeof(FishingSystem);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        stateField = type.GetField("state", flags);
        activeBaitField = type.GetField("activeBait", flags);
        castPointField = type.GetField("castPoint", flags);
        lureStartField = type.GetField("lureStart", flags);
        lureEndField = type.GetField("lureEnd", flags);
        lureLengthField = type.GetField("lureLength", flags);
        lureRetrievedField = type.GetField("lureRetrieved", flags);
        reelPowerField = type.GetField("reelPower", flags);
        depthMethod = type.GetMethod("TryGetTerrainWaterDepth", flags);

        if (stateField == null || activeBaitField == null || castPointField == null ||
            lureStartField == null || lureEndField == null || lureLengthField == null ||
            lureRetrievedField == null || reelPowerField == null || depthMethod == null)
        {
            Debug.LogError("LureShallowWaterBiteGuard could not bind to FishingSystem. Lure bite safety disabled.");
            enabled = false;
        }
    }

    private void Update()
    {
        // Default to normal behavior. We suppress only the exact active lure
        // retrieve frame where the lure is actually shallower than 1 metre.
        FishingRules.LureBiteAllowed = true;

        if (fishing == null || !enabled)
            return;

        object stateValue = stateField.GetValue(fishing);
        if (stateValue == null || !string.Equals(stateValue.ToString(), "Waiting", StringComparison.Ordinal))
            return;

        if ((int)activeBaitField.GetValue(fishing) != ShopCatalog.StarterLure)
            return;

        Vector3 probePoint = (Vector3)castPointField.GetValue(fishing);

        if (hud == null)
            hud = FindFirstObjectByType<FishingHUD>();

        bool reeling = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;

        // FishingSystem advances the lure before rolling for a bite. Predict that
        // exact same next retrieve position so crossing the 1 m contour cannot get
        // one final shallow-water bite because this guard observed the prior frame.
        if (reeling)
        {
            float lureLength = (float)lureLengthField.GetValue(fishing);
            float lureRetrieved = (float)lureRetrievedField.GetValue(fishing);
            float reelPower = (float)reelPowerField.GetValue(fishing);

            if (lureLength > 0.0001f && lureRetrieved < lureLength)
            {
                float step = Mathf.Min(
                    lureLength - lureRetrieved,
                    2.4f * reelPower * Time.deltaTime);

                float nextRetrieved = lureRetrieved + Mathf.Max(0f, step);
                Vector3 lureStart = (Vector3)lureStartField.GetValue(fishing);
                Vector3 lureEnd = (Vector3)lureEndField.GetValue(fishing);
                probePoint = Vector3.Lerp(
                    lureStart,
                    lureEnd,
                    Mathf.Clamp01(nextRetrieved / lureLength));
            }
        }

        if (TryGetFishingDepth(probePoint, out float depth))
            FishingRules.LureBiteAllowed = depth >= MinimumLureBiteDepth;
    }

    private bool TryGetFishingDepth(Vector3 worldPosition, out float depth)
    {
        depth = float.PositiveInfinity;

        try
        {
            depthArguments[0] = worldPosition;
            depthArguments[1] = 0f;
            depthArguments[2] = 0f;
            depthArguments[3] = 0f;

            object result = depthMethod.Invoke(fishing, depthArguments);
            bool valid = result is bool value && value;
            if (valid)
                depth = (float)depthArguments[3];
            return valid;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void OnDisable()
    {
        FishingRules.LureBiteAllowed = true;
    }

    private void OnDestroy()
    {
        FishingRules.LureBiteAllowed = true;
    }
}
