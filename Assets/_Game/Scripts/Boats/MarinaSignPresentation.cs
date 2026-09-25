using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Repairs the generated Suncrest Marina lettering at runtime. The old TextMesh was
/// offset/scaled in a way that could intersect the sign and was only present on one
/// face. These two labels sit just above the wood on both faces, stay parented to
/// the sign, and opt out of occlusion culling so the lettering remains stable at
/// distance without becoming a camera-facing billboard.
/// </summary>
public sealed class MarinaSignPresentation : MonoBehaviour
{
    private const string FrontName="Marina lettering stable front";
    private const string BackName="Marina lettering stable back";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject marina=GameObject.Find("MarinaShop");
        if(marina!=null && marina.GetComponent<MarinaSignPresentation>()==null)
            marina.AddComponent<MarinaSignPresentation>();
    }

    private void Start(){Repair();}

    private void Repair()
    {
        Transform sign=FindDeepChild(transform,"Marina sign");
        if(sign==null)return;

        // Hide/remove the original single-face text immediately so it cannot
        // z-fight with the repaired lettering for even one frame.
        Transform[] children=sign.GetComponentsInChildren<Transform>(true);
        foreach(Transform child in children)
        {
            if(child==null || child==sign || child.name==FrontName || child.name==BackName)continue;
            if(child.name=="Marina lettering")
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        EnsureFace(sign,FrontName,-.515f,Quaternion.identity);
        EnsureFace(sign,BackName,.515f,Quaternion.Euler(0f,180f,0f));
    }

    private static void EnsureFace(Transform sign,string objectName,float localZ,Quaternion localRotation)
    {
        Transform existing=sign.Find(objectName);
        GameObject go=existing!=null?existing.gameObject:new GameObject(objectName,typeof(TextMesh));
        go.transform.SetParent(sign,false);
        go.transform.localPosition=new Vector3(0f,0f,localZ);
        go.transform.localRotation=localRotation;

        // Counter the primitive sign's non-uniform transform so the font is not
        // stretched with the wooden cube.
        Vector3 signScale=sign.localScale;
        go.transform.localScale=new Vector3(
            1f/Mathf.Max(.001f,Mathf.Abs(signScale.x)),
            1f/Mathf.Max(.001f,Mathf.Abs(signScale.y)),
            1f/Mathf.Max(.001f,Mathf.Abs(signScale.z)));

        TextMesh text=go.GetComponent<TextMesh>();
        text.text="SUNCREST MARINA";
        text.anchor=TextAnchor.MiddleCenter;
        text.alignment=TextAlignment.Center;
        text.characterSize=.14f;
        text.fontSize=56;
        text.color=Color.white;
        text.richText=false;

        MeshRenderer renderer=go.GetComponent<MeshRenderer>();
        if(renderer!=null)
        {
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.allowOcclusionWhenDynamic=false;
            renderer.sortingOrder=20;
        }
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            if(t!=null && t.name==name)return t;
        return null;
    }
}
