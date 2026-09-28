# Fishing tuning — edit these 3 CSVs

## 1) Fish weight range per biome
`Assets/Resources/FishingTuning/BiomeFishWeights.csv`

Edit only:
`minKg, maxKg`

**Set both `minKg` and `maxKg` to `0` to disable that species in this biome.**
Its column in `BiomeBaitSpeciesChance.csv` must then be `0` for **every** bait/lure row
in that biome, including `worms-legacy` and all five lures. Validation reports the exact
biome, species and bait/lure if they disagree. Redistribute removed odds yourself so each
row still totals 100%; the validator never rewrites your numbers.

A single zero, negative bounds, or reversed/equal positive bounds is still invalid.
Positive ranges may have 0% chance for some or even all baits; they are simply not selected.
The `0,0` sentinel applies only to biome ranges, not the global FishStats bounds.
Disabled species remain absent from the fish index and cannot be rolled in that biome.

For positive ranges, runtime automatically builds a bounded normal (bell-curve) distribution between those values.

Internally it defines the underlying normal curve as:
- `P01 = minKg + 1% of (maxKg-minKg)`
- `P99 = maxKg - 1% of (maxKg-minKg)`

The curve is truncated and re-normalized at `minKg/maxKg`, so catches never go outside the range and there is no clamped probability pile-up at either endpoint.

## 2) Species-wide health/value curve
`Assets/Resources/FishingTuning/FishStats.csv`

Edit:
`minWeightKg, maxWeightKg, minHealth, maxHealth, minCostCoins, maxCostCoins, yellowSpeedMps`

`minWeightKg/maxWeightKg` are species-wide physical/gameplay bounds. Biome ranges in `BiomeFishWeights.csv` must stay inside them.

Health and sale value use:
`t = (weight-minWeight)/(maxWeight-minWeight)`
`value = minValue + (maxValue-minValue) * t^2`

`yellowSpeedMps` is the irritated (yellow) free-swimming baseline in metres/second.
Green uses `0.95 / 1.35 = 0.7037037×` yellow; red uses `1.75 / 1.35 = 1.2962963×` yellow.
The existing initial escape burst (up to 1.34×) and surge (up to 1.28×) still multiply movement.
Reeling, obstacle routing and pond containment can change the actual movement observed.
Previously yellow was `(1.85 + 1.10 × effectiveDifficulty) × 1.35`, or 2.4975–3.9825 m/s.
Now the CSV value replaces that difficulty-derived baseline; there is no second hidden size multiplier.
Existing species defaults approximate their former middle-size yellow speed.

Shoreline depth quality is applied **after** the species/biome weight roll:
- Caught weight = rolled weight × quality (quality ranges from 0.20 to 1.00).
- Fight health = original rolled-weight health × `(0.5 + 0.5 × quality)`.
- Fight difficulty uses the original rolled weight, then the same gentler multiplier.
- Thus 80% less weight means 40% less health/difficulty, not 80% less.
- Sale value and displayed length still follow the actual reduced caught weight. Palm Pond is exempt.

## 3) Species chance by biome + bait/lure
`Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv`

Every fish probability is a WHOLE-NUMBER percent. Every biome+bait/lure row must total exactly `100`.
The fish index hides 0% entries for the selected biome and equipped bait/lure; those entries are also excluded from rolls.

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

