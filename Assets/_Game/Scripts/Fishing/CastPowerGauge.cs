using UnityEngine;
using UnityEngine.UI;

// Presentation only: rounded quarter arc, white rim and black-outlined needle.
public sealed class CastPowerGauge : MaskableGraphic
{
    private float power;
    private bool[] samples;
    private bool available;
    public void Set(float value,bool[] validSamples,bool rangeAvailable)
    {power=value;samples=validSamples;available=rangeAvailable;SetVerticesDirty();}

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        const int segments=64;
        // A continuous white silhouette underneath the colored track.
        for(int i=0;i<segments;i++)
            Quad(vh,i/(float)segments,(i+1f)/segments,176,222,Color.white);
        Cap(vh,0,23,Color.white);
        Cap(vh,1,23,Color.white);
        for(int i=0;i<segments;i++)
            Quad(vh,i/(float)segments,(i+1f)/segments,180,218,Shade((i+.5f)/segments));
        Cap(vh,0,19,Shade(0));
        Cap(vh,1,19,Shade(1));
        // Draw the black outline first so the white moving line remains distinct
        // against every gauge color, including yellow and the white track rim.
        Needle(vh,Mathf.Clamp01(power),168,232,7.5f,Color.black);
        Needle(vh,Mathf.Clamp01(power),171,229,4.5f,Color.white);
    }

    private Color Shade(float t)
    {
        return !available || !FishingRules.IsCastPowerAvailable(samples,t)?new Color(.22f,.25f,.27f,.95f):
            t<.33f?Color.Lerp(new Color(.12f,.85f,.3f),Color.yellow,t/.33f):
            t<.67f?Color.Lerp(Color.yellow,new Color(1,.42f,.04f),(t-.33f)/.34f):
            Color.Lerp(new Color(1,.42f,.04f),new Color(.95f,.06f,.04f),(t-.67f)/.33f);
    }

    private static Vector2 Point(float t,float radius)
    {float angle=Mathf.Lerp(180,90,t)*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}

    private static void Cap(VertexHelper vh,float t,float radius,Color color)
    {
        Vector2 center=Point(t,199);
        Vector2 radial=Point(t,1);
        // Extend each semicircle away from the track, without covering its gradient.
        Vector2 tangent=t==0?new Vector2(0,-1):new Vector2(1,0);
        const int steps=24;
        for(int i=0;i<steps;i++)
        {
            float a=i*Mathf.PI/steps,b=(i+1)*Mathf.PI/steps;
            int start=vh.currentVertCount;
            vh.AddVert(center,color,Vector2.zero);
            vh.AddVert(center+(radial*Mathf.Cos(a)+tangent*Mathf.Sin(a))*radius,color,Vector2.zero);
            vh.AddVert(center+(radial*Mathf.Cos(b)+tangent*Mathf.Sin(b))*radius,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);
        }
    }

    private static void Needle(VertexHelper vh,float t,float inner,float outer,float halfWidth,Color color)
    {
        Vector2 radial=Point(t,1),side=new Vector2(-radial.y,radial.x)*halfWidth;
        AddQuad(vh,radial*inner-side,radial*outer-side,radial*outer+side,radial*inner+side,color);
    }

    private static void Quad(VertexHelper vh,float a,float b,float inner,float outer,Color color)
    {AddQuad(vh,Point(a,inner),Point(a,outer),Point(b,outer),Point(b,inner),color);}

    private static void AddQuad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
    {
        int start=vh.currentVertCount;
        vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);
        vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
    }
}
