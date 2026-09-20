# Fishing rod + animated reel

After pulling `main`, let Unity finish compiling, then click:

**Tools > Open World > Install Fishing Rod + Animated Reel (One Click)**

Press Play in the existing fishing scene. Hotbar slot 1 equips the complete rod/reel item. No ZIP selection, Blender installation, Inspector wiring, or scene rebuild is required. Re-running the installer updates the same prefab, meshes, materials and clip, preserving their asset GUIDs.

## Repair white/untextured equipment

Pull the latest `main`, exit Play Mode, and run the same installation menu again. This updates existing material assets in place. The installer now finishes both PNG imports before loading texture references, rejects null/placeholder textures, sets explicit UV tiling `(1,1)` and offset `(0,0)`, and uses `OpenWorld/FishingEquipment`, which always samples the supplied atlas through UV0. It validates the six renderer bindings, non-collapsed mesh UVs, and the saved prefab's texture dependencies. A failed check reports an error instead of reporting a successful installation.

The original PNGs and mesh/animation export are unchanged. The exact failing state in the user's locally generated materials was not available for inspection; these changes repair the material setup and remove reliance on the previous URP Lit material state. Final Unity/Android rendering still requires a local check.

## Behavior

- The uploaded wood rod and silver/black reel retain their UVs and original textures.
- Rod and ReelMount are separate children of one equipped root. The reel foot is seated against the handle and held by two small collars.
- Casting rotates the complete assembly, winds back, releases the bobber from the moving tip, then settles into its resting pose. The line continues to follow RodTip.
- Holding REEL during Fighting plays the authored rotor, spool and handle movements. Releasing freezes the current reel pose; holding again resumes. Casting/waiting do not run the reel animation.
- Catching, switching to a fish, canceling, disabling the fishing system, and app focus/pause clear the relevant motion/input. Losing a fight stops cranking immediately.
- The original hotbar slot and saved fish inventory remain in use. The reel is not a second item.

The installer creates `Assets/Resources/Fishing/FishingRodReel.prefab` and native assets under `Assets/_Game/Fishing/RodReel/Generated`. Commit those generated assets and their `.meta` files if you want teammates/build machines to skip installation. Until installed, the game keeps its old placeholder and logs the exact setup menu name.

## Asset conversion

Original files are archived outside Unity's Assets folder in `SourceAssets/FishingRodReel`. The evaluated mesh export includes the reel body's Mirror modifier, split normals, UV seams, and all three source actions. Nothing uses the static STL copies, which do not contain the animation/UV data needed here.

The Blender scene ends at frame 29, but rotor/spool actions end at 30 and the handle action at 31. Each complete authored action is sampled at 121 points and fitted to a common 1.25-second cycle. This preserves the 4-turn rotor, 1-turn handle and reciprocating spool with a closed seam. Quaternion continuity avoids wrap flips. Only the three moving children receive animation curves; neither the mounting transform nor the rod is animated by that clip.

Coordinate conversion, scale and placement are fixed in `export_models.py`: rod is approximately 1.802 m long, reel units are 0.04 m, and the mounting root is at `(0, 0.17, 0.1035)` relative to the rod butt. Adjust these intentionally and regenerate if the source dimensions change.

To regenerate, run from the repository root with Blender:

```sh
blender -b --python Tools/FishingRodReel/export_models.py -- SourceAssets/FishingRodReel Assets/_Game/Fishing/RodReel/Source/RodReel.json
blender -b --python Tools/FishingRodReel/verify_export.py -- SourceAssets/FishingRodReel Assets/_Game/Fishing/RodReel/Source/RodReel.json
```

The original texture PNGs are in `Assets/_Game/Fishing/RodReel/Source`. The source .blend files retain their original relative image filenames; relink those PNGs when editing them in Blender. Exporting mesh/animation data does not require image relinking.

## Verification

Completed outside Unity:

- Rendered and inspected the assembled rod and a close-up of the reel mounting, UVs and texture placement.
- Checked all 6 meshes (3,112 source triangles) for valid indices and UV counts.
- Compared all original animated vertices against the exported motion at every sampled phase. Maximum error: 0.000000180 m. All three animation loops close.
- Parsed the changed C# files for syntax errors.

The installer validates mesh data and loop seams, restricts clip bindings to Handle/Rotor/Spool, and samples the clip through a full cycle to reject any rod-tip or mount displacement before saving the prefab.

Unity compilation, actual Play Mode behavior and Android rendering have not been run in this environment. After installation, check one cast, hold/release REEL, switch to a caught fish, re-equip, and background/resume the app while reeling. The reel should pause without detaching or restarting on a normal button release.
