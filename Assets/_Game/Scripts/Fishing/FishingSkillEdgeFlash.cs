using UnityEngine;
using UnityEngine.UI;

// A transparent center with a soft white border; never intercepts touch input.
public sealed class FishingSkillEdgeFlash : MaskableGraphic
{
    private const float Duration = 2f;
    private float startedAt;

    public void Play()
    {
        startedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        SetVerticesDirty();
    }

    private void Update()
    {
        if (Time.unscaledTime - startedAt >= Duration)
        {
            gameObject.SetActive(false);
            return;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float elapsed = Time.unscaledTime - startedAt;
        if (elapsed >= Duration) return;
        float pulse = .5f + .5f * Mathf.Cos(elapsed * Mathf.PI * 2f * 6f);
        float alpha = (.12f + .5f * pulse) * Mathf.Clamp01((Duration - elapsed) / .2f);
        Rect r = rectTransform.rect;
        float inset = Mathf.Min(r.width, r.height) * .075f;
        Color outer = new Color(1f, 1f, 1f, alpha);
        Color inner = new Color(1f, 1f, 1f, 0f);
        vh.AddVert(new Vector2(r.xMin, r.yMin), outer, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin, r.yMax), outer, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMax), outer, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMin), outer, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin + inset, r.yMin + inset), inner, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin + inset, r.yMax - inset), inner, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax - inset, r.yMax - inset), inner, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax - inset, r.yMin + inset), inner, Vector2.zero);
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            vh.AddTriangle(i, next, next + 4);
            vh.AddTriangle(i, next + 4, i + 4);
        }
    }
}
