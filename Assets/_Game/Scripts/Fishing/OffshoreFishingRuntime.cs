using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Converts distance beyond Suncrest Reef into the open-ocean size bonus used by
/// FishingRules. The reef/coastal distribution is untouched; the bonus begins only
/// after the player has travelled beyond the authored reef and becomes very strong
/// far offshore.
/// </summary>
[DefaultExecutionOrder(-240)]
public sealed class OffshoreFishingRuntime : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ShopProgress progress=FindFirstObjectByType<ShopProgress>();
        if(progress!=null)
            ShopCatalog.SetActiveLureVariant(progress.Data.lureEquipped);

        foreach(FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if(system!=null && system.GetComponent<OffshoreFishingRuntime>()==null)
                system.gameObject.AddComponent<OffshoreFishingRuntime>();

    }

    private void Update()
    {
        ReefZone zone=ReefZone.Active;
        if(zone==null)
        {
            FishingRules.OffshoreFactor=0f;
            return;
        }

        float beyondReef=zone.OpenOceanDistance(transform.position);
        // The first tens of metres beyond the reef stay close to the existing
        // experience. From 35 m to 240 m beyond it, the giant-fish bias ramps in.
        FishingRules.OffshoreFactor=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(35f,240f,beyondReef));
    }

    private void OnDisable(){FishingRules.OffshoreFactor=0f;}
    private void OnDestroy(){FishingRules.OffshoreFactor=0f;}
}
