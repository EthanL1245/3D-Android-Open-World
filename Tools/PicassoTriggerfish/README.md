# Picasso Triggerfish — stable species ID 31

Added to the fishing species catalog without renumbering previous IDs.
Default habitat is **Suncrest Reef** (internal `suncrest-reef`). Since no
spawning area or chance was specified, use a low **3% catch chance** for
all nine bait/lure entries. Only 2 percentage points were removed from Blue
Mackerel and 1 from Sea Bass in the Suncrest tables, keeping all existing
Red Snapper / Flounder / Stingray odds unchanged and each row at 100%.
Every other biome remains at zero chance and an unavailable weight range.

## Physically realistic weight and length

Scientific species: *Rhinecanthus aculeatus*, also known as white-banded
triggerfish. FishBase records a common length of 15 cm total length and a
maximum of 30 cm. Measurements of 17 wild specimens from Rodrigues lagoon
(Edwards et al., 2011) give this empirical relationship:

    weight_grams = 0.1559 * total_length_cm ^ 2.4059

The game evaluates that function every 5 cm for `FishSizeTable` and uses the
existing lookup/interpolation system. The normal catch range is 0.06–0.56 kg,
which corresponds to roughly 11.9–30 cm. 15 cm is roughly 105 g; 30 cm
is roughly 558 g. This is a measured species-specific relationship, not a
copied generic Yellowtail/Red Snapper size curve. Outside the normal range,
pre-existing hotspot bonuses and legacy extrapolation rules remain unchanged.

Research:
- https://www.ncl.ac.uk/tcmweb/tmr/Length-weight%20relationships%20for%20Rodrigues%20fishes.pdf
- https://fishbase.org/summary/Rhinecanthus-aculeatus

## Fighting mechanics

Matches the **Red Snapper** gameplay settings: difficulty 0.42, health
115–342, yellow swim speed 3.152 m/s, and sell endpoints 40–300 coins.
Health still varies across Picasso Triggerfish's own min/max weight percentile,
so a fish weighing 200 g fights proportionally to its own size, rather than
being evaluated against a Red Snapper's kilogram range.

Existing fishing mechanics, rod line, hooksets, reeling, damage, charm, bait,
hotspots, catching and inventory are shared. Fish visuals use
`FishVisualFactory` and `RedSnapperPresentation` in every relevant context:
ambient fish, active fights, held catches, fish index, aquarium and shop,
and remote-player presentation. Server tournament max includes the existing
50% hotspot bonus (0.84 kg).

## Import from supplied files

The user provided a Blender source, an **animated FBX**, and a PNG texture.
The FBX is already exported; Blender is not needed on the game PC.\nThe FBX contains two 0.792-second takes (both named ArmatureAction variants).\nThe first take drives the regular Swim state; the second is installed as\nthe HeldStruggle state used by CaughtFishFlop. Both use the nine-bone rig,\nand the game shares the usual mouth anchor and held-fish placement.\nThe second animation's intended presentation still needs a Unity visual check.

The repository includes `Assets/_Game/Editor/PicassoTriggerfishInstaller.cs`.
Upon Unity Editor startup or before building, it discovers these local files
under `Assets`, Desktop, Documents or Downloads (and common OneDrive folders):

- `Picasso Triggerfish.fbx`
- `Picasso Triggerfish Texture.png`

It copies them to canonical asset names and uses the existing
`SeaBassImporter.InstallModel` pipeline:

- `Assets/_Game/Reef/Source/PicassoTriggerfish.fbx`
- `Assets/_Game/Reef/Source/PicassoTriggerfishTexture.png`
- Generated `Assets/Resources/Fishing/PicassoTriggerfish.prefab`

Unity menu options are **Tools > Open World > Rebuild Picasso Triggerfish**
and **Tools > Open World > Locate Picasso Triggerfish Files** (manual fallback).

The GitHub connection used for the code cannot transfer binary chat uploads
directly, so the original FBX/PNG are not checked into GitHub. Keep the files
on the computer that runs Unity, or extract the accompanying downloadable
Unity asset ZIP's `Assets` folder into the Unity project. A `git pull`
alone transfers the code, not these local binary artworks.

Validation command: `node Tools/PicassoTriggerfish/verify_rules.mjs`
Unity Editor Play Mode/import validation still requires running Unity locally.
