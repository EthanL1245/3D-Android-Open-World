using System;

/// <summary>Saved character progression. One permanent point per level; no equipment state.</summary>
[Serializable]
public sealed class AnglerProgression
{
    public const int MaxLevel = 601;
    public int level = 1;
    public double experience;
    // Raw damage, percent damage, reel speed, tension control, skill, charm.
    public int[] stats = new int[6];
    public static readonly string[] Names = { "Raw Damage", "Percent Damage", "Reeling Speed", "Tension Control", "Skill", "Charm" };
    public static readonly string[] AbilityNames = { "BURNING FISH", "HOOKSET POWER", "LINE IMMORTALITY", "SIREN'S CALL" };
    public static readonly string[] AbilityAssets = { "BurningFish", "HooksetPower", "LineImmortality", "SirensCall" };
    public int Value(int stat) => stats != null && stat >= 0 && stat < stats.Length ? Math.Max(0, Math.Min(100, stats[stat])) : 0;
    public float Multiplier(int stat) => 1f + Value(stat) * .01f;
    public bool Unlocked(int ability) => ability >= 0 && ability < 4 && level >= (ability + 1) * 20;
    public static double RequiredXp(int currentLevel) => Math.Ceiling(250d * Math.Pow(1.015d, Math.Max(0, Math.Min(MaxLevel - 1, currentLevel - 1))));
    public static int CatchXp(int maximumHealth, float difficulty)
        => Math.Max(1, (int)Math.Round(4d * Math.Sqrt(Math.Max(1, maximumHealth)) * (1d + 2d * Math.Max(0d, Math.Min(2d, difficulty)))));

    public bool Normalize()
    {
        bool changed = false;
        if (level < 1 || level > MaxLevel) { level = Math.Max(1, Math.Min(MaxLevel, level)); changed = true; }
        if (stats == null || stats.Length != 6) { var old = stats; stats = new int[6]; if (old != null) Array.Copy(old, stats, Math.Min(6, old.Length)); changed = true; }
        for (int i = 0; i < 6; i++) { int value = Value(i); if (value != stats[i]) { stats[i] = value; changed = true; } }
        if (double.IsNaN(experience) || double.IsInfinity(experience) || experience < 0) { experience = 0; changed = true; }
        return changed;
    }

    // Optional per-level callback: new level, increased stat index, new stat value.
    // A stat index of -1 means all permanent attributes were already capped.
    public int AddXp(int amount, Func<int, int> choose, Action<int, int, int> onLevelUp = null)
    {
        Normalize();
        if (amount <= 0 || level >= MaxLevel) return 0;
        experience += amount;
        int gained = 0;
        while (level < MaxLevel && experience >= RequiredXp(level))
        {
            experience -= RequiredXp(level);
            int available = 0;
            for (int i = 0; i < 6; i++) if (stats[i] < 100) available++;
            int increasedStat = -1;
            if (available > 0)
            {
                int pick = Math.Max(0, Math.Min(available - 1, choose(available)));
                for (int i = 0; i < 6; i++)
                    if (stats[i] < 100 && pick-- == 0)
                    {
                        stats[i]++;
                        increasedStat = i;
                        break;
                    }
            }
            level++; gained++;
            if (onLevelUp != null)
                onLevelUp(level, increasedStat, increasedStat >= 0 ? stats[increasedStat] : 0);
        }
        if (level == MaxLevel) experience = 0;
        return gained;
    }
}
