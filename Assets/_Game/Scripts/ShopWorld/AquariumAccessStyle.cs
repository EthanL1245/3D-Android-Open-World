using System.Collections.Generic;
using UnityEngine;

// Upgrade existing Home and Quay stairs without rebuilding either saved scene.
public sealed class AquariumAccessStyle : MonoBehaviour
{
    private Material glass,metal;
    private void Start()
    {
        var steps=new List<Transform>();Transform landing=null;
        foreach(Transform child in transform)
        {if(child.name=="AccessStep")steps.Add(child);else if(child.name=="AccessLanding")landing=child;}
        if(steps.Count==0)return;
        steps.Sort((a,b)=>a.localPosition.z.CompareTo(b.localPosition.z));
        glass=new Material(Resources.Load<Shader>("Fishing/ShopGlass"));glass.SetColor("_BaseColor",new Color(.28f,.76f,.82f,.38f));glass.SetFloat("_Water",0);
        metal=new Material(Resources.Load<Shader>("Fishing/ShopSurface"));metal.SetColor("_BaseColor",new Color(.18f,.29f,.32f));metal.SetFloat("_Metallic",.7f);metal.SetFloat("_Smoothness",.7f);
        foreach(var step in steps)
        {
            float top=step.localPosition.y+step.localScale.y*.5f;
            var p=step.localPosition;p.y=top-.045f;step.localPosition=p;
            var size=step.localScale;size.y=.09f;step.localScale=size;
            step.GetComponent<Renderer>().sharedMaterial=glass;
            // Slim opaque nosing makes every translucent step easy to read.
            Part("Tread edge",new Vector3(p.x,top-.015f,p.z-.15f),new Vector3(size.x,.035f,.025f),metal);
        }
        var first=steps[0];var last=steps[steps.Count-1];
        Vector3 start=first.localPosition,finish=last.localPosition;
        foreach(float side in new[]{-.73f,.73f})
        {
            Beam("Side stringer",start+new Vector3(side,-.08f,-.15f),finish+new Vector3(side,-.08f,.15f),.065f,metal);
            Beam("Handrail",start+new Vector3(side,.99f,-.15f),finish+new Vector3(side,.99f,.18f),.045f,metal);
            for(int i=0;i<steps.Count;i+=5)
            {
                var p=steps[i].localPosition+new Vector3(side,.045f,0);
                Beam("Balustrade post",p,p+Vector3.up*.94f,.035f,metal);
            }
        }
        if(landing!=null)
        {
            landing.GetComponent<Renderer>().sharedMaterial=glass;
            // Outside guard only: leave the tank-facing edge open for swimming.
            float outside=landing.localPosition.x+landing.localScale.x*.5f;
            Vector3 a=new Vector3(outside,landing.localPosition.y+1f,landing.localPosition.z-.75f);
            Vector3 b=a+Vector3.forward*1.5f;Beam("Landing guard",a,b,.045f,metal);
            Beam("Landing post",a-Vector3.up*.9f,a,.035f,metal);Beam("Landing post",b-Vector3.up*.9f,b,.035f,metal);
        }
    }
    private GameObject Part(string label,Vector3 point,Vector3 size,Material material)
    {
        var node=GameObject.CreatePrimitive(PrimitiveType.Cube);node.name=label;node.transform.SetParent(transform,false);
        node.transform.localPosition=point;node.transform.localScale=size;node.GetComponent<Renderer>().sharedMaterial=material;
        node.GetComponent<Collider>().enabled=false;Destroy(node.GetComponent<Collider>());return node;
    }
    private void Beam(string label,Vector3 a,Vector3 b,float width,Material material)
    {
        var node=Part(label,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material);
        node.transform.localRotation=Quaternion.LookRotation(b-a,Mathf.Abs(Vector3.Dot((b-a).normalized,Vector3.up))>.98f?Vector3.forward:Vector3.up);
    }
    private void OnDestroy(){if(glass!=null)Destroy(glass);if(metal!=null)Destroy(metal);}
}
