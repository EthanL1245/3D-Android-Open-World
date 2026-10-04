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
        Disc(vh, rect.center, radius, new Color(.015f,.18f,.23f,color.a));
        Disc(vh, rect.center, radius-3f, new Color(.02f,.95f,1f,color.a));
        Disc(vh, rect.center, radius-7f, new Color(.01f,.35f,.44f,color.a));
        Disc(vh, rect.center, radius*.63f, new Color(.80f,.96f,1f,color.a));
    }

    private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        const int segments = 64;
        int first = vh.currentVertCount;
        vh.AddVert(center, tint, new Vector2(.5f,.5f));
        for(int i=0;i<segments;i++)
        {
            float angle=i*Mathf.PI*2f/segments;
            var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            vh.AddVert(center+direction*radius,tint,direction*.5f+Vector2.one*.5f);
        }
        for(int i=0;i<segments;i++) vh.AddTriangle(first,first+i+1,first+(i+1)%segments+1);
    }
}
