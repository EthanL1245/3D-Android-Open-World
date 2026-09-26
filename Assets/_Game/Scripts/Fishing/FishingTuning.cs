using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Runtime contract for the human-editable fishing balance CSVs under
/// Assets/Resources/FishingTuning. Invalid data is rejected instead of silently
/// normalized; callers then fall back to the embedded legacy rules.
/// </summary>
public static class FishingTuning
{
    public sealed class SpeciesStats
    {
        public int SpeciesId;
        public string SpeciesName;
        public float MinWeightKg;
        public float MaxWeightKg;
        public int MinHealth;
        public int MaxHealth;
        public int MinCostCoins;
        public int MaxCostCoins;
    }

    public sealed class WeightDistribution
    {
        public string BiomeId;
        public int SpeciesId;
        public float MinKg;
        public float P01Kg;
        public float P25Kg;
        public float P50Kg;
        public float P75Kg;
        public float P99Kg;
        public float MaxKg;
    }

    private const string StatsResource="FishingTuning/FishStats";
    private const string WeightResource="FishingTuning/BiomeFishWeights";
    private const string ChanceResource="FishingTuning/BiomeBaitSpeciesChance";
    private const float ChanceTotalTolerance=.01f;

    // Stable save-game species IDs. Retired ID 4 must never be reused.
    private static readonly int[] ActiveSpecies={0,1,2,3,5,6,7,8,9,10,11,12};
    private static readonly string[] Biomes={"suncrest-reef","brinebreak-isle","deep-ocean"};
    private static readonly string[] RequiredBaits={
        "worms","worms-legacy","shrimp","squid",
        "lure:0","lure:1","lure:2","lure:3","lure:4"
    };

    private static readonly Dictionary<int,SpeciesStats> Stats=new Dictionary<int,SpeciesStats>();
    private static readonly Dictionary<string,WeightDistribution> WeightRows=new Dictionary<string,WeightDistribution>();
    private static readonly Dictionary<string,float[]> ChanceRows=new Dictionary<string,float[]>();

    private static bool loaded;
    private static bool valid;
    private static string validationError;

    public static int LastBiome { get; private set; }
    public static bool IsValid { get { EnsureLoaded(); return valid; } }
    public static string ValidationError { get { EnsureLoaded(); return validationError; } }

    public static void RememberBiome(int biome)
    {
        LastBiome=Mathf.Clamp(biome,0,Biomes.Length-1);
    }

    public static string BiomeId(int biome)
    {
        return Biomes[Mathf.Clamp(biome,0,Biomes.Length-1)];
    }

    public static string BaitKey(int bait)
    {
        if(bait==0)return "worms";
        if(bait==1)return "worms-legacy";
        if(bait==2)return "shrimp";
        if(bait==3)return "squid";
        if(bait==ShopCatalog.StarterLure)return "lure:"+Mathf.Clamp(ShopCatalog.ActiveLureVariant,0,4);
        return "worms";
    }

    public static void Reload()
    {
        loaded=false;
        Load();
    }

    public static bool TryGetSpeciesStats(int species,out SpeciesStats stats)
    {
        // Assign first so C# definite-assignment rules are satisfied even when
        // validation is false and the dictionary lookup is intentionally skipped.
        stats=null;
        EnsureLoaded();
        if(!valid)return false;
        return Stats.TryGetValue(Canonical(species),out stats);
    }

    public static bool TryGetWeightDistribution(int species,int biome,out WeightDistribution row)
    {
        row=null;
        EnsureLoaded();
        if(!valid)return false;
        return WeightRows.TryGetValue(WeightKey(BiomeId(biome),Canonical(species)),out row);
    }

    public static bool TryGetQuartiles(int species,int biome,out float p25,out float p50,out float p75)
    {
        WeightDistribution row;
        if(TryGetWeightDistribution(species,biome,out row))
        {
            p25=row.P25Kg;p50=row.P50Kg;p75=row.P75Kg;return true;
        }
        p25=p50=p75=0f;return false;
    }

    public static bool TryGetWeightRange(int species,int biome,out float minimum,out float maximum)
    {
        minimum=maximum=0f;
        WeightDistribution row;
        if(!TryGetWeightDistribution(species,biome,out row))return false;
        minimum=row.MinKg;maximum=row.MaxKg;return true;
    }

