# Distant ocean extension

The authored 600 m ocean grid, OceanWater shader/materials, camera far clip,
water physics, swimming, shoreline foam, and scene objects are unchanged.
OceanWater.OnEnable installs a visual-only DistantOceanRuntime in Play Mode.
No setup/rebake command is needed after pulling this change.

The runtime builds one hollow mesh around the original grid. Its innermost
640 vertices are copied directly from the mesh boundary. Both surfaces use
OceanGerstner.hlsl and the original renderer's current property block, including
integrated phases, biome colours and local storm parameters. The child follows
the original ocean's exact recenter transform. It runs after the ocean and biome
LateUpdates, with no separate clock. The original grid remains the entire near
surface; there is no overlapping plane underneath it.

For the current asset the extension has 832 vertices and 1,008 triangles
(previously 1,932 vertices and 3,208 triangles: about 69% fewer triangles).
Only the 640 shared boundary vertices evaluate waves. A 20 m transition strip
joins those moving vertices/normals to a flat, unanimated outer surface. The
outer vertices skip the wave trigonometry. All joins are stitched without
T-junctions, including the transition from 160 to 40 subdivisions per side.
The surface still recenters with the ocean, but its flat geometry and lighting
have no independently moving or scrolling features.

Distant lighting retains the original improved shader's deep-water tint,
Fresnel and distant sun glint, driven by the same main light and biome colours.
A subtle 4% increase in the deep tint's blue channel fades in beyond the seam.
The previous extra distant haze has been removed to reduce the washed-out
horizon. No painted texture is needed. This is opaque distant water: shallow
bottom transparency and contact foam outside the original patch are omitted.
The simplified shading can therefore differ at a distant shallow coastline.

A very sparse outer ring reaches 100 km from the patch edge. The shader clamps
only its own clip-space far depth; it does not increase camera/world render
distance. It reconstructs true per-pixel depth before clamping so distant
triangles cannot incorrectly cover closer islands. Both normal GLES and
reversed-Z depth conventions are handled. The skybox renders before this
transparent-queue, opaque-output pass, so the far-plane ocean also covers the
sky below the horizon.
The shader is under Resources so it is included in Android builds.

## Cost and verification

One draw/pass; zero texture/depth samples, reflection cameras, render textures,
colliders, CPU wave sampling, Update-time mesh construction, or frame allocations
in the extension's steady-state C# path. Mesh and material are destroyed with
the component. Extra screen coverage and fragment depth output still have GPU
cost, especially on mobile tiled renderers: zero lag / a specific FPS delta is
not established by source checks.

Run `python Tools/WaterLook/check_distant_ocean.py` for offline checks against the
actual authored grid: winding, no coverage inside the near patch, manifold LOD
joins, exact total area, odd-grid topology, flat outer vertices, triangle budget, depth ordering and shader source
constraints. These checks model the mesh algorithm; they do not execute Unity
C# or compile the GPU shader. Unity and Android are unavailable in the authoring
environment.

Before release, build both Vulkan and GLES3 if enabled. Compare standing on the
highest mountain, looking down at the boundary, moving across recenter steps,
teleporting, storm/deep-ocean water, underwater and the motorboat. Check that
near water looks unchanged, terrain above water occludes the extension, and
terrain below water is hidden. Compare Galaxy A71/S23 GPU frame times with the
DistantOceanRuntime component enabled/disabled at identical views; disabling
it hides only the extension. Check Frame Debugger for one extra draw and no
extra depth or shadow pass. No phone performance or visual validation is
claimed by the offline checks.
