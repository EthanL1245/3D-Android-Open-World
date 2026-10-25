# Winter Flounder (stable species ID 27)

Suncrest Beach only (internal biome ID `suncrest-reef`). Every bait/lure has the
same probability for Winter Flounder as Red Snapper: worms 13%, shrimp 15%, squid
9%, all five lures 16%. Red Snapper retains its previous percentages. Other
Suncrest odds are reduced proportionally, using largest-remainder integer
rounding so every row remains exactly 100%. Other biomes retain their odds.

Difficulty is Yellowtail (ID 3), not Yellowtail Snapper: 0.68 base difficulty,
240–603 health, yellow swim speed 3.438 m/s. Flounder uses a gameplay size range
of 0.25–3.6 kg, with a Suncrest catch range of 0.25–1.541871456 kg. This preserves
Yellowtail's normalized size range in Suncrest, giving identical health and
size-dependent fight difficulty at matching catch percentiles. The length curve
is a gameplay approximation, kg = 10 * metres^3. Sale endpoints also match
Yellowtail (150–1100 coins). Existing hotspot/Charm modifiers apply normally.

The supplied original Blender and STL files are in ArtSources. The exported FBX
and PNG are committed in Assets/_Game/Reef/Source. The rig is rolled flat with
its brown, eyed side up, and retains all six bones, UV0 and the authored swim.
Seven meshes, 1,604 triangles. Empty armature modifier slots were removed only
from the exported copy. The source files are unchanged.

After pulling, Unity automatically builds the Resources/Fishing/WinterFlounder
prefab using SeaBassImporter, including its shared RedSnapperPresentation,
Animator, texture material, and animated lip anchor. No Blender installation or
manual file selection is needed. Play and build entry points also ensure the
prefab exists. Tools > Open World > Rebuild Winter Flounder can regenerate it.
The optional preserveWorldUp importer argument applies only to this flatfish;
all existing fish retain their normal import behavior.

The shared factory covers fights, ambient fish, inventory/shop previews,
aquariums/ponds, held fish, and multiplayer. Existing save arrays expand via
FishCatalog.Count. Server tournament limits include this species only in
Suncrest and retain the normal hotspot and Charm validation.

Validation:
- C# rules compiled against the existing lightweight Unity stubs: 784 checks,
  including syntax across project scripts, 45 complete chance tables, 450,000
  actual species rolls, exclusivity, Yellowtail fight parity, size conversion,
  fallback tables, and stable IDs.
- 36 server tests pass, including flounder catch/weight/biome validation.
- FBX reimport verified six bones, UVs, animation movement and matching loop
  endpoints, flat body orientation and eyed side up; textured render inspected.
- Unity Editor is unavailable in the build environment. Full Unity compilation,
  automatic prefab creation and Play Mode still require verification in Unity.

Run rules: dotnet run --project Tools/WinterFlounder/Checks -- .
Run server: node --test Server/tests/*.test.mjs
Run export validation: blender --background --python Tools/WinterFlounder/verify_export.py
