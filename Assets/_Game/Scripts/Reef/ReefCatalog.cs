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
            new float[] {18,11,13,6,0,2,25,25})
    };
    public static Zone Starter => Zones[0];
    public static float Weight(int species) => species>=0 && species<Starter.weights.Length ? Starter.weights[species] : 0f;
    public static string Rarity(int id) => Weight(id)>=18 ? "Common" : Weight(id)>=10 ? "Uncommon" : Weight(id)>=4 ? "Rare" : "Very rare";
    public static int Roll(float random01, int bait = 0)
    {
        float total=0; for(int i=0;i<Starter.weights.Length;i++) total+=Weight(i)*BaitMultiplier(i,bait);
        float pick=Mathf.Clamp01(random01)*total;
        for(int i=0;i<Starter.weights.Length;i++) {pick-=Weight(i)*BaitMultiplier(i,bait);if(pick<0)return i;}
        return Starter.weights.Length-1;
    }
    private static float BaitMultiplier(int id,int bait) => bait==2 ? (id==1 || id==6 || id==7 ? 2.5f:1f) : bait==3 ? (id==3 || id==5 ? 4f:1f):1f;
}

