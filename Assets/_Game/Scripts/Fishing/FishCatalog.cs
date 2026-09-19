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

    public FishSpeciesDefinition(
        string name,
        Color bodyColor,
        Color accentColor,
        float minWeightKg,
        float maxWeightKg,
        float difficulty,
        float relativeChance)
    {
        Name = name;
        BodyColor = bodyColor;
        AccentColor = accentColor;
        MinWeightKg = minWeightKg;
        MaxWeightKg = maxWeightKg;
        Difficulty = difficulty;
        RelativeChance = relativeChance;
    }
}

public static class FishCatalog
{
    private static readonly FishSpeciesDefinition[] Species =
    {
        new FishSpeciesDefinition(
            "Blue Mackerel",
            new Color(0.20f, 0.42f, 0.58f),
            new Color(0.72f, 0.86f, 0.90f),
            0.25f,
            1.10f,
            0.25f,
            42f
        ),
        new FishSpeciesDefinition(
            "Red Snapper",
            new Color(0.72f, 0.20f, 0.16f),
            new Color(0.96f, 0.54f, 0.34f),
            0.70f,
            3.20f,
            0.42f,
            26f
        ),
        new FishSpeciesDefinition(
            "Sea Bass",
            new Color(0.25f, 0.38f, 0.31f),
            new Color(0.65f, 0.72f, 0.58f),
            0.90f,
            4.80f,
            0.52f,
            18f
        ),
        new FishSpeciesDefinition(
            "Yellowtail",
            new Color(0.18f, 0.38f, 0.56f),
            new Color(0.94f, 0.76f, 0.18f),
            1.40f,
            6.50f,
            0.68f,
            10f
        ),
        new FishSpeciesDefinition(
            "Young Tuna",
            new Color(0.12f, 0.28f, 0.48f),
            new Color(0.78f, 0.82f, 0.78f),
            2.50f,
            10.00f,
            0.82f,
            4f
        )
    };

    public static int Count => Species.Length;

    public static FishSpeciesDefinition Get(int id)
    {
        return Species[
            Mathf.Clamp(id, 0, Species.Length - 1)
        ];
    }

    public static int RollSpecies()
    {
        float total = 0f;

        for (int i = 0; i < Species.Length; i++)
            total += Species[i].RelativeChance;

        float roll = Random.value * total;
        float cumulative = 0f;

        for (int i = 0; i < Species.Length; i++)
        {
            cumulative += Species[i].RelativeChance;

            if (roll <= cumulative)
                return i;
        }

        return 0;
    }

    public static float RollWeight(int speciesId)
    {
        FishSpeciesDefinition definition = Get(speciesId);

        float t = Random.value;

        // Bias toward ordinary-sized catches while still allowing trophies.
        t = t * t;

        return Mathf.Lerp(
            definition.MinWeightKg,
            definition.MaxWeightKg,
            t
        );
    }
}
