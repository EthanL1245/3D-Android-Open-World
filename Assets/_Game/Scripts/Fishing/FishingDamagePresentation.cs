using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only layer for the floating fishing damage numbers.
/// It leaves FishingHUD's pooling/lifetime logic intact, adds a small black
/// outline to every damage number, and makes critical-hit numbers 1.5x larger.
/// </summary>
[DefaultExecutionOrder(1100)]
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

    private void Awake()
    {
        hud=GetComponent<FishingHUD>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        damageLabelsField=typeof(FishingHUD).GetField("damageLabels",flags);
        damageLifeField=typeof(FishingHUD).GetField("damageLife",flags);
    }

    private void LateUpdate()
    {
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
                label.resizeTextForBestFit=false;
                label.fontSize=critical?48:32; // 48 is exactly 1.5x the normal 32 px size.
                label.rectTransform.sizeDelta=critical?new Vector2(240f,90f):new Vector2(160f,60f);
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
        outline.effectColor=new Color(0f,0f,0f,0.92f);
        outline.effectDistance=new Vector2(1.25f,-1.25f);
        outline.useGraphicAlpha=true;
    }
}
