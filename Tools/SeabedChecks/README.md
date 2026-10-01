# Underwater terrain relief

`IslandExpansionWorld` applies deterministic world-space relief after its existing
island/shelf height calculation. Winding canyons, tributaries, basin depressions and
stepped shelves cover the generated map. The first 0.8 m below sea level and all dry
land retain the original height function. Relief fades in fully by 9 m depth.
Crests remain underwater; an extra 35 m of terrain vertical capacity prevents valley
floors from being clipped. The existing terrain and its collider use the same data.

Underwater slopes blend to a dedicated repeating fractured-stone texture and normal
map; flatter seabed uses the existing sand layer. Paint changes fade between 1.5 and
5 m depth, preserving above-water island materials. Existing Brinebreak rock remains
in its own layer. No new per-frame work or scenery colliders are introduced; terrain
and textures are generated once on Play and cleaned up with the existing world.

Run `dotnet run --project Tools/SeabedChecks/Checks.csproj -- .` from the repository.
Checks execute the real relief function across a 2 km region, checking dry-land
preservation, bounds, sea-level independence, deterministic output, nonflat relief,
cliff coverage and rock-weight limits, plus C# syntax. Unity Play Mode and Android
visual/performance verification still need to be performed in the editor/on device.
