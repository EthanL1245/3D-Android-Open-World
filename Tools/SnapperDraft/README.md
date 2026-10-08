# Snapper Island — stage one visual review

## Open the draft

1. Pull `main` and allow Unity to finish importing/compiling.
2. Exit Play Mode. Choose **Tools > Open World > Snapper Draft > 1 - Open or Create Stage 1**. Save your current scene when Unity offers.
3. Review the main formation in **Scene view**. The same menu has **Front**, **West**, **Back**, and **Overhead** views. Send screenshots of these views for approval before further detailing.
4. Move any named rock parent in **STAGE 1 - Main connected formation**. Its imported FBX child retains the supplied geometry and UVs. Sculpt or paint **Sculptable Sand - Stage 1** using normal Terrain tools. Save with Ctrl+S.

The first run creates and saves `Assets/_Game/SnapperDraft/Review/SnapperFormation_Stage1.unity`, its TerrainData, materials and terrain layers. Later runs open the saved scene without regenerating anything. Keep the entire generated `Review` folder and its `.meta` files to share edits between computers.

This is an isolated **Scene-view art draft**, not a gameplay scene. Do not use Play Mode for the art review: the fishing game's global runtime installers still exist. Reopen `Assets/Scenes/PrototypeWorld.unity` to play the game. The draft is not added to build settings. Integration into the fishing map is intentionally deferred until the main shape is approved.

## Authored composition

- 21 individually specified placements, all six supplied FBXs; no scatter or random placement.
- Original proportions with uniform scales of 0.90–1.15. The narrow Rock 3 is horizontal, not an upright pillar.
- Main retaining faces, lower west buttresses, overlapping basal slabs and a submerged continuation.
- Rock caps at 0.28–3.35 metres above sea level; sand shelf at most 2.30 metres.
- Approximately 34 by 26 metres of emergent terrain, with a quiet beach foundation awaiting review.
- Ordinary saved GameObjects with MeshColliders, plus a real sculptable Terrain with a TerrainCollider.
- Four Scene-view viewpoints, also stored as named transform bookmarks.
- Flat review water is only a visual sea-level reference, not the game's ocean system.

`Assets/_Game/SnapperDraft/Stage1Layout.json` contains the explicit placements used for first creation. After creation, edit the scene directly; changing JSON deliberately does not reset a saved draft.

## UV correction and preservation

The six legacy FBXs in `Assets/Resources/Islands/SnapperIsland/Rocks` were ASCII geometry-only conversions: they have no UV layer. Their box-projection shader also ignores UVs. The supplied `Rock Collection.zip` includes original binary FBXs containing valid authored UV0 maps and the original texture image.

`Assets/_Game/SnapperDraft/Source` contains byte-for-byte copies of those original binary FBXs and that texture. The draft uses URP/Lit at texture scale 1, offset 0, with no UV regeneration, UV swapping or box projection. Source-specific import settings preserve normals, disable mesh compression and enable Read/Write for collider cooking. The editor command explicitly reimports and validates all six source assets before building the draft.

The original game paths and scene are deliberately not overwritten at this stage: replacing the old converted FBXs in place can change imported mesh file IDs referenced by locally saved scenes. The draft imports the originals under new GUIDs, so the current fishing map and all existing local placements remain intact. Approved integration must replace only Snapper Island's scenery/terrain patch and repair the corresponding mesh/material references, preserving other islands and gameplay.

## Verification and limits

Verified outside Unity: all six restored files and the texture match the supplied archive byte-for-byte; every FBX has a noncollapsed UV layer and valid UV indices; all six source types are used; scales are uniform and moderate; a mesh-based shape preview was inspected from four angles and the sand/rock intersections revised. Bounds overlap checks find one connected placement group, but bounds alone do not prove exact mesh contact.

At draft creation Unity checks source UV0/readability, assigns exact mesh colliders, confirms collider mesh references and saves the terrain assets and scene. **Unity is not installed in the authoring workspace**, so compilation, final materials, physics and editor persistence have not been executed here. Review these in Unity: inspect seams from the four views, check for Console errors, move a rock and sculpt terrain, save/reopen, and check the edit persists. Visual approval remains pending; full island detailing and gameplay integration have not been performed.
