using UnityEngine;

/// <summary>
/// Persisted in PrototypeWorld after the owner authorizes a starter-only grass
/// repair. Never repaint/rebuild this terrain except on a subsequent user request.
/// </summary>
[DisallowMultipleComponent]
public sealed class StarterIslandGrassLock : MonoBehaviour
{
    [SerializeField, HideInInspector] private bool saved;
    public bool IsSaved => saved;

#if UNITY_EDITOR
    public void MarkSaved() { saved = true; }
#endif
}
