using System;
using UnityEngine;

public class EconomySystem : MonoBehaviour
{
    private const string CoinsKey =
        "OpenWorld.Coins.v1";

    [SerializeField] private int startingCoins = 0;

    public int Coins { get; private set; }

    public event Action Changed;

    private void Awake()
    {
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

    private void Save()
    {
        PlayerPrefs.SetInt(
            CoinsKey,
            Coins
        );

        PlayerPrefs.Save();

        Changed?.Invoke();
    }
}
