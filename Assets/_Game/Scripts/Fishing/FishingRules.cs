using UnityEngine;

// Shared, deterministic tuning. Depth affects size; region/bait affect species odds.
public static class FishingRules
{
    // Updated by LureShallowWaterBiteGuard immediately before FishingSystem.Update.
    // Keeping this gate in the actual bite-probability function makes the shallow
    // water rule authoritative: even if another caller asks for a lure bite chance,
    // it receives zero while the lure is in less than 1 m of water.
    public static bool LureBiteAllowed { get; set; } = true;

    // 0 at the reef/coast and 1 far into open ocean. This is updated from the
    // player's world position by OffshoreFishingRuntime. Coastal fishing keeps its
    // existing size distribution exactly; only distant open-ocean fishing gains
    // the giant-fish distribution below.
    public static float OffshoreFactor { get; set; }

    // FishingSystem historically measured the cast gauge from the camera while the
    // in-world LINE meter measures from the rod tip. The authored rod is several
    // metres long, so a gauge reading such as 27 m could look like only ~22–23 m
    // after landing. CastDistanceAccuracyRuntime updates these three values from the
    // real camera/rod/water geometry before FishingSystem.Update. They are zero by
    // default so headless checks and scenes without the runtime retain old behavior.
    public static float CastForwardOffset { get; set; }
    public static float CastPerpendicularOffsetSqr { get; set; }
    public static float CastVerticalOffset { get; set; }

    public static float CastPower(float elapsed) => 1f-Mathf.Sqrt(1f-Mathf.PingPong(Mathf.Max(0,elapsed)/1.25f,1f));

    public static float RequestedCastDistance(float power,float maximum)
        => Mathf.Lerp(5f,maximum,Mathf.Clamp01(power));

    // Return the camera-relative target distance required for the STRAIGHT rendered
    // line from RodTip to the water to equal the requested 5–maximum metres.
    // If the runtime geometry has not been supplied, this collapses exactly to the
    // old 5–maximum mapping.
    public static float CastDistance(float power,float maximum)
    {
        float requested=RequestedCastDistance(power,maximum);
        float side=Mathf.Max(0f,CastPerpendicularOffsetSqr);
        float vertical=CastVerticalOffset*CastVerticalOffset;
        float remaining=requested*requested-side-vertical;
        if(remaining<=0f)return requested;
        float target=CastForwardOffset+Mathf.Sqrt(remaining);
        return Mathf.Max(0.01f,target);
    }

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

    public static float OffshoreMaximumWeight(int species)
    {
        return FishCatalog.Get(species).MaxWeightKg*2.25f;
    }

    private static float BaseWeightAtDepth(int species,float depth,float random01)
    {
        var fish=FishCatalog.Get(species);
        float deep=Mathf.InverseLerp(.6f,6f,depth);
        float upper=Mathf.Lerp(.30f,1f,deep);
        float sample=Mathf.Pow(Mathf.Clamp01(random01),Mathf.Lerp(3.5f,.65f,deep));
        return Mathf.Lerp(fish.MinWeightKg,fish.MaxWeightKg,upper*sample);
    }

    private static float ApplyOffshoreSize(int species,float normalWeight,float random01,float factor)
    {
        float offshore=Mathf.Clamp01(factor);
        if(offshore<=0f)return normalWeight;

        var fish=FishCatalog.Get(species);
        float offshoreMaximum=OffshoreMaximumWeight(species);

        // In truly distant water, the distribution shifts dramatically upward:
        // giant candidates start around the old species maximum and extend to
        // 2.25x it. The .55 exponent strongly favors the upper end. Because this
        // is blended by OffshoreFactor, ordinary reef/coastal catches are not made
        // smaller or otherwise retuned.
        float giantRoll=Mathf.Pow(Mathf.Clamp01(random01),.55f);
        float giant=Mathf.Lerp(fish.MaxWeightKg*.92f,offshoreMaximum,giantRoll);
        float strength=Mathf.SmoothStep(0f,1f,offshore);
        return Mathf.Lerp(normalWeight,Mathf.Max(normalWeight,giant),strength);
    }

    public static float WeightAtDepth(int species,float depth,float random01,float offshore=-1)
    {
        return ApplyOffshoreSize(species,BaseWeightAtDepth(species,depth,random01),random01,offshore<0?OffshoreFactor:offshore);
    }

    public static float WeightAtCastDistance(int species,float distance,float random01,float offshore=-1)
    {
        // Fire Shad (variant 2) intentionally uses the exact same fish-size
        // sampling as Neon Breach (variant 0). Its ONLY gameplay difference is
        // the Red Snapper-heavy species table in ReefCatalog.
        float sizeBias=ShopCatalog.ActiveLureVariant==3 ? .18f : 0f;
        float sample=Mathf.Lerp(Mathf.Clamp01(random01),1f,sizeBias);
        float depth=Mathf.Lerp(.6f,6f,Mathf.InverseLerp(5f,30f,distance));
        return ApplyOffshoreSize(species,BaseWeightAtDepth(species,depth,sample),sample,offshore<0?OffshoreFactor:offshore);
    }

    public static float LureBiteChance(float castDistance,float retrievedFraction)
    {
        if(!LureBiteAllowed)return 0f;
        // Fire Shad also keeps Neon Breach's normal strike rate. Reef Minnow and
        // Deep Flash retain their existing strike-rate modifiers.
        float biteMultiplier=ShopCatalog.ActiveLureVariant==1 ? 1.15f : ShopCatalog.ActiveLureVariant==3 ? .85f : 1f;
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

