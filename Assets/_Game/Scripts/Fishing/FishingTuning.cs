using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Single runtime contract for editable fishing balance CSVs under
/// Assets/Resources/FishingTuning. Invalid data never silently normalizes: the
/// configured system is disabled and legacy rules remain available as a fallback.
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
        public float P25Kg;
        public float P50Kg;
        public float P75Kg;

        public float SigmaKg => (P75Kg-P25Kg)/(2f*0.67448975f);
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
        EnsureLoaded();
        return valid && Stats.TryGetValue(Canonical(species),out stats);
    }

    public static bool TryGetWeightDistribution(int species,int biome,out WeightDistribution row)
    {
        EnsureLoaded();
        return valid && WeightRows.TryGetValue(WeightKey(BiomeId(biome),Canonical(species)),out row);
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
        SpeciesStats stats;
        if(!TryGetWeightDistribution(species,biome,out row) || !TryGetSpeciesStats(species,out stats))return false;
        // The index shows the practical central 99.7% range of the configured normal,
        // clamped to the species' absolute physical/gameplay min/max bounds.
        float sigma=row.SigmaKg;
        minimum=Mathf.Max(stats.MinWeightKg,row.P50Kg-3f*sigma);
        maximum=Mathf.Min(stats.MaxWeightKg,row.P50Kg+3f*sigma);
        return true;
    }

    public static bool TryRollWeight(int species,int biome,float random01,out float kg)
    {
        kg=0f;
        WeightDistribution row;
        SpeciesStats stats;
        if(!TryGetWeightDistribution(species,biome,out row) || !TryGetSpeciesStats(species,out stats))return false;
        double p=Math.Max(0.000001,Math.Min(0.999999,random01));
        double z=InverseNormal(p);
        kg=Mathf.Clamp(row.P50Kg+(float)z*row.SigmaKg,stats.MinWeightKg,stats.MaxWeightKg);
        return true;
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

    // Endpoint-defined parabola convention: t=0 at minimum weight, t=1 at maximum,
    // value=min+(max-min)*t^2. This uniquely supplies the missing third constraint by
    // placing the parabola's vertex (zero slope) at the minimum-weight endpoint.
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
            if(c.Length!=6)return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" must have 6 columns.");
            string biome=c[0].Trim();int id;float p25,p50,p75;
            if(BiomeIndex(biome)<0)return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" has unknown biome '"+biome+"'.");
            if(!Int(c[1],out id) || !Float(c[3],out p25) || !Float(c[4],out p50) || !Float(c[5],out p75))
                return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" contains an invalid number.");
            id=Canonical(id);
            SpeciesStats stats;
            if(!Stats.TryGetValue(id,out stats))return Fail("BiomeFishWeights.csv line "+(lineIndex+1)+" references species "+id+" before/without FishStats.");
            if(!(p25<p50 && p50<p75))return Fail("BiomeFishWeights.csv "+biome+" species "+id+" requires P25 < P50 < P75.");
            float midpoint=(p25+p75)*.5f;
            float symmetryTolerance=Mathf.Max(.005f,(p75-p25)*.01f);
            if(Mathf.Abs(p50-midpoint)>symmetryTolerance)
                return Fail("BiomeFishWeights.csv "+biome+" species "+id+" is not a normal distribution: P50 must equal the midpoint of P25/P75 (within "+symmetryTolerance.ToString("0.###",CultureInfo.InvariantCulture)+" kg). For asymmetric quartiles use a skewed distribution instead.");
            if(p25<stats.MinWeightKg || p75>stats.MaxWeightKg)
                return Fail("BiomeFishWeights.csv "+biome+" species "+id+" quartiles must stay inside FishStats min/max weight.");
            string key=WeightKey(biome,id);
            if(WeightRows.ContainsKey(key))return Fail("BiomeFishWeights.csv has duplicate row for "+biome+" species "+id+".");
            WeightRows[key]=new WeightDistribution{BiomeId=biome,SpeciesId=id,P25Kg=p25,P50Kg=p50,P75Kg=p75};
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

    // Peter J. Acklam's rational approximation of the inverse standard-normal CDF.
    private static double InverseNormal(double p)
    {
        double[] a={-3.969683028665376e+01,2.209460984245205e+02,-2.759285104469687e+02,1.383577518672690e+02,-3.066479806614716e+01,2.506628277459239e+00};
        double[] b={-5.447609879822406e+01,1.615858368580409e+02,-1.556989798598866e+02,6.680131188771972e+01,-1.328068155288572e+01};
        double[] c={-7.784894002430293e-03,-3.223964580411365e-01,-2.400758277161838e+00,-2.549732539343734e+00,4.374664141464968e+00,2.938163982698783e+00};
        double[] d={7.784695709041462e-03,3.224671290700398e-01,2.445134137142996e+00,3.754408661907416e+00};
        const double plow=.02425,phigh=1-.02425;
        if(p<plow)
        {
            double q=Math.Sqrt(-2*Math.Log(p));
            return (((((c[0]*q+c[1])*q+c[2])*q+c[3])*q+c[4])*q+c[5])/((((d[0]*q+d[1])*q+d[2])*q+d[3])*q+1);
        }
        if(p>phigh)
        {
            double q=Math.Sqrt(-2*Math.Log(1-p));
            return -(((((c[0]*q+c[1])*q+c[2])*q+c[3])*q+c[4])*q+c[5])/((((d[0]*q+d[1])*q+d[2])*q+d[3])*q+1);
        }
        double r=p-.5;double s=r*r;
        return (((((a[0]*s+a[1])*s+a[2])*s+a[3])*s+a[4])*s+a[5])*r/(((((b[0]*s+b[1])*s+b[2])*s+b[3])*s+b[4])*s+1);
    }
}
