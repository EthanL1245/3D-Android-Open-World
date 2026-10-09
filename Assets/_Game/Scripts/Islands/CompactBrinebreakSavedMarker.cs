using UnityEngine;

/// <summary>
/// Records the one-time saved-scene conversion. Prevents repeated reshaping after
/// the player manually edits the island or surrounding ocean in the Scene editor.
/// </summary>
[DisallowMultipleComponent]
public sealed class CompactBrinebreakSavedMarker : MonoBehaviour
{
    [SerializeField, HideInInspector] private bool baked;
    public bool IsBaked => baked;
    public void MarkBaked() { baked = true; }
}

