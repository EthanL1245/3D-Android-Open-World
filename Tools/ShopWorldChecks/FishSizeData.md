# Fish length data

Game length means straight, neutral-pose **total length (snout to tail tip)**, with 1 Unity world unit = 1 metre. FishBase Bayesian length–weight estimates supply W in grams from total length L in centimetres: W = a L^b. These are population/model estimates, not exact individual weights. We sample them every 5 cm into checked-in tables and linearly interpolate length by weight at runtime. Below the first interval the result has a 5 cm floor. Above a table's authored range, open-ocean gameplay continues that species' final length/weight trend with a safety cap of 1.8× the tabulated maximum length; existing save weights are not rewritten.

Generic names are resolved as game-design assumptions: Sea Bass = European sea bass, Yellowtail = yellowtail amberjack/kingfish, Black Spot Goatfish = blackspot goatfish, Bonito = Atlantic bonito, and Black Sea Bass = Centropristis striata. Limits below describe the tabulated curve range, not claims of record lengths. Coastal catch ceilings remain controlled separately by `FishCatalog`.

| Game species ID | Scientific name | a | b | Tabulated length | Source |
|---|---|---:|---:|---:|---|
| 0 | Scomber australasicus | 0.00891 | 3.06 | 0.55 m | [FishBase](https://www.fishbase.se/summary/Scomber-australasicus.html) |
| 1 | Lutjanus campechanus | 0.01479 | 2.96 | 1.00 m | [FishBase](https://www.fishbase.se/summary/Lutjanus-campechanus.html) |
| 2 | Dicentrarchus labrax | 0.00933 | 3.02 | 1.00 m | [FishBase](https://www.fishbase.se/summary/Dicentrarchus-labrax.html) |
| 3 | Seriola lalandi | 0.01862 | 2.93 | 2.00 m | [FishBase](https://www.fishbase.se/summary/Seriola-lalandi.html) |
| 4 | Thunnus albacares | 0.01413 | 3.03 | 2.00 m | Retired ID retained for save compatibility |
| 5 | Thunnus albacares | 0.01413 | 3.03 | 2.00 m | [FishBase](https://www.fishbase.se/summary/Thunnus-albacares.html) |
| 6 | Parupeneus cyclostomus | 0.01259 | 3.09 | 0.50 m | [FishBase](https://www.fishbase.se/summary/Parupeneus-cyclostomus.html) |
| 7 | Parupeneus spilurus | 0.01259 | 3.04 | 0.50 m | [FishBase](https://www.fishbase.se/summary/Parupeneus-spilurus.html) |
| 8 | Tuna-family gameplay approximation | 0.01413 | 3.03 | 2.00 m | Bigeye gameplay curve retained from existing tuna sizing |
| 9 | Sarda sarda (Bonito) | 0.00851 | 3.06 | 1.00 m | [FishBase](https://www.fishbase.se/summary/Sarda-sarda.html) |
| 10 | Centropristis striata (Black Sea Bass) | 0.01148 | 3.04 | 0.70 m | [FishBase](https://www.fishbase.se/summary/Centropristis-striata.html) |

The new Bonito and Black Sea Bass rows use the same 5 cm sampling/interpolation pipeline as the existing fish. Their normal coastal min/max weights are configured in `FishCatalog`; far-offshore scaling can exceed those coastal maxima without shrinking or changing near-shore fish.

Rod/reel first-person presentation is intentionally enlarged. Fish world dimensions are driven by these length curves and are not presentation-scaled.
