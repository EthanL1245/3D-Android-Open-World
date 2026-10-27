# Roughtail Stingray (stable species ID 28)

Uses Winter Flounder's gameplay settings: Suncrest Beach only, Red Snapper odds
for each bait/lure (worms 13%, shrimp 15%, squid 9%, all lures 16%), Yellowtail
fight difficulty/health/speed, and Flounder's gameplay weight, length and value
curves. Existing Red Snapper and Flounder probabilities are preserved; remaining
Suncrest probabilities are reduced proportionally with integer largest-remainder
rounding. Other islands are unchanged. Server tournament validation includes 28.

The original files are preserved in ArtSources. Both full 1–30-frame actions
come from the supplied Blender file. The separate struggling FBX is an
animation-only version of the same motion ending one source frame earlier.
The STL is archived for reference and is not a runtime input.

The exported FBX contains one textured mesh (1,586 triangles), 29 bones, and
Swim / HeldStruggle clips. Fin roots are attached to the shared spine so aquarium
trail turns carry the fins; rebaked armature-space matrices preserve the authored
poses (maximum export error 0.00000191). Brown side faces up while swimming.
Index and inventory previews display the brown side like Winter Flounder.

Unity automatically generates the prefab/controller/material/atlas after pull,
using the same shared installer as Flounder. No Blender installation is required.
Tools > Open World > Rebuild Roughtail Stingray forces regeneration. Swim is the
default controller state without automatic transitions. Only ConfigurePlayerHeld
selects HeldStruggle, continuously sampled at authored speed for local and remote
players; the post-pose mouth correction retains the string attachment.

The held Flounder and Ray use a total 90-degree counterclockwise offset from the
standard held fish yaw (the latest Flounder adjustment adds 10 degrees). Pitch,
roll, swimming and existing Flounder line-anchor calculation are unchanged.

## Future asset handoff

Preferred: one .blend containing the mesh, UVs, skinned rig and named swimming
and struggling actions, plus texture PNGs (or pack textures into the blend).
Alternatively: a skinned FBX with the model and both animation takes, plus
textures; or a skinned swimming FBX plus a separate struggling FBX exported from
the exact same rig and bone hierarchy. An animation-only FBX cannot replace the
model. STL is unnecessary because it contains no rig, UVs or animation.
For this addition, the .blend and texture PNG were sufficient.

## Verification

- Rules checks pass for both IDs 27 and 28: 785 checks each, including all 45
  spawn tables, 450,000 actual rolls each, Yellowtail fight parity, size round
  trips, unavailable biomes, fallback tables and stable species IDs.
- All 37 server tests pass, including Stingray tournament limits and island checks.
- FBX roundtrip verifies both distinct moving clips and exact loop endpoints,
  all 29 bones, UVs, triangle count and brown side up. Textured render inspected.
- C# syntax checked across project. Held-animation script compiled against the
  project's archived UnityEngine assemblies (a temporary copy substitutes the
  existing Mathf.PI literal because that constant is stripped from those DLLs).
- Unity Editor is unavailable here; prefab import and live Play Mode still need
  an in-editor check.

Run rules from root: dotnet run --project Tools/WinterFlounder/Checks -- . 28
Run server: node --test Server/tests/*.test.mjs
Run FBX check: blender --background --python Tools/RoughtailStingray/verify_export.py
Re-export: blender --background ArtSources/RoughtailStingray.blend --python Tools/RoughtailStingray/export_stingray.py -- Assets/_Game/Reef/Source/RoughtailStingray.fbx
