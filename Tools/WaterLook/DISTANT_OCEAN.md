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

For the current asset the extension has 1,932 vertices and 3,208 triangles.
The first ring retains the boundary density; subsequent rings reduce it with
stitched joins. Displacement and wave normals fade to flat over 225 m outside
the boundary, before the broad outer triangles. Distant lighting uses the
original improved shader's deep-water tint, Fresnel and distant sun glint.
This is opaque distant water: shallow bottom transparency and contact foam
outside the original patch are intentionally omitted. The simplified shading
can therefore differ at a distant shallow coastline.

A very sparse outer ring reaches 100 km from the patch edge. Water-only haze
starts at 900 m and reaches its full strength at 6 km. The shader clamps only
its own clip-space far depth; it does not increase camera/world render distance.
It reconstructs the true per-pixel depth before clamping so distant triangles
cannot incorrectly cover closer islands. Both normal GLES and reversed-Z depth
conventions are handled. The skybox renders before this transparent-queue,
opaque-output pass, so the far-plane ocean also covers the sky below the horizon.
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
joins, exact total area, odd-grid topology, depth ordering and shader source
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
