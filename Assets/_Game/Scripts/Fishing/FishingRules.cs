using UnityEngine;

// Shared, deterministic tuning. Depth affects size; region/bait affect species odds.
public static class FishingRules
{
    public static float CastPower(float elapsed) => Mathf.PingPong(Mathf.Max(0,elapsed)/.85f,1f);
    public static float CastDistance(float power,float maximum) => Mathf.Lerp(2f,maximum,Mathf.Clamp01(power));
    public static float WeightAtDepth(int species,float depth,float random01)
    {
        var fish=FishCatalog.Get(species);
        float deep=Mathf.InverseLerp(.6f,6f,depth);
        float upper=Mathf.Lerp(.30f,1f,deep);
        float sample=Mathf.Pow(Mathf.Clamp01(random01),Mathf.Lerp(3.5f,.65f,deep));
        return Mathf.Lerp(fish.MinWeightKg,fish.MaxWeightKg,upper*sample);
    }
    public static int MaxHealth(int species,float kg)
    {
        var fish=FishCatalog.Get(species);
        float rarity=1f/Mathf.Sqrt(Mathf.Max(1,ReefCatalog.Weight(species)));
        return Mathf.Max(1,Mathf.RoundToInt((10+22*Mathf.Sqrt(Mathf.Max(0,kg))+3*kg)*(1+fish.Difficulty*.5f+rarity)));
    }
}
