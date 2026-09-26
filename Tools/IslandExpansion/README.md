# Brinebreak Isle expansion

## Apply

Pull main and open your installed Suncrest Reef scene. Click **Tools → Setup Island Expansion** once, then Play. The setup adds/links the expansion settings and saves the scene. Existing installed Suncrest scenes also auto-attach the runtime expansion. The committed PrototypeWorld scene contains the expansion anchor/config reference; the repository's base scene does not contain the locally generated Suncrest terrain, so the setup installs Suncrest first if it is absent. It does not reinstall Suncrest when a ReefZone already exists.

The committed terrain data is the deterministic `Resources/Islands/BrinebreakExpansion.asset` recipe and generator, not an opaque baked Unity TerrainData binary. Terrain, colliders, scenery, welcome sign and discovery volume build once before gameplay at runtime, on Android as well as Editor Play Mode. No scene edits made during Play need saving. Original TerrainData remains untouched; the same Terrain component receives a generated runtime copy, keeping existing fishing/boat references valid.

## World

Brinebreak Isle is an approximately 190 × 130 m oval east of Suncrest, centred 550 m beyond Suncrest's east radius near the former outer drop-off. Low rocky hills rise roughly 4–14 m; scrub, weathered boulders, sandy beaches and a reserved arrival/sign approach furnish the land. Its underwater beach reaches the 28 m shelf within 65 m. Existing Suncrest land, pond and nearshore heights are sampled from the installed terrain.

A single enlarged elliptical outer boundary encloses both islands and the boat route between them. The connecting shelf is approximately 28 m deep away from coastal shelves. The true outer drop-off descends to 65 m across 130 m beyond that boundary. Terrain resolution defaults to 1025² with a 512² splat map. The boat Water-layer query surface expands to cover the same terrain. Known-terrain, full-hull clearance and one-active-boat logic remain in force.

## Fishing / index

1. **Suncrest Waters / Suncrest Reef:** existing base, shrimp, squid and all five lure tables and sampled rolls preserved exactly, including Bonito and Black Sea Bass. Palm Pond stays 5–12 cm.
2. **Brinebreak Isle Waters:** higher yellowtail/tuna concentration for every equipped bait/lure, sizes up to 1.6× species coastal maximum, 1.4× fish HP before size effects, +15% reeling tension growth.
3. **Deep Ocean:** separate pelagic-heavy weights, sizes up to the existing 2.25× coastal maximum with increasing offshore distance, 1.7× HP and +25% reeling tension growth.

Tables multiply each biome's base weights by the existing equipped bait/lure preference ratios. The fish index uses precisely the same normalized function as random rolls. Percentages retain the existing whole-number display; rounding may make the displayed total slightly different from 100. The fishing biome, size and fight tuning are selected at the **bobber/lure location when the bite occurs**, then remain fixed through that fight. Casting across a boundary works; moving the player during an existing fight cannot retune its HP.

The island index shows all three areas with distinct previews. Brinebreak's entry is locked until discovered. Ambient fish keep the bounded 12-fish animated pool and refresh off-screen, one model per frame, after a stable biome change.

## Waves

The CPU height sampler and ocean shader blend the same two continuous wave patterns over a 160 m transition around Brinebreak. The rough pattern uses 2.8× amplitude and 1.65× speed. Spatial smoothstep blending preserves phase continuity instead of multiplying global time by a changing speed. Shader normals include the blend gradient. Boats, bobbers and swimming use that same height field; Palm Pond remains separate and calm.

## Discovery / travel

A trigger covers Brinebreak's landmass, but proximity alone is insufficient. Discovery requires the player to be grounded, not swimming, not a boat passenger, above dry terrain and within 0.6 m of the terrain surface. Sailing past or standing on the boat does not unlock it. A six-second banner announces discovery and `ShopLedger.brinebreakDiscovered` is saved through the existing ShopProgress backup/save system. Old saves default to locked; other ownership and catch records are preserved.

MENU / TRAVEL shows a locked Brinebreak destination until discovery. The travel method independently enforces the unlock. Once unlocked, it works from Suncrest, Brinebreak, Home or Quay, with the normal boat cleanup and fishing cancellation. Suncrest returns to the marina dock; Brinebreak uses a clear land arrival. Deep Ocean is a fishing area, not a land teleport destination. The welcome sign clones the installed Suncrest arrival sign, changing only the title/placement; a matching geometry/typeface/ShopSign fallback supports scenes without that sign.

## Validation

`dotnet run --project Tools/IslandExpansion/Checks/Checks.csproj -- <repo-root>` checks actual C# catalogue/geometry/save defaults and parses every project C# source. The baseline fixture comes from main before this update. Tests cover exact Suncrest odds/roll compatibility for every lure/bait, region normalization and index/roll agreement, increased rare-fish odds, no ocean boundary between the islands, the real outer abyss, continuous wave blending and default/discovered unlock states.

Unity Editor and Android are unavailable in the remote workspace. Full Unity compilation, GPU shader compilation, generated terrain appearance, trigger physics, boat handling and device frame time still require local validation:

- Setup and Play: inspect preserved Suncrest/pond/marina; sail east over the deep shelf and around both outer coasts.
- Sail around Brinebreak without landing: it stays locked. Step onto its beach: one banner; restart and verify the unlock persists.
- Travel to Home/Quay and back to each island: correct clear arrival, boat despawned, no stuck controls.
- Cast on each side of biome boundaries; compare index odds/caps, HP and tension. Verify normal Suncrest catches and tiny pond catches remain unchanged.
- Compare rendered wave crests with boat/bobber movement through the blend region; inspect sign approaches and planted rocks/scrub.

## Follow-up: spacing and menu routing

Brinebreak has moved 300 m farther east and the shared crossing shelf is now 28 m deep. MENU / TRAVEL uses four matching destination cards in two rows. Locked cards and unavailable shop buttons have explicit neutral-grey backgrounds and dimmed labels/previews. Fish-index cards carry a BiomeIndexLink, so the passive fishing-menu safety handler preserves the selected biome instead of routing every card to the last/default index. Per-area size labels replace the ambiguous Ocean label. Pull and restart Play; no scene overwrite or installer rerun is required for this follow-up.

