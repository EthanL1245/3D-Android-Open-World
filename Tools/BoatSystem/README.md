# Suncrest Marina and deployable boats

## Install

Pull main, open the saved Suncrest Reef / PrototypeWorld scene, exit Play Mode, then click **Tools → Setup Boat System**. The command saves the scene and creates the materials, BaseBoat/Raft, Sailboat and Yacht prefabs in `Assets/Resources/Boats`. The three BoatData assets are included in source control; setup links their generated prefabs. No manual component wiring or layer assignment is needed. Unity-generated prefab/material meta files are created alongside those assets by the editor. Re-running setup replaces only the generated marina/query surface and preserves existing boat prefabs, meshes, prices and tuning.

Play and approach **SUNCREST MARINA**. Press the on-screen MARINA SHOP button (or E), buy a permanent boat, and select hotbar **slot 2**. Aim at water: green means clear; red means blocked/shallow. Click or tap PLACE BOAT. Slot 2 cancels. Buy/equip another owned boat at the marina. Prices: Raft 150, Sailboat 1,800, Yacht 9,000 coins. Existing saves start with no owned boats; no currency or catches are reset.

Approach the stern and press BOARD BOAT if needed. At the seat press DRIVE BOAT; use the existing joystick or WASD. Press LEAVE HELM to walk and fish on the deck while the hull coasts. Jump over the low gunwale to disembark. Only one driver can hold the helm. Capacities include the driver: 2 / 4 / 8. The current game is single-player; this does not add networking or replicated passenger authority.

## Architecture

- `BoatData`: stable ID, cost, speed, capacity, footprint/draft and prefab. Swap artwork under `ModelContainer`; leave the independent deck/hull colliders, DriverSeat and DeckExit in place. If a replacement changes hull size, update the colliders and BoatData footprint/draft together.
- `BoatSystem`: slot selection, mouse/mobile placement, marina UI, one active boat, and travel cleanup. Occupied boats cannot be relocated. Switching boat types deactivates the old hull before spawning the replacement.
- `ShopLedger`: permanent owned boat IDs and equipped ID share the existing shop save/backup with coins. Duplicate purchases, negative prices, insufficient funds and unowned equips are rejected. ShopProgress's read-only recovery protection is respected.
- `BoatClearance`: Water-layer ray, terrain depth samples over the expanded footprint, and an oriented OverlapBox including terrain and non-trigger obstacles. Requires known terrain, excludes ponds, and reserves a 1 m horizontal margin plus 0.8 m under-keel clearance. Placement rejects intervening walls/rocks. A moving hull checks its predicted path/turn and brakes before insufficient clearance.
- `BoatController`: Unity 6 Rigidbody forces/torque, linearDamping/angularDamping (Unity 6 names for drag/angularDrag), wave-height buoyancy and strong lateral resistance. No kinematic steering or hard velocity zeroing for normal stopping.
- `BoatPassenger`: applies the deck's position/rotation delta through CharacterController.Move before player movement. This avoids parenting a CharacterController into the Rigidbody hierarchy. At the helm walking pauses; on deck the existing walking, camera and fishing code remain active.
- `ShopDimensionManager`: every existing teleport destroys the active hull and detaches the passenger. Ownership persists. Return travel to the island uses IslandDockArrival instead of the previous offshore position. Travel to Home/Quay still uses those destinations' own arrival points.
- Setup verifies Water/Terrain layers, searches shoreline candidates against colliders AND renderer bounds (including non-solid bushes/rocks), adds a ramp and a clear arrival point. It never deletes scenery to force a fit. Failure to find a site stops before scene changes with a clear message. The separate Water-layer trigger is only a placement query surface; existing movement and fishing rays ignore triggers.

## Validation

Run `dotnet run --project Tools/BoatSystem/Checks/Checks.csproj -- <repo-root>` for the actual ShopLedger purchase/equip checks and Roslyn syntax checks of project C# sources. These are not a Unity build or physics test.

Unity/device acceptance checks:

