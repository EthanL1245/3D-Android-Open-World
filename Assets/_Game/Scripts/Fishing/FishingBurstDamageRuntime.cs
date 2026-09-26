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
/// applies one truthful randomized burst whenever REEL is pressed, then every 0.35 s
/// while it remains held:
///   Woodland: 2-4
///   Level 2:  4-8, 5% critical, critical = 8-16
///   Level 3:  6-12, 8% critical, critical = 12-24
/// The popup value, health-bar loss and critical styling all come from the same roll.
/// </summary>
[DefaultExecutionOrder(2200)]
public sealed class FishingBurstDamageRuntime : MonoBehaviour
{
    private const float RepeatSeconds = 0.35f;
    private const float SuppressedLegacyAccumulator = -1000f;

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

        // Undo any legacy per-frame decrement that FishingSystem.Update attempted
        // earlier this frame, then keep its old accumulator far below 1 so it cannot
        // create its own popup before this component gets control next frame.
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
        int multiplier = tier + 1;
        int minimum = 2 * multiplier;
        int maximum = 4 * multiplier;
        int normalDamage = UnityEngine.Random.Range(minimum, maximum + 1);

        float criticalChance = tier >= 2 ? 0.08f : tier >= 1 ? 0.05f : 0f;
        bool critical = criticalChance > 0f && UnityEngine.Random.value < criticalChance;
        int rolledDamage = critical ? normalDamage * 2 : normalDamage;
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
            hud.ShowDamage(appliedDamage, screenPoint);
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
        DamageFractionField.SetValue(fishing, SuppressedLegacyAccumulator);
        DamageDisplayTimerField.SetValue(fishing, SuppressedLegacyAccumulator);
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