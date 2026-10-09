# Giant Trevally – species ID 26

Giant Trevally is **exclusive to Bluewater Cay**. It reuses the existing authored fish movement, rig/animation import, fight, hook, line, reel, drag, skill, landing, inventory, shops, home tanks and ponds, and animated 3D Fish Index preview.

## Gameplay

| Setting | Bluefin Trevally | Giant Trevally |
|---|---:|---:|
| Catch zone | Bluewater Cay | Bluewater Cay |
| Weight at 0th–100th percentile | 2–55 kg | 3.6–99 kg |
| Expected weight (symmetric tuned distribution) | 28.5 kg | 51.3 kg |
| Fish-wide weight bounds | 1–70 kg | 1.8–126 kg |
| HP at matched weight percentile | 352–5500 HP | 704–11000 HP |
| Yellow-state base swim speed | 3.85 m/s | 3.85 m/s |
| Sell value range | 286–8800 coins | 572–17600 coins |

**Difficulty:** exactly double Bluefin HP for the same catch percentile, with the same swimming speed and unchanged fish-fighting mechanics. This produces approximately double the amount of rod damage needed without introducing arbitrary extra speed multipliers.

**Size:** Giant fish catches use **1.8× the weight of Bluefin** at an equivalent Bluewater Cay weight percentile, not simply an increased maximum. The `FishSizeTable` also uses the existing variant-scaling pattern to make the displayed total length **1.8×** at that percentile (and inverse length–weight lookup scales consistently). This applies in the world, when held, and in the bag/aquarium. Fish Index preview normalizes display size to fit the panel.

**Spawn chance:** worms 2%, legacy worms 2%, shrimp 2%, squid 2%, all five lure variants 3%. Every one of 45 bait/biome rows still sums to 100%. Giant's odds are taken from common Bluewater species, retaining Bluefin and Golden odds unchanged. Giant gets **0% chance** in Suncrest Reef, Brinebreak Isle, Deep Ocean, and Snapper Island, with `0,0` unavailable weight bounds.

## Install original model

The uploaded `Giant Trevally.zip` contains `Giant Trevally.blend` (5-bone rig), `Giant Trevally Texture.png` (1448×1086, existing UVs), and `Giant Trevally.stl`. The image and Blender binary are **not in this GitHub commit**; this update adds a one-click importer that installs them locally.

1. Copy the ZIP into the Unity project root or your Windows Downloads directory.
2. Pull main and open the Unity project. It will try to auto-import after compilation.
3. If automatic import is not completed, choose **Tools > Open World > Import Giant Trevally (One Click)** and select the ZIP or Blender executable as necessary.
4. The importer exports the original rig + active swim action to FBX, preserves the UV layout, builds the Unity atlas/material/looping controller and prefab, and validates the animator, mouth anchor, and texture dependencies.
5. Commit the generated FBX, prefab, atlas, controller, material, and meta files to GitHub if the resulting binary art should be available after Git pulls on other machines.

Do not treat the temporary procedural fallback as a finished visual until the importer has produced `Assets/Resources/Fishing/GiantTrevally.prefab`.

## Verification

Static checks: all 45 updated probability rows total 100; Giant has no chance outside Bluewater Cay; existing Bluefin/Golden chance columns are unchanged; Giant's mean, endpoints, and HP curve scale 1.8×, 1.8×, and 2× respectively. Runtime testing/Android build requires Unity on the owner's machine.
