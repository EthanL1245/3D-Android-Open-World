using UnityEngine;
using UnityEngine.UI;

// Traces the circular status icon clockwise from twelve o'clock.
public sealed class FishingStatusOutline : MaskableGraphic
{
    private float progress;
    public float Progress
    {
        get => progress;
        set
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, next)) return;
            progress = next;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (progress <= 0f) return;
        Rect r = rectTransform.rect;
        // The supplied artwork has a small transparent margin around its circle.
        float outer = Mathf.Min(r.width, r.height) * .475f;
        float inner = Mathf.Max(0f, outer - 5f);
        const int segments = 96;
        int count = Mathf.CeilToInt(progress * segments);
        var quad = new UIVertex[4];
        for (int i = 0; i < count; i++)
        {
            float start = (float)i / segments * Mathf.PI * 2f;
            float end = Mathf.Min((float)(i + 1) / segments, progress) * Mathf.PI * 2f;
            Vector2 a = new Vector2(Mathf.Sin(start), Mathf.Cos(start));
            Vector2 b = new Vector2(Mathf.Sin(end), Mathf.Cos(end));
            Vector2[] corners = { r.center + a * outer, r.center + b * outer,
                r.center + b * inner, r.center + a * inner };
            for (int j = 0; j < 4; j++)
            {
                quad[j] = UIVertex.simpleVert;
                quad[j].position = corners[j];
                quad[j].color = color;
            }
            vh.AddUIVertexQuad(quad);
        }
    }
}
