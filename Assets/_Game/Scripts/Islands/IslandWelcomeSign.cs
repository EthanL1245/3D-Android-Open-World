using UnityEngine;

public static class IslandWelcomeSign
{
    public static GameObject Create(Transform parent,string title,Vector3 position,Quaternion rotation,Material wood)
    {
        // Prefer an exact clone of the installed Suncrest arrival sign.
        foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            if(text.text!="SUNCREST REEF" || text.transform.parent==null)continue;
            var clone=Object.Instantiate(text.transform.parent.gameObject,parent);clone.name=title;
            clone.transform.SetPositionAndRotation(position,rotation);
            foreach(var label in clone.GetComponentsInChildren<TextMesh>())label.text=title;
            foreach(var fit in clone.GetComponentsInChildren<ShopSign>())fit.Fit();
            return clone;
        }
        // Same dimensions, posts, typography, material and two-sided format as Sign(...,false).
        var root=new GameObject(title);root.transform.SetParent(parent,false);root.transform.SetPositionAndRotation(position,rotation);
        foreach(float x in new[]{0f,-1.45f,1.45f})
        {var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);post.transform.SetParent(root.transform,false);post.transform.localPosition=new Vector3(x,.7f,0);post.transform.localScale=new Vector3(x==0?.12f:.18f,.7f,x==0?.12f:.18f);post.GetComponent<Renderer>().sharedMaterial=wood;}
        var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.transform.SetParent(root.transform,false);board.transform.localPosition=new Vector3(0,1.5f,0);board.transform.localScale=new Vector3(3.5f,.72f,.16f);board.GetComponent<Renderer>().sharedMaterial=wood;
        for(int side=0;side<2;side++)
        {
            var label=new GameObject(side==0?"Carved trail sign":"Reverse sign text",typeof(TextMesh),typeof(ShopSign));label.transform.SetParent(root.transform,false);
            label.transform.localPosition=new Vector3(0,1.5f,side==0?-.09f:.09f);label.transform.localRotation=Quaternion.Euler(0,side*180,0);
            var text=label.GetComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=title;text.fontSize=64;text.characterSize=.042f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.96f,.91f,.74f);
            var material=new Material(Resources.Load<Shader>("Fishing/ShopSign"));material.mainTexture=text.font.material.mainTexture;label.GetComponent<Renderer>().sharedMaterial=material;
            var fit=label.GetComponent<ShopSign>();fit.area=new Vector2(3.2f,.58f);fit.Fit();
        }
        return root;
    }
}
