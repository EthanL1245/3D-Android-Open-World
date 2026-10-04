using UnityEngine;

// Follows the existing distance label, including its no-fish position.
public sealed class FishingHudDistanceBackdrop : MonoBehaviour
{
    public RectTransform label;
    private void LateUpdate()
    {
        if (label == null) return;
        var r = (RectTransform)transform;
        r.anchorMin = label.anchorMin; r.anchorMax = label.anchorMax; r.pivot = label.pivot;
        r.sizeDelta = label.sizeDelta + new Vector2(16,4);
        r.anchoredPosition = label.anchoredPosition - new Vector2(8,0);
    }
}
