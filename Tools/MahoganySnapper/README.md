# Mahogany Snapper (save-safe ID 30)

Snapper Island only (biome 4). Chances equal Yellowtail Snapper in all nine
bait/lure rows. Existing Snapper Island catch odds are proportionally rebalanced
with largest remainder rounding to retain integer 100% rows.
The new species is very slightly harder than Yellowtail Snapper: base difficulty
0.48 vs 0.46; health 96–315 vs 90–300; swim speed 2.90 vs 2.85 m/s.
Catches weigh 0.25–4.1 kg. Sale range 40–250 coins.

Uses the shared fish visual and presentation factory, including index/shop,
inventory, held fish, aquarium, ambient previews, multiplayer and tournaments.

## Automatic local FBX / texture import

The Unity Editor automatically searches for these locally saved files when it
opens, recompiles the installer or sees either asset added under `Assets`:

- `Mahogany Snapper.blend` (Blender source) **or** an already exported
  `Mahogany Snapper.fbx` / `MahoganySnapper.fbx`
- `Mahogany Snapper Texture.png` (also accepts `MahoganySnapperTexture.png`)

**No manual FBX export is needed when Blender is installed locally.** If there
is no FBX, the Editor locates the .blend file and the installed Blender
executable (normal Windows Program Files installations, PATH, or BLENDER_BIN).
It runs `Tools/MahoganySnapper/export_snapper.py` in Blender background mode
to export the animated rig as an FBX, then imports the PNG and creates the
normal Unity fish prefab. Errors from Blender are printed to the Unity Console.

It searches recursively inside this project's `Assets` directory, and scans
the project root, `ArtSources`, Desktop, Downloads and Documents (including
immediate subfolders and standard OneDrive Desktop locations).
It places the files into `Assets/_Game/Reef/Source/` with canonical names and
calls `SeaBassImporter.InstallModel` to generate
`Assets/Resources/Fishing/MahoganySnapper.prefab` with the authored swim
animation, atlas, mouth pin and standard fish presentation.

The local FBX must contain the rigged animated fish with the shared snapper
five-bone naming and UVs. The STL is a static mesh, not a replacement for the
animated FBX. If Unity reports that the FBX lacks a swim take or expected rig,
the source needs to be re-exported from Blender.

Normally just pull the code, leave the .blend (or FBX) and PNG in a searched folder on
**the same computer**, and open Unity. If they are stored elsewhere, use
`Tools > Open World > Locate Mahogany Snapper Files` once. The
`Tools > Open World > Rebuild Mahogany Snapper` command forces regeneration.

The local .blend / FBX and PNG are **not** uploaded to GitHub by the Editor auto-import.
Generated model, material, atlas, controller and prefab live locally unless
the user also commits and pushes the binary assets. Other computers cannot
obtain locally generated artwork via `git pull` alone. Fish gameplay works
with its generic fallback if no local source is available.

Unity Editor compilation and Play Mode visual verification are still required.
