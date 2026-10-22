# Water-shader contact foam

The ocean shader now draws textured foam at its live intersection with opaque
scene geometry. Pull the update, let Unity compile, and enter Play Mode or build.
**No bake, regeneration, added source component, or scene setup is required.**
New rocks and terrain edits that render into the camera depth texture are picked
up automatically. Both ocean look variants use the replacement.

The existing OceanWater inspector exposes **Water Contact Foam**:

- Strength: 0.9 by default; set to 0 for an A/B comparison or to turn it off.
- Width: 0.6 m depth-contact range by default.
- Texture Scale: 1.1 repeats per world metre by default.

Width is a depth-contact threshold, not a guaranteed horizontal band width.
Apparent thickness changes with camera angle and slope. Foam is drawn in the same
pass on the actual displaced water surface, so it does not float independently
of the ocean or remain attached to the mean-waterline contour.

## Old baked foam

Existing `ShorelineFoam` components retain their serialized fields and GUID, but
now hide their old mesh renderers in edit and play mode. There is no material
instance, CullingGroup or Update loop for these retired strips. They are no longer
rendered or generated. Old bake assets are retained to avoid missing references.
Optional cleanup: **Tools > Open World > Shoreline Foam > Remove Retired Baked
Foam From Scene**, then save. The cleanup supports Undo and does not delete assets
which might be referenced by another scene. The old baker and its validation menu
have been removed.

## Boats and marina

BoatController registers each boat hull after initialization. The fitted marina
registers its bounds after placement (including the fallback/original dock).
These are conservative moving XZ exclusion boxes for contact foam only; the
existing motorboat wake is unchanged. Foam on nearby shoreline inside those boxes
is also suppressed. No colliders, physics layers, boat mechanics, or rock/terrain
transforms are changed.

The previous marina obstruction fix remains: renderer bounds belonging to old
ShorelineFoam roots cannot obstruct marina placement.

## Cost and limits

- Reuses the depth sample already used for ocean fog. No additional camera,
  render target, depth pass, draw call or foam mesh.
- Two samples from one repeating, mipmapped 256x256 RGB texture, on contact
  fragments. Branches skip texture samples outside the depth band/distance range;
  exact savings depend on GPU execution of adjacent pixels.
- Fades from 66 to 120 m. Explicit mip sampling uses derivatives calculated before
  divergent branches, to reduce shimmer at shallow viewing angles.
- A bounded shader list supports 16 nearby boat/marina exclusion boxes. If that
  capacity is exceeded, contact foam is disabled for that view until space opens;
  gameplay, water and boat wakes are unaffected. Only registered exclusions are
  updated; there is no per-frame search over world rocks or terrain.

This is screen-space contact foam. It depends on opaque geometry writing depth;
transparent objects without a depth pass do not create an edge. Other opaque
props can also create contact foam unless excluded. Boats and the marina are
explicitly excluded. Extremely shallow views and fine geometry can expose normal
screen-depth limitations. It is not a physical surf or wet-rock simulation.

## Verification

Static source/property/resource checks were run without Unity. Unity and Android
are not installed in the implementation environment, so shader compilation,
visual quality and device frame time still require validation:

1. Open the world with a previously baked foam root. Confirm its child renderers
   are disabled and no duplicate strips are visible.
2. In Play Mode, inspect Bluewater Cay and Snapper Island during full wave cycles,
   from above and near water level. The contact line should move with the water.
3. Move/add a rock or sculpt terrain in edit mode, then enter Play Mode again.
   No regeneration should be needed.
4. View rowboat and motorboat hulls offshore and beside rocks; check the existing
   motorboat wake still renders. Confirm the marina loads without the foam-related
   obstruction error.
5. Compare Contact Foam Strength 0 versus 0.9 on the target Android device from
   identical shoreline views; record GPU/CPU frame time and inspect visual aliasing.
