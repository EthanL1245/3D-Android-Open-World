using UnityEngine;

// Shared, deterministic tuning. Depth affects size; region/bait affect species odds.
public static class FishingRules
{
    // Updated by LureShallowWaterBiteGuard immediately before FishingSystem.Update.
    // Keeping this gate in the actual bite-probability function makes the shallow
    // water rule authoritative: even if another caller asks for a lure bite chance,
    // it receives zero while the lure is in less than 1 m of water.
    public static bool LureBiteAllowed { get; set; } = true;

    public static float CastPower(float elapsed) => 1f-Mathf.Sqrt(1f-Mathf.PingPong(Mathf.Max(0,elapsed)/1.25f,1f));
    public static float CastDistance(float power,float maximum) => Mathf.Lerp(5f,maximum,Mathf.Clamp01(power));
    // Any continuous interval is usable; gaps and either end can be blocked.
    public static bool ContinuousCastRange(bool[] samples,out float minimumPower)
    {
        minimumPower=1f;
        if(samples==null || samples.Length<2)return false;
        for(int i=0;i<samples.Length-1;i++)if(samples[i] && samples[i+1])
        {minimumPower=i/(float)(samples.Length-1);return true;}
        return false;
    }
    public static bool IsCastPowerAvailable(bool[] samples,float power)
    {
        if(samples==null || samples.Length<2)return false;
        int i=Mathf.Clamp((int)(Mathf.Clamp01(power)*(samples.Length-1)),0,samples.Length-2);
        return samples[i] && samples[i+1];
    }
    public static float WeightAtDepth(int species,float depth,float random01)
    {
        var fish=FishCatalog.Get(species);
        float deep=Mathf.InverseLerp(.6f,6f,depth);
        float upper=Mathf.Lerp(.30f,1f,deep);
        float sample=Mathf.Pow(Mathf.Clamp01(random01),Mathf.Lerp(3.5f,.65f,deep));
        return Mathf.Lerp(fish.MinWeightKg,fish.MaxWeightKg,upper*sample);
    }
    public static float WeightAtCastDistance(int species,float distance,float random01)
    {
        // Premium lures do not change the 5-30 m cast envelope. They only
        // nudge the sampled fish size upward after a successful lure strike.
        float sizeBias=ShopCatalog.ActiveLureVariant==2 ? .08f : ShopCatalog.ActiveLureVariant==3 ? .18f : 0f;
        float sample=Mathf.Lerp(Mathf.Clamp01(random01),1f,sizeBias);
        return WeightAtDepth(species,Mathf.Lerp(.6f,6f,Mathf.InverseLerp(5f,30f,distance)),sample);
    }
    public static float LureBiteChance(float castDistance,float retrievedFraction)
    {
        if(!LureBiteAllowed)return 0f;
        float biteMultiplier=ShopCatalog.ActiveLureVariant==1 ? 1.15f : ShopCatalog.ActiveLureVariant==2 ? .95f : ShopCatalog.ActiveLureVariant==3 ? .85f : 1f;
        float fullChance=Mathf.Clamp01(.5f*Mathf.Clamp01(castDistance/30f)*biteMultiplier);
        return 1f-Mathf.Pow(1f-fullChance,Mathf.Clamp01(retrievedFraction));
    }
    public static int MaxHealth(int species,float kg)
    {
        var fish=FishCatalog.Get(species);
        float rarity=1f/Mathf.Sqrt(Mathf.Max(1,ReefCatalog.Weight(species)));
        return Mathf.Max(1,Mathf.RoundToInt(2.5f*(10+22*Mathf.Sqrt(Mathf.Max(0,kg))+3*kg)*(1+fish.Difficulty*.5f+rarity)));
    }
}