1. Run setup twice: one marina, one Water query surface, unchanged boat ownership/assets. Check ramp, lettering, counter and arrival are clear of scenery.
2. Buy, equip, restart: coins charged exactly once, permanent unlock preserved. Try insufficient funds and switching boats.
3. Try dry land, shallow shelf, a rock at the hull edge, pond and open reef water. Only the fully clear/deep footprint may turn green or place. Test both desktop click and mobile PLACE.
4. Place twice: only one hull. Occupied hull cannot move. Board from water, drive, release joystick and observe gliding stop; turn near shore and verify braking.
5. Leave helm while moving: walk/jump and cast from the deck without sliding off or entering swimming. Test reef waves and phone frame rates.
6. Travel while aboard/driving/placing: boat disappears, no stuck input or parenting. Return to Suncrest: stand at marina dock; boat remains owned and can be redeployed.

Unity Editor/Android are not available in the remote code workspace, so generation, full Unity compilation, physics and visual acceptance must be checked in the editor after setup.

## Wave-following hull rocking (October 13, 2026)

Deployed rafts, rowboats, sailboats and yachts now pitch/roll from four surface
samples near the bow, stern, port and starboard each FixedUpdate. A damped
angular spring rotates the physical Rigidbody and deck, not just the model.
Limits are 13°, 11°, 8° and 5° respectively. Existing wave amplitudes and
appearance remain unchanged. No new render passes, textures, mesh density or
objects are introduced.

Steering, lateral resistance, thrust and clearance continue using horizontal
yaw axes despite the tilted hull. Rowboat anti-flooding height accounts for
the downward movement of its rotated hull, and BoatPassenger already carries
the full boat rotation. All boats rock even without any driving input.

After pulling, verify rocking at anchor and underway, boarding/rowing and
walking/fishing on deck, crest water not clipping through the rowboat,
collision clearance near shore, and Android frame timing. Source inspection
is not a Unity build or device benchmark: the <=5% FPS target is unverified.

## October 13: smooth wave crossing, no false "wall" braking

- The rowboat no longer chases the **maximum crest height at 27 hull points**
  or instantly writes its Rigidbody Y position/vertical velocity. The authored
  hull now uses the same four points as wave rocking plus a centre sample,
  averages the target surface and adds 0.30 m centre freeboard (the shallow interior floor is about 0.247 m below the root). A damped vertical
  spring with a small immersion-dependent lift allows some water entry at a
  breaking bow but recovers without snapping or stopping horizontal travel.
- All other boats keep their previous vertical buoyancy spring; pitch/roll
  stabilization remains as previously implemented.
- Navigation obstruction checks are evaluated at the still-water elevation,
  so wave crests and troughs cannot change the clearance overlap height.
  Underwater terrain depth, shore obstacles and actual solid collisions are
  still enforced by BoatClearance and the Rigidbody.
- Four rocking samples are reused for rowboat flotation, replacing the old
  27 additional hull-surface samples with only one additional centre sample
  per FixedUpdate. This reduces CPU height-query work for the rowboat.
- Verify forward progress into swell in deep open water, wave crossing on the
  reef shelf, actual braking near shallow terrain and rocks, stable occupants
  while steering/fishing, and the Android <=5% FPS target. Unity/device runtime
  validation is still required.

## Follow-up: wave crossings and rowboat interior

The last buoyancy change left the shallow interior too close to the wave
surface. Rowboat flotation now uses a raised target relative to the **highest
wave beneath the hull**, including bow/stern, both sides, and four corners.
The four rocking samples are reused. The physics spring remains continuous
with bounded acceleration and never teleports the Rigidbody. Resting freeboard
is 0.50 m above the highest sampled waterline; the authored interior floor
is roughly 0.247 m below the hull origin.

The previous runtime navigation code used a *placement* check with a broad
collider OverlapBox, which can create false-positive instant braking in open
water. Navigation now checks stable seabed depth only. Solid colliders on
the shoreline, rocks and dock still stop real physical contacts. Strict
full-hull placement validation is unchanged. Out-of-terrain open water
no longer synthesizes a physics wall.

Test at deep-water swells, rough-water biome boundaries, near a shallow
coast, and around rocks/dock. Watch the rowboat floor, player boarding,
fishing, and Android FPS. Unity runtime and FPS still need device verification.

## October 13: resilient boat crossings through consecutive big waves

A rowboat no longer tries to levitate its entire hull to the highest bow or
corner crest during a big offshore swell. The previously unbounded highest
crest target created huge vertical accelerations as the bow dropped into the
next wave. Instead, the target follows the centre sample, with at most 0.65 m
extra lift from the bow/edge; some temporary bow immersion is deliberate.
A speed-limited buoyancy target and a vertical-only damped spring keep the
up/down response smooth without teleporting the Rigidbody.

