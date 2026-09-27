# Fishing tuning — edit these 3 CSVs

## 1) Fish weight range per biome
`Assets/Resources/FishingTuning/BiomeFishWeights.csv`

Edit only:
`minKg, maxKg`

Runtime automatically builds a bounded normal (bell-curve) distribution between those values.

Internally it defines the underlying normal curve as:
- `P01 = minKg + 1% of (maxKg-minKg)`
- `P99 = maxKg - 1% of (maxKg-minKg)`

The curve is truncated and re-normalized at `minKg/maxKg`, so catches never go outside the range and there is no clamped probability pile-up at either endpoint.

## 2) Species-wide health/value curve
`Assets/Resources/FishingTuning/FishStats.csv`

Edit:
`minWeightKg, maxWeightKg, minHealth, maxHealth, minCostCoins, maxCostCoins`

`minWeightKg/maxWeightKg` are species-wide physical/gameplay bounds. Biome ranges in `BiomeFishWeights.csv` must stay inside them.

Health and sale value use:
`t = (weight-minWeight)/(maxWeight-minWeight)`
`value = minValue + (maxValue-minValue) * t^2`

## 3) Species chance by biome + bait/lure
`Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv`

Every fish probability is a WHOLE-NUMBER percent. Every biome+bait/lure row must total exactly `100`.

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
