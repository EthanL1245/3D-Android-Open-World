# Manual scenery: source of truth

The Android build loads `Assets/Scenes/PrototypeWorld.unity`. The islands are
generated at runtime until you run **Tools > Open World > Make Main Fishing Map
Editable** in the Unity Editor (once, outside Play Mode). That tool makes a
pre-conversion backup and persists the generated terrain, layers, materials,
meshes, and scenery in the main scene. Press Ctrl+S. Never edit terrain and
rocks only in Play Mode; changes made there do not persist.

## Protected hand-placed objects

Place Blender FBX rocks, trees, structures and other user-authored objects as
children of the **USER PLACED SCENERY - DO NOT MODIFY BY CODE** root. You can
drag them in the Hierarchy, or select existing scenery and use
**Tools > Open World > Protect Selected Scenery**. The root is created by the
map-conversion command and never regenerated. Save the Unity scene after changes.

**Mandatory future code rule:** Treat that root and every descendant as
human-owned data. Do not destroy, reposition, reparent, rescale, swap materials,
replace meshes, or procedurally scatter into that hierarchy. Code should
inspect and work around those objects. Add *new* AI-authored content under
separate generated roots; do not modify existing user-authored objects. The
`UserPlacedScenery.Contains(transform)` helper identifies protected objects.
Some legacy generators are disabled on editable maps for safety.

Whenever `PrototypeWorld.unity` is saved, the Editor exports
`Assets/_Game/EditableFishingMap/UserPlacedSceneryManifest.json`.
That manifest lists manually placed top-level objects, asset paths, IDs, and
transforms to make them discoverable from GitHub. Nested mesh parts remain in
the scene/prefab. Commit and push BOTH the `.unity` scene and the generated
manifest when making manual edits on your own computer; a local Ctrl+S does
not transmit scene changes to GitHub or this assistant.

Do not rebuild or reinstall Suncrest / use legacy Visual World Pass on a
baked scene. To extend the world, write compatible additive generators instead.
