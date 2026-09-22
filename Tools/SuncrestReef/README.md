# Suncrest Reef

## Install

Exit Play Mode. Pull this update, let Unity finish compiling, open the original fishing-island scene, and choose **Tools → Open World → Install Suncrest Reef (One Click)**. Save the scene. No Blender installation is needed.

The installer requires the existing island's Terrain, OceanWater and FirstPersonController. It installs the supplied sea bass prefab, measures the original dry land above mean water level, and targets 50% of that area. “Half size” refers to area, not halving both dimensions. Repeating the installer uses the original measurement instead of halving again. It replaces the generated reef layout. Manual edits inside SuncrestReef should be saved separately before rerunning.

Before modifying the scene, the installer saves a scene and independent TerrainData backup under `Assets/_Game/Reef/Backups`. On generation failure it reloads that snapshot and restores the original scene path. Original terrain data and WorldDecor are preserved, with their old scene objects deactivated. Home, Tideglass Quay, economy and owned fish are preserved.

The island has sandy coves, a low grassy interior, palms, coastal grass, driftwood, shell banks, a limestone arch and trail signs. Outside shore, the seabed reaches about 2m depth over the first 30m, then gradually reaches 6m at 160m. Deeper water ramps down beyond the reef; there is no abrupt reef-edge cliff. These distances use the terrain's ellipse-based radial profile and vary slightly with coastal shape.

## Island index and fish

MENU / TRAVEL still has three destinations. **VIEW INDEX** beneath them opens the island catalog, unlock status and fish legend. Suncrest Reef is the only zone and is unlocked by default. Future definitions can add stable IDs, individual weights and persisted unlock keys (`ReefUnlock.<id>`). No other playable zones or unlock purchases are claimed to exist yet.

| Species | Rarity | Base probability |
| --- | --- | --- |
| Yellow Goatfish | Common | 24% |
| Black Spot Goatfish | Common | 24% |
| Blue Mackerel | Common | 18% |
| Sea Bass | Uncommon | 12% |
| Red Snapper | Uncommon | 10% |
| Yellowtail | Rare | 6% |
| Young Tuna | Rare | 4% |
| Yellowfin Tuna | Very rare | 2% |

Fishing and ambient selection both use `ReefCatalog`. Specialty bait retains its previous multipliers; the legend explicitly describes unmodified base odds. Existing bag records/species IDs are unchanged. Catching beyond the installed reef bounds is disabled until additional zones exist.

## Authored fish and performance

Sea bass uses the provided UVs/PNG, five-bone skeleton and complete 20-frame swim take. Its prefab uses `RedSnapperPresentation`, so existing aquarium path-following, held-mouth attachment and struggle controls recognize it without another species-specific behavior. Young Tuna uses the authored yellowfin model scaled by juvenile weight.

Ambient fish use the same authored models as caught fish. There are at most 12 pooled fish, instantiated over separate frames, with shared materials, no fish shadows, offscreen Animator culling, and presentation updates only near the view. Distance-indexed head trails send delayed headings to the same aquarium presentation classes. Seabed/surface/zone checks prevent terrain traversal; unavailable water leaves a pool entry hidden and retrying. Travel hides ambient fish in Home/Quay.

Palms/coral/grass use shared generated meshes/materials and LOD culling. These are bounded-cost choices, **not a guarantee of lag-free performance**. Profile the Android target.

## Validation and local testing

Automated: C# syntax parsing; deterministic rarity and terrain-property checks; Blender-source vs exported-FBX animated vertices at frames 1/5/10/15/20 (accounting for the importer time offset). UVs and source bone animation confirmed. Unity compilation, actual scene appearance, Android frame rate and in-engine movement still need local testing.

1. Check installer Console summary: new dry area should be about 50% of original. Run again and check it does not halve a second time.
2. Explore the shore, arch, grove and reef; check player spawn, swimming and ocean visibility.
3. Open VIEW INDEX, then fish with reusable lure and specialty baits; goatfish should dominate over many catches, not necessarily every short session.
4. Hold a sea bass, preview it in the bag, and place one in a Home aquarium; verify authored swimming and turns.
5. Swim around ambient fish; check terrain clearance, no live fish in Quay, and performance on Android.
6. Verify Home purchases and inventory remain intact.

After testing, save the Unity scene, then run from the project root:

```powershell
git add Assets Tools SourceAssets
git status --short
git commit -m "Save tested Suncrest Reef scene and generated assets"
git push origin main
```

The code/source changes are already published by the assistant. These commands save the assets and scene produced on your machine. Backup folders are intentionally ignored; keep them locally for recovery.
