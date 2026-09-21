# Fish length data

Game length means straight, neutral-pose **total length (snout to tail tip)**, with 1 Unity world unit = 1 metre. FishBase Bayesian length–weight estimates supply W in grams from total length L in centimetres: W = a L^b. These are population/model estimates, not exact individual weights. We sampled them every 5 cm into checked-in tables, then linearly interpolate length by weight at runtime. Below the first interval the line is extrapolated with a 5 cm floor; above the table it is capped, never extrapolated to giant impossible fish. Existing save weights are not rewritten.

Generic names are resolved as game-design assumptions: Sea Bass = European sea bass, Yellowtail = yellowtail amberjack/kingfish, Young Tuna = juvenile yellowfin, Black Spot Goatfish = blackspot goatfish. Limits below are conservative game bounds, not claims of record lengths. The mackerel source lists different length conventions for its maximum; its 55 cm total-length game bound is deliberately separate from the reported standard length. The new goatfish catch ceilings are 2.2 kg and 1.8 kg respectively.

| Game species | Scientific name | a | b | Game length cap | Source |
|---|---|---:|---:|---:|---|
| 0 | Scomber australasicus | 0.00891 | 3.06 | 0.55 m | [FishBase](https://www.fishbase.se/summary/Scomber-australasicus.html) |
| 1 | Lutjanus campechanus | 0.01479 | 2.96 | 1.00 m | [FishBase](https://www.fishbase.se/summary/Lutjanus-campechanus.html) |
| 2 | Dicentrarchus labrax | 0.00933 | 3.02 | 1.00 m | [FishBase](https://www.fishbase.se/summary/Dicentrarchus-labrax.html) |
| 3 | Seriola lalandi | 0.01862 | 2.93 | 2.00 m | [FishBase](https://www.fishbase.se/summary/Seriola-lalandi.html) |
| 4 | Thunnus albacares | 0.01413 | 3.03 | 2.00 m | [FishBase](https://www.fishbase.se/summary/Thunnus-albacares.html) |
| 5 | Thunnus albacares | 0.01413 | 3.03 | 2.00 m | [FishBase](https://www.fishbase.se/summary/Thunnus-albacares.html) |
| 6 | Parupeneus cyclostomus | 0.01259 | 3.09 | 0.50 m | [FishBase](https://www.fishbase.se/summary/Parupeneus-cyclostomus.html) |
| 7 | Parupeneus spilurus | 0.01259 | 3.04 | 0.50 m | [FishBase](https://www.fishbase.se/summary/Parupeneus-spilurus.html) |

## Example lookup results (metres)

| Species | 0.5 kg | 1 kg | 5 kg |
|---|---:|---:|---:|
| Scomber australasicus | 0.36 | 0.45 | 0.55 |
| Lutjanus campechanus | 0.34 | 0.43 | 0.74 |
| Dicentrarchus labrax | 0.37 | 0.46 | 0.79 |
| Seriola lalandi | 0.32 | 0.41 | 0.71 |
| Thunnus albacares | 0.32 | 0.40 | 0.68 |
| Thunnus albacares | 0.32 | 0.40 | 0.68 |
| Parupeneus cyclostomus | 0.31 | 0.39 | 0.50 |
| Parupeneus spilurus | 0.33 | 0.41 | 0.50 |

Examples are curve estimates rounded to 2 decimals; runtime table interpolation may differ slightly. Values beyond a species range clamp to its game cap. Rod/reel first-person presentation is intentionally enlarged; fish world dimensions are not presentation-scaled.
