using UnityEngine;

public struct FishSpeciesDefinition
{
    public string Name;
    public Color BodyColor;
    public Color AccentColor;
    public float MinWeightKg;
    public float MaxWeightKg;
    public float Difficulty;
    public float RelativeChance;
    public int BaseSellCoins;

    public FishSpeciesDefinition(string name,Color bodyColor,Color accentColor,float minWeightKg,float maxWeightKg,float difficulty,float relativeChance,int baseSellCoins)
    {
        Name=name;BodyColor=bodyColor;AccentColor=accentColor;MinWeightKg=minWeightKg;MaxWeightKg=maxWeightKg;
        Difficulty=difficulty;RelativeChance=relativeChance;BaseSellCoins=baseSellCoins;
    }
}

public static class FishCatalog
{
    public const int RedSnapperId=1;
    public const int YellowfinTunaId=5;
    public const int YellowGoatfishId=6;
    public const int BlackSpotGoatfishId=7;
    public const int BigeyeTunaId=8;
    public const int BonitoId=9;
    public const int BlackSeaBassId=10;
    public const int StripedBassId=11;
    public const int SpottedSandBassId=12;

    private static readonly FishSpeciesDefinition[] Species=
    {
        new FishSpeciesDefinition("Blue Mackerel",new Color(.20f,.42f,.58f),new Color(.72f,.86f,.90f),.25f,1.10f,.25f,42f,16),
        new FishSpeciesDefinition("Red Snapper",new Color(.72f,.20f,.16f),new Color(.96f,.54f,.34f),.70f,3.20f,.42f,26f,34),
        new FishSpeciesDefinition("Sea Bass",new Color(.25f,.38f,.31f),new Color(.65f,.72f,.58f),.90f,4.80f,.52f,18f,52),
        new FishSpeciesDefinition("Yellowtail",new Color(.18f,.38f,.56f),new Color(.94f,.76f,.18f),1.40f,6.50f,.68f,10f,92),
        default, // Retired species ID 4; never reuse saved IDs.
        new FishSpeciesDefinition("Yellowfin Tuna",new Color(.07f,.18f,.30f),new Color(.96f,.72f,.08f),4f,28f,.90f,2f,280),
        new FishSpeciesDefinition("Yellow Goatfish",new Color(.78f,.76f,.62f),new Color(.96f,.78f,.08f),.35f,2.20f,.46f,13f,48),
        new FishSpeciesDefinition("Black Spot Goatfish",new Color(.72f,.73f,.70f),new Color(.22f,.18f,.15f),.30f,1.80f,.43f,14f,44),
        new FishSpeciesDefinition("Bigeye Tuna",new Color(.10f,.22f,.32f),new Color(.75f,.80f,.80f),3f,40f,.86f,4f,220),
        new FishSpeciesDefinition("Bonito",new Color(.08f,.23f,.38f),new Color(.72f,.82f,.86f),.50f,5f,.58f,6f,78),
        new FishSpeciesDefinition("Black Sea Bass",new Color(.10f,.14f,.18f),new Color(.50f,.57f,.62f),.35f,2f,.68f,6f,74),
        // Difficulty + baseline rarity match Sea Bass so equal-weight fish have the
        // exact same FishingRules.MaxHealth. The larger weight ceiling creates the
        // tougher trophy fish rather than an invisible species-specific bonus.
        new FishSpeciesDefinition("Striped Bass",new Color(.34f,.40f,.39f),new Color(.76f,.78f,.69f),.90f,25f,.52f,13f,128),
        // Difficulty + baseline rarity match Black Sea Bass for exact same-weight fights.
        new FishSpeciesDefinition("Spotted Sand Bass",new Color(.42f,.37f,.28f),new Color(.76f,.66f,.43f),.25f,2f,.68f,6f,72)
    };

    public static int Count=>Species.Length;
    public static readonly int[] ActiveIds={0,1,2,3,5,6,7,8,9,10,11,12};
    public static int CanonicalId(int id)=>id==4?YellowfinTunaId:id;

    public static FishSpeciesDefinition Get(int id)
    {
        int index=Mathf.Clamp(CanonicalId(id),0,Species.Length-1);
        var definition=Species[index];definition.RelativeChance=ReefCatalog.Weight(index);return definition;
    }

    public static string FormatWeight(float kg)=>kg<.1f?(kg*1000f).ToString("0.0")+" g":kg.ToString("0.00")+" kg";
    public static int RollSpecies()=>ReefCatalog.Roll(Random.value);

    public static float RollWeight(int speciesId)
    {
        FishSpeciesDefinition definition=Get(speciesId);float t=Random.value;t*=t;
        return Mathf.Lerp(definition.MinWeightKg,definition.MaxWeightKg,t);
    }

    public static int GetSellValue(int speciesId,float weightKg)
    {
        FishSpeciesDefinition species=Get(speciesId);
        float length=FishSizeTable.LengthMetres(speciesId,weightKg);
        if(length<=.15f)
        {
            float tiny=Mathf.InverseLerp(.05f,.15f,length);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f,12f,tiny)),1,12);
        }
        float weightPercent=Mathf.InverseLerp(species.MinWeightKg,species.MaxWeightKg,weightKg);
        float weightMultiplier=Mathf.Lerp(.70f,1.90f,weightPercent);
        return Mathf.Max(1,Mathf.RoundToInt(species.BaseSellCoins*weightMultiplier));
    }

    public static float GetVisualScale(int speciesId,float weightKg)=>FishSizeTable.LengthMetres(speciesId,weightKg);
}
