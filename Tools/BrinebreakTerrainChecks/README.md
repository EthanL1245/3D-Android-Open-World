# Brinebreak southern terrain regression checks

The relocated island straddles z=-843.6983, the old main terrain and fixed
`BoatPlacementWater` box boundary. The southern TerrainCollider already points
to the correct TerrainData; fish were bypassing it by reading only
`Terrain.activeTerrain`, while boat aiming missed the finite water box entirely.

Changes:
- Bounds-aware terrain sampling shared once per frame across terrain consumers.
  Queries use the highest valid tile at overlaps/seams and never clamp an
  out-of-bounds request onto the original map edge.
- Fight depth checks, subdued retrieval, ambient spawning/movement/recovery,
  lure routing and cast-depth evaluation now recognize the southern tile.
  Retrieval samples intermediate positions and completes a shore catch when
  the subdued fish reaches a bank instead of advancing through it.
- Boat aiming intersects the ocean datum, with the same 18 m range. Actual hull
  depth, props, line of sight and unknown-bathymetry rejection remain enforced.
- Relocated Brinebreak uses 90% of full deep-ocean wave amplitude (4.05 versus
  4.5 baseline multiplier), smoothed by the existing transition. The legacy
  extra island multiplier is disabled consistently in CPU and shader inputs
  for the relocated map. Wave phases and rendering geometry are unchanged.
- No scene objects, transforms, terrain assets, or rock meshes are edited.

Run with .NET 8:

```sh
dotnet run --project Tools/BrinebreakTerrainChecks/Checks.csproj -- "$PWD"
```

The harness compiles the production terrain and boat-clearance implementations
against small Unity stubs and checks southern samples, seams, disabled tiles,
unknown bounds, a ridge between deep endpoints, subdued bank contact, boat
clearance, solid obstruction rejection and aim range/direction. It also parses
all game C# sources with Roslyn. These are offline checks, not a Unity Play Mode
or Android physics/rendering test.

Unity follow-up: place each owned boat in clear/deep water on both sides of the
seam at Brinebreak; confirm rocks and shallow banks still reject placement.
Fight and retrieve a subdued fish toward the southern bank and across the seam.
Watch ambient fish at banks and compare waves after transitions settle in
Brinebreak and full deep ocean. No terrain regeneration is needed.
