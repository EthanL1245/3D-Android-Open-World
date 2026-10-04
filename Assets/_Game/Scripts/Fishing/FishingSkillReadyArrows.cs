using UnityEngine;
using UnityEngine.UI;

// Five translucent arrowheads. Brightness travels from bottom to top.
public sealed class FishingSkillReadyArrows : MaskableGraphic
{
    private void Update() { SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        for (int i = 0; i < 5; i++)
        {
            float phase = Mathf.Repeat(Time.unscaledTime * 1.2f - i * .16f, 1f);
            float pulse = Mathf.Pow((Mathf.Cos(phase * Mathf.PI * 2f) + 1f) * .5f, 4f);
            Color tint = new Color(1f, 1f, 1f, .12f + .43f * pulse);
            float y = rect.yMin + 10f + i * 20f;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(rect.center.x - 17f, y), tint, Vector2.zero);
            vh.AddVert(new Vector2(rect.center.x, y + 12f), tint, Vector2.zero);
            vh.AddVert(new Vector2(rect.center.x + 17f, y), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
