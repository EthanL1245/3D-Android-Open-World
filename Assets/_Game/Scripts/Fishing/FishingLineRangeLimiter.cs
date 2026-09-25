using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Enforces the equipped line's hard maximum against the ACTUAL rendered line.
/// This intentionally runs after lure presentation so walking away from a lure,
/// fighting fish, or any other movement can never leave more line out than the
/// configured FishingSystem maximum.
/// </summary>
[DefaultExecutionOrder(1400)]
public sealed class FishingLineRangeLimiter : MonoBehaviour
{
    private FishingSystem fishing;
    private FieldInfo stateField;
    private FieldInfo rodEquippedField;
    private FieldInfo maximumLineDistanceField;
    private FieldInfo fishingLineField;
    private FieldInfo activeBaitField;
    private MethodInfo failFishingMethod;
    private bool retracting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        FishingSystem[] systems = FindObjectsByType<FishingSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            FishingSystem system = systems[i];
            if (system != null && system.GetComponent<FishingLineRangeLimiter>() == null)
                system.gameObject.AddComponent<FishingLineRangeLimiter>();
        }
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(FishingSystem);
        stateField = type.GetField("state", flags);
        rodEquippedField = type.GetField("rodEquipped", flags);
        maximumLineDistanceField = type.GetField("maximumLineDistance", flags);
        fishingLineField = type.GetField("fishingLine", flags);
        activeBaitField = type.GetField("activeBait", flags);
        failFishingMethod = type.GetMethod("FailFishing", flags);

        if (stateField == null || rodEquippedField == null ||
            maximumLineDistanceField == null || fishingLineField == null ||
            activeBaitField == null || failFishingMethod == null)
        {
            Debug.LogError("FishingLineRangeLimiter could not bind to FishingSystem and was disabled.");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (retracting || fishing == null)
            return;

        if (!(bool)rodEquippedField.GetValue(fishing))
            return;

        object stateValue = stateField.GetValue(fishing);
        string state = stateValue != null ? stateValue.ToString() : string.Empty;
        if (state == "Idle" || state == "Charging")
            return;

        LineRenderer line = fishingLineField.GetValue(fishing) as LineRenderer;
        if (line == null || !line.enabled || line.positionCount < 2)
            return;

        float maximum = Mathf.Max(0.01f, (float)maximumLineDistanceField.GetValue(fishing));
        float actualLength = RenderedLineLength(line);
        if (actualLength <= maximum + 0.001f)
            return;

        retracting = true;
        bool lure = (int)activeBaitField.GetValue(fishing) == ShopCatalog.StarterLure;
        string message = lure
            ? $"Line exceeded {maximum:0} m — lure automatically returned to your tackle."
            : $"Line exceeded {maximum:0} m — line automatically retracted.";

        try
        {
            failFishingMethod.Invoke(fishing, new object[] { message });
        }
        finally
        {
            retracting = false;
        }
    }

    private static float RenderedLineLength(LineRenderer line)
    {
        float length = 0f;
        Vector3 previous = LinePointWorld(line, 0);
        for (int i = 1; i < line.positionCount; i++)
        {
            Vector3 current = LinePointWorld(line, i);
            length += Vector3.Distance(previous, current);
            previous = current;
        }
        return length;
    }

    private static Vector3 LinePointWorld(LineRenderer line, int index)
    {
        Vector3 point = line.GetPosition(index);
        return line.useWorldSpace ? point : line.transform.TransformPoint(point);
    }
}
