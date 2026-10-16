using System.Collections.Generic;
using UnityEngine;

// Local scenery attached to the starter marina; no networked objects are needed.
public sealed class TournamentStand : MonoBehaviour
{
    public Transform Counter {get;private set;}
    private GameObject stand;
    private readonly List<Material> materials=new List<Material>();
    private void Start()
    {
        var marina=GameObject.Find("MarinaShop");if(marina==null)return;
        var existing=marina.transform.Find("Tournament Stand");
        if(existing!=null){Counter=existing.Find("Interact");return;}
        stand=new GameObject("Tournament Stand");stand.transform.SetParent(marina.transform,false);
        // +X is the right side when approaching from land toward the pier (+Z).
        stand.transform.localPosition=new Vector3(6f,.12f,-1f);
        var wood=Material(new Color(.38f,.22f,.10f));var teal=Material(new Color(.035f,.32f,.35f));
        var cream=Material(new Color(.94f,.89f,.72f));var gold=Material(new Color(.92f,.64f,.18f));
        Part("Side platform",new Vector3(0,-.05f,0),new Vector3(3.8f,.25f,4f),wood);
        // The platform overlaps the dock edge; its supports extend toward the seabed.
        foreach(float x in new[]{-1.65f,1.65f})foreach(float z in new[]{-1.65f,1.65f})
        {
            Part("Pier support",new Vector3(x,-2f,z),new Vector3(.22f,4f,.22f),wood);
            Part("Canopy post",new Vector3(x,1.5f,z),new Vector3(.13f,3f,.13f),cream);
        }
        Part("Canopy",new Vector3(0,3.05f,0),new Vector3(3.8f,.2f,4f),teal);
        Part("Counter",new Vector3(0,.65f,.55f),new Vector3(2.7f,1.2f,.8f),teal);
        Part("Countertop",new Vector3(0,1.3f,.55f),new Vector3(2.9f,.14f,1f),wood);
        Part("Tournament sign",new Vector3(0,2.4f,-1.7f),new Vector3(3.35f,.6f,.12f),teal);
        var label=new GameObject("Tournament lettering",typeof(TextMesh));label.transform.SetParent(stand.transform,false);
        label.transform.localPosition=new Vector3(0,2.4f,-1.78f);
        var text=label.GetComponent<TextMesh>();text.text="FISHING TOURNAMENTS";text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if(text.font!=null)label.GetComponent<Renderer>().sharedMaterial=text.font.material;
        text.fontSize=48;text.characterSize=.105f;text.color=new Color(1f,.87f,.42f);
        Part("Trophy base",new Vector3(0,1.43f,.55f),new Vector3(.45f,.12f,.4f),wood);
        Part("Trophy stem",new Vector3(0,1.65f,.55f),new Vector3(.10f,.4f,.10f),gold);
        var cup=GameObject.CreatePrimitive(PrimitiveType.Sphere);cup.name="Golden trophy";cup.transform.SetParent(stand.transform,false);
        cup.transform.localPosition=new Vector3(0,1.9f,.55f);cup.transform.localScale=new Vector3(.42f,.32f,.35f);cup.GetComponent<Renderer>().sharedMaterial=gold;
        var point=new GameObject("Interact");point.transform.SetParent(stand.transform,false);point.transform.localPosition=new Vector3(0,0,-.8f);Counter=point.transform;
    }
    private Material Material(Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var material=new Material(shader);material.color=color;materials.Add(material);return material;
    }
    private void Part(string name,Vector3 at,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(stand.transform,false);
        go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
    }
    private void OnDestroy(){if(stand!=null)Destroy(stand);foreach(var material in materials)Destroy(material);}
}
