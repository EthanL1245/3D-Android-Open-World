using System;

public enum GearKind { Rod, Reel, Line }

[Serializable]
public sealed class HabitatDefinition
{
    public string id, name;
    public int price, fishLimit;
    public float maxFishKg, totalKg, width, depth, height;
    public bool pond, swimmable;
    public HabitatDefinition(string id, string name, int price, int count, float maxKg, float totalKg,
        float width, float height, float depth, bool pond = false, bool swim = false)
    {
        this.id=id; this.name=name; this.price=price; fishLimit=count; maxFishKg=maxKg; this.totalKg=totalKg;
        this.width=width; this.height=height; this.depth=depth; this.pond=pond; swimmable=swim;
    }
}

public static class ShopCatalog
{
    public static readonly HabitatDefinition[] Habitats = {
        new HabitatDefinition("nano", "Tidepool Cabinet", 250, 3, 0.8f, 1.8f, 2.2f, 1.2f, 1.4f),
        new HabitatDefinition("reef", "Reef Gallery", 700, 6, 4f, 14f, 4.8f, 2.2f, 2.8f),
        new HabitatDefinition("lagoon", "Lagoon Suite", 2000, 10, 12f, 55f, 8f, 3.6f, 5f, false, true),
        new HabitatDefinition("grand", "Grand Ocean Gallery", 6500, 20, 35f, 220f, 14f, 5f, 9f, false, true),
        new HabitatDefinition("ocean", "Oceanarium", 18000, 40, 120f, 1000f, 26f, 7f, 16f, false, true),
        new HabitatDefinition("pond", "Courtyard Pond", 1400, 12, 8f, 45f, 10f, 2.8f, 8f, true, true),
        new HabitatDefinition("garden", "Garden Lagoon", 5000, 25, 35f, 240f, 18f, 4f, 14f, true, true),
        new HabitatDefinition("lake", "Sanctuary Lake", 15000, 50, 120f, 1200f, 32f, 6f, 24f, true, true)
    };
    public static readonly string[] RodNames = { "Woodland Rod", "Coastal Carbon", "Offshore Carbon", "Bluewater Elite" };
    public static readonly string[] ReelNames = { "Starter Reel", "Smooth Drag", "Precision Drag", "Deepwater Pro" };
    public static readonly string[] LineNames = { "Standard Line", "+15 m Extension", "+35 m Extension", "+65 m Extension" };
    public static readonly string[] BaitNames = { "Reusable Lure", "Worms", "Shrimp", "Squid" };
    public static readonly int[] BaitPrices = { 0, 35, 90, 180 };
    public static readonly int[] RodPrices = { 0, 180, 650, 1800 };
    public static readonly int[] ReelPrices = { 0, 160, 550, 1500 };
    public static readonly int[] LinePrices = { 0, 100, 400, 1100 };
    public static readonly float[] LineBonus = { 0, 15, 35, 65 };
    public static HabitatDefinition Habitat(string id) => Array.Find(Habitats, h => h.id == id);
    public static string GearName(GearKind kind, int tier) => (kind == GearKind.Rod ? RodNames : kind == GearKind.Reel ? ReelNames : LineNames)[tier];
    public static int GearPrice(GearKind kind, int tier) => (kind == GearKind.Rod ? RodPrices : kind == GearKind.Reel ? ReelPrices : LinePrices)[tier];
    public static float FishLength(int species,float kg) => FishSizeTable.LengthMetres(species,kg);
}
