# Ocean reference look

Reference: the user-supplied water image and its seven-item checklist, October 13, 2026.
Baseline commit: `753518eb7b74c4b76401c5394577354bdf923d14`.

## Changes

- Turquoise shallows and richer blue offshore water, still modulated by the existing biome tints.
- Depth-dependent transparency and color from the existing depth texture.
- Two moving, world-anchored fine ripple bands perturb the lighting normal only.
- Sun highlights with distant detail fading and screen-derivative filtering to reduce aliasing.
- Broken, restrained crest/shore foam and faint shallow surface light bands.
- Grazing-angle sky color and a softer distant horizon.

The main scene already references `SuncrestWater.mat`; enabling its shader keyword
updates the saved scene's appearance without regenerating or modifying the map.
`OceanWater.mat` is enabled too. Pond and shop materials keep the original shader path.
Wave heights, timing, CPU water queries, boats, fishing hotspots and splashes are unchanged.

This is an inexpensive approximation of the reference, not a photorealistic renderer.
The light bands are surface shading, not projected seabed caustics; the sky tint is
an approximation rather than a reflection of scene objects.

## Performance scope and validation status

The user requires no more than a 5% FPS reduction. **This has not been measured or
certified.** Unity 6000.3.24f1 and an Android test device were not available in the
implementation environment. Source checks are not a Unity shader compilation,
rendered visual test, or GPU benchmark.

Both variants retain one transparent pass and one depth sample per fragment.
No added textures, render targets, cameras, post-processing, particles, mesh density,
CPU frame callbacks, or camera depth/opaque-texture settings. Fine ripples use
arithmetic rather than additional texture fetches or fragment sine/cosine calls.
Eye depth and local crest amplitude are calculated per vertex in the improved path.
The improved path adds fragment arithmetic and one float2 interpolator; their cost
is device-dependent and could exceed the requested budget in a fill-rate-bound view.

The original shader functions are preserved behind the disabled keyword. Both
variants are retained with `multi_compile_local` so the original path is available
even in builds. Original material colors, alpha and biome settings are preserved.

## A/B verification in Unity and on Android

1. Open `Assets/Scenes/PrototypeWorld.unity` in Unity 6000.3.24f1 and wait for imports.
   Confirm no shader/compiler errors for both Vulkan and OpenGL ES 3 if both ship.
2. Check the improved look from the starter beach, shallow seabed, low sun angle,
   offshore horizon, Brinebreak rough water and underwater. Check moving the camera
   and crossing ocean-follow snap boundaries for shimmer or sliding patterns.
3. Use **Tools > Open World > Water Look > Use Original Look (Performance Baseline)**.
   Build the baseline APK. This selects the original shader, with original colors.
4. Use **Tools > Open World > Water Look > Use Improved Look** and build the candidate
   with identical player settings, resolution, graphics API and quality settings.
5. On the same target phone, compare the same camera positions/routes in the above
   locations, including a screen mostly filled with water. Use an uncapped test
   configuration where supported; a fixed 60 FPS cap can conceal a GPU regression.
   Keep thermal state comparable, warm each build for 30 seconds, then capture at
   least 60 seconds per case. Alternate A/B order and repeat three times.
6. Compare median FPS across runs for each case. Candidate FPS must be at least
   `baseline FPS * 0.95` (60 -> 57, 90 -> 85.5). Also inspect GPU frame time and
   slow frames. A 5% FPS loss corresponds to a 5.263% frame-time increase.
7. If any case fails, select the original look for that device/build while tuning
   and retesting. Do not mark the performance requirement passed without captures.
   Leave **Use Improved Look** selected when preparing the improved candidate.

No benchmark values or in-game screenshots have been invented for this change.
