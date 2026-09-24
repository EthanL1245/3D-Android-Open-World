using UnityEngine;
using UnityEngine.UI;

// One UI mesh: a bottom-to-right quarter arc and a moving white needle.
public sealed class CastPowerGauge : MaskableGraphic
{
    private float power, minimum;
    private bool available;
    public void Set(float value,float minimumPower,bool rangeAvailable)
    {power=value;minimum=minimumPower;available=rangeAvailable;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();const int segments=64;
        for(int i=0;i<segments;i++)
        {
            float t=(i+.5f)/segments;
            Color shade=!available || t<minimum?new Color(.22f,.25f,.27f,.95f):
                t<.33f?Color.Lerp(new Color(.12f,.85f,.3f),Color.yellow,t/.33f):
                t<.67f?Color.Lerp(Color.yellow,new Color(1,.42f,.04f),(t-.33f)/.34f):
                Color.Lerp(new Color(1,.42f,.04f),new Color(.95f,.06f,.04f),(t-.67f)/.33f);
            Quad(vh,i/(float)segments,(i+1f)/segments,122,144,shade);
        }
        Quad(vh,Mathf.Max(0,power-.014f),Mathf.Min(1,power+.014f),115,153,Color.white);
    }
    private static Vector2 Point(float t,float radius)
    {float angle=Mathf.Lerp(-90,0,t)*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;}
    private static void Quad(VertexHelper vh,float a,float b,float inner,float outer,Color color)
    {
        int start=vh.currentVertCount;
        vh.AddVert(Point(a,inner),color,Vector2.zero);vh.AddVert(Point(a,outer),color,Vector2.zero);
        vh.AddVert(Point(b,outer),color,Vector2.zero);vh.AddVert(Point(b,inner),color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
    }
}
