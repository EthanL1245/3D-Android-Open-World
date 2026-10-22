using UnityEngine;

// Editor bake hint only; no per-rock Update, physics or runtime generation.
[DisallowMultipleComponent]
public sealed class ShorelineFoamSource : MonoBehaviour
{
    [Tooltip("Include this object's mesh children in the shoreline bake. Untick to exclude them. Boats and rigidbodies are always excluded.")]
    public bool include = true;
}
