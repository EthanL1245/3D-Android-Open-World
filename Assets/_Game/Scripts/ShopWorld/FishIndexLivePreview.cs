using UnityEngine;
using UnityEngine.UI;

// Exactly one animated studio for the selected fish. Grid cards use cached
// snapshots, not a camera per card. This studio is stopped when the menu closes.
[DefaultExecutionOrder(1000)]
public sealed class FishIndexLivePreview : MonoBehaviour
{
    private GameObject stage, model;
    private Camera studio;
    private RenderTexture texture;
    private RawImage target;
    private Transform fallbackTail;
    private Quaternion tailRest;
    private static int nextStudio;

    public void Show(RawImage image,int species)
    {
        Clear();
        target=image;
        stage=new GameObject("FishIndexAnimatedStudio");
        stage.transform.position=new Vector3(18000f+100f*nextStudio++,12000f,12000f);
        studio=new GameObject("FishIndexCamera").AddComponent<Camera>();
        studio.transform.SetParent(stage.transform,false);
        studio.enabled=false;
        studio.clearFlags=CameraClearFlags.SolidColor;
        studio.backgroundColor=Color.clear;
        studio.orthographic=true;
        studio.nearClipPlane=.01f;
        studio.farClipPlane=20f;
        studio.cullingMask=1<<30;
        studio.allowHDR=false;
        studio.allowMSAA=false;

        model=FishVisualFactory.CreateFish("IndexSwimmingFish",stage.transform,species,1f);
        foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))
            script.enabled=script is HeroFishAnimator;
        foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))
        {body.isKinematic=true;body.detectCollisions=false;}
        foreach(var source in model.GetComponentsInChildren<AudioSource>(true))source.enabled=false;
        foreach(var light in model.GetComponentsInChildren<Light>(true))light.enabled=false;
        foreach(var child in model.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
        bool animated=model.GetComponentInChildren<HeroFishAnimator>()!=null;
        foreach(var animator in model.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode=AnimatorUpdateMode.UnscaledTime;
            animator.enabled=true;
            animator.speed=1f;
            animator.Update(0f);
            animated|=animator.runtimeAnimatorController!=null;
        }
        foreach(var animation in model.GetComponentsInChildren<Animation>(true))
        {
            animation.cullingType=AnimationCullingType.AlwaysAnimate;
            animation.enabled=true;
            if(animation.clip!=null){animation.wrapMode=WrapMode.Loop;animation.Play();animated=true;}
        }
        FishWorldSize.SetLength(model,2f);
        // All fish prefabs use +Z for their nose; +90 yaw faces screen right.
        model.transform.localRotation=Quaternion.Euler(0,90,0);
        if(!animated)
        {
            fallbackTail=model.transform.Find("Tail");
            if(fallbackTail!=null)tailRest=fallbackTail.localRotation;
        }
        bool found=false;
        Bounds bounds=default;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())
        {
            if(!renderer.enabled)continue;
            if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
            if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
        }
        if(!found){Clear();return;}
        texture=new RenderTexture(768,384,16,RenderTextureFormat.ARGB32){name="FishIndexLive",antiAliasing=1};
        texture.Create();
        studio.targetTexture=texture;
        studio.aspect=2f;
        studio.orthographicSize=Mathf.Max(bounds.size.x/2f,bounds.size.y)*.67f;
        studio.transform.position=bounds.center+Vector3.back*(bounds.size.z+5f);
        studio.transform.rotation=Quaternion.identity;
        target.texture=texture;
        target.enabled=true;
        studio.enabled=true;
    }

    private void LateUpdate()
    {
        if(studio!=null)studio.enabled=target!=null && target.gameObject.activeInHierarchy && !target.canvasRenderer.cull;
        if(fallbackTail!=null)
            fallbackTail.localRotation=tailRest*Quaternion.Euler(0,Mathf.Sin(Time.unscaledTime*5f)*18f,0);
    }

    public void Clear()
    {
        if(studio!=null){studio.enabled=false;studio.targetTexture=null;}
        if(target!=null)target.texture=null;
        if(stage!=null){stage.SetActive(false);Destroy(stage);}
        if(texture!=null){texture.Release();Destroy(texture);}
        stage=model=null;studio=null;texture=null;target=null;fallbackTail=null;
    }

    private void OnDisable(){Clear();}
    private void OnDestroy(){Clear();}
}
