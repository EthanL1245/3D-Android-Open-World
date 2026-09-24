using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class ShopProgress : MonoBehaviour
{
    public const string SaveKey = "OpenWorld.ShopWorld.v1";
    public ShopLedger Data { get; private set; }
    public event Action Changed;
    public string Notice { get; private set; }
    public bool ReadOnly { get; private set; }
    private void Awake()
    {
        try
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                try { Data=Read(PlayerPrefs.GetString(SaveKey)); }
                catch { Data=Read(PlayerPrefs.GetString(SaveKey+".backup")); Notice="Recovered the previous shop save."; }
            }
            else Migrate();
            bool migrated=Data.EnsureCatchStats();
            if(!Data.infiniteWormsMigrated){Data.baitEquipped=0;Data.infiniteWormsMigrated=true;migrated=true;}
            if(Data.baitEquipped==1){Data.baitEquipped=0;migrated=true;}
            foreach(var fish in Data.bag)if(fish.speciesId==4){fish.speciesId=FishCatalog.YellowfinTunaId;migrated=true;}
            foreach(var habitat in Data.habitats)foreach(var fish in habitat.fish)
                if(fish.speciesId==4){fish.speciesId=FishCatalog.YellowfinTunaId;migrated=true;}
            if(migrated)Save();
            if(!Data.homeMigrated) { Data.MigrateHome(); Save(); Notice="Your habitats now live at Home. Earlier smaller purchases were credited; all fish were preserved."; }
        }
        catch(Exception ex)
        {
            ReadOnly=true; Data=new ShopLedger(); Data.EnsureCatchStats(); Notice="Save could not be loaded. Purchases and transfers are disabled; your saved data has not been overwritten.";
            Debug.LogError(Notice+" "+ex.Message);
        }
    }
    private static ShopLedger Read(string json)
    {
        if(string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Empty save");
        var d=JsonUtility.FromJson<ShopLedger>(json);
        if(d==null || d.version!=1 || d.bag==null || d.habitats==null || d.bait==null || d.bait.Length!=4) throw new InvalidOperationException("Invalid save");
        foreach(GearKind kind in Enum.GetValues(typeof(GearKind)))
            if(d.Owned(kind)<0 || d.Owned(kind)>3 || d.Equipped(kind)<0 || d.Equipped(kind)>d.Owned(kind))
                throw new InvalidOperationException("Invalid saved equipment");
        if(d.coins<0 || d.baitEquipped<0 || d.baitEquipped>ShopCatalog.StarterLure) throw new InvalidOperationException("Invalid saved balance or bait");
        foreach(int amount in d.bait) if(amount<0) throw new InvalidOperationException("Invalid bait quantity");
        foreach(var habitat in d.habitats) if(habitat==null || ShopCatalog.Habitat(habitat.id)==null || habitat.fish==null)
            throw new InvalidOperationException("Invalid saved habitat");
        return d;
    }
    private void Migrate()
    {
        Data=new ShopLedger { coins=PlayerPrefs.GetInt("OpenWorld.Coins.v1",0), legacyMigrated=true };
        string fishJson=PlayerPrefs.GetString("OpenWorld.FishingInventory.v1","");
        if(!string.IsNullOrWhiteSpace(fishJson))
        {
            var old=JsonUtility.FromJson<FishingInventorySaveData>(fishJson);
            if(old?.fish!=null) Data.bag.AddRange(old.fish);
        }
        string tanks=PlayerPrefs.GetString("OpenWorld.Aquariums.v1","");
        int refunded=0, rescued=0;
        if(!string.IsNullOrWhiteSpace(tanks))
        {
            var old=JsonUtility.FromJson<AquariumSaveData>(tanks);
            if(old!=null)
            {
                refunded=Math.Max(0,old.unplacedTanks);
                if(old.tanks!=null) foreach(var tank in old.tanks)
                {
                    refunded++;
                    if(tank.fish!=null) { Data.bag.AddRange(tank.fish); rescued+=tank.fish.Count; }
                }
                Data.coins+=refunded*AquariumSystem.TankPrice;
            }
        }
        Notice=refunded>0 ? $"Moved {rescued} fish to your bag and refunded {refunded*AquariumSystem.TankPrice} coins for old aquariums." : "Welcome to Tideglass Quay. Sell catches to fund your first upgrades.";
        Save();
    }
    public void Save()
    {
        if(ReadOnly) return;
        string json=JsonUtility.ToJson(Data);
        if(PlayerPrefs.HasKey(SaveKey)) PlayerPrefs.SetString(SaveKey+".backup",PlayerPrefs.GetString(SaveKey));
        PlayerPrefs.SetString(SaveKey,json); PlayerPrefs.Save(); Changed?.Invoke();
    }
    public bool Commit(bool changed) { if(changed) Save(); return changed; }
    public bool CanTrade => !ReadOnly && ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.InShop;
    public bool BuyGear(GearKind kind,int tier) => CanTrade && ShopDimensionManager.Instance.Near("gear") && Commit(Data.BuyGear(kind,tier));
    public bool BuyBait(int id) => CanTrade && ShopDimensionManager.Instance.Near("gear") && Commit(Data.BuyBait(id));
    public bool BuyHabitat(string id) => CanTrade && ShopDimensionManager.Instance.Near(id) && Commit(Data.BuyHabitat(id));
    public bool Sell(CaughtFishRecord fish) => CanTrade && ShopDimensionManager.Instance.Near("market") && Commit(Data.Sell(fish,FishCatalog.GetSellValue(fish.speciesId,fish.weightKg)));
    public bool CanManage => !ReadOnly && ShopDimensionManager.Instance!=null && ShopDimensionManager.Instance.InHome;
    public bool Deposit(string id,CaughtFishRecord fish) => CanManage && ShopDimensionManager.Instance.Near(id) && Commit(Data.Deposit(id,fish));
    public bool Withdraw(string id,CaughtFishRecord fish) => CanManage && ShopDimensionManager.Instance.Near(id) && Commit(Data.Withdraw(id,fish));
    public int TakeBait() { if(ReadOnly) return 0; int id=Data.ConsumeBait(); if(!ShopCatalog.PermanentBait(id)) Save(); return id; }
}
