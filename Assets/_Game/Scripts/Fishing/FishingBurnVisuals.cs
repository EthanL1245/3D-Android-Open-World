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
    private FishingLightningStrikeAnimation lightning;
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
        if (ShopWorldHUD.MenuOpen || (hud != null && !hud.FishingUiVisible)) return;
        for (int i = ripples.Count - 1; i >= 0; i--)
            if (ripples[i] == null) ripples.RemoveAt(i);

        bool active = damage.BurningSeconds > .0001f;
        if (!active)
        {
            striking = false;
            if (lightning != null) lightning.gameObject.SetActive(false);
            return;
        }
        if (!striking)
        {
            striking = true;
            untilNextRipple = 0f;
        }
        if (lightning == null) lightning = FishingLightningStrikeAnimation.Create(transform);
        if (lightning != null)
        {
            if (!lightning.gameObject.activeSelf) lightning.Restart();
            lightning.Tick(fishing.NetworkLineEnd, fishing.PresentationWater,
                fishing.PresentationCamera, damage.BurningSeconds);
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
        if (lightning != null) lightning.gameObject.SetActive(false);
        foreach (GameObject ripple in ripples)
            if (ripple != null) Destroy(ripple);
        ripples.Clear();
    }
}
