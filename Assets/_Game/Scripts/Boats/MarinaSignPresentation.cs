using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps SUNCREST MARINA fitted to the land-facing side of its wooden board.
/// It deliberately uses the same depth-tested, front-only shader as the other
/// world/shop signs, so terrain, trees, walls and other geometry can occlude it.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class MarinaSignPresentation : MonoBehaviour
{
    private const string StableName="Marina lettering stable";
    private Transform sign;
    private Transform label;
    private Font labelFont;
    private Material depthMaterial;
    private bool fitted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject marina=GameObject.Find("MarinaShop");
        if(marina!=null && marina.GetComponent<MarinaSignPresentation>()==null)
            marina.AddComponent<MarinaSignPresentation>();
    }

    private void OnEnable()
    {
        Font.textureRebuilt+=OnFontTextureRebuilt;
    }

    private void OnDisable()
    {
        Font.textureRebuilt-=OnFontTextureRebuilt;
    }

    private void OnDestroy()
    {
        if(depthMaterial!=null)
        {
            Destroy(depthMaterial);
            depthMaterial=null;
        }
    }

    private void Awake(){Repair();}

    private void Repair()
    {
        sign=FindDeepChild(transform,"Marina sign");
        if(sign==null)return;

        // Remove every older lettering implementation. In particular, the old
        // built-in font material rendered like an overlay and the previous runtime
        // script swapped the label to whichever face the camera occupied.
        TextMesh[] old=sign.GetComponentsInChildren<TextMesh>(true);
        foreach(TextMesh text in old)
        {
            if(text==null)continue;
            text.gameObject.SetActive(false);
            Destroy(text.gameObject);
        }

        if(depthMaterial!=null)
        {
            Destroy(depthMaterial);
            depthMaterial=null;
        }

        GameObject go=new GameObject(StableName,typeof(TextMesh));
        go.transform.SetParent(sign,false);
        label=go.transform;

        TextMesh mesh=go.GetComponent<TextMesh>();
        labelFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font=labelFont;
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
            // This is the exact shader contract used by the normal shop/trail
            // signs: Transparent queue, ZTest LEqual and Cull Back. That means the
            // letters obey the scene depth buffer and disappear behind geometry,
            // and they cannot be read through the back of the wooden sign.
            Shader shader=Resources.Load<Shader>("Fishing/ShopSign");
            if(shader==null)shader=Shader.Find("OpenWorld/ShopSign");
            if(shader!=null)
            {
                depthMaterial=new Material(shader)
                {
                    name="Suncrest Marina Sign Text (Runtime)"
                };
                if(labelFont!=null)depthMaterial.mainTexture=labelFont.material.mainTexture;
                renderer.sharedMaterial=depthMaterial;
            }
            else
            {
                Debug.LogError("SUNCREST MARINA could not load Fishing/ShopSign. The sign text has been hidden rather than rendered through world geometry.");
                renderer.enabled=false;
            }

            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.allowOcclusionWhenDynamic=true;
            renderer.sortingOrder=0;
        }

        // TextMesh faces local -Z and BoatSystemSetup defines -Z as the land-facing
        // side. Keep it on that one face permanently, just like the other signs.
        // Do not camera-swap it to the rear face.
        label.localPosition=new Vector3(0f,0f,-.535f);
        label.localRotation=Quaternion.identity;

        // Start harmlessly tiny. Once Unity has generated TextMesh geometry on the
        // next frame, FitToSign measures the real bounds and scales it to the board.
        SetWorldScale(.001f);
        StartCoroutine(FitNextFrame());
    }

    private void OnFontTextureRebuilt(Font rebuilt)
    {
        if(rebuilt==null || rebuilt!=labelFont || depthMaterial==null)return;
        depthMaterial.mainTexture=rebuilt.material.mainTexture;
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
        float factor=Mathf.Min(signWidth*.88f/size.x,signHeight*.62f/size.y);
        float currentWorldScale=Mathf.Max(.000001f,Mathf.Abs(label.lossyScale.x));
        SetWorldScale(currentWorldScale*factor);
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
            fitted=false;
            Repair();
            return;
        }
        if(!fitted)FitToSign();
    }

    private static Transform FindDeepChild(Transform root,string name)
    {
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            if(t!=null && t.name==name)return t;
        return null;
    }
}
