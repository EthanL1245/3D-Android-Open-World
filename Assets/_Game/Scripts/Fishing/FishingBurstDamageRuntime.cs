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
/// This runtime suppresses only that legacy frame damage while a fight is active and
/// applies one authoritative randomized burst whenever REEL is pressed, then every
/// 0.35 s while it remains held:
///   Woodland: 2-4
///   Level 2:  4-8, 5% critical, critical = 8-16
///   Level 3:  6-12, 8% critical, critical = 12-24
///
/// The displayed popup is the rolled hit, including legitimate overkill on the final
/// blow, while the health bar clamps at zero. This guarantees that every Level 2
/// normal popup is 4-8 and every Level 2 critical popup is 8-16 instead of showing a
/// misleading capped -1/-2/-3/-4 when the fish has little health remaining.
/// </summary>
[DefaultExecutionOrder(2200)]
public sealed class FishingBurstDamageRuntime : MonoBehaviour
{
    private const float RepeatSeconds = 0.35f;
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

    private FishingSystem fishing;
    private FishingHUD hud;
    private ShopProgress progress;
    private Level2FishingRodRuntime legacyUpgradeRuntime;

    private bool inFight;
    private bool wasReeling;
    private bool waitForHookRelease;
    private float heldBurstTimer;
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
            DamageDisplayTimerField == null || UnconsciousField == null)
        {
            Debug.LogError("FishingBurstDamageRuntime could not bind the fishing damage fields. Legacy damage was left untouched.");
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
            heldBurstTimer = 0f;
            wasReeling = false;

            bool heldNow = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
            int activeBait = ActiveBaitField != null ? (int)ActiveBaitField.GetValue(fishing) : ShopCatalog.StarterLure;
            // A normal HOOK tap must not itself count as a REEL attack. Lures are
            // different: their existing held REEL intentionally carries into fight.
            waitForHookRelease = activeBait != ShopCatalog.StarterLure && heldNow;
        }

        // FishingSystem.Update ran earlier this frame. Restore the authoritative HP
        // before applying our discrete burst so any legacy frame tick cannot leak
        // into health or the popup. Resetting damageFraction to ZERO (not a negative
        // number) is important: a negative accumulator would make the legacy code
        // calculate negative damage and briefly heal the fish on the next frame.
        WriteAuthoritativeHealth(maxHp);
        SuppressLegacyDamageFields();

        if (authoritativeHp <= 0)
        {
            UnconsciousField.SetValue(fishing, true);
            return;
        }

        bool reeling = hud != null && hud.ActionInput != null && hud.ActionInput.IsHeld;
        if (waitForHookRelease)
        {
            if (!reeling) waitForHookRelease = false;
            wasReeling = reeling;
            return;
        }

        if (reeling)
        {
            if (!wasReeling)
            {
                DealBurst(maxHp);
                heldBurstTimer = 0f;
            }
            else
            {
                heldBurstTimer += Time.deltaTime;
                while (heldBurstTimer >= RepeatSeconds && authoritativeHp > 0)
                {
                    heldBurstTimer -= RepeatSeconds;
                    DealBurst(maxHp);
                }
            }
        }
        else
        {
            heldBurstTimer = 0f;
        }

        wasReeling = reeling;
        WriteAuthoritativeHealth(maxHp);
        SuppressLegacyDamageFields();
        UnconsciousField.SetValue(fishing, authoritativeHp <= 0);
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
        wasReeling = false;
        waitForHookRelease = false;
        heldBurstTimer = 0f;
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
