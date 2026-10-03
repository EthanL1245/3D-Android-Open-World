using UnityEngine;
using UnityEngine.UI;

// Traces the actual rectangular status perimeter clockwise from the top centre.
public sealed class FishingStatusOutline : MaskableGraphic
{
    private float progress;
    public float Progress
    {
        get=>progress;
        set {float next=Mathf.Clamp01(value);if(Mathf.Approximately(progress,next))return;progress=next;SetVerticesDirty();}
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(progress<=0f)return;
        Rect r=rectTransform.rect;
        const float thickness=6f;
        float half=thickness*.5f;
        float left=r.xMin+half,right=r.xMax-half,top=r.yMax-half,bottom=r.yMin+half;
        Vector2[] points={new Vector2(r.center.x,top),new Vector2(right,top),new Vector2(right,bottom),
            new Vector2(left,bottom),new Vector2(left,top),new Vector2(r.center.x,top)};
        float remaining=progress*2f*((right-left)+(top-bottom));
        for(int i=0;i<points.Length-1 && remaining>0f;i++)
        {
            Vector2 a=points[i],delta=points[i+1]-a;
            float length=delta.magnitude;if(length<=0f)continue;
            Vector2 b=a+delta*(Mathf.Min(remaining,length)/length);
            Vector2 normal=new Vector2(-delta.y,delta.x)/length*half;
            var quad=new UIVertex[4];
            Vector2[] corners={a-normal,a+normal,b+normal,b-normal};
            for(int j=0;j<4;j++){quad[j]=UIVertex.simpleVert;quad[j].position=corners[j];quad[j].color=color;}
            vh.AddUIVertexQuad(quad);remaining-=length;
        }
    }
}
