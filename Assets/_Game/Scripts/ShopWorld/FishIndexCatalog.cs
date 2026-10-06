using System.Collections.Generic;

// Index-only grouping. Catch IDs and saved records remain stable when variants
// are grouped into a single species card. Add future variant families here.
public static class FishIndexCatalog
{
    private static readonly int[][] VariantFamilies = {
        new[] { FishCatalog.MakoSharkId, FishCatalog.BattleScarredMakoSharkId }
    };

    public sealed class BaitOption
    {
        public readonly string Key, Name, PreviewKey;
        public BaitOption(string key,string name,string previewKey)
        { Key=key; Name=name; PreviewKey=previewKey; }
    }

    public static List<BaitOption> Baits()
    {
        var result=new List<BaitOption>();
        foreach(int id in new[] { 0, 2, 3 })
            result.Add(new BaitOption(FishingTuning.BaitKey(id),ShopCatalog.BaitNames[id],"Bait"+id));
        for(int i=0;i<ShopCatalog.LureVariantCount;i++)
            result.Add(new BaitOption("lure:"+i,ShopCatalog.LureNames[i],ShopCatalog.LurePreviewKey(i)));
        return result;
    }

    public static int Family(int species)
    {
        species=FishCatalog.CanonicalId(species);
        foreach(var family in VariantFamilies)
            foreach(int variant in family) if(variant==species)return family[0];
        return species;
    }

    public static int[] Variants(int species)
    {
        int id=Family(species);
        foreach(var family in VariantFamilies)if(family[0]==id)return family;
        return new[] { id };
    }

    public static bool InRegion(int species,int biome)
    {
        if(FishingTuning.IsValid)
        {
            foreach(var bait in Baits())
                if(FishingTuning.TryGetChance(species,bait.Key,biome,out float odds) && odds>0f)return true;
            return false;
        }
        // Retain a usable index if editable tuning is invalid. Detail odds are
        // shown as unavailable instead of silently inventing percentages.
        foreach(int bait in new[] { 0, 2, 3, ShopCatalog.StarterLure })
            if(ReefCatalog.EquippedChance(species,bait,biome)>0f)return true;
        return false;
    }

    public static List<int> RegionSpecies(int biome)
    {
        var result=new List<int>();
        foreach(int id in FishCatalog.ActiveIds)
        {
            int family=Family(id);
            if(InRegion(id,biome) && !result.Contains(family))result.Add(family);
        }
        return result;
    }

    public static bool Discovered(ShopLedger data,int species)
    {
        return data!=null && data.totalCaught!=null && species>=0 &&
            species<data.totalCaught.Length && data.totalCaught[species]>0;
    }

    public static float Best(ShopLedger data,int species)
    {
        return Discovered(data,species) && data.personalBestKg!=null && species<data.personalBestKg.Length
            ? data.personalBestKg[species] : 0f;
    }

    public static int FoundVariants(ShopLedger data,int family)
    {
        int found=0;
        foreach(int id in Variants(family))if(Discovered(data,id))found++;
        return found;
    }

    public static int BestVariant(ShopLedger data,int family)
    {
        int best=family;
        foreach(int id in Variants(family))
            if(Best(data,id)>Best(data,best))best=id;
        return best;
    }
}
