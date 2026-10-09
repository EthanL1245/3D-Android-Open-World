using UnityEngine;

/// <summary>Once-only marker for the persistent Brinebreak relocation/seabed edit.
/// Prevents a later Unity session from overwriting hand-authored scene changes.</summary>
[DisallowMultipleComponent]
public sealed class BrinebreakRelocationMarker : MonoBehaviour
{
    [SerializeField, HideInInspector] private bool completed;
    public bool Completed => completed;
    public void MarkCompleted() { completed = true; }
}
