using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Owns fishing damage as discrete visible reel bursts.
///
/// The original FishingSystem accumulates tiny frame-by-frame integer damage and the
/// upgraded-rod compatibility layer multiplies those tiny decrements later. That can
/// make a 4-8 damage Level 2 rod visibly show -1/-2/-3, and can mark a tiny popup as
/// critical even though the advertised critical is 2x a full burst.
///
/// Damage is now earned strictly from ACTUAL REEL HOLD TIME. Every 0.35 seconds of
/// accumulated held REEL time produces one authoritative randomized burst:
///   Woodland: 2-4
///   Level 2:  4-8, 5% critical, critical = 8-16
///   Level 3:  6-12, 8% critical, critical = 12-24
///
/// Pressing REEL does not deal an instant hit. Releasing and rapidly tapping also
/// does not reset or accelerate the clock: only the sum of time the input is truly
/// held advances damage. This removes the tap-spam exploit while keeping continuous
/// reeling at the same 0.35-second burst cadence.
///
/// IMPORTANT: this compatibility layer can be the code that actually delivers the
/// KO. FishingSystem normally creates the unconscious/dead fish surface visual inside
/// its own zero-health branch. Because this component runs later in the frame, a burst
/// KO can otherwise set fishUnconscious=true after FishingSystem.Update has already
/// passed that branch; on the next frame FishingSystem sees the flag and goes straight
/// to retrieval, leaving only the bobber visible. We explicitly enter the existing
/// FishingSystem unconscious presentation when the authoritative burst reaches zero so
/// the dead fish floats at the surface and the fishing line remains attached to its
/// mouth exactly as the core fishing system intends.
/// </summary>
[DefaultExecutionOrder(2200)]
public sealed class FishingBurstDamageRuntime : MonoBehaviour
{
    private const float SecondsPerBurst = 0.35f;
    private const float SuppressedLegacyDisplayTimer = -1000f;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo StateField = typeof(FishingSystem).GetField("state", Flags);
    private static readonly FieldInfo HpField = typeof(FishingSystem).GetField("fishHealthPoints", Flags);
    private static readonly FieldInfo MaxHpField = typeof(FishingSystem).GetField("fishMaxHealth", Flags);
    private static readonly FieldInfo HealthField = typeof(FishingSystem).GetField("fishHealth", Flags);
    private static readonly FieldInfo PendingDamageField = typeof(FishingSystem).GetField("pendingDamage", Flags);
    private static readonly FieldInfo DamageFractionField = typeof(FishingSystem).GetField("damageFraction", Flags);
    private static readonly FieldInfo DamageDisplayTimerField = typeof(FishingSystem).GetField("damageDisplayTimer", Flags);
    private static readonly FieldInfo UnconsciousField = typeof(FishingSystem).GetField("fishUnconscious", Flags);
    private static readonly FieldInfo BobberField = typeof(FishingSystem).GetField("bobber", Flags);
    private static readonly FieldInfo PlayerCameraField = typeof(FishingSystem).GetField("playerCamera", Flags);
    private static readonly FieldInfo ActiveBaitField = typeof(FishingSystem).GetField("activeBait", Flags);
    private static readonly MethodInfo EnsureUnconsciousFishVisualMethod =
        typeof(FishingSystem).GetMethod("EnsureUnconsciousFishVisual", Flags);

    private FishingSystem fishing;
    private FishingHUD hud;
    private ShopProgress progress;
    private Level2FishingRodRuntime legacyUpgradeRuntime;

    private bool inFight;
    private bool waitForHookRelease;
    private float accumulatedReelSeconds;
    private int authoritativeHp;

