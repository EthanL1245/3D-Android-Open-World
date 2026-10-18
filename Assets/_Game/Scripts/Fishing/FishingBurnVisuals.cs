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
    private const float RippleInterval = .35f;
    private readonly List<GameObject> ripples = new List<GameObject>();
    private FishingSystem fishing;
    private FishingBurstDamageRuntime damage;
    private FishingHUD hud;
    private readonly List<FishingLightningStrikeAnimation> strikes = new List<FishingLightningStrikeAnimation>();
    private bool wasPaused;
    private bool striking;
    private float untilNextRipple;

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
        // Drain actual damage events, including the fourth hit at exactly 5s
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
            if (strike.Finished) { Destroy(strike.gameObject); strikes.RemoveAt(i); }
        }
        for (int i = ripples.Count - 1; i >= 0; i--)
            if (ripples[i] == null) ripples.RemoveAt(i);

        bool active = damage.BurningSeconds > .0001f;
        if (!active)
        {
            striking = false;
            return;
        }
        if (!striking)
        {
            striking = true;
            untilNextRipple = 0f;
        }
        untilNextRipple -= Time.deltaTime;
        if (untilNextRipple <= 0f)
        {
            // Each ring stays at its emission point; new rings follow the fish.
            // At most five normally overlap. Final rings finish fading naturally.
            var ripple = FishingElectricRippleEffect.Spawn(fishing.NetworkLineEnd, fishing.PresentationWater);
            if (ripple != null) ripples.Add(ripple);
            untilNextRipple = RippleInterval;
        }
    }

    private void OnDisable()
    {
        striking = false;
        foreach (var strike in strikes) if (strike != null) Destroy(strike.gameObject);
        strikes.Clear();
        wasPaused = false;
        foreach (GameObject ripple in ripples)
            if (ripple != null) Destroy(ripple);
        ripples.Clear();
    }
}