    public static bool TryRollWeight(int species,int biome,float random01,out float kg)
    {
        kg=0f;
        WeightDistribution row;
        if(!TryGetWeightDistribution(species,biome,out row))return false;
        kg=WeightAtPercentile(row,Mathf.Clamp01(random01));
        return true;
    }

    // This is deliberately percentile-defined instead of a textbook normal/skew-normal.
    // It gives the tuning file exact, intuitive control and has no clamped pile-up at
    // either hard bound. A uniform random percentile is mapped through these anchors:
    //   0%, 1%, 25%, 50%, 75%, 99%, 100%.
    private static float WeightAtPercentile(WeightDistribution row,float p)
    {
        if(p<=.01f)return Mathf.Lerp(row.MinKg,row.P01Kg,p/.01f);
        if(p<=.25f)return Mathf.Lerp(row.P01Kg,row.P25Kg,(p-.01f)/.24f);
        if(p<=.50f)return Mathf.Lerp(row.P25Kg,row.P50Kg,(p-.25f)/.25f);
        if(p<=.75f)return Mathf.Lerp(row.P50Kg,row.P75Kg,(p-.50f)/.25f);
        if(p<=.99f)return Mathf.Lerp(row.P75Kg,row.P99Kg,(p-.75f)/.24f);
        return Mathf.Lerp(row.P99Kg,row.MaxKg,(p-.99f)/.01f);
    }

    public static bool TryGetChance(int species,int bait,int biome,out float percent)
    {
        percent=0f;
        EnsureLoaded();
        if(!valid)return false;
        float[] row;
        if(!ChanceRows.TryGetValue(ChanceKey(BiomeId(biome),BaitKey(bait)),out row))return false;
        int index=ActiveIndex(Canonical(species));
        if(index<0)return false;
        percent=row[index];
        return true;
    }

    public static bool TryRollSpecies(float random01,int bait,int biome,out int species)
    {
        species=0;
        EnsureLoaded();
        if(!valid)return false;
        float[] row;
        if(!ChanceRows.TryGetValue(ChanceKey(BiomeId(biome),BaitKey(bait)),out row))return false;
        float target=Mathf.Clamp01(random01)*100f;
        float cumulative=0f;
        for(int i=0;i<ActiveSpecies.Length;i++)
        {
            cumulative+=row[i];
            if(target<=cumulative || i==ActiveSpecies.Length-1)
            {species=ActiveSpecies[i];return true;}
        }
        return false;
    }

    public static bool TryGetHealth(int species,float weightKg,out int health)
    {
        health=0;
        SpeciesStats stats;
        if(!TryGetSpeciesStats(species,out stats))return false;
        health=Mathf.Max(1,Mathf.RoundToInt(QuadraticByWeight(weightKg,stats.MinWeightKg,stats.MaxWeightKg,stats.MinHealth,stats.MaxHealth)));
        return true;
    }

    public static bool TryGetCost(int species,float weightKg,out int coins)
    {
        coins=0;
        SpeciesStats stats;
        if(!TryGetSpeciesStats(species,out stats))return false;
        coins=Mathf.Max(1,Mathf.RoundToInt(QuadraticByWeight(weightKg,stats.MinWeightKg,stats.MaxWeightKg,stats.MinCostCoins,stats.MaxCostCoins)));
        return true;
    }

    // Endpoint-defined parabola: t=0 at the species-wide minimum weight and t=1 at
    // the species-wide maximum. The minimum endpoint is the parabola's vertex.
    public static float QuadraticByWeight(float weight,float minWeight,float maxWeight,float minValue,float maxValue)
    {
        if(maxWeight<=minWeight)return maxValue;
        float t=Mathf.Clamp01((weight-minWeight)/(maxWeight-minWeight));
        return minValue+(maxValue-minValue)*t*t;
    }

    private static void EnsureLoaded()
    {
        if(!loaded)Load();
    }

