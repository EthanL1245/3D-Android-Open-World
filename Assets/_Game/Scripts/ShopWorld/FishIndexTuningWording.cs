using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the existing Fish Index UI copy aligned with the data-driven fishing rules.
/// ShopWorldHUD predates FishingTuning and rebuilds its rows dynamically, so this
/// lightweight presenter cleans only the obsolete explanatory copy while the menu is open.
/// </summary>
public sealed class FishIndexTuningWording : MonoBehaviour
{
    private static FishIndexTuningWording instance;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if(instance!=null)return;
        var go=new GameObject("Fish Index Tuning Wording");
        DontDestroyOnLoad(go);
        instance=go.AddComponent<FishIndexTuningWording>();
    }

    private void Update()
    {
        if(!ShopWorldHUD.MenuOpen || Time.unscaledTime<nextRefresh)return;
        nextRefresh=Time.unscaledTime+.15f;

        foreach(Text label in FindObjectsByType<Text>(FindObjectsSortMode.None))
        {
            if(label==null || string.IsNullOrEmpty(label.text))continue;
            string current=label.text;

            const string oldMarker="whole-percent odds (rounded) apply when a fish bites.";
            if(current.IndexOf(oldMarker,StringComparison.Ordinal)>=0)
            {
                int equipped=current.IndexOf(" equipped",StringComparison.Ordinal);
                string bait=equipped>0?current.Substring(0,equipped):"Equipped bait";
                bool lure=current.IndexOf("bites only while reeling",StringComparison.OrdinalIgnoreCase)>=0;
                bool palm=current.IndexOf("Palm Pond: 5–12 cm",StringComparison.OrdinalIgnoreCase)>=0;
                label.text=bait+" equipped • species odds are whole-number percentages from this biome + bait/lure table. "+
                    "Fish start from this biome's MIN–MAX bounded normal weight range. Island/ocean cast quality then scales both fish size and fight HP: "+
                    "about 5 m gives 20% potential, 30 m gives full potential, and deeper local water improves intermediate casts."+
                    (lure?" Lure bites occur while reeling.":string.Empty)+
                    (palm?" Palm Pond remains a separate 5–12 cm tiny-fish area and ignores cast-quality scaling.":string.Empty);
                continue;
            }

            // The row already displays both endpoints, so '(max)' was misleading.
            if(current.IndexOf(" m (max)\nCaught:",StringComparison.Ordinal)>=0)
                label.text=current.Replace(" m (max)\nCaught:"," m\nCaught:");
        }
    }
}
