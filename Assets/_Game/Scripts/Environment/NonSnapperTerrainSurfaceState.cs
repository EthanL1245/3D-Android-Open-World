using UnityEngine;

/// <summary>
/// Persistent, scene-owned marker. Prevents texture restoration from running
/// repeatedly and overwriting the user's later manual terrain painting.
/// </summary>
[DisallowMultipleComponent]
public sealed class NonSnapperTerrainSurfaceState : MonoBehaviour
{
    [SerializeField, HideInInspector] private int completedVersion;
    public bool Restored => completedVersion >= 1;

#if UNITY_EDITOR
    public void MarkRestored()
    {
        completedVersion = 1;
    }
#endif
}
