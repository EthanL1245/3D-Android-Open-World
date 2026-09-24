using UnityEngine;

// Shared, deterministic tuning. Depth affects size; region/bait affect species odds.
public static class FishingRules
{
    public static float CastPower(float elapsed) => 1f-Mathf.Sqrt(1f-Mathf.PingPong(Mathf.Max(0,elapsed)/1.25f,1f));
    public static float CastDistance(float power,float maximum) => Mathf.Lerp(5f,maximum,Mathf.Clamp01(power));
    // Only an invalid prefix is allowed. A hole after the first valid sample
    // blocks the entire range, including an invalid maximum endpoint.
    public static bool ContinuousCastRange(bool[] samples,out float minimumPower)
    {
        minimumPower=1f;
        if(samples==null || samples.Length<2)return false;
        int first=-1;
        for(int i=0;i<samples.Length;i++)
        {
            if(samples[i]){if(first<0)first=i;}
            else if(first>=0)return false;
        }
        if(first<0 || first==samples.Length-1)return false;
        minimumPower=first/(float)(samples.Length-1);return true;
    }
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
        return Mathf.Max(1,Mathf.RoundToInt(2.5f*(10+22*Mathf.Sqrt(Mathf.Max(0,kg))+3*kg)*(1+fish.Difficulty*.5f+rarity)));
    }
}
