# South Ocean Seabed

PrototypeWorld now contains a separate South Ocean Seabed root with references
to the existing main terrain and OceanWater. When Unity compiles the scripts
and opens this scene, SouthSeabedTerrainEditor bakes the marker once into a real
Terrain/TerrainCollider and saves TerrainData to:

Assets/_Game/EditableFishingMap/SouthOceanSeabed.asset

Press Ctrl+S to persist the generated component/asset references in the scene.
Opening Unity does not silently save unrelated scene edits. The build scene
processor also handles builds that never opened this scene: it embeds the same
terrain in the build, with no generation at runtime. Once a terrain is assigned,
subsequent scene opens/builds leave its sculpting, painting and placement alone.

The new tile copies the main tile's dimensions (currently 1740.2677 m square),
1025 heightmap resolution, terrain layers and material. It sits immediately
south (-Z), extending from z=-843.6983 to approximately z=-2583.966 at the
existing x extent. Existing terrain assets, islands and protected scenery are
not modified. A 96 m blend matches the existing southern edge. A new independent
heightmap uses the existing deterministic SeabedRelief ridge/canyon algorithm
with the configured deep ocean depth; it never runs any island generators.
Every new height is capped at least 5 m below sea level. The current source edge
is already 44.9-92.3 m underwater, so the cap doesn't cut the shared edge.

Sand covers flatter seabed; existing underwater stone covers steep slopes.
There are no trees or details. GPU terrain instancing, normal terrain LOD and
culling remain active, with pixel error 8 (or a higher source value). Height
queries outside the primary terrain now consult this tile, so water-depth
consumers see its actual floor. The primary map's height queries are unchanged.
A matching TerrainCollider on the source layer supplies physical collision.

Validation in Unity runs before initially saving the generated asset: it checks
every height against the underwater ceiling and the full shared edge against
the source within terrain quantization tolerance. The offline check script uses
UnityPy/numpy to inspect the actual source asset and model the generation:

python Tools/SouthSeabed/check_south_seabed.py

UnityPy and numpy are only optional development-test dependencies, not game
packages. Offline checks are not a Unity C# compilation or Android performance
measurement. Unity Editor was unavailable in the implementation environment.
Verify after import that South Ocean Seabed has Terrain and TerrainCollider,
inspect the join underwater, sculpt/paint the new tile to confirm editability,
and test a fresh build. Commit the generated TerrainData, its meta and saved
scene with future map edits so other machines share the baked asset.
