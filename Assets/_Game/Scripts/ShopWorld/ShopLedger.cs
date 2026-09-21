using System;
using System.Collections.Generic;

[Serializable] public sealed class HabitatOwnership
{
    public string id;
    public List<CaughtFishRecord> fish = new List<CaughtFishRecord>();
}

[Serializable] public sealed class ShopLedger
{
    public int version = 1, coins;
    public int rodOwned, reelOwned, lineOwned, rodEquipped, reelEquipped, lineEquipped, baitEquipped;
    public int[] bait = new int[4];
    public List<CaughtFishRecord> bag = new List<CaughtFishRecord>();
    public List<HabitatOwnership> habitats = new List<HabitatOwnership>();
    public bool legacyMigrated;
    public int Owned(GearKind kind) => kind == GearKind.Rod ? rodOwned : kind == GearKind.Reel ? reelOwned : lineOwned;
    public int Equipped(GearKind kind) => kind == GearKind.Rod ? rodEquipped : kind == GearKind.Reel ? reelEquipped : lineEquipped;
    public HabitatOwnership Habitat(string id) => habitats.Find(h => h.id == id);
    public bool Spend(int price) { if (price < 0 || coins < price) return false; coins -= price; return true; }
    public bool BuyGear(GearKind kind, int tier)
    {
        if (tier < 1 || tier > 3 || tier != Owned(kind)+1 || !Spend(ShopCatalog.GearPrice(kind,tier))) return false;
        if (kind == GearKind.Rod) rodOwned=tier;
        else if (kind == GearKind.Reel) reelOwned=tier; else lineOwned=tier;
        return Equip(kind,tier);
    }
    public bool Equip(GearKind kind, int tier)
    {
        if (tier < 0 || tier > Owned(kind)) return false;
        if (kind == GearKind.Rod) rodEquipped=tier;
        else if (kind == GearKind.Reel) reelEquipped=tier; else lineEquipped=tier;
        return true;
    }
    public bool BuyBait(int id)
    {
        if (id < 1 || id > 3 || bait[id] > 9990 || !Spend(ShopCatalog.BaitPrices[id])) return false;
        bait[id]+=10; baitEquipped=id; return true;
    }
    public int ConsumeBait()
    {
        int id=baitEquipped;
        if (id < 1 || id > 3 || bait[id] <= 0) { baitEquipped=0; return 0; }
        bait[id]--; if (bait[id]==0) baitEquipped=0; return id;
    }
    public bool BuyHabitat(string id)
    {
        var definition=ShopCatalog.Habitat(id);
        if (definition==null || Habitat(id)!=null || !Spend(definition.price)) return false;
        habitats.Add(new HabitatOwnership { id=id }); return true;
    }
    public string Admission(string id, CaughtFishRecord fish)
    {
        var owned=Habitat(id); var d=ShopCatalog.Habitat(id);
        if (owned==null || d==null) return "Purchase this habitat first.";
        if (fish==null || float.IsNaN(fish.weightKg) || float.IsInfinity(fish.weightKg) || fish.weightKg<=0) return "Invalid fish weight.";
        if (owned.fish.Count>=d.fishLimit) return "Fish count limit reached.";
        if (fish.weightKg>d.maxFishKg) return "This fish exceeds the individual weight limit.";
        float weight=fish.weightKg; foreach(var f in owned.fish) weight+=f.weightKg;
        if (weight>d.totalKg+0.0001f) return "This fish would exceed the total weight limit.";
        if (ShopCatalog.FishLength(fish.speciesId,fish.weightKg)>Math.Min(d.width,d.depth)*0.48f) return "This fish needs more turning space.";
        return null;
    }
    public bool Deposit(string id, CaughtFishRecord fish)
    {
        if (!bag.Contains(fish) || Admission(id,fish)!=null) return false;
        bag.Remove(fish); Habitat(id).fish.Add(fish); return true;
    }
    public bool Withdraw(string id, CaughtFishRecord fish)
    {
        var h=Habitat(id); if(h==null || !h.fish.Remove(fish)) return false;
        bag.Add(fish); return true;
    }
    public bool Sell(CaughtFishRecord fish, int value)
    {
        if (value<1 || coins>int.MaxValue-value || !bag.Remove(fish)) return false;
        coins+=value; return true;
    }
}
