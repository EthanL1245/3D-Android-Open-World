using UnityEngine;

// Text stays on its board and is rendered by a depth-tested, front-only shader.
[ExecuteAlways]
public sealed class ShopSign : MonoBehaviour
{
    public Vector2 area=new Vector2(4.6f,1.2f);
    private string previous;
    private void OnEnable() { Font.textureRebuilt+=AtlasChanged; }
    private void OnDisable() { Font.textureRebuilt-=AtlasChanged; }
    private void AtlasChanged(Font font)
    {
        var text=GetComponent<TextMesh>();var renderer=GetComponent<MeshRenderer>();
        if(text!=null && text.font==font && renderer!=null && renderer.sharedMaterial!=null)
        {renderer.sharedMaterial.mainTexture=font.material.mainTexture;previous=null;}
    }
    private void LateUpdate() { var t=GetComponent<TextMesh>(); if(t!=null && t.text!=previous)Fit(); }
    public void Fit()
    {
        var text=GetComponent<TextMesh>(); var renderer=GetComponent<MeshRenderer>();
        if(text==null || renderer==null)return;
        previous=text.text; transform.localScale=Vector3.one;
        var bounds=renderer.localBounds;
        float scale=Mathf.Min(1,area.x/Mathf.Max(0.001f,bounds.size.x),area.y/Mathf.Max(0.001f,bounds.size.y));
        transform.localScale=Vector3.one*scale;
    }
}
