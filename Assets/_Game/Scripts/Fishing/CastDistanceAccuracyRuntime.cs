using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the cast gauge describe the same distance the player sees on the LINE
/// meter after landing. FishingSystem aims from the camera, but the rendered line
/// begins at the authored RodTip several metres in front of it. This component
/// supplies the real rod/camera/water geometry to FishingRules before each fishing
/// Update, then keeps the gauge caption at the requested 5–30 m value.
/// </summary>
[DefaultExecutionOrder(-950)]
public sealed class CastDistanceAccuracyRuntime : MonoBehaviour
{
    private FishingSystem fishing;
    private FishingHUD hud;

    private FieldInfo stateField;
    private FieldInfo rodTipField;
    private FieldInfo cameraField;
    private FieldInfo oceanField;
    private FieldInfo chargeStartedField;
    private FieldInfo maximumCastDistanceField;
    private FieldInfo originalCastDistanceField;
    private FieldInfo castCaptionField;

    private Transform rodTip;
    private Camera playerCamera;
    private OceanWater ocean;
    private string previousState=string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
        {
            if(system!=null && system.GetComponent<CastDistanceAccuracyRuntime>()==null)
                system.gameObject.AddComponent<CastDistanceAccuracyRuntime>();
        }
    }

    private void Awake()
    {
        fishing=GetComponent<FishingSystem>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        Type type=typeof(FishingSystem);
        stateField=type.GetField("state",flags);
        rodTipField=type.GetField("rodTip",flags);
        cameraField=type.GetField("playerCamera",flags);
        oceanField=type.GetField("oceanWater",flags);
        chargeStartedField=type.GetField("chargeStarted",flags);
        maximumCastDistanceField=type.GetField("maximumCastDistance",flags);
        originalCastDistanceField=type.GetField("originalCastDistance",flags);
        castCaptionField=typeof(FishingHUD).GetField("castCaption",flags);
    }

    private void Start()
    {
        Resolve();
        if(fishing==null || stateField==null || rodTipField==null || cameraField==null ||
           oceanField==null || chargeStartedField==null || maximumCastDistanceField==null ||
           originalCastDistanceField==null || castCaptionField==null)
        {
            Debug.LogError("CastDistanceAccuracyRuntime could not bind to the fishing cast fields and was disabled.");
            enabled=false;
        }
    }

    private void Update()
    {
        Resolve();
        if(rodTip==null || playerCamera==null || ocean==null)return;
        UpdateGeometry();
    }

    private void LateUpdate()
    {
        if(fishing==null || stateField==null)return;
        Resolve();
        string state=stateField.GetValue(fishing)?.ToString()??string.Empty;

        if(state=="Charging")
            CorrectGaugeCaption();

        // FishingSystem stores camera-to-target distance for lure size/strike logic.
        // Convert that once to the actual straight RodTip-to-water distance so all
        // downstream 5–30 m gameplay tuning uses the same number shown to the user.
        if(state=="Casting" && previousState!="Casting")
            CorrectStoredCastDistance();

        previousState=state;
    }

    private void Resolve()
    {
        if(fishing==null)fishing=GetComponent<FishingSystem>();
        if(fishing==null)return;
        if(rodTip==null && rodTipField!=null)rodTip=rodTipField.GetValue(fishing) as Transform;
        if(playerCamera==null && cameraField!=null)playerCamera=cameraField.GetValue(fishing) as Camera;
        if(ocean==null && oceanField!=null)ocean=oceanField.GetValue(fishing) as OceanWater;
        if(hud==null)hud=FindFirstObjectByType<FishingHUD>();
    }

    private void UpdateGeometry()
    {
        Vector3 direction=Vector3.ProjectOnPlane(playerCamera.transform.forward,Vector3.up);
        if(direction.sqrMagnitude<0.0001f)direction=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
        if(direction.sqrMagnitude<0.0001f)return;
        direction.Normalize();

        Vector3 cameraToTip=rodTip.position-playerCamera.transform.position;
        Vector3 horizontal=Vector3.ProjectOnPlane(cameraToTip,Vector3.up);
        float forward=Vector3.Dot(horizontal,direction);
        Vector3 perpendicular=horizontal-direction*forward;

        float maximum=30f;
        if(maximumCastDistanceField!=null)
            maximum=Mathf.Max(5f,(float)maximumCastDistanceField.GetValue(fishing));
        Vector3 waterProbe=playerCamera.transform.position+direction*maximum;
        float targetY=ocean.GetSurfaceHeight(waterProbe)+0.06f;

        FishingRules.CastForwardOffset=forward;
        FishingRules.CastPerpendicularOffsetSqr=perpendicular.sqrMagnitude;
        FishingRules.CastVerticalOffset=rodTip.position.y-targetY;
    }

    private void CorrectGaugeCaption()
    {
        if(hud==null || castCaptionField==null)return;
        Text caption=castCaptionField.GetValue(hud) as Text;
        if(caption==null || !caption.gameObject.activeInHierarchy)return;

        float chargeStarted=(float)chargeStartedField.GetValue(fishing);
        float maximum=Mathf.Max(5f,(float)maximumCastDistanceField.GetValue(fishing));
        float power=FishingRules.CastPower(Time.time-chargeStarted);
        float requested=FishingRules.RequestedCastDistance(power,maximum);
        bool blocked=caption.text!=null && caption.text.IndexOf("BLOCKED",StringComparison.OrdinalIgnoreCase)>=0;
        caption.text=requested.ToString("0.0")+" m"+(blocked?" • BLOCKED":string.Empty);
    }

    private void CorrectStoredCastDistance()
    {
        float cameraDistance=(float)originalCastDistanceField.GetValue(fishing);
        if(cameraDistance<=0f)return;

        float along=cameraDistance-FishingRules.CastForwardOffset;
        float actual=Mathf.Sqrt(Mathf.Max(0f,
            along*along+
            FishingRules.CastPerpendicularOffsetSqr+
            FishingRules.CastVerticalOffset*FishingRules.CastVerticalOffset));
        float maximum=Mathf.Max(5f,(float)maximumCastDistanceField.GetValue(fishing));
        originalCastDistanceField.SetValue(fishing,Mathf.Clamp(actual,5f,maximum));
    }

    private void OnDisable()
    {
        FishingRules.CastForwardOffset=0f;
        FishingRules.CastPerpendicularOffsetSqr=0f;
        FishingRules.CastVerticalOffset=0f;
    }
}
