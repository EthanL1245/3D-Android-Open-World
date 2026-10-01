# Snapper update

Pull and let Unity finish importing. The editor automatically rebuilds Red Snapper
in place and creates Yellowtail Snapper through `SeaBassImporter` and
`RedSnapperPresentation`, including the five-bone aquarium trail behavior, authored
Swim clip, and animated mouth anchor. Existing held-fish code supplies resting poses
and three-loop flopping bursts. A Play/build gate verifies the generated assets.
No external Blender install, ZIP picker, or scene setup is needed.

Red Snapper keeps species ID 1 and its resource prefab path/GUID. Its existing FBX
and texture paths now contain the user's fixed model/animation and supplied texture.
Yellowtail Snapper is a distinct new species, ID 16; existing Yellowtail remains ID 3.
Blacktip Shark keeps ID 15 and receives only a display-name change.

Yellowtail Snapper occurs only in Suncrest Reef. The existing two-goatfish probability
pool is divided approximately equally between all three species in each bait row:

| Bait | Yellow Goatfish | Black Spot Goatfish | Yellowtail Snapper |
|---|---:|---:|---:|
| Worms / legacy worms | 13% | 13% | 13% |
| Shrimp | 15% | 15% | 15% |
| Squid | 7% | 8% | 8% |
| Each crankbait | 2% | 2% | 2% |

Whole-number rounding accounts for the squid difference. All other species chances
and all nonstarter rows are preserved. Every row still sums to 100%.

Starter fish tuning: 0.25–4.1 kg, 90–300 HP, 40–250 coins, yellow speed 2.85 m/s.
The size curve (kg = 10 × metres³) is a gameplay approximation.

Source files: `ArtSources/RedSnapperFixed.blend`, `ArtSources/YellowtailSnapper.blend`.
Export with the existing `Tools/BlacktipShark/export_shark.py` authored-fish exporter.
Check prepared FBXs with `Tools/SnapperUpdate/verify_export.py` under Blender.
The updated `Tools/BlacktipShark/Checks` project executes the real tuning parser and
selector for all 27 bait/biome rows, including starter-only snapper exclusions.
Unity compilation and Play Mode remain required for final in-game visual validation.
