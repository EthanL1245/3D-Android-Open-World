# Authored tandem rowboat

This update does not rebuild scenes/marinas or modify any FishingTuning CSV.
Unity imports `RowboatSetup` automatically after scripts compile. No Tools setup
is necessary. The build preprocessor also checks installation. Generated assets
are native Unity meshes, materials, motion data and a prefab. Blender is not
required on the game developer's machine or on Android.

## Shop and gameplay

Order: Raft (1 occupant), Rowboat (2), Sailboat, Yacht. Existing permanent unlock
IDs and saves are unchanged. Rowboat ID is `rowboat`; default price 900 coins,
solo speed 4.5 m/s, tandem speed 9 m/s. Edit Rowboat.asset to tune cost/solo speed;
tandem is always 2x solo. Installation preserves existing Rowboat data tuning.
The source Raft.asset capacity and setup default are both 1.

Board and press ROW BOAT near the seat; movement input rows. STOP ROWING releases
the seat for walking/fishing. The front rower steers. A second BoatPassenger can
claim the rear seat; only held movement input contributes a second paddle/speed.
An idle passenger does not increase speed. When the front rower leaves the helm,
the remaining rower takes the front seat. With zero input the boat coasts naturally.

IMPORTANT: the repository currently has no networked player implementation.
Two independent BoatPassenger actors are supported, but two human clients cannot
use this together until multiplayer replication/authority and remote input are
implemented. This update does not claim to add multiplayer. Both actors must
be boarded on the same authoritative BoatController. Future network code must
synchronize boat physics, occupants and rowing input and handle disconnects.

## Exact source and texture mapping

The two supplied .blend files are preserved in ArtSources/Rowboat. Native mesh UVs
and normals are exported without remapping. `Plane` is the identical stationary
hull in both versions and uses ONLY `Rowboat Textures.png`. Every oar mesh uses
ONLY `Oar Texture.jpg`:

- Solo: Empty.002/Plane.001, Empty.003/Plane.002.
- Tandem adds Empty.001/Plane.003, Empty.004/Plane.004.

The hull is instantiated once. Separate solo/tandem oar groups use the respective
supplied animations. Only one group is visible: one active paddler -> solo front
pair; two -> both pairs; none -> retain the last visible configuration and pose. There is no stationary duplicate oar
mesh, hull overlay, or synthesized rowing motion. Oars have no physics colliders.

The action's full authored loop is frames 1–37 at 24 fps (1.5 seconds). Frame 37
matches frame 1 exactly. The saved preview range ends at 35, which would cut off
the end of the loop. Motion is baked at 96 samples/sec including quaternion
rotation, then interpolated; it pauses when input stops and retains cycle phase
when switching modes. Speeds do not speed up or distort the supplied stroke.

Exports use Blender 5.2.0. The solo source was saved in Edit Mode: the exporter
flushes Edit Mode before reading the hull UVs. This step is essential. Coordinate
conversion is (-Blender Y, Blender Z, Blender X), with hull center/waterline offset.
No Blender/FBX importer dependency is shipped into Assets.

To regenerate from repository root (replace BLENDER with executable path):

    BLENDER -b -t 2 --python Tools/Rowboat/export_rowboat.py -- ArtSources/Rowboat/Solo.blend Assets/_Game/Boats/Rowboat/Source/Solo.json
    BLENDER -b -t 2 --python Tools/Rowboat/export_rowboat.py -- ArtSources/Rowboat/Tandem.blend Assets/_Game/Boats/Rowboat/Source/Tandem.json

If changing source geometry/motion, increment RowboatPaddleAnimator.SourceVersion
and the matching RowboatSetup version comparison so assets rebuild once. Existing
prefab GUID is preserved by SaveAsPrefabAsset. ModelContainer separates visuals
from hull/deck colliders and rowing anchors.

## Verification and diagnostics

Before publishing: source mesh/UV comparison confirmed identical hulls; both
stroke exports contain 145 samples per part, nonzero movement and seamless end
poses. Baked-pose comparison against Blender at intermediate sample times is
checked by verify_export.py. Textured source render inspected. C# syntax checked
with Roslyn. Unity editor/Android runtime validation still needs a real Unity run.

After pulling, let Unity finish import, enter Play and buy/equip the rowboat.
Check solo rowing/steering, stopping/coasting, walking/fishing, shallow-water
rejection, occupied relocation rejection and travel cleanup. Confirm the raft
still operates and shop ordering/capacity is correct.

For a diagnostic, Tools > Open World > Check Rowboat copies a concise report.
Send that report plus the first Console error if anything fails. This command
only checks/installs rowboat assets; it never rebuilds the world.

For the supplied tandem-animation visual check in Play Mode, select the spawned
rowboat, open RowboatPaddleAnimator's component context menu and select Preview
two paddlers. Preview one paddler checks the front-only layout; End rowing preview
returns to actual input. These editor-only previews do not add occupants, grant
speed, create multiplayer, or exist in Android builds.

## Rowboat correction (source version 2)

Hotbar text fits the actual slot rectangle automatically. Idle rowing keeps the
last oar pose/configuration, starts at the supplied first pose, and resumes the
same stroke phase. The float target samples waves across the whole hull and
adds 0.38 m; a minimum-height guard protects the shallow interior from rising
crests. Raft buoyancy is unchanged.

Collision.json contains 34 convex pieces derived from the stationary source mesh:
clipped floor/side sections plus both benches and all four fixed mounts. Each
piece is below PhysX's convex triangle limit. The hollow interior is not filled
by a single convex hull. Oars remain non-colliding. Feet anchors avoid benches;
entering a seat moves directly to its clear anchor, while ordinary walking
continues to collide normally. Only upward-facing contacts count as deck support.

Run `python Tools/Rowboat/build_collision.py` from the repository root to regenerate
collision data after changing the exported hull (numpy/scipy required only for
this offline tool). Unity automatically upgrades the prefab to version 2 on
import, preserving its GUID and existing BoatData prices/speed. Do not rerun
Setup Boat System. Check Rowboat remains available for diagnostics.

Checks: geometry tests verify solid floor/benches and empty rowing spaces; all
34 convex pieces have at most 52 triangles. C# syntax passes. Unity and Android
play-testing is still required, especially swim-under collision, boarding,
walking over benches, and flotation near Brinebreak's larger waves.
