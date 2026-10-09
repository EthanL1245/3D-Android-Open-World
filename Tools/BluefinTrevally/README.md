# Bluefin Trevally (species ID 24)

- **Region:** Bluewater Cay only; all four other fishing biomes have zero weight and zero chance.
- **Difficulty:** 10% higher min/max health than Greater Amberjack (352–5500 vs 320–5000), with a slightly higher yellow-state pull speed (3.85 m/s vs 3.70 m/s).
- **Size and value:** 2–55 kg in Bluewater Cay; fish-species bounds 1–70 kg; value curve 286–8800 coins.
- **Spawn percentages:** Bluewater Cay worms/legacy 3%, shrimp 3%, squid 4%, each lure 5%; fractions are taken from existing Bluewater species, and all bait rows still total 100%.
- **Animations:** Imported from the supplied original Blender armature/action. Uses the existing RedSnapperPresentation pipeline for normal swimming, held-fish flop, aquarium/pond movement and index animation.
- **UVs:** Original Blender UV map and supplied 1448×1086 PNG are retained by the FBX export and atlas importer.

## Import the authored 3D fish once

The large user-supplied Blender model and texture are **not stored inside this GitHub commit**. Put the original `Bluefin Trevally.zip` (containing `Bluefin Trevally.blend`, `Bluefin Trevally Texture.png`, and `Bluefin Trevally.stl`) in your Downloads folder or project root.

After `git pull`, open the Unity project. The new importer will try to find the ZIP and automatically create the `Assets/Resources/Fishing/BluefinTrevally.prefab` with its FBX, atlas, materials, and looping animation. If auto-import cannot find it, use **Tools > Open World > Import Bluefin Trevally (One Click)** and choose the ZIP or Blender executable if prompted. The importer reuses the existing fish pipeline and saves the result as Unity assets. Commit the newly generated art assets from your PC if you want them available to other clones from GitHub.

Do not rely on the fallback procedural fish in release builds: the authored prefab must be generated successfully to display the supplied model.
