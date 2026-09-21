using System;
using UnityEngine;

public class EconomySystem : MonoBehaviour
{
    private const string CoinsKey =
        "OpenWorld.Coins.v1";

    [SerializeField] private int startingCoins = 0;

    private int legacyCoins;
    private ShopProgress shop;
    public int Coins { get => shop!=null ? shop.Data.coins : legacyCoins; private set { if(shop!=null) shop.Data.coins=value; else legacyCoins=value; } }

    public event Action Changed;

    private void Awake()
    {
        shop=GetComponent<ShopProgress>();
        if(shop!=null) { shop.Changed+=OnShopChanged; return; }
        Coins =
            PlayerPrefs.GetInt(
                CoinsKey,
                startingCoins
            );
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        Coins += amount;
        Save();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
            return true;

        if (Coins < amount)
            return false;

        Coins -= amount;
        Save();

        return true;
    }

    private void OnShopChanged() => Changed?.Invoke();
    private void OnDestroy() { if(shop!=null) shop.Changed-=OnShopChanged; }
    private void Save()
    {
        if(shop!=null) { shop.Save(); return; }
        PlayerPrefs.SetInt(
            CoinsKey,
            Coins
        );

        PlayerPrefs.Save();

        Changed?.Invoke();
    }
}
