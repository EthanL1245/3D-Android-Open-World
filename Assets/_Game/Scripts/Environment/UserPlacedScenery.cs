using UnityEngine;

/// <summary>
/// Editor-authored scenery belongs to the scene and is never owned by a generator.
/// All procedural world scripts must exclude this hierarchy from deletion, grounding,
/// rebuilding, reparenting, or repositioning. It remains editable in Unity's Scene view.
/// </summary>
[DisallowMultipleComponent]
public sealed class UserPlacedScenery : MonoBehaviour
{
    public const string RootName = "USER PLACED SCENERY - DO NOT MODIFY BY CODE";

    public static bool Contains(Transform candidate)
    {
        while (candidate != null)
        {
            if (candidate.GetComponent<UserPlacedScenery>() != null)
                return true;
            candidate = candidate.parent;
        }
        return false;
    }
}
