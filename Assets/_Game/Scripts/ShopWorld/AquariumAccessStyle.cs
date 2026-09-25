using System.Collections.Generic;
using UnityEngine;

// Upgrade existing Home and Quay stairs without rebuilding either saved scene.
// Important: the generated AccessStep cubes are marked static for batching. Merely
// changing their transforms at runtime can leave the old tall batched geometry on
// screen, which is why the previous "thin them to 0.09 m" fix did not visibly
// change some builds. We now hide the original static blocks completely and build
// fresh non-static tread geometry at the exact same top heights.
public sealed class AquariumAccessStyle : MonoBehaviour
{
    private Material glass,metal;
    private readonly List<GameObject> generated=new List<GameObject>();

    private void Start()
    {
        var steps=new List<Transform>();Transform landing=null;
        foreach(Transform child in transform)
        {
            if(child.name=="AccessStep")steps.Add(child);
            else if(child.name=="AccessLanding")landing=child;
        }
        if(steps.Count==0)return;
        steps.Sort((a,b)=>a.localPosition.z.CompareTo(b.localPosition.z));

        Shader glassShader=Resources.Load<Shader>("Fishing/ShopGlass");
        if(glassShader==null)glassShader=Shader.Find("Universal Render Pipeline/Lit");
        glass=new Material(glassShader);glass.SetColor("_BaseColor",new Color(.28f,.76f,.82f,.38f));
        if(glass.HasProperty("_Water"))glass.SetFloat("_Water",0);

        Shader surfaceShader=Resources.Load<Shader>("Fishing/ShopSurface");
        if(surfaceShader==null)surfaceShader=Shader.Find("Universal Render Pipeline/Lit");
        metal=new Material(surfaceShader);metal.SetColor("_BaseColor",new Color(.18f,.29f,.32f));
        if(metal.HasProperty("_Metallic"))metal.SetFloat("_Metallic",.7f);
        if(metal.HasProperty("_Smoothness"))metal.SetFloat("_Smoothness",.7f);

        var treadPositions=new List<Vector3>();
        var treadSizes=new List<Vector3>();

        foreach(var step in steps)
        {
            // Capture the original walkable top before hiding the old cumulative
            // cube. This works for old scenes, new scenes, editor static batching,
            // and player builds because we never depend on moving the old renderer.
            float top=step.localPosition.y+step.localScale.y*.5f;
            Vector3 p=step.localPosition;p.y=top-.045f;
            Vector3 size=step.localScale;size.y=.09f;
            treadPositions.Add(p);treadSizes.Add(size);

            var oldRenderer=step.GetComponent<Renderer>();
            if(oldRenderer!=null)oldRenderer.enabled=false;
            var oldCollider=step.GetComponent<Collider>();
            if(oldCollider!=null)oldCollider.enabled=false;

            // Keep the original object as an inert marker only. Disabling the
            // renderer is essential: changing a statically-batched transform alone
            // does not guarantee the baked tall pane disappears visually.
            GameObject tread=Part("AccessTread",p,size,glass,true);
            Part("Tread edge",new Vector3(p.x,top-.015f,p.z-.15f),new Vector3(size.x,.035f,.025f),metal,false);
        }

        Vector3 start=treadPositions[0],finish=treadPositions[treadPositions.Count-1];
        foreach(float side in new[]{-.73f,.73f})
        {
            Beam("Side stringer",start+new Vector3(side,-.08f,-.15f),finish+new Vector3(side,-.08f,.15f),.065f,metal);
            Beam("Handrail",start+new Vector3(side,.99f,-.15f),finish+new Vector3(side,.99f,.18f),.045f,metal);
            for(int i=0;i<treadPositions.Count;i+=5)
            {
                var p=treadPositions[i]+new Vector3(side,.045f,0);
                Beam("Balustrade post",p,p+Vector3.up*.94f,.035f,metal);
            }
        }

        if(landing!=null)
        {
            var landingRenderer=landing.GetComponent<Renderer>();
            if(landingRenderer!=null)landingRenderer.sharedMaterial=glass;
            // Outside guard only: leave the tank-facing edge open for swimming.
            float outside=landing.localPosition.x+landing.localScale.x*.5f;
            Vector3 a=new Vector3(outside,landing.localPosition.y+1f,landing.localPosition.z-.75f);
            Vector3 b=a+Vector3.forward*1.5f;Beam("Landing guard",a,b,.045f,metal);
            Beam("Landing post",a-Vector3.up*.9f,a,.035f,metal);Beam("Landing post",b-Vector3.up*.9f,b,.035f,metal);
        }
    }

    private GameObject Part(string label,Vector3 point,Vector3 size,Material material,bool collide)
    {
        var node=GameObject.CreatePrimitive(PrimitiveType.Cube);generated.Add(node);node.name=label;node.transform.SetParent(transform,false);
        node.transform.localPosition=point;node.transform.localScale=size;node.GetComponent<Renderer>().sharedMaterial=material;
        var collider=node.GetComponent<Collider>();
        if(!collide && collider!=null){collider.enabled=false;Destroy(collider);}
        return node;
    }

    private void Beam(string label,Vector3 a,Vector3 b,float width,Material material)
    {
        var node=Part(label,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material,false);
        node.transform.localRotation=Quaternion.LookRotation(b-a,Mathf.Abs(Vector3.Dot((b-a).normalized,Vector3.up))>.98f?Vector3.forward:Vector3.up);
    }

    private void OnDestroy()
    {
        if(glass!=null)Destroy(glass);if(metal!=null)Destroy(metal);
        for(int i=0;i<generated.Count;i++)if(generated[i]!=null)Destroy(generated[i]);
    }
}
