# Reef and Mako shark update

Pull and let Unity finish importing. `SharkSpeciesSetup` automatically builds the three prepared FBXs with `SeaBassImporter`, the shared `RedSnapperPresentation`, authored mouth anchors, atlas material, and looping swim controller. It retries before Play and checks before builds. Manual retry: **Tools > Open World > Install Reef and Mako Sharks**.

Existing Blacktip Shark ID 15 is unchanged. New stable IDs:

| ID | Species | Habitats | Chance for every bait/lure | Weight |
|---|---|---|---|---|
| 18 | Blacktip Reef Shark | Brinebreak, Snapper Island | 2% | 3–18 kg |
| 19 | Mako Shark | Bluewater Cay, Deep Ocean | 2% | 20–150 kg |
| 20 | Battle Scarred Mako Shark | Bluewater Cay, Deep Ocean | 1% | 30–225 kg |

New odds are taken from the most common non-shark in each applicable row. Existing Blacktip Shark's Bloody Bait test odds remain intact. Snapper Island now has ordinary CSV rows: its previous relative snapper probabilities share 98%, with the remaining 2% assigned to Blacktip Reef Shark. All 45 bait/biome rows sum to 100.

At the same weight percentile, the scarred Mako has 1.5x weight, 1.5x total length, 1.5x health, 1.5x yellow swim speed, and 1.5x final effective fight difficulty (after the usual clamp). Length explicitly delegates to the normal Mako curve with weight divided by 1.5, then multiplies length by 1.5. These are game balance defaults, not biological measurements.

Snapper Island starts undiscovered, unlocks after landing on dry ground, persists discovery in ShopLedger, and has a dedicated preview and arrival point. Both menu builders support its teleport. Order: Suncrest, Snapper Island, Brinebreak, Bluewater, Deep Ocean. Core biome queries, fish index, ambient spawning, catch override, and weight tables use biome ID 4.

## Asset pipeline

Supplied original `.blend` files remain in ArtSources. Export with Blender:

`blender -b ArtSources/MakoShark.blend --python Tools/SharkSpecies/export_sharks.py -- ABSOLUTE_OUTPUT_PATH/MakoShark.fbx`

Repeat for BlacktipReefShark and BattleScarredMakoShark. The Makos have seven bones: the extra fin branch is preserved, while the four spine descendants are renamed to Bone.001 through Bone.004 for aquarium train-track turns. Bone-parented fins retain their attachment and animation. Texture PNGs are the supplied atlases.

## Verification

`dotnet run --project Tools/SharkSpecies/Checks -- .`

Checks execute actual catalog/tuning/size code, validate every C# file under Assets, roll all 45 bait/biome combinations, and check paired size/health/speed ratios, discovery state and index order. Blender reimport checks verify UVs, skin weights or bone parenting, animated poses, and the five-bone spine contract. Unity Play/Android rendering is still required to visually verify tank swimming, held flops, attachment and travel.
