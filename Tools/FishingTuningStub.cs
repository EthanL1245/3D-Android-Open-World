// Headless rule-check projects do not load Unity Resources/TextAssets. Returning false
// keeps their legacy-fallback assertions meaningful while production Unity builds use
// Assets/_Game/Scripts/Fishing/FishingTuning.cs.
public static class FishingTuning
{
    public sealed class SpeciesStats
    {
        public int SpeciesId;
        public string SpeciesName;
        public float MinWeightKg,MaxWeightKg;
        public int MinHealth,MaxHealth,MinCostCoins,MaxCostCoins;
    }

    public static bool IsValid=>false;
    public static int LastBiome {get;private set;}
    public static void RememberBiome(int biome){LastBiome=biome;}
    public static bool TryGetSpeciesStats(int species,out SpeciesStats stats){stats=null;return false;}
    public static bool TryGetChance(int species,int bait,int biome,out float percent){percent=0;return false;}
    public static bool TryGetWeightRange(int species,int biome,out float minimum,out float maximum){minimum=maximum=0;return false;}
    public static bool TryRollWeight(int species,int biome,float random01,out float kg){kg=0;return false;}
    public static bool TryRollSpecies(float random01,int bait,int biome,out int species){species=0;return false;}
    public static bool TryGetHealth(int species,float weightKg,out int health){health=0;return false;}
    public static bool TryGetCost(int species,float weightKg,out int coins){coins=0;return false;}
}
