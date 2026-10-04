using UnityEngine;
using UnityEngine.UI;

// One circular graphic and matching hit area, with no texture import dependency.
public sealed class FishingDragHandle : MaskableGraphic, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPoint, eventCamera, out Vector2 local)) return false;
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        return (local - rect.center).sqrMagnitude <= radius * radius;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        const int segments = 64;
        vh.AddVert(rect.center, color, new Vector2(.5f, .5f));
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(rect.center + direction * radius, color, direction * .5f + Vector2.one * .5f);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(0, i + 1, (i + 1) % segments + 1);
    }
}
