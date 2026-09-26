using UnityEngine;

// Stable zone/species IDs are save-safe; add future zones here without renumbering.
public static class SuncrestBaseline
{
    public const string StarterId = "suncrest-reef";
    public const string StarterName = "Suncrest Reef";
    public sealed class Zone
    {
        public readonly string id, name, description;
        public readonly bool unlockedByDefault;
        public readonly float[] weights;
        public Zone(string id, string name, string description, bool unlocked, float[] weights)
        { this.id=id; this.name=name; this.description=description; unlockedByDefault=unlocked; this.weights=weights; }
        public bool Unlocked => unlockedByDefault || PlayerPrefs.GetInt("ReefUnlock."+id,0)==1;
    }

    // Raw weights are normalized at use time. This lets new fish be appended without
    // hand-rebalancing every table back to an exact total of 100.
    public static readonly Zone[] Zones = {
        new Zone(StarterId, StarterName, "A palm-lined beginner island, sandy coves and a broad shallow reef.", true,
            new float[] {20,11,13,6,0,2,22,22,4,6,6})
    };
    public static Zone Starter => Zones[0];
    public static float Weight(int species) => species>=0 && species<Starter.weights.Length ? Starter.weights[species] : 0f;
    public static string Rarity(int id) => Weight(id)>=18 ? "Common" : Weight(id)>=10 ? "Uncommon" : Weight(id)>=4 ? "Rare" : "Very rare";

    private static readonly float[] ShrimpOdds={11,15,8,3,0,1,30,30,2,4,8};
    private static readonly float[] SquidOdds={14,8,10,20,0,6,15,15,12,8,4};

    // Indexes: Mackerel, Snapper, Sea Bass, Yellowtail, retired, Yellowfin,
    // Yellow Goatfish, Black Spot Goatfish, Bigeye Tuna, Bonito, Black Sea Bass.
    // Bloody Bait intentionally keeps Neon Breach's balanced behavior.
    private static readonly float[][] LureOdds=
    {
        new float[] {4,21,36,19,0,6,4,4,6,6,7},   // Neon Breach Crankbait
        new float[] {4,20,42,16,0,6,3,3,6,4,10},  // Reef Minnow
        new float[] {4,45,22,9,0,6,4,4,6,5,5},    // Fire Shad Crankbait
        new float[] {3,24,34,16,0,9,3,3,8,10,4},  // Deep Flash
        new float[] {4,21,36,19,0,6,4,4,6,6,7}    // Bloody Bait Crankbait
    };

    private static float[] EquippedWeights(int bait)
    {
        if(bait==ShopCatalog.StarterLure)
        {
            int lure=Mathf.Clamp(ShopCatalog.ActiveLureVariant,0,LureOdds.Length-1);
            return LureOdds[lure];
        }
        return bait==2?ShrimpOdds:bait==3?SquidOdds:Starter.weights;
    }

    private static float Total(float[] weights)
    {
        float total=0f;
        if(weights!=null)for(int i=0;i<weights.Length;i++)total+=Mathf.Max(0f,weights[i]);
        return total;
    }

    public static float EquippedChance(int species,int bait)
    {
        float[] weights=EquippedWeights(bait);
        if(species<0 || species>=weights.Length)return 0f;
        float total=Total(weights);
        return total>0f?Mathf.Max(0f,weights[species])/total*100f:0f;
    }

    public static int Roll(float random01, int bait = 0)
    {
        float[] weights=EquippedWeights(bait);
        float total=Total(weights);
        if(total<=0f)return FishCatalog.ActiveIds[0];
        float pick=Mathf.Clamp01(random01)*total;
        int fallback=FishCatalog.ActiveIds[0];
        for(int i=0;i<weights.Length;i++)
        {
            float weight=Mathf.Max(0f,weights[i]);
            if(weight<=0f)continue;
            fallback=i;
            pick-=weight;
            if(pick<0f)return i;
        }
        return fallback;
    }
}

