# Fishing tuning files

These three CSVs are the intended human-editable balance surface. Editing them changes runtime fishing; no C# edit is required.

## 1. `Assets/Resources/FishingTuning/BiomeFishWeights.csv`

One row per **biome + species**. It defines the fish-weight normal distribution using the 25th, 50th and 75th percentile weights in kilograms.

For a true normal distribution, the median is the mean and the quartiles are symmetric:

- `P50 = mean`
- `sigma = (P75 - P25) / (2 * 0.67448975)`
- therefore `P50` must be approximately `(P25 + P75) / 2`

The validator rejects asymmetric quartiles. Runtime normal samples are clamped to the species-wide min/max in `FishStats.csv`.

Biome IDs currently are `suncrest-reef`, `brinebreak-isle`, and `deep-ocean`.

## 2. `Assets/Resources/FishingTuning/FishStats.csv`

One row per fish species, independent of biome. Edit:

- `minWeightKg`
- `maxWeightKg`
- `minHealth`
- `maxHealth`
- `minCostCoins`
- `maxCostCoins`

Health and sale value use the same endpoint-defined parabola. For weight `w`:

```
t = clamp01((w - minWeightKg) / (maxWeightKg - minWeightKg))
value = minValue + (maxValue - minValue) * t^2
```

The minimum endpoint is the parabola vertex, so the curve starts gently and grows faster for larger fish. Biome does not multiply fish health anymore.

Fish visual length still comes from `FishSizeTable.cs`, which contains species-specific biological weight/length tables and keeps heavier fish longer within each species.

## 3. `Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv`

One row per **biome + bait/lure**. Each species column is a percentage chance after a bite. Every row must total **100% +/- 0.01**. The game does **not** silently normalize a bad row; validation reports an error and the data-driven rules stay disabled until corrected.

Current bait keys:

- `worms`
- `worms-legacy`
- `shrimp`
- `squid`
- `lure:0` = Neon Breach Crankbait
- `lure:1` = Reef Minnow
- `lure:2` = Fire Shad Crankbait
- `lure:3` = Deep Flash
- `lure:4` = Bloody Bait Crankbait

## Validation

Unity auto-validates these files after import. You can also run:

`Tools -> Open World -> Validate Fishing Tuning`

A valid setup logs `[FISH TUNING] Fishing tuning OK...`. A bad value logs a specific `[FISH TUNING]` error naming the file/row/problem.

## Push only your tuning edits to `main`

From PowerShell:

```powershell
cd "C:\Users\Ken\UnityProjects\3D-Android-Open-World"
git status
git add Assets/Resources/FishingTuning
git commit -m "Tune fish balance"
git pull --rebase origin main
git push origin main
```

Using the specific `git add Assets/Resources/FishingTuning` path avoids accidentally committing unrelated Unity/editor changes.
