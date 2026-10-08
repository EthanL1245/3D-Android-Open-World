# Snapper Island — north-matched underwater shelf and rock collision

This update targets the smaller Snapper Island, not Suncrest, Brinebreak, Bluewater
or the shared deep ocean. Runtime-generated maps apply the new bathymetry
automatically before gameplay. Saved editable maps **do not** regenerate at
runtime; you must apply the editor command once to update their existing
persistent TerrainData.

In Unity (outside Play Mode) open `Assets/Scenes/PrototypeWorld.unity` and run
**Tools > Open World > Snapper Island > Match Underwater Depth to North Side**.

This samples the existing seafloor just north of Snapper to establish a local
depth, then adjusts the underwater ring on all sides with low-amplitude natural
undulations. The outside edge gently blends into the existing ocean floor.
The island itself, land heights, terrain paint, manually placed props, fishing
biomes and object transforms stay untouched. A backup scene and INDEPENDENT
terrain asset are saved under `Assets/_Game/EditableFishingMap/SnapperSeabed-*`.
The operation is versioned and does not re-sculpt the same saved map repeatedly.

The tool also adds **static non-convex MeshColliders** to recognizable Snapper
rock meshes in the area. These include the original
`Resources/Islands/SnapperIsland/Rocks` FBX kit and mesh objects named rock,
cliff or formation around Snapper. It does not change rock transforms or UV
mapping. If your specific main formation isn't identified, select its parent
in the Hierarchy and run **Tools > Open World > Snapper Island > Add Collisions
to Selected Rock Formation**. Save the scene with Ctrl+S afterward. The tool
enables ModelImporter Read/Write for any FBX that requires it.

For authored scenery, the USER PLACED SCENERY hierarchy continues to belong to
the human user: **future code must not move, delete or replace those objects**.
These collision additions are limited to the change specifically requested.

Finally, commit the updated PrototypeWorld scene, generated TerrainData,
backup's required asset .meta files, and rock changes to GitHub. An unpublished
local Unity scene cannot be modified directly from the remote repository.

Unity Editor/Android visual and physics validation must still be done locally.
