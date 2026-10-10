# Suncrest Reef protected grass + ripple penalty exemption

## Owner instructions

The original, supplied green grass texture is already an asset in the repository:
`Assets/_Game/EditableFishingMap/SurfaceRecovery-20261007-232425362/RecoveredSuncrestGrass.terrainlayer`.
The owner requested the grass to be restored on **Suncrest Reef only**, and
never repainted/replaced unless the owner explicitly requests it.

1. Pull `upstream/main` and open `Assets/Scenes/PrototypeWorld.unity`.
2. In Edit Mode (outside Play), approve the prompt **Restore and permanently
   save Suncrest grass**, or run **Tools > Open World > Restore and Lock STARTER
   Island Grass**.
3. The editor saves **SceneBeforeGrass.unity** and an independently copied
   `TerrainBeforeGrass.asset` as backups, writes a NEW persistent
   `ProtectedSuncrestTerrain.asset`, paints Suncrest using the existing
   original grass layer, adds a serialized `StarterIslandGrassLock` component
   and saves `PrototypeWorld.unity`.
4. Approve **Commit and push**. Unity commits *only* the scene and new
   `LockedSuncrestGrass-*` asset folder, including its `.meta`. If Git fails,
   the Unity project still contains the saved repair. Re-run Git push manually
   (do not run the paint tool again).
5. Verify the updated Scene, Play Mode, and Android Build & Run. A completed
   Git commit of the scene and terrain asset is required for permanence on
   other computers. Do not claim the binary asset is uploaded before that push.

**Protected area:** only Suncrest's above-water interior receives restored
grass. The sandy coast, original pathway and pond bank remain sandy. Existing
terrain heights, Snapper Island, Brinebreak Isle, Bluewater Cay, underwater
cliffs and user-placed rocks are untouched. The paint tool never changes
object transforms or materials outside the grass layer.

**Future developer requirement:** Never resculpt/repaint/replace this saved
starter terrain in code without an explicit new request from the owner.
Do not call old global reset tools or regenerate the already-baked fishing
terrain. The grass marker blocks repeated repairs; load-time texture swapping
already excludes saved fishing maps.

## Fishing ripples

At **every island and fishing biome, existing or added in the future**, a
successful water cast whose *frozen landing point* hit an active fishing ripple
has no cast-depth/close-water penalty: fish weight potential and fight
HP/difficulty quality remain 100%. The previous ripple size bonus still applies
(10–50%, strongest in the centre). Only normal casts outside a ripple keep
their depth/close-water penalties. The exemption is independent of biome ID,
island name and shore distance. Eligibility is captured at water landing, so
a lure moving through a ripple later does not retroactively earn it. Pond
casts keep their separate existing rules.

Source changes to this global ripple rule and the one-time grass editor are on main. The
Unity scene/serialized TerrainData need the local editor pass to complete.
