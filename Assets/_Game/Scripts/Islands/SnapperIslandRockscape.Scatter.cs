using UnityEngine;

public sealed partial class SnapperIslandRockscape
{
    void Scatter()
    {
        ShoreArc(32,68,7);ShoreArc(132,188,8);ShoreArc(205,244,5);ShoreArc(276,322,7);
        for(int i=0;i<18;i++)
        {
            float a=N(0,Mathf.PI*2),r=N(.38f,.78f);
            Vector2 o=new Vector2(Mathf.Cos(a)*SnapperIslandGeometry.RadiusX*r,Mathf.Sin(a)*SnapperIslandGeometry.RadiusZ*r);
            if(Clear(o,12)||Mathf.Abs(o.x)<7&&o.y>9)continue;
            float s=N(.72f,1.20f);
            Place(Detail(),o,s,Tilt(),N(.16f,.27f),s>1.08f&&rng.NextDouble()<.22,s>.9f,-1);
        }
    }

    void Cluster(float cx,float cz,float rx,float rz,int count,float min,float max,bool colliders)
    {
        for(int i=0;i<count;i++)
        {
            float a=N(0,Mathf.PI*2),r=Mathf.Sqrt(N(0,1));
            Vector2 o=new Vector2(cx+Mathf.Cos(a)*r*rx,cz+Mathf.Sin(a)*r*rz);
            if(SnapperIslandGeometry.Ellipse(center+new Vector3(o.x,0,o.y),center)>.94f||Clear(o,13)||Mathf.Abs(o.x)<6.5f&&o.y>10)continue;
            float s=N(min,max);
            Place(Transition(),o,s,Tilt(),N(.18f,.28f),colliders&&s>1.35f&&rng.NextDouble()<.35,s>1,-1);
        }
    }

    void ShoreArc(float min,float max,int count)
    {
        for(int i=0;i<count;i++)
        {
            float a=N(min,max)*Mathf.Deg2Rad,r=N(.94f,1.10f);
            Vector2 o=new Vector2(Mathf.Cos(a)*SnapperIslandGeometry.RadiusX*r,Mathf.Sin(a)*SnapperIslandGeometry.RadiusZ*r);
            if(Clear(o,14))continue;
            float s=N(.72f,1.28f);
            Place(ShoreAsset(),o,s,Tilt(),N(.15f,.23f),s>1.12f&&rng.NextDouble()<.24,s>1.05f,N(.22f,.49f));
        }
    }

    int Transition(){int[] a={1,4,3,5,1,4,0};return a[rng.Next(a.Length)];}
    int ShoreAsset(){int[] a={3,5,4,5,3,1};return a[rng.Next(a.Length)];}
    int Detail(){int[] a={5,3,4,1,5,3,2};return a[rng.Next(a.Length)];}
    Vector3 Tilt(){return new Vector3(N(-13,13),N(0,360),N(-13,13));}
}
