using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only layer for floating fishing damage numbers.
/// Damage amounts are owned by FishingBurstDamageRuntime; this component never
/// changes fish HP or popup values. A critical is therefore the same numeric burst
/// that actually received the 2x gameplay multiplier; only its styling changes.
/// </summary>
[DefaultExecutionOrder(3100)]
public sealed class FishingDamagePresentation : MonoBehaviour
{
    private static bool criticalQueued;

    private FishingHUD hud;
    private FieldInfo damageLabelsField;
    private FieldInfo damageLifeField;
    private readonly Dictionary<Text,string> previousText=new Dictionary<Text,string>();
    private readonly Dictionary<Text,float> previousLife=new Dictionary<Text,float>();
    private readonly Dictionary<Text,bool> previousActive=new Dictionary<Text,bool>();

    public static void MarkCriticalHit()
    {
        criticalQueued=true;
    }

    public static void ClearPendingCritical()
    {
        criticalQueued=false;
    }

    private void Awake()
    {
        hud=GetComponent<FishingHUD>();
        if(hud==null)hud=FindFirstObjectByType<FishingHUD>();

        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        damageLabelsField=typeof(FishingHUD).GetField("damageLabels",flags);
        damageLifeField=typeof(FishingHUD).GetField("damageLife",flags);
    }

    private void LateUpdate()
    {
        if(hud==null)hud=FindFirstObjectByType<FishingHUD>();
        if(hud==null || damageLabelsField==null || damageLifeField==null)return;

        Text[] labels=damageLabelsField.GetValue(hud) as Text[];
        float[] life=damageLifeField.GetValue(hud) as float[];
        if(labels==null || life==null)return;

        for(int i=0;i<labels.Length && i<life.Length;i++)
        {
            Text label=labels[i];
            if(label==null)continue;
            EnsureOutline(label);

            bool active=label.gameObject.activeInHierarchy && life[i]>0f;
            previousActive.TryGetValue(label,out bool wasActive);
            previousLife.TryGetValue(label,out float oldLife);
            previousText.TryGetValue(label,out string oldText);

            bool newPopup=active && (!wasActive || life[i]>oldLife+0.15f || oldText!=label.text);
            if(newPopup)
            {
                bool critical=criticalQueued;
                if(critical)criticalQueued=false;

                // Keep criticals numeric-only. The gameplay number itself is already
                // exactly doubled by FishingBurstDamageRuntime. A modest size/tint
                // change is enough to distinguish it without covering the screen.
                label.resizeTextForBestFit=false;
                if(critical)
                {
                    label.fontSize=38;
                    label.rectTransform.sizeDelta=new Vector2(205f,72f);
                    label.color=new Color(1f,.62f,.24f,1f);
                }
                else
                {
                    label.fontSize=32;
                    label.rectTransform.sizeDelta=new Vector2(180f,64f);
                    label.color=new Color(1f,.85f,.35f,1f);
                }
            }

            previousActive[label]=active;
            previousLife[label]=life[i];
            previousText[label]=label.text;
        }
    }

    private static void EnsureOutline(Text label)
    {
        Outline outline=label.GetComponent<Outline>();
        if(outline==null)outline=label.gameObject.AddComponent<Outline>();
        outline.effectColor=new Color(0f,0f,0f,0.94f);
        outline.effectDistance=new Vector2(1.6f,-1.6f);
        outline.useGraphicAlpha=true;
    }
}
