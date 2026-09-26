using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation layer for floating fishing damage numbers.
///
/// Besides the outline/critical styling, this fixes the short first damage window
/// created by the hook grace period. If the first visible reel burst would only show
/// 1 damage, it is promoted to the same advertised randomized burst range as later
/// hits and the fish HP is adjusted by the exact same amount so the popup and health
/// bar remain truthful.
/// </summary>
[DefaultExecutionOrder(1100)]
public sealed class FishingDamagePresentation : MonoBehaviour
{
    private static bool criticalQueued;

    private FishingHUD hud;
    private FishingSystem fishing;
    private ShopProgress progress;
    private FieldInfo damageLabelsField;
    private FieldInfo damageLifeField;
    private FieldInfo stateField;
    private FieldInfo hpField;
    private FieldInfo maxHpField;
    private FieldInfo healthField;
    private FieldInfo pendingDamageField;
    private readonly Dictionary<Text,string> previousText=new Dictionary<Text,string>();
    private readonly Dictionary<Text,float> previousLife=new Dictionary<Text,float>();
    private readonly Dictionary<Text,bool> previousActive=new Dictionary<Text,bool>();
    private bool firstPopupHandled;

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
        fishing=GetComponent<FishingSystem>();
        progress=GetComponent<ShopProgress>();
        if(fishing==null)fishing=FindFirstObjectByType<FishingSystem>();
        if(progress==null)progress=FindFirstObjectByType<ShopProgress>();

        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        damageLabelsField=typeof(FishingHUD).GetField("damageLabels",flags);
        damageLifeField=typeof(FishingHUD).GetField("damageLife",flags);
        stateField=typeof(FishingSystem).GetField("state",flags);
        hpField=typeof(FishingSystem).GetField("fishHealthPoints",flags);
        maxHpField=typeof(FishingSystem).GetField("fishMaxHealth",flags);
        healthField=typeof(FishingSystem).GetField("fishHealth",flags);
        pendingDamageField=typeof(FishingSystem).GetField("pendingDamage",flags);
    }

    private void LateUpdate()
    {
        if(hud==null || damageLabelsField==null || damageLifeField==null)return;
        Text[] labels=damageLabelsField.GetValue(hud) as Text[];
        float[] life=damageLifeField.GetValue(hud) as float[];
        if(labels==null || life==null)return;

        bool fighting=IsFighting();
        if(!fighting)firstPopupHandled=false;

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

                if(fighting && !firstPopupHandled)
                {
                    CorrectOpeningBurst(label,critical);
                    firstPopupHandled=true;
                }

                label.resizeTextForBestFit=false;
                // Criticals should be unmistakable on a phone screen: 2x normal size.
                label.fontSize=critical?64:32;
                label.rectTransform.sizeDelta=critical?new Vector2(300f,112f):new Vector2(160f,60f);
            }

            previousActive[label]=active;
            previousLife[label]=life[i];
            previousText[label]=label.text;
        }
    }

    private bool IsFighting()
    {
        if(fishing==null || stateField==null)return false;
        object value=stateField.GetValue(fishing);
        return value!=null && value.ToString()=="Fighting";
    }

    private void CorrectOpeningBurst(Text label,bool critical)
    {
        if(fishing==null || hpField==null || maxHpField==null || healthField==null)return;
        if(!TryReadDamage(label.text,out int shownDamage))return;

        int tier=progress!=null?Mathf.Clamp(progress.Data.rodEquipped,0,ShopCatalog.MaxRodTier):0;
        int multiplier=tier>=2?3:tier>=1?2:1;
        int criticalMultiplier=critical?2:1;
        int minimum=2*multiplier*criticalMultiplier;

        // Later bursts are advertised as 2-4 / 4-8 / 6-12 for rod tiers 1/2/3.
        // Only repair a short opening burst that fell below that normal range.
        if(shownDamage>=minimum)return;

        int queuedAfterPopup=0;
        if(pendingDamageField!=null)
        {
            object queued=pendingDamageField.GetValue(fishing);
            if(queued is int amount)queuedAfterPopup=Mathf.Max(0,amount);
        }

        int target=UnityEngine.Random.Range(2,5)*multiplier*criticalMultiplier;
        int alreadyApplied=shownDamage+queuedAfterPopup;
        int finalDamage=Mathf.Max(target,alreadyApplied);
        int extra=Mathf.Max(0,finalDamage-alreadyApplied);

        if(extra>0)
        {
            int hp=Mathf.Max(0,(int)hpField.GetValue(fishing)-extra);
            hpField.SetValue(fishing,hp);
            int max=Mathf.Max(1,(int)maxHpField.GetValue(fishing));
            healthField.SetValue(fishing,hp/(float)max);
        }

        // Upgraded-rod bonus damage can be queued in LateUpdate immediately after
        // FishingSystem emitted this popup. It is already included in finalDamage,
        // so do not leak it into the next visible burst as a second hit.
        if(queuedAfterPopup>0 && pendingDamageField!=null)
            pendingDamageField.SetValue(fishing,0);

        label.text="−"+finalDamage;
    }

    private static bool TryReadDamage(string value,out int amount)
    {
        amount=0;
        if(string.IsNullOrEmpty(value))return false;
        bool found=false;
        for(int i=0;i<value.Length;i++)
        {
            char c=value[i];
            if(c<'0' || c>'9')continue;
            found=true;
            amount=amount*10+(c-'0');
        }
        return found;
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