The second defence preserves the last legitimate forward speed when an input-
held boat abruptly loses momentum in open water. It touches ONLY the forward
horizontal velocity component, never the vertical motion or lateral drift.
It does not run during turns, throttle release or shallow-water braking, and
only activates after a non-alloc physics overlap confirms no real solid
obstacle ahead. Real shorelines, rocks and docks still physically stop boats.
If a legacy OceanWater GameObject carries an unintended collider, hull/water
collision pairs are ignored upon boat initialization.

The one-time water collider setup adds no runtime graphics work. The
contact query runs only when a sudden forward slowdown is detected. Android
and Unity runtime acceptance are still required; no FPS or gameplay
success claims without device tests.

## October 14: camera follows boat rocking

While aboard a raft, rowboat, sailboat or yacht, the first-person camera now
inherits the hull's interpolated pitch and roll, smoothly damped at the camera
without rotating the player's CharacterController. Works while driving, rowing
and walking/fishing on deck. Existing mouse and mobile look yaw stays independent
of the boat's yaw, and normal look pitch remains user-controlled. On disembark,
teleport or boat removal, the camera levels gradually back to normal.

Tuning on `FirstPersonController`: Boat Camera Tilt Amount defaults to 1.0
(full hull tilt); Boat Camera Tilt Follow Speed defaults to 10.0. The tilt is
applied in LateUpdate after movement and input, including when menus block
look input. Neither floating, collision detection nor camera position changes,
and no mesh, material, or extra rendering work is added.

Validate in Unity/Android: traverse waves while at helm and on deck, test
rotating the view and looking up/down, then disembark and inspect horizon
leveling. Check fishing aim and underwater transitions. This change has not
been play-tested on an Android device.


### Motorboat passenger and wheel presentation (October 20)

The supplied `Female Being a Motorboat Passenger.fbx` is imported as a generic
clip with preserved hierarchy and validated against the existing female rig.
`FishingRemoteAvatarSetup` resolves its actual clip reference on editor load,
reimport, and before builds. No source animation joints are rewritten.

The passenger pelvis uses the driver's previous cushion height and +0.05 m
fore/aft offset, translated laterally to the modeled right cushion. The driver's
visual root uses the original cushion-based placement plus 0.02 m toward the
wheel (0.07 m total forward offset); automatic palm-to-wheel alignment is removed.
Both arms keep their authored animation. The passenger's Shin.L and Shin.R bones
receive a 15-degree clockwise visual rotation viewed from the driver's left chair,
after each animation sample, around the boat's right axis. Both seats
follow hull pitch/roll/yaw, and both first-person rendered cameras use their
animated head position without changing the simulation/steering camera.

Validation here: all 32 server tests pass, including motorboat occupied-helm
requirement, contested seats, zero passenger input, stand/re-sit, driver exit,
owner riding while a guest drives, boat removal, and unchanged raft/yacht seats.
The passenger FBX's 50 model paths all exist in the original character FBX.
Unity is unavailable in the editing environment; check both rebuilt clients in
Play Mode/on device for final hand contact, cushion placement, bank/turn follow,
first/third-person toggles, stand-up and disconnect behavior.

## Procedural motorboat wake

`BoatController.Initialize` attaches `MotorboatWake` only to the motorboat, for
both locally simulated boats and remote multiplayer replicas. This is entirely
visual: no collider, forces, rigidbody writes, input changes, or network messages.
Placement previews do not initialize a wake. All existing motion/seating is intact.

The effect draws central propeller foam and two outward-spreading ribbons. It
records world-space stern positions so turns leave curved trails. Strength grows
with measured speed up to 27 m/s; yaw rates up to the existing 32 degrees/second
increase ribbon width, breakup, and outside-turn foam. Emission stops below
0.7 m/s, old foam dissolves over four seconds, and a new strip starts after a stop.
Teleports and sample gaps over 0.4 seconds clear history instead of drawing a
long streak. The stern offset uses the already-scaled HullSize (not TransformScale).

