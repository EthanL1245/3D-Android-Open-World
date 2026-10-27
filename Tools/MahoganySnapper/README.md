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

- `Mahogany Snapper.fbx` (also accepts `MahoganySnapper.fbx` and names such
  as `Mahogany Snapper Swimming.fbx`)
- `Mahogany Snapper Texture.png` (also accepts `MahoganySnapperTexture.png`)

It searches recursively inside this project's `Assets` directory, and scans
the project root, `ArtSources`, Desktop, Downloads and Documents (including
immediate subfolders and standard OneDrive Desktop locations).
It copies the files into `Assets/_Game/Reef/Source/` with canonical names and
calls `SeaBassImporter.InstallModel` to generate
`Assets/Resources/Fishing/MahoganySnapper.prefab` with the authored swim
animation, atlas, mouth pin and standard fish presentation.

The local FBX must contain the rigged animated fish with the shared snapper
five-bone naming and UVs. The STL is a static mesh, not a replacement for the
animated FBX. If Unity reports that the FBX lacks a swim take or expected rig,
the source needs to be re-exported from Blender.

Normally just pull the code, leave the FBX and PNG in a searched folder on
**the same computer**, and open Unity. If they are stored elsewhere, use
`Tools > Open World > Locate Mahogany Snapper Files` once. The
`Tools > Open World > Rebuild Mahogany Snapper` command forces regeneration.

The FBX and PNG are **not** uploaded to GitHub by the Editor auto-import.
Generated model, material, atlas, controller and prefab live locally unless
the user also commits and pushes the binary assets. Other computers cannot
obtain locally generated artwork via `git pull` alone. Fish gameplay works
with its generic fallback if no local source is available.

Unity Editor compilation and Play Mode visual verification are still required.
