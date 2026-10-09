# Golden Trevally (species ID 25)

Adds the original authored Golden Trevally to Bluewater Cay only. The supplied ZIP contains `Golden Trevally.blend`, `Golden Trevally Texture.png` (1448 × 1086), and `Golden Trevally.stl`.

## Fishing tuning

- Region: **Bluewater Cay only**. Weight distribution in all four other biomes is 0/0 and every bait/lure spawn chance elsewhere is 0%.
- Bluewater weight range: 2–55 kg, with species limits 1–70 kg (matching Bluefin for like-for-like comparisons).
- Slightly easier than Bluefin Trevally: **317–4950 HP** vs Bluefin's **352–5500 HP** (approximately 10% lower), **3.75 m/s** yellow swimming speed vs Bluefin's **3.85 m/s**.
- Sell value: 260–8000 coins.
- Bait/lure probabilities: worms 3%, legacy worms 3%, shrimp 3%, squid 4%, all five lure variants 5%. Every row totals exactly 100%, with Golden's allocation taken from other Bluewater species rather than reducing Bluefin's odds.
- New stable species ID 25 preserves all prior save IDs and works with existing catch records, bag, markets, tanks/ponds and Fish Index.

## Import supplied animated model

The uploaded Blender/texture binary files are **not included in this GitHub commit**. Put `Golden Trevally.zip` in the Unity project root or Windows Downloads. Once pulled, Unity's editor auto-importer tries to find and build the original model/prefab automatically. Alternatively select **Tools > Open World > Import Golden Trevally (One Click)**. Supply Blender's `blender.exe` if Unity asks.

The importer exports the original rig and swim animation from the .blend with the authored UV map, installs the supplied PNG, then uses `SeaBassImporter.InstallModel` and `RedSnapperPresentation` to build `Assets/Resources/Fishing/GoldenTrevally.prefab`. The existing fish visual factory and Fish Index preview use that prefab. The model does not fully appear in the game until this local asset import succeeds.

If sharing with other machines, commit the generated prefab/model/atlas/material/controller and their Unity .meta files to GitHub after import.
