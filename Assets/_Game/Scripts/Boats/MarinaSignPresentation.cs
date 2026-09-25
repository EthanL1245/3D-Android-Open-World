using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps one fitted SUNCREST MARINA label physically attached to the wooden sign.
/// The label never billboards around the camera; it only swaps to the opposite sign
/// face when the player walks behind it. Using one visible TextMesh at a time avoids
/// the double-sided font shader drawing front/back copies on top of each other.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class MarinaSignPresentation : MonoBehaviour
{
    private const string StableName="Marina lettering stable";
    private Transform sign;
    private Transform label;
    private Camera view;
    private bool fitted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject marina=GameObject.Find("MarinaShop");
        if(marina!=null && marina.GetComponent<MarinaSignPresentation>()==null)
            marina.AddComponent<MarinaSignPresentation>();
    }

    private void Awake(){Repair();}

    private void Repair()
    {
        sign=FindDeepChild(transform,"Marina sign");
        if(sign==null)return;

        // Remove every older lettering implementation first. The built-in TextMesh
        // font material is double-sided, so keeping two opposite labels caused the
        // mirrored/stacked text seen in game.
        TextMesh[] old=sign.GetComponentsInChildren<TextMesh>(true);
        foreach(TextMesh text in old)
        {
            if(text==null)continue;
            text.gameObject.SetActive(false);
            Destroy(text.gameObject);
        }

        GameObject go=new GameObject(StableName,typeof(TextMesh));
        go.transform.SetParent(sign,false);
        label=go.transform;

        TextMesh mesh=go.GetComponent<TextMesh>();
        Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font=font;
        mesh.text="SUNCREST MARINA";
        mesh.anchor=TextAnchor.MiddleCenter;
        mesh.alignment=TextAlignment.Center;
        mesh.characterSize=1f;
        mesh.fontSize=64;
        mesh.color=Color.white;
        mesh.richText=false;

        MeshRenderer renderer=go.GetComponent<MeshRenderer>();
        if(renderer!=null)
        {
            if(font!=null)renderer.sharedMaterial=font.material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.allowOcclusionWhenDynamic=false;
            renderer.sortingOrder=20;
        }

        // Start harmlessly tiny. Once Unity has generated TextMesh geometry on the
        // next frame, FitToSign measures the real bounds and scales it exactly to
        // this sign instead of relying on font-size guesses.
        SetWorldScale(.001f);
        UpdateFace();
        StartCoroutine(FitNextFrame());
    }

    private IEnumerator FitNextFrame()
    {
        yield return null;
        FitToSign();
    }

    private void FitToSign()
    {
        if(sign==null || label==null)return;
        Renderer renderer=label.GetComponent<Renderer>();
        if(renderer==null)return;

        Vector3 size=renderer.bounds.size;
        if(size.x<0.000001f || size.y<0.000001f)return;

        float signWidth=Vector3.Distance(sign.TransformPoint(new Vector3(-.5f,0,0)),sign.TransformPoint(new Vector3(.5f,0,0)));
        float signHeight=Vector3.Distance(sign.TransformPoint(new Vector3(0,-.5f,0)),sign.TransformPoint(new Vector3(0,.5f,0)));
        float worldScale=Mathf.Min(signWidth*.88f/size.x,signHeight*.62f/size.y)*.001f;
        SetWorldScale(worldScale);
        fitted=true;
    }

    private void SetWorldScale(float uniformWorldScale)
    {
        if(sign==null || label==null)return;
        Vector3 parentScale=sign.lossyScale;
        label.localScale=new Vector3(
            uniformWorldScale/Mathf.Max(.000001f,Mathf.Abs(parentScale.x)),
            uniformWorldScale/Mathf.Max(.000001f,Mathf.Abs(parentScale.y)),
            uniformWorldScale/Mathf.Max(.000001f,Mathf.Abs(parentScale.z)));
    }

    private void LateUpdate()
    {
        if(sign==null || label==null)
        {
            Repair();
            return;
        }
        UpdateFace();
        if(!fitted)FitToSign();
    }

    private void UpdateFace()
    {
        if(sign==null || label==null)return;
        if(view==null)view=Camera.main!=null?Camera.main:FindFirstObjectByType<Camera>();

        bool positiveSide=view!=null && Vector3.Dot(view.transform.position-sign.position,sign.forward)>=0f;
        // Primitive cube face is at local +/-0.5 Z. The small extra offset keeps the
        // glyphs above the wood so there is no z-fighting at any viewing distance.
        label.localPosition=new Vector3(0f,0f,positiveSide?.535f:-.535f);
        label.localRotation=positiveSide?Quaternion.Euler(0f,180f,0f):Quaternion.identity;
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            if(t!=null && t.name==name)return t;
        return null;
    }
}
