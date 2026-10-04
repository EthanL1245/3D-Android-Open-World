using UnityEngine;
using UnityEngine.UI;

// Resolution-independent HUD artwork, drawn without font-dependent glyphs.
public sealed class FishingHudSymbol : MaskableGraphic
{
    public enum Kind { Reel, Bolt, Pin, Island, Gear }
    public Kind kind;
    public static void Add(Transform parent, Kind kind, Vector2 anchor, Vector2 position, float size)
    {
        var go = new GameObject("Hud" + kind, typeof(RectTransform), typeof(FishingHudSymbol));
        go.transform.SetParent(parent, false);
        var graphic = go.GetComponent<FishingHudSymbol>();
        graphic.kind = kind; graphic.raycastTarget = false;
        graphic.color = kind == Kind.Bolt ? FishingHudTheme.Cyan : new Color(.85f,.97f,1f,1f);
        var r = graphic.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.anchoredPosition = position; r.sizeDelta = Vector2.one * size;
    }
    private Vector3 P(float x, float y)
    {
        Rect r = rectTransform.rect;
        return new Vector3(r.xMin + x*r.width, r.yMin + y*r.height, 0);
    }
    private void Quad(VertexHelper vh, float x, float y, float w, float h)
    {
        int n = vh.currentVertCount;
        vh.AddVert(P(x,y),color,Vector2.zero); vh.AddVert(P(x+w,y),color,Vector2.zero);
        vh.AddVert(P(x+w,y+h),color,Vector2.zero); vh.AddVert(P(x,y+h),color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
    }
    private void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
    {
        int n = vh.currentVertCount;
        vh.AddVert(P(a.x,a.y),color,Vector2.zero); vh.AddVert(P(b.x,b.y),color,Vector2.zero);
        vh.AddVert(P(c.x,c.y),color,Vector2.zero); vh.AddTriangle(n,n+1,n+2);
    }
    private void Ring(VertexHelper vh, float x, float y, float radius, float width, float fraction = 1f)
    {
        for(int i=0;i<64*fraction;i++)
        {
            float a=i*Mathf.PI*2/64, b=(i+1)*Mathf.PI*2/64;
            Vector2 center = new Vector2(x,y), u=new Vector2(Mathf.Sin(a),Mathf.Cos(a)), v=new Vector2(Mathf.Sin(b),Mathf.Cos(b));
            Tri(vh,center+u*radius,center+v*radius,center+v*(radius-width));
            Tri(vh,center+u*radius,center+v*(radius-width),center+u*(radius-width));
        }
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        switch(kind)
        {
            case Kind.Reel:
                Ring(vh,.5f,.5f,.44f,.065f,.8f);
                Quad(vh,.30f,.25f,.31f,.5f); Quad(vh,.25f,.22f,.08f,.56f); Quad(vh,.60f,.22f,.08f,.56f);
                Quad(vh,.67f,.40f,.16f,.06f); Quad(vh,.79f,.28f,.065f,.16f); Ring(vh,.82f,.26f,.08f,.08f);
                break;
            case Kind.Bolt:
                Tri(vh,new Vector2(.49f,.96f),new Vector2(.18f,.43f),new Vector2(.73f,.43f));
                Tri(vh,new Vector2(.36f,.02f),new Vector2(.80f,.59f),new Vector2(.36f,.59f)); break;
            case Kind.Pin:
                Ring(vh,.5f,.66f,.29f,.14f);
                Tri(vh,new Vector2(.22f,.59f),new Vector2(.78f,.59f),new Vector2(.5f,.06f)); break;
            case Kind.Island:
                Tri(vh,new Vector2(.04f,.12f),new Vector2(.94f,.12f),new Vector2(.54f,.30f));
                Quad(vh,.47f,.26f,.065f,.47f);
                Tri(vh,new Vector2(.5f,.72f),new Vector2(.12f,.65f),new Vector2(.27f,.87f));
                Tri(vh,new Vector2(.5f,.72f),new Vector2(.87f,.62f),new Vector2(.75f,.86f));
                Tri(vh,new Vector2(.5f,.72f),new Vector2(.38f,.98f),new Vector2(.65f,.91f)); break;
            case Kind.Gear:
                Ring(vh,.5f,.5f,.32f,.13f);
                for(int i=0;i<8;i++) {float a=i*Mathf.PI/4; Quad(vh,.45f+Mathf.Sin(a)*.32f,.45f+Mathf.Cos(a)*.32f,.1f,.1f);} break;
        }
    }
}
