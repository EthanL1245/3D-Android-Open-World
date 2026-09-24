using UnityEngine;

// Stable zone/species IDs are save-safe; add future zones here without renumbering.
public static class ReefCatalog
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
    public static readonly Zone[] Zones = {
        new Zone(StarterId, StarterName, "A palm-lined beginner island, sandy coves and a broad shallow reef.", true,
            new float[] {20,11,13,6,0,2,22,22,4})
    };
    public static Zone Starter => Zones[0];
    public static float Weight(int species) => species>=0 && species<Starter.weights.Length ? Starter.weights[species] : 0f;
    public static string Rarity(int id) => Weight(id)>=18 ? "Common" : Weight(id)>=10 ? "Uncommon" : Weight(id)>=4 ? "Rare" : "Very rare";
    // Whole-percent tables are shared by the roll and equipped index.
    private static readonly float[] ShrimpOdds={11,15,8,3,0,1,30,30,2};
    private static readonly float[] SquidOdds={14,8,10,20,0,6,15,15,12};
    private static readonly float[] LureOdds={4,5,5,24,0,18,4,4,36};
    public static float EquippedChance(int species,int bait)
    {
        var weights=bait==4?LureOdds:bait==2?ShrimpOdds:bait==3?SquidOdds:Starter.weights;
        return species>=0 && species<weights.Length?weights[species]:0;
    }
    public static int Roll(float random01, int bait = 0)
    {
        float pick=Mathf.Clamp01(random01)*100f;
        for(int i=0;i<Starter.weights.Length;i++){pick-=EquippedChance(i,bait);if(pick<0)return i;}
        return Starter.weights.Length-1;
    }
}
