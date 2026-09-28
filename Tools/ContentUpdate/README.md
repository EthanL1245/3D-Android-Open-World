# Authored fish, gear and marina pipeline

This update auto-installs prepared source assets on editor reload, and checks them before builds.
Manual retry: **Tools → Open World → Install Fish, Level 4 Gear and Dock**.
It does not require Blender on the player's/developer's machine, modify PrototypeWorld.unity,
reset catches, or overwrite fish-tuning CSVs. A runtime replacement updates MarinaShop on Play.
The old marina import menu forwards to the same installer instead of restoring cube geometry.

## Gear values and behavior

| Displayed level | Saved tier | Rod price | Normal damage | Crit chance | Critical damage | Reel price | Reel speed |
|---|---:|---:|---:|---:|---:|---:|---:|
| Woodland / Starter | 0 | 0 | 2–4 | 0% | — | 0 | 1.00× |
| Level 2 | 1 | 2,000 | 4–8 | 5% | 8–16 | 1,500 | 1.20× |
| Level 3 | 2 | 6,000 | 6–12 | 8% | 12–24 | 4,500 | 1.40× |
| Level 4 | 3 | 10,000 | 10–20 | 10% | 20–40 | 6,500 | 1.60× |

Damage remains one randomized burst per **0.35 accumulated seconds of held REEL input**.
Critical chance applies once to the whole burst, doubling it. Tapping does not accelerate damage.
Reels change retrieval/animation speed, not authoritative burst damage. The fight's net inward
pull now also uses the exact reel multiplier (previously live-fish inward pull omitted it).

## How rods and reels are added

1. Keep the supplied ZIPs as authoring originals. Run `export_assets.py --blender <executable>
   --packages <folder-containing-zips>` from the repository root to regenerate this update's
   FBX/geometry/texture sources. Exported UVs come from the Blend; STL does not contain UVs.
2. A rod uses the main authored mesh. Its longest axis is fitted to the starter rod's exact
   RodTip distance. `FishingRodLevel3Importer.InstallPrepared(json, texture, prefab, generated)`
   clones `Resources/Fishing/FishingRodReel.prefab` and replaces **only RodBlank mesh/material**.
   The reel, cast/flex component, RodTip and line attachment hierarchy remain intact.
3. A reel exports these five authored objects separately:

   | Blender object | Existing animated Unity child |
   |---|---|
   | Plane | ReelFootAndBody |
   | Cylinder | Rotor |
   | Cylinder.001 | ReelHousing |
   | Cylinder.002 | Spool |
   | Cylinder.003 | Handle |

   Frame 1 is the rest frame. The existing export basis is `(x,-y,-z) × 0.04`.
   `FishingReelLevel3Importer.InstallPrepared(...)` clones the starter assembly and fits mesh
   vertices around its **preserved animation pivots**. It retains the proven rotor/spool/handle
   animation, rather than spinning the whole reel. It verifies transforms and animation references.
4. Prepared Level 4 files live in `Assets/_Game/ContentUpdate/Source`; generated meshes/materials
   live in `Assets/_Game/ContentUpdate/Generated/Level4Rod` and `Level4Reel`.
   The two prefabs are `Resources/Fishing/FishingRodReelLevel4Rod` and `FishingRodReelLevel4`.
   The installer configures textures as sRGB, mipmapped, repeat wrap, up to 2048 pixels.
5. Wire prices, names, max tiers and resource paths in **ShopCatalog**. Saved tiers start at zero.
   Wire rod damage/critical values in **FishingBurstDamageRuntime** and reel multiplier in
   **Level2FishingReelRuntime.ReelMultiplier**. The existing legacy class names are retained
   because saved components reference them. Update its rod compatibility fallback too.
6. Held rod/reel mesh replacement uses **Level2FishingRodRuntime / Level2FishingReelRuntime**.
   **ShopPreview** renders icons from the actual prefab; add a tier name mapping in its
   `ResolveGearKey` resolver (search for `Level 4 Fishing Rod`). **ShopWorldHUD**
   reads damage and speed from the same gameplay methods, so shop descriptions stay accurate.
