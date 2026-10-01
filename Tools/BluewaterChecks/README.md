# Bluewater Cay and water visibility

Pull, wait for compilation, then Play. The existing island generator builds the new
island without a scene installer. Bluewater Cay is a 58 m radius island southwest of
Brinebreak, southeast of Suncrest (+Z north). Its closest shoreline gap to Brinebreak
is approximately 197 m. Its fishing biome is a circle extending exactly 75 m from its
shore (133 m from its center), with priority over overlapping older biome circles.
Deep Ocean retains ID 2; Bluewater adds ID 3. All previous biome CSV rows are preserved.

Only Blue Mackerel, Yellowfin Tuna, Bigeye Tuna, Bonito and Albacore can roll in this
biome. Ambient fish also respect their source biome and the new 75 m limit, including
body clearance. Land discovery persists in the existing save as bluewaterDiscovered;
missing fields in old saves default to false. The index has a dedicated preview,
fish-index button and unlockable teleport. Existing dry island geometry is retained.

Water fixes:
- Replace near-black offshore surface/fog colors with blue-green values.
- Reduce deep underwater fog density from 0.13 to 0.055 and tint opacity from 0.34 to 0.12.
- Request the URP camera depth texture. The ocean shader measures the opaque scene
  behind each visible water surface and increases opacity with water-column length.
  Shore observers therefore cannot see the remote bottom through shallow-player water.
- Keep a minimum ambient water color rather than multiplying it entirely by direct light.
- Preserve wave simulation, buoyancy, and the existing underwater/above-water transition.

Run `dotnet run --project Tools/BluewaterChecks/Checks.csproj -- .` from repo root.
Tests execute actual fishing selection/parsing and geometry boundary functions with
small Unity math stubs (Perlin terrain aesthetics are not simulated), checking 36
bait/biome tables, pelagic exclusions, weight bounds, discovery flags, save-safe IDs,
shore separation and the entire 75 m perimeter. C# syntax is also checked.
Unity editor compilation, shader compilation/rendering, and Android performance still
need verification in Unity/on device. Test both shore views and swimming in deep water,
and discovery/teleport from the fishing world, Home and Tideglass Quay.