Mobile budget per boat: 80 fixed history entries, at most 720 vertices / 948
triangles, one transparent draw, no shadows, physics probes, depth-texture reads,
particles, or fluid simulation. Geometry updates at most 20 Hz, dropping to 10 Hz
and alternate path samples beyond 65 m from the wake bounds. Foam fades between
100–140 m per vertex; sampling stops beyond 250 m from the boat (including space
for the 108 m full-speed trail). Unity also frustum-culls the mesh. Wave movement
and fading continue in the shader between mesh uploads. Arrays are reused.

A 64x64 repeating noise texture with mipmaps is generated once and shared with
one material across active wakes; both are released when the last wake is destroyed.
The Resources material references the shader explicitly so Android shader stripping
does not remove it. No imported model, image, or manual Unity setup is needed.
The shader uses OceanGerstner.hlsl and copies the ocean's current property block
after its LateUpdate, including island wave phases and cockpit stencil masking.

Validation before release on phones (Unity/device execution is required):
- Drive straight at low speed, then 27 m/s; check propeller origin and stronger foam.
- Hold sharp turns both ways; check curved history and stronger outer-side foam.
- Coast, stop, restart, remove and replace the boat; old trails must fade/clear.
- Observe a second player's boat; both clients should see the interpolated wake.
- Cross normal and offshore storm waves and view from first and third person.
- Check close, distant, and underwater views; foam must fade without cockpit bleed.
- Profile on Galaxy A71 and S23, especially turns and multiple visible boats.
  Verify no steady-state wake GC allocations and compare CPU/GPU frame times
  with the component disabled. The budget is bounded; zero FPS impact is not a
  claim that can be made without these device measurements.


### Wake visibility and passenger contact correction

PrototypeWorld's PlayerCamera is untagged. Wakes now resolve
FirstPersonController.ViewCamera directly, with Camera.main only as a fallback.
They cache it and retry at most once per second if unavailable. Previously the
Camera.main-only lookup returned null and cleared the wake every frame.
The shader also no longer masks foam by comparing the camera's height against
individual distant wave crests. The geometry/update budget remains unchanged.

While aboard a rowboat or motorboat, BoatPassenger temporarily sets the local
CharacterController.detectCollisions to false. This disables incoming hull
contacts while preserving CharacterController.Move deck/wall collision queries.
All seated riders, including rowboat rowers, suspend their capsule entirely.
Exact platform displacement and seat anchors still carry riders with the boat.
The original incoming-collision setting is restored when detaching, disabling,
or losing the boat; leaving a seat while still aboard keeps contact protection.
No passenger-based ballast, buoyancy, thrust, steering, or wave settings are added.
Existing deliberate rowing input is unchanged. Remote avatar colliders were
already disabled; no new network messages or physics queries are introduced.

Regression playtest: in two-client sessions, board a rowboat and motorboat, both
standing and seated. Drive/row, turn, cross waves, stand up, jump, and disembark.
Neither rider should hold down or obstruct the hull, and both should follow it.
Check the wake from an untagged PlayerCamera in first and third person. These
runtime/device checks require Unity; source inspection alone cannot prove them.

### Plain git pull on the developer PC

Branch tracking is local Git configuration, not a versioned project setting.
For the existing upstream remote, run once in the Unity project folder:

```powershell
git fetch upstream
git branch --set-upstream-to=upstream/main main
```

Then, while on main, future updates need only `git pull`. Save and commit local
Unity edits before pulling if Git reports conflicting uncommitted changes.


### Wake shape tuning

The center ribbon starts 0.10 m forward of its previous stern position, with
30% more initial half-width. Its initial expansion remains 0.25 m/s for 0.65 s;
it then smoothly fans out through age 1.8 s to overlap both side ribbons. The
final width accounts for the side ribbons' farther-forward emission station
and extra spreading time at the same point along a straight wake.

Side ribbons start beneath the authored windshield station (resolved once from
imported windshield renderer centers, with amidships as fallback). Each moves
0.10 m inward, reducing total separation by 0.20 m, and is 50% thicker. Their
outward expansion rate is unchanged. Each sample stores separate center and
side origins in world space, so both retain their traveled paths in turns.

Vertex/triangle counts, update rates, material count and physics are unchanged.
Wider foam covers more pixels; actual GPU cost still requires device profiling.
Check windshield alignment and overlap visually in Unity at low/full speed and
on both turn directions; source/numerical checks do not replace that playtest.