    private static void Load()
    {
        loaded=true;valid=false;validationError=null;
        Stats.Clear();WeightRows.Clear();ChanceRows.Clear();
        try
        {
            TextAsset statsAsset=Resources.Load<TextAsset>(StatsResource);
            TextAsset weightAsset=Resources.Load<TextAsset>(WeightResource);
            TextAsset chanceAsset=Resources.Load<TextAsset>(ChanceResource);
            if(statsAsset==null){Fail("Missing Resources/FishingTuning/FishStats.csv");return;}
            if(weightAsset==null){Fail("Missing Resources/FishingTuning/BiomeFishWeights.csv");return;}
            if(chanceAsset==null){Fail("Missing Resources/FishingTuning/BiomeBaitSpeciesChance.csv");return;}

            if(!ParseStats(statsAsset.text))return;
            if(!ParseWeights(weightAsset.text))return;
            if(!ParseChances(chanceAsset.text))return;
            if(!ValidateCompleteness())return;
            valid=true;
        }
        catch(Exception e)
        {
            Fail("Unexpected parser error: "+e.Message);
        }
    }

    private static bool ParseStats(string text)
    {
        string[] lines=Lines(text);
        for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
        {
            string line=lines[lineIndex].Trim();
            if(Ignore(line) || line.StartsWith("speciesId,"))continue;
            string[] c=line.Split(',');
            if(c.Length!=8)return Fail("FishStats.csv line "+(lineIndex+1)+" must have 8 columns.");
            int id,minHealth,maxHealth,minCost,maxCost;float minWeight,maxWeight;
            if(!Int(c[0],out id) || !Float(c[2],out minWeight) || !Float(c[3],out maxWeight) ||
               !Int(c[4],out minHealth) || !Int(c[5],out maxHealth) || !Int(c[6],out minCost) || !Int(c[7],out maxCost))
                return Fail("FishStats.csv line "+(lineIndex+1)+" contains an invalid number.");
            id=Canonical(id);
            if(ActiveIndex(id)<0)return Fail("FishStats.csv line "+(lineIndex+1)+" uses inactive/unknown species ID "+id+".");
            if(Stats.ContainsKey(id))return Fail("FishStats.csv has duplicate species ID "+id+".");
            if(minWeight<=0f || maxWeight<=minWeight)return Fail("FishStats.csv species "+id+" requires 0 < minWeightKg < maxWeightKg.");
            if(minHealth<1 || maxHealth<minHealth)return Fail("FishStats.csv species "+id+" requires 1 <= minHealth <= maxHealth.");
            if(minCost<1 || maxCost<minCost)return Fail("FishStats.csv species "+id+" requires 1 <= minCost <= maxCost.");
            Stats[id]=new SpeciesStats{SpeciesId=id,SpeciesName=c[1].Trim(),MinWeightKg=minWeight,MaxWeightKg=maxWeight,
                MinHealth=minHealth,MaxHealth=maxHealth,MinCostCoins=minCost,MaxCostCoins=maxCost};
        }
        return true;
    }

