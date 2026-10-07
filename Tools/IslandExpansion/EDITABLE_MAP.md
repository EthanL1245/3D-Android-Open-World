# Editable main fishing map

1. Exit Play Mode. The command finds your active/loaded fishing map, or opens `Assets/Scenes/PrototypeWorld.unity` automatically. If multiple scenes are loaded, Unity offers to save modified scenes before isolating the map. Cancel leaves them untouched.
2. Run **Tools > Open World > Make Main Fishing Map Editable** once. Wait for terrain generation and asset saving to finish.
3. In the Hierarchy, expand **Island Expansion**, **Snapper Island Runtime > Snapper Coastal Rockscape**, or **SuncrestReef**. Select a rock's named parent to move/rotate/scale the complete piece. Select the terrain to sculpt or paint it.
4. Save with **Ctrl+S**. Play and Android builds use this saved terrain and object layout. Normal Unity Play Mode object edits are temporary; do placement work outside Play Mode.

The conversion preserves the current Suncrest installation, generates the existing expansion and Snapper island once, and saves their terrain, materials, textures, transforms, colliders, arrival points and discovery volumes. Thereafter startup only restores gameplay references; it does not regenerate terrain, re-ground props, or replace deleted rocks. Running the conversion again on a completed map does nothing to its layout.

Generated assets and a pre-conversion scene copy are under `Assets/_Game/EditableFishingMap/Map-<timestamp>/`. Commit the edited scene and this folder (including all `.meta` files) to share the map with another computer. Keep the generated assets: the saved scene references them. If conversion fails, reopen `BeforeConversion.unity` and save it as your original scene path before retrying.

## Future island work

The original procedural generators are retained for unconverted scenes. New island work can still be authored in code and generated into the saved map. On a converted map, use an additive island generator that edits only the new island's terrain region and creates a new scenery root, then persist its generated assets and save the scene. Preserve existing island roots and avoid rebuilding the shared terrain from the old recipe. New species, discovery and travel entries still need their normal gameplay configuration.

Changing an old generation recipe deliberately does not overwrite a saved map: apply future redesigns only to the requested island. The legacy Suncrest reinstall command is a separate destructive redesign workflow; do not use it to refresh an edited map.

## Verification in Unity

After conversion, move and rotate one Snapper rock, sculpt a small terrain patch and save. Enter Play twice, then close/reopen the scene and check both edits. Check Brinebreak/Bluewater/Snapper arrival and discovery, rock collision, and an Android build. The implementation was source-reviewed outside Unity; these runtime/editor checks require the Unity editor.
