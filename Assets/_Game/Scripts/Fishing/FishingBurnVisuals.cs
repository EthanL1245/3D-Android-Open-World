using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightning Strike water visuals. The legacy component name keeps existing
/// scene/bootstrap references valid; no flame overlays or line tint remain.
/// </summary>
[DefaultExecutionOrder(5000)]
[DisallowMultipleComponent]
public sealed class FishingBurnVisuals : MonoBehaviour
{
    private readonly List<GameObject> ripples = new List<GameObject>();
    private FishingSystem fishing;
    private FishingBurstDamageRuntime damage;
    private FishingHUD hud;
    private readonly List<FishingLightningStrikeAnimation> strikes = new List<FishingLightningStrikeAnimation>();
    private bool wasPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (FishingSystem system in FindObjectsByType<FishingSystem>(FindObjectsSortMode.None))
            if (system != null && system.GetComponent<FishingBurnVisuals>() == null)
                system.gameObject.AddComponent<FishingBurnVisuals>();
    }

    private void LateUpdate()
    {
        if (fishing == null) fishing = GetComponent<FishingSystem>();
        if (damage == null) damage = GetComponent<FishingBurstDamageRuntime>();
        if (fishing == null || damage == null) return;
        if (hud == null) hud = GetComponent<FishingHUD>();
        bool paused = ShopWorldHUD.MenuOpen || (hud != null && !hud.FishingUiVisible);
        if (paused != wasPaused)
        {
            foreach (var strike in strikes) if (strike != null) strike.SetPaused(paused);
            wasPaused = paused;
        }
        if (paused) return;
        // Drain actual damage events, including the eighth hit at exactly 4s
        // when BurningSeconds has already reached zero.
        while (damage.TryTakeLightningZap(out int zapIndex))
        {
            var strike = FishingLightningStrikeAnimation.Create(transform);
            if (strike == null) continue;
            strike.transform.position = fishing.NetworkLineEnd;
            strike.Restart(zapIndex);
            strikes.Add(strike);
        }
        for (int i = strikes.Count - 1; i >= 0; i--)
        {
            var strike = strikes[i];
            if (strike == null) { strikes.RemoveAt(i); continue; }
            strike.Tick(fishing.NetworkLineEnd, fishing.PresentationWater, fishing.PresentationCamera);
            if (strike.TakeRippleCue())
            {
                var ripple = FishingElectricRippleEffect.Spawn(fishing.NetworkLineEnd, fishing.PresentationWater);
                if (ripple != null) ripples.Add(ripple);
            }
            if (strike.Finished) { Destroy(strike.gameObject); strikes.RemoveAt(i); }
        }
        for (int i = ripples.Count - 1; i >= 0; i--)
            if (ripples[i] == null) ripples.RemoveAt(i);

    }

    private void OnDisable()
    {
        foreach (var strike in strikes) if (strike != null) Destroy(strike.gameObject);
        strikes.Clear();
        wasPaused = false;
        foreach (GameObject ripple in ripples)
            if (ripple != null) Destroy(ripple);
        ripples.Clear();
    }
}
