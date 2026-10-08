# Persistent terrain grass and cliff stone — excluding Snapper Island

Open `Assets/Scenes/PrototypeWorld.unity` outside Play Mode. Run
**Tools > Open World > Restore Grass and Rock Textures (Except Snapper)**.

This is a **one-time scene-editing operation**, not a runtime world regeneration.
It bakes into a NEW persistent `TerrainData`, plus NEW saved grass/stone PNGs and
three `TerrainLayer` assets. The originals, heights, mesh colliders, scenery
and existing layer textures stay untouched.

- Suncrest Reef: recovered green meadow on higher ground, sand on shore, path
  and pond banks.
- Brinebreak Isle: stone on inland elevations, sandy shoreline.
- Bluewater Cay: blended stone patches and grass inland, sandy shoreline.
- Seabed: stone on steep underwater cliffs, existing flats unchanged.
- **Snapper Island and the whole local shelf remain unchanged**, including
  their original alphamap values and heightmap, with an additional 12-metre buffer.

The full pre-change scene plus an **independent copy of the original TerrainData**
are backed up under `Assets/_Game/EditableFishingMap/SurfaceRecovery-<timestamp>/`.
That folder also owns every new persistent terrain/image/layer asset. Keep the
whole folder with the updated scene and its `.meta` files.

The scene's `NonSnapperTerrainSurfaceState` marker prevents rerunning this paint
pass after it succeeds. Existing manually added paint later stays intact. The
legacy world visual generator and legacy full-map restoration are blocked for
the saved map. The runtime supplied-texture installer skips the already-baked
shared map to avoid replacing its layers with temporary clones on game launch.

Once saved, verify in Scene and Play/Android builds, then commit and push:
`Assets/Scenes/PrototypeWorld.unity` plus the entire new `SurfaceRecovery-*`
folder (including `.meta`). The repository cannot see or automatically persist
changes made only on your local Unity installation.

Only source-level validation has been performed remotely; Unity Editor visual
validation and Play/Android builds must be run locally.
