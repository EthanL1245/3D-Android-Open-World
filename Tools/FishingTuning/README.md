# Fishing tuning — edit these 3 CSVs

## 1) Fish size distribution per biome
`Assets/Resources/FishingTuning/BiomeFishWeights.csv`

Columns:
`minKg, p01Kg, p25Kg, p50Kg, p75Kg, p99Kg, maxKg`

Meaning:
- `minKg` = smallest possible catch in that biome (0th percentile)
- `p01Kg` = 1% of catches are lighter than this
- `p25Kg` = 25% are lighter than this
- `p50Kg` = median; 50% lighter / 50% heavier
- `p75Kg` = 75% are lighter than this
- `p99Kg` = 99% are lighter than this; top 1% are heavier
- `maxKg` = largest possible catch in that biome (100th percentile)

Required order:
`min < P01 < P25 < P50 < P75 < P99 < max`

The sampler maps a random percentile through those anchors. There is **no clamping and no probability pile-up at min/max**. Move the upper anchors farther apart to create a longer/heavier right tail.

## 2) Species-wide health/value curve
`Assets/Resources/FishingTuning/FishStats.csv`

Edit:
`minWeightKg, maxWeightKg, minHealth, maxHealth, minCostCoins, maxCostCoins`

`minWeightKg/maxWeightKg` are species-wide physical/gameplay bounds. Biome hard ranges in `BiomeFishWeights.csv` must stay inside them.

Health and sale value use:
`t = (weight-minWeight)/(maxWeight-minWeight)`
`value = minValue + (maxValue-minValue) * t^2`

## 3) Species chance by biome + bait/lure
`Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv`

Each row is one biome + one bait/lure. Edit the fish percentages directly. **Every row must total 100%.** Bad totals are rejected, not auto-normalized.

Lure keys: `lure:0` Neon Breach, `lure:1` Reef Minnow, `lure:2` Fire Shad, `lure:3` Deep Flash, `lure:4` Bloody Bait.

## Validate in Unity
`Tools -> Open World -> Validate Fishing Tuning`

## Pull latest
```powershell
cd "C:\Users\Ken\UnityProjects\3D-Android-Open-World"
git pull
```

## Push only your tuning changes
```powershell
cd "C:\Users\Ken\UnityProjects\3D-Android-Open-World"
git add Assets/Resources/FishingTuning
git commit -m "Tune fish balance"
git pull --rebase origin main
git push origin main
```