    private static bool ParseWeights(string text)
    {
        string[] lines=Lines(text);
        for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
        {
            string line=lines[lineIndex].Trim();
            if(Ignore(line) || line.StartsWith("biomeId,"))continue;
            string[] c=line.Split(',');
            if(c.Length!=10)return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" must have 10 columns.");

            string biome=c[0].Trim();
            int id;
            float min,p01,p25,p50,p75,p99,max;
            if(BiomeIndex(biome)<0)return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" has unknown biome '"+biome+"'.");
            if(!Int(c[1],out id) || !Float(c[3],out min) || !Float(c[4],out p01) || !Float(c[5],out p25) ||
               !Float(c[6],out p50) || !Float(c[7],out p75) || !Float(c[8],out p99) || !Float(c[9],out max))
                return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" contains an invalid number.");

            id=Canonical(id);
            SpeciesStats stats;
            if(!Stats.TryGetValue(id,out stats))return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" references species "+id+" before/without FishStats.");
            if(!(min<p01 && p01<p25 && p25<p50 && p50<p75 && p75<p99 && p99<max))
                return Fail("BiomeFishWeights.csv "+biome+" species "+id+" requires min < P01 < P25 < P50 < P75 < P99 < max.");
            if(min<stats.MinWeightKg || max>stats.MaxWeightKg)
                return Fail("BiomeFishWeights.csv "+biome+" species "+id+" hard min/max must stay inside FishStats species min/max ("+
                    stats.MinWeightKg.ToString("0.###",CultureInfo.InvariantCulture)+"–"+stats.MaxWeightKg.ToString("0.###",CultureInfo.InvariantCulture)+" kg).");

            string key=WeightKey(biome,id);
            if(WeightRows.ContainsKey(key))return Fail("BiomeFishWeights.csv has duplicate row for "+biome+" species "+id+".");
            WeightRows[key]=new WeightDistribution{BiomeId=biome,SpeciesId=id,MinKg=min,P01Kg=p01,P25Kg=p25,P50Kg=p50,P75Kg=p75,P99Kg=p99,MaxKg=max};
        }
        return true;
    }

    private static bool ParseChances(string text)
    {
        string[] lines=Lines(text);
        for(int lineIndex=0;lineIndex<lines.Length;lineIndex++)
        {
            string line=lines[lineIndex].Trim();
            if(Ignore(line) || line.StartsWith("biomeId,"))continue;
            string[] c=line.Split(',');
            if(c.Length!=2+ActiveSpecies.Length)
                return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" must have "+(2+ActiveSpecies.Length)+" columns.");
            string biome=c[0].Trim();string bait=c[1].Trim();
            if(BiomeIndex(biome)<0)return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" has unknown biome '"+biome+"'.");
            if(!RequiredBait(bait))return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" has unknown baitKey '"+bait+"'.");
            float[] chances=new float[ActiveSpecies.Length];float total=0f;
            for(int i=0;i<chances.Length;i++)
            {
                if(!Float(c[i+2],out chances[i]) || chances[i]<0f)
                    return Fail("BiomeBaitSpeciesChance.csv line "+(lineIndex+1)+" has an invalid/negative probability in species column "+ActiveSpecies[i]+".");
                total+=chances[i];
            }
            if(Mathf.Abs(total-100f)>ChanceTotalTolerance)
                return Fail("BiomeBaitSpeciesChance.csv "+biome+" + "+bait+" totals "+total.ToString("0.######",CultureInfo.InvariantCulture)+"%, not 100%. Fix the row; probabilities are NOT auto-normalized.");
            string key=ChanceKey(biome,bait);
            if(ChanceRows.ContainsKey(key))return Fail("BiomeBaitSpeciesChance.csv has duplicate row for "+biome+" + "+bait+".");
            ChanceRows[key]=chances;
        }
        return true;
    }

    private static bool ValidateCompleteness()
    {
        for(int i=0;i<ActiveSpecies.Length;i++)
            if(!Stats.ContainsKey(ActiveSpecies[i]))return Fail("FishStats.csv is missing active species ID "+ActiveSpecies[i]+".");
        for(int b=0;b<Biomes.Length;b++)
        {
            for(int i=0;i<ActiveSpecies.Length;i++)
                if(!WeightRows.ContainsKey(WeightKey(Biomes[b],ActiveSpecies[i])))
                    return Fail("BiomeFishWeights.csv is missing "+Biomes[b]+" species "+ActiveSpecies[i]+".");
            for(int k=0;k<RequiredBaits.Length;k++)
                if(!ChanceRows.ContainsKey(ChanceKey(Biomes[b],RequiredBaits[k])))
                    return Fail("BiomeBaitSpeciesChance.csv is missing "+Biomes[b]+" + "+RequiredBaits[k]+".");
        }
        return true;
    }

    private static bool Fail(string message)
    {
        valid=false;validationError=message;
        Debug.LogError("[FISH TUNING] "+message);
        return false;
    }

    private static string[] Lines(string text)=>text.Replace("\r",string.Empty).Split('\n');
    private static bool Ignore(string line)=>string.IsNullOrWhiteSpace(line) || line.StartsWith("#");
    private static bool Int(string s,out int value)=>int.TryParse(s.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out value);
    private static bool Float(string s,out float value)=>float.TryParse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out value);
    private static int Canonical(int id)=>id==4?5:id;
    private static string WeightKey(string biome,int species)=>biome+"|"+species;
    private static string ChanceKey(string biome,string bait)=>biome+"|"+bait;

    private static int ActiveIndex(int species)
    {
        for(int i=0;i<ActiveSpecies.Length;i++)if(ActiveSpecies[i]==species)return i;
        return -1;
    }

    private static int BiomeIndex(string biome)
    {
        for(int i=0;i<Biomes.Length;i++)if(string.Equals(Biomes[i],biome,StringComparison.OrdinalIgnoreCase))return i;
        return -1;
    }

    private static bool RequiredBait(string bait)
    {
        for(int i=0;i<RequiredBaits.Length;i++)if(string.Equals(RequiredBaits[i],bait,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
}