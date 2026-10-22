# Baked shoreline foam

Open `Assets/Scenes/PrototypeWorld.unity`, leave Play Mode, then run
**Tools > Open World > Shoreline Foam > Regenerate Shoreline Foam**.
Save the scene. First import alone does not generate foam. A Unity editor bake is
required before playing or building. Regenerate after moving/adding rocks,
sculpting terrain, changing the ocean base level, or changing foam width.

The generated root also has a **Regenerate Shoreline Foam** Inspector button.
Defaults: 0.65 m width, 2.8 s wash, distance fade from 85 to 140 m, 256-square
tileable breakup texture. Width changes require regeneration. Other settings are
applied in Play Mode. Scene view shows the flat, animated mean-waterline band;
Play Mode supplies the ocean's live wave parameters.

## Sources and ownership

All active terrains in the active scene are read. Active MeshFilters whose
hierarchy names or mesh source asset paths contain `rock` or `cliff` are included.
For differently named rocks, add **Shoreline Foam Source** to the object or parent.
Untick its Include field to exclude that hierarchy. Disabled renderers, inactive
objects, rigidbody hierarchies, BoatController hierarchies, water and generated
foam are excluded. Lower LOD renderers are excluded automatically; the highest
detail LOD supplies the bake geometry.

The bake reads mesh vertices, transforms and terrain heights; it does not modify
scenery, materials, UVs, mesh import Read/Write, colliders, or gameplay scripts.
Closed solid meshes are expected. Open/non-manifold meshes, touching/coplanar
faces, nested shells within one mesh and terrain holes touching the shoreline
need visual inspection. Foam is visual only and has no collider.

## Generated assets

Each successful bake creates one asset under
`Assets/_Game/Water/ShorelineBakes/`, containing the mesh chunks, one material and
one texture. Commit that asset, its `.meta`, and the saved scene together. Older
bakes are retained to support Undo. After saving and confirming a new bake,
unreferenced old bake assets may be deleted in Unity's Project window. Cancel or
failure before replacement leaves the previous foam intact.

## Rendering and performance

The editor slices rock triangles and terrain at mean water level. The union of
those solids is sampled on a 0.25 m grid; only a narrow 0–width distance band is
triangulated. Shared world-grid boundaries prevent tile cracks. This suppresses
overlapping foam layers where separate rocks meet each other or terrain.
24 m chunks let Unity frustum-cull individual shoreline sections, including
sections on large islands. A native CullingGroup disables distant renderers.
One scene controller copies the existing ocean wave phases into one shared
runtime material. It does no physics, shoreline detection or mesh generation.
The shader uses OceanGerstner.hlsl, two texture samples, fog and distance fading.
No shadows or probes are used. Bounds include vertical wave displacement.

This is a baked **mean shoreline**, not a live moving water/land intersection.
Foam follows analytic ocean wave heights; steep rock shores suit this best.
On shallow slopes, waves can move the true shore beyond the baked strip.
The finite-resolution ocean mesh also interpolates wave heights differently from
the finer foam mesh. Inspect for gaps/submergence during full wave cycles before
shipping; extreme waves, dynamic terrain/rocks and changing water levels are not
covered by a static bake. Very fine details smaller than 0.25 m may be omitted.

## Verification in Unity / Android

Run **Tools > Open World > Shoreline Foam > Validate Baker Geometry** first.
Then regenerate and check the reported source, chunk and triangle counts. Inspect
Snapper Island, Bluewater Cay, overlapping rocks, isolated rocks, shallow terrain
and 24 m chunk boundaries for a full wave cycle. Add a temporary rock duplicate,
regenerate, verify coverage, then undo the temporary edit and regenerate again.
Check the distance fade and confirm boats have no new foam generated around them.

Build to the target Android phone. Compare GPU/CPU frame time with the generated
foam root enabled and disabled from identical camera positions, especially the
densest shoreline. Record draw calls, triangles and transparent overdraw. The
system is designed to bound runtime work to nearby shoreline, but no FPS claim
is made without this device measurement.