7. For Level 5+, extend the four-slot gear arrays, ShopLedger ownership-mask/tier bounds,
   EquipmentPage tier loop, and the max-tier constants. Never renumber saved tiers.
   Add a prepared installer call. Current source validation expects 1,340 rod triangles and
   1,772 total reel triangles; a different authoring topology requires updating or parameterizing
   that validation, not substituting an unrelated mesh.
8. Validate in Unity: buy/equip separately; check each icon, held mixed rod/reel combination,
   RodTip/string placement, cast/flex, animated handle/spool, 0.35-second damage bursts,
   doubled crits, and increased lure/live-fish/unconscious retrieval speed.

## New fish

IDs **13 Albacore** and **14 Greater Amberjack** are appended; retired ID 4 stays retired.
The prepared FBXs contain the supplied five-bone rigs and full frame 1–20 actions. Textures
are the supplied JPEG atlases. `SeaBassImporter.InstallModel` builds the same Animator,
**RedSnapperPresentation**, and animated **AuthoredMouthAnchor** as other authored fish.
`FishVisualFactory` registers their resource paths; all bag/held/aquarium/ambient routes share it.
Aquarium train-track deformation and the standard held struggle/rest cycle need no special cases.
Catch counters grow to the new catalog count while retaining all existing records.

Editable defaults are in all three `Resources/FishingTuning` CSVs:

| Fish | Global weight range | Health range | Yellow baseline | Suncrest chance | Brinebreak chance | Deep Ocean chance |
|---|---|---|---|---|---|---|
| Albacore | 2–45 kg | 240–1,700 | 3.65 m/s | 0% | 4% | 8% |
| Greater Amberjack | 1.5–70 kg | 250–2,100 | 3.70 m/s | 0% | 6% | 6% |

These are game-balance defaults, not biological measurements. New chances are the same across
baits initially; the existing outer-water probabilities are reduced proportionally, using largest
remainders to preserve whole numbers totaling 100. Existing Suncrest odds remain unchanged.
The new fish therefore appear only in the outer-water indices. All values remain editable.
Length curves are explicitly game approximations: Albacore `kg = 18 × metres³`, Amberjack
`kg = 14 × metres³`, tabulated every 5 cm. Update FishSizeTable for a different size curve.

## Marina geometry and collision

The supplied Dock.blend contains **Deck** and a separate **EdgeTrim**, not two duplicate decks.
Both keep their authored UVs. Blender +Y is mapped to Unity +Z (seaward). The wide platform
occupies local z −5..2; the existing shop/canopy stays on it. Only the narrow pier beyond z=2
is length-fitted to the same deep-water deployment checks used by the old extension.
The mesh and its non-convex static MeshCollider use the same fitted vertices: no invisible
box across the water beside the pier. The supplied Post mesh is reused individually at 24 cm
width and stretched down into the sampled seabed. Post tops stop under the deck.
Old Boardwalk, primitive Dock pilings, imported overlays and the old extension are disabled
before replacement. The edge trim does not overlay the deck top; no stacked full deck surfaces.
The beach ramp terminates at the deck's landward edge.

Startup checks the actual deck surface for terrain/scenery blockage and tries small shoreline
adjustments. If no safe footprint exists, it logs a clear error and keeps the previous marina.
Ambient fish test their body clearance against dock geometry; fight routing also avoids its
posts. Water between the supports stays navigable. These are stationary world colliders;
no Rigidbody or broad enclosing box is added to the marina.

## Validation and Git

Run `python Tools/ContentUpdate/check_assets.py` for source mesh/UV/CSV checks.
`Checks/Program.cs` additionally executes the real tuning/catalog code with small Unity stubs
and checks C# syntax with Roslyn. Neither substitutes for Unity compilation or Android Play Mode.

After pulling, let Unity finish importing. Use the setup menu only if auto-install reports an error.
Test the new gear, catches, aquarium/held animations and marina walk/swim clearance.
Unity creates generated prefab/controller/mesh assets and their metadata locally; after testing,
commit these if desired alongside any intentional scene changes. Keep your existing local scene
work when pulling; do not discard it to install this update.
