# Metal Spoon and crankbait presentation

Metal Spoon replaces the former Deep Flash slot (`lure:3`). Existing ownership,
price, bite chances, preferred-fish tables and size bias are unchanged. It uses
the shared cast/retrieve path, depth, line placement and held-REEL animation speed.
Other crankbaits receive a fixed +10 degree roll about the retrieve direction,
clockwise when viewed from the approaching lure's player-facing end. This is
applied before the authored animation and does not change the retrieve heading.

The original package is preserved in `Source/MetalSpoon.zip`. Blender 5.2 exported
both body and hook geometry, original UV0/split normals, and both authored actions
into `Assets/_Game/Fishing/MetalSpoon/AuthoredSpoon.json`. The hook retains its
one-frame phase offset within the repeating cycle. The supplied texture is copied
byte-for-byte. The spoon receives metallic silver materials and its line attaches
to the small eye at the end opposite the hook, following body animation.

`MetalSpoonImporter` creates the native meshes, looping Animator clip/controller,
materials and `Resources/Fishing/MetalSpoon.prefab` automatically after Unity
compiles. It also checks before Play and Build & Run, including batch builds.
Blender is not required on the game developer's computer. Existing complete spoon
assets are left intact; explicit re-generation is available through
**Tools > Open World > Rebuild Metal Spoon**. The old Deep Flash import tool cannot
overwrite the new resource path. Shop, equipped-bait and fish-index previews use
the same new prefab, with an explicit broad-face preview orientation.

Validated outside Unity: original UV/normal/geometry export, 800 triangles,
73 samples per part, both actions move and loop continuously, non-hook line eye,
clockwise rotation sign and unchanged numeric fishing tuning. Unity Editor and
Android Play Mode/rendering must be checked in a Unity installation.
