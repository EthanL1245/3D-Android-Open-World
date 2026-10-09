# Brinebreak 3x smaller; retired coral

The latest PrototypeWorld scene in GitHub already has **68 Coral prefab
instances disabled**. Future Suncrest creation no longer spawns coral. The
Brinebreak generation configuration has radii 31.67 m by 21.67 m, one third
the old radii. Bluewater Cay's generated location still uses its historical
separation from Brinebreak and is not shifted.

**An already baked map cannot be resized by changing source-code values
alone.** Unity serialized its terrain in a binary `TerrainData` asset. The
one-time editor migration does the actual scene/binary update:

1. Pull `upstream/main` and open `Assets/Scenes/PrototypeWorld.unity` in Unity.
2. When Unity prompts to **Finish saved Brinebreak Island update**, select
   **Back up and apply**. If you dismissed it, run
   **Tools > Open World > Apply 3x Smaller Brinebreak and Remove Coral**.
3. Wait for completion. It makes an independent scene + TerrainData backup;
   contracts Brinebreak to 1/3 width and length, scales/retains only roughly
   1/9 of its **generator-owned** scenery, re-grounds it, adjusts arrival
   and land discovery, removes retired Coral instances, and saves the edited
   scene.
4. Inspect in Scene View; test Play Mode and the Android build.
5. Commit `Assets/Scenes/PrototypeWorld.unity` and the NEW
   `Assets/_Game/EditableFishingMap/CompactBrinebreak-*` directory
   (all assets and .meta files), then push to `upstream main`.

The migration is one-time and guarded by a saved marker; future loads do
not repeatedly resculpt the island. Snapper, Suncrest, Bluewater Cay,
and the **USER PLACED SCENERY** root are excluded from transformations.
Preexisting manual terrain edits inside Brinebreak's old footprint necessarily
yield to its requested shoreline reduction, but the original terrain and
scene are independently backed up.

Until the local Unity migration is run AND its changed binary TerrainData
and scene are pushed, GitHub does **not** contain the completely resized
saved terrain. Code and disabled coral scene instances alone are not the
finished map. A pure GitHub connector cannot run your local Unity Editor
to serialize that binary asset.

This update was source-reviewed remotely; test actual terrain and physics
in the Unity Editor.
