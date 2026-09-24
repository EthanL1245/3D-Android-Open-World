# Tuna, island index and fishing update

## Install and test

1. Exit Play Mode and close Unity. Pull `main` in your existing project (`git pull --ff-only origin main`). If Git reports local changes, save/commit those changes before pulling; do not discard your scene work.
2. Reopen Unity and let scripts compile.
3. Run **Tools > Open World > Install Updated Tuna + Fishing (One Click)**. This builds both tuna prefabs, animation controllers, native texture assets and animated mouth anchors. It preserves the existing Yellowfin prefab path/GUID. No ZIP selection or Blender installation is needed.
4. Press Play. Runtime index/casting/health changes need no scene rebuild. Do not rerun the island installer just for this update.

The Suncrest Reef installer also installs these tuna when setting up a fresh island. Original supplied Blender sources are under `SourceAssets/Reef`; Unity imports the exported FBXs, not the blend files.

## Controls and behavior

- Top-left **ISLAND / FISH INDEX** opens large island cards and species previews. Menu/Travel remains top-right with its original three destinations and tabs. Bag preview framing is preserved; index previews fit the whole fish.
- Tap **CAST** once to start the oscillating power bar, then tap again to release. A low reading casts 5 m; peak reaches 30 m. All lines are 40 m long; line upgrades improve tension tolerance.
- The cast button is clickable only for a continuous clear range ending at 30 m. Invalid near distances appear grey on the quarter-circle gauge; interior gaps and a blocked maximum disable idle CAST completely. Selecting a grey gauge distance cancels before animation, with no bait used. A red cancel button temporarily replaces Jump.
- Bait is consumed only after landing in valid water. Failed or interrupted airborne casts spend none. Canceling after a valid landing retains the normal spent-bait behavior.
- Ocean size increases with depth, reaching the full species range at the outer reef's 6 m depth. Shallow casts favor the lower range. Depth changes size, not species rarity. Palm Pond remains 5–12 cm for every species.
- Fish have integer HP based on species difficulty, rarity and weight. Holding REEL does cumulative damage; releasing it retains damage dealt and fractional progress. Floating numbers show the actual HP removed. Larger and rarer fish take longer to exhaust. Tension and line breaking still apply.
- Both tuna use their supplied 20-frame animation and textures, with the shared snapper train-track turns, held three-cycle flop/settle behavior and animated mouth anchors. Ambient spawns use the same active catalogue/odds.

| Species | Base chance |
|---|---:|
| Yellow Goatfish | 22% |
| Black Spot Goatfish | 22% |
| Blue Mackerel | 20% |
| Sea Bass | 13% |
| Red Snapper | 11% |
| Yellowtail | 6% |
| Bigeye Tuna | 4% |
| Yellowfin Tuna | 2% |

Infinite worms are the default. Specialty bait adjusts these base odds. Both tuna receive the same bait multiplier, preserving their 2:1 ratio. Retired Young Tuna never rolls; its old save ID still maps to Yellowfin. Bigeye has a new stable ID (8). Its length/weight curve is a tuna-family gameplay approximation; displayed limits are game catch limits, not claimed biological maxima.

## Verification and local acceptance

Automated checks: all available project C# sources parsed without syntax errors; 51,007 assertions against the actual rules/catalogue/size-table C# passed. The reef property tests passed. Blender FBX reimport checks confirmed five bones, UVs, all vertices weighted (753 Bigeye, 749 Yellowfin), and changing evaluated mesh positions. Source and export motion amplitudes agreed within 0.00001 Blender units. Supplied atlases were visually checked on both exported meshes.

Unity compilation, Play Mode rendering and Android profiling must be checked locally:

- From dry shore, aim toward the sea; test low, half and peak casts, then repeat into Palm Pond.
- Aim at land, a tree and outside the reef; confirm immediate retraction and unchanged bait count. Open a menu during charging/flight and confirm it cancels cleanly.
- Check top-left index on phone, scroll all eight species, and compare shallow/deep catch sizes and numeric HP.
- Reel, release, reel again: HP must never regenerate; damage numbers equal HP lost. Exhaust and land small and large fish; verify tension still breaks the line normally.
- Catch/deposit/hold both tuna. Check textures, full swimming/flopping cycles, train-track turns and line attachment through motion.
- Test the Android build on the target phone for frame rate and touch usability.

Re-run pure checks from the repository root:

```sh
dotnet run --project Tools/TunaUpdate/Checks/Checks.csproj -- .
python Tools/SuncrestReef/check_reef.py
```

After successful Unity testing, save the scene/project and commit generated assets:

```sh
git add Assets ProjectSettings
git commit -m "Install and verify updated tuna assets in Unity"
git push origin main
```
