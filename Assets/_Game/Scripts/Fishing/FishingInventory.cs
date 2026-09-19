using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CaughtFishRecord
{
    public int speciesId;
    public float weightKg;
    public long caughtUtcTicks;
}

[Serializable]
internal class FishingInventorySaveData
{
    public List<CaughtFishRecord> fish =
        new List<CaughtFishRecord>();
}

public class FishingInventory : MonoBehaviour
{
    private const string SaveKey =
        "OpenWorld.FishingInventory.v1";

    [SerializeField]
    private List<CaughtFishRecord> fish =
        new List<CaughtFishRecord>();

    public event Action Changed;

    public IReadOnlyList<CaughtFishRecord> Fish => fish;

    private void Awake()
    {
        Load();
    }

    public int AddFish(
        int speciesId,
        float weightKg)
    {
        CaughtFishRecord record =
            new CaughtFishRecord
            {
                speciesId = speciesId,
                weightKg = weightKg,
                caughtUtcTicks =
                    DateTime.UtcNow.Ticks
            };

        fish.Add(record);
        Save();

        Changed?.Invoke();

        return fish.Count - 1;
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        string json =
            PlayerPrefs.GetString(
                SaveKey,
                string.Empty
            );

        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            FishingInventorySaveData data =
                JsonUtility.FromJson<FishingInventorySaveData>(
                    json
                );

            if (data != null &&
                data.fish != null)
            {
                fish = data.fish;
            }
        }
        catch
        {
            fish =
                new List<CaughtFishRecord>();
        }
    }

    private void Save()
    {
        FishingInventorySaveData data =
            new FishingInventorySaveData
            {
                fish = fish
            };

        string json =
            JsonUtility.ToJson(data);

        PlayerPrefs.SetString(
            SaveKey,
            json
        );

        PlayerPrefs.Save();
    }
}