    public static int NormalMinimumForTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, ShopCatalog.MaxRodTier);
        return 2 * (tier + 1);
    }

    public static int NormalMaximumForTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, ShopCatalog.MaxRodTier);
        return 4 * (tier + 1);
    }

    public static float CriticalChanceForTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, ShopCatalog.MaxRodTier);
        return tier >= 2 ? 0.08f : tier >= 1 ? 0.05f : 0f;
    }

    /// <summary>
    /// Deterministic entry point used by the editor validation pass as well as the
    /// live game. damageRoll01 chooses an integer uniformly across the advertised
    /// inclusive normal range; criticalRoll01 determines whether that whole burst is
    /// doubled. This keeps the UI description and gameplay formula literally shared.
    /// </summary>
    public static int RollBurstForTier(int tier, float damageRoll01, float criticalRoll01, out bool critical)
    {
        int minimum = NormalMinimumForTier(tier);
        int maximum = NormalMaximumForTier(tier);
        float roll = Mathf.Clamp(damageRoll01, 0f, 0.999999f);
        int normal = minimum + Mathf.FloorToInt(roll * (maximum - minimum + 1));
        critical = criticalRoll01 < CriticalChanceForTier(tier);
        return critical ? normal * 2 : normal;
    }

    private void Awake()
    {
        fishing = GetComponent<FishingSystem>();
        if (fishing == null) fishing = FindFirstObjectByType<FishingSystem>();
        progress = fishing != null ? fishing.GetComponent<ShopProgress>() : FindFirstObjectByType<ShopProgress>();
        legacyUpgradeRuntime = fishing != null ? fishing.GetComponent<Level2FishingRodRuntime>() : null;
        hud = FindFirstObjectByType<FishingHUD>();

        if (fishing == null || StateField == null || HpField == null || MaxHpField == null ||
            HealthField == null || PendingDamageField == null || DamageFractionField == null ||
            DamageDisplayTimerField == null || UnconsciousField == null ||
            EnsureUnconsciousFishVisualMethod == null)
        {
            Debug.LogError("FishingBurstDamageRuntime could not bind the fishing damage/presentation fields. Legacy damage was left untouched.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (!enabled || fishing == null) return;
        if (hud == null) hud = FindFirstObjectByType<FishingHUD>();
        if (progress == null) progress = fishing.GetComponent<ShopProgress>();
        if (legacyUpgradeRuntime == null) legacyUpgradeRuntime = fishing.GetComponent<Level2FishingRodRuntime>();

        bool fighting = IsFighting();
        if (!fighting)
        {
            EndFightOwnership();
            return;
        }

        // The older upgraded-rod runtime must not multiply our already-complete
        // bursts. It still runs normally outside fights, so rod model/equipment UI
        // behavior is preserved.
        if (legacyUpgradeRuntime != null && legacyUpgradeRuntime.enabled)
            legacyUpgradeRuntime.enabled = false;

        int maxHp = Mathf.Max(1, (int)MaxHpField.GetValue(fishing));
        if (!inFight)
        {
            inFight = true;
            authoritativeHp = maxHp;
            accumulatedReelSeconds = 0f;

            bool heldNow = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
            int activeBait = ActiveBaitField != null ? (int)ActiveBaitField.GetValue(fishing) : ShopCatalog.StarterLure;

            // Consumable bait enters Fighting from a manual HOOK press, so that
            // press must be released before it can begin earning reel time. Permanent
            // lures auto-hook while REEL is already held; that same continuous hold
            // must start the 0.35 s damage clock immediately without a release/repress.
            waitForHookRelease = activeBait != ShopCatalog.StarterLure && heldNow;
        }

        // FishingSystem.Update ran earlier this frame. Restore the authoritative HP
        // before applying our burst so legacy frame ticks cannot leak into health or
        // spawn their old -1/-2/-3 popups.
        WriteAuthoritativeHealth(maxHp);
        SuppressLegacyDamageFields();

        if (authoritativeHp <= 0)
        {
            EnterUnconsciousPresentation();
            return;
        }

        bool reeling = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
        if (waitForHookRelease)
        {
            if (!reeling) waitForHookRelease = false;
            return;
        }

        if (reeling)
        {
            // The only thing that advances damage is real time spent holding REEL.
            // Do NOT award a burst on button-down and do NOT reset this accumulator
            // on release. Therefore 7 x 0.05 s taps equal 0.35 s of reel time, not
            // seven full hits, while a continuous hold behaves identically.
            accumulatedReelSeconds += Time.deltaTime;
            while (accumulatedReelSeconds >= SecondsPerBurst && authoritativeHp > 0)
            {
                accumulatedReelSeconds -= SecondsPerBurst;
                DealBurst(maxHp);
            }
        }

        WriteAuthoritativeHealth(maxHp);
        SuppressLegacyDamageFields();

        if (authoritativeHp <= 0)
            EnterUnconsciousPresentation();
        else
            UnconsciousField.SetValue(fishing, false);
    }

    private void DealBurst(int maxHp)
    {
        int tier = progress != null ? Mathf.Clamp(progress.Data.rodEquipped, 0, ShopCatalog.MaxRodTier) : 0;
        bool critical;
        int rolledDamage = RollBurstForTier(tier, UnityEngine.Random.value, UnityEngine.Random.value, out critical);
        int appliedDamage = Mathf.Min(authoritativeHp, rolledDamage);
        if (appliedDamage <= 0) return;

        authoritativeHp -= appliedDamage;
        WriteAuthoritativeHealth(maxHp);

        if (critical) FishingDamagePresentation.MarkCriticalHit();

        if (hud != null)
        {
            Vector3 screenPoint = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);
            GameObject bobber = BobberField != null ? BobberField.GetValue(fishing) as GameObject : null;
            Camera camera = PlayerCameraField != null ? PlayerCameraField.GetValue(fishing) as Camera : null;
            if (bobber != null && camera != null)
                screenPoint = camera.WorldToScreenPoint(bobber.transform.position);

            // Show the rolled attack value, not the remaining-HP-capped value. A
            // finishing 12-damage critical against a 3-HP fish is still a 12-damage
            // critical; only the health bar clamps at zero.
            hud.ShowDamage(rolledDamage, screenPoint);
        }
    }

    private void EnterUnconsciousPresentation()
    {
        // This must be done as one transition. If we only set fishUnconscious here,
        // FishingSystem skips its own zero-health branch on the following frame and
        // never creates the floating fish. Invoke the existing private presentation
        // method immediately; it creates the correctly-sized species model, freezes
        // its animation, places it on the water surface, hides the bobber renderer,
        // and makes GetLineTargetPosition use the mouth marker.
        WriteAuthoritativeHealth(Mathf.Max(1, (int)MaxHpField.GetValue(fishing)));
        SuppressLegacyDamageFields();
        UnconsciousField.SetValue(fishing, true);

        try
        {
            EnsureUnconsciousFishVisualMethod.Invoke(fishing, null);
        }
        catch (TargetInvocationException e)
        {
            Debug.LogError("FishingBurstDamageRuntime could not create the unconscious fish presentation: " +
                (e.InnerException != null ? e.InnerException.ToString() : e.ToString()));
        }
        catch (Exception e)
        {
            Debug.LogError("FishingBurstDamageRuntime could not create the unconscious fish presentation: " + e);
        }
    }

    private bool IsFighting()
    {
        object state = StateField.GetValue(fishing);
        return state != null && string.Equals(state.ToString(), "Fighting", StringComparison.Ordinal);
    }

    private void WriteAuthoritativeHealth(int maxHp)
    {
        authoritativeHp = Mathf.Clamp(authoritativeHp, 0, maxHp);
        HpField.SetValue(fishing, authoritativeHp);
        HealthField.SetValue(fishing, authoritativeHp / (float)Mathf.Max(1, maxHp));
    }

    private void SuppressLegacyDamageFields()
    {
        PendingDamageField.SetValue(fishing, 0);
        DamageFractionField.SetValue(fishing, 0f);
        DamageDisplayTimerField.SetValue(fishing, SuppressedLegacyDisplayTimer);
    }

    private void EndFightOwnership()
    {
        if (!inFight)
        {
            if (legacyUpgradeRuntime != null && !legacyUpgradeRuntime.enabled)
                legacyUpgradeRuntime.enabled = true;
            return;
        }

        inFight = false;
        waitForHookRelease = false;
        accumulatedReelSeconds = 0f;
        FishingDamagePresentation.ClearPendingCritical();

        if (legacyUpgradeRuntime != null && !legacyUpgradeRuntime.enabled)
            legacyUpgradeRuntime.enabled = true;
    }

    private void OnDisable()
    {
        if (legacyUpgradeRuntime != null && !legacyUpgradeRuntime.enabled)
            legacyUpgradeRuntime.enabled = true;
    }
}
