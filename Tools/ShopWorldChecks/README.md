# Tideglass Quay shop world

## One-click installation

1. Pull `main` and let Unity compile.
2. Open the fishing island scene and exit Play Mode.
3. Run **Tools > Open World > Install Shop World (One Click)**.
4. Press Play, then **MENU / TRAVEL > Tideglass Quay**.

The installer creates and registers a separate additive scene, `Assets/_Game/ShopWorld/TideglassShopWorld.unity`, and configures the existing player/canvas. It also creates `Assets/_Game/ShopWorld/FishingHomeWorld.unity`. The island remains loaded; only the current destination is loaded additively. Run the installer again after this update to regenerate both worlds. No Inspector wiring or external models/packages are needed.

The original island scene is saved and copied to `Assets/_Game/ShopWorld/Backups/IslandBeforeShopWorld.unity` before the first installation changes it. The installer removes the old HomeBase shops, platform and placed tank objects, replaces the old economy HUD/aquarium component, and places the player on nearby dry terrain. Re-running updates the same shop scene and generated assets without clearing ownership or fish.

Commit the generated `_Game/ShopWorld` assets, updated island scene and `ProjectSettings/EditorBuildSettings.asset` after installation if other machines should receive the completed scenes directly. The repository's current Android build profile uses the global scene list, which the installer updates.

## Shopping and inventory

The menu has Travel, Fish Bag and Equipment tabs. All long lists have touch dragging, wheel scrolling and a visible scrollbar. The bag lists every saved fish without the old ten-row cap. HOLD displays a selected fish using the existing hook system.

Walk to a counter or habitat sign to use its OPEN button (keyboard E). Purchases and sales require the Quay; resident transfers require Home. Both require proximity to the appropriate interaction point. Opening the menu cancels the current cast/fight and blocks camera/movement input until closing it.

The fish market sells individual bag fish at the existing FishCatalog values. Sell All first shows a total and confirmation. It never sells habitat residents. Gear tiers are permanent; purchases auto-equip and require the previous tier. Earlier owned tiers remain selectable from Equipment.

| Item | Tier 1 | Tier 2 | Tier 3 | Effect per tier |
|---|---:|---:|---:|---|
| Rod | 180 | 650 | 1,800 | +18% tension-control factor |
| Reel | 160 | 550 | 1,500 | +22% fish tiring/retrieval speed; faster reel animation |
| Line | 100 | 400 | 1,100 | +15 / +35 / +65 m range; +12% tension-tolerance factor per tier |

Rod and line factors divide tension buildup, rather than acting as percentage-point reductions. Gear affects existing fishing logic; there are no separate dummy inventory items. Rod/reel tier tints distinguish upgrades while preserving the authored models and animations.

| Bait | Price / 10 | Effect |
|---|---:|---|
| Reusable lure | Free, unlimited | Standard catch chances |
| Worms | 35 | 35% shorter wait |
| Shrimp | 90 | 20% shorter wait; 2.5x species-selection weight for snapper/goatfish |
| Squid | 180 | 4x species-selection weight for yellowtail/tuna |

One bait is consumed only after a valid cast target is found. Canceling an already-started cast spends that bait. The last unit still affects its cast, then selection falls back to the free lure for the next cast. Species weights modify relative odds, not guaranteed catches.

## Aquariums and ponds

All exhibits are built before purchase, with prices and limits on physical signs. The Quay is a showroom. Purchases install at Home: one aquarium and one pond, each upgraded in order at a fixed, spacious plot. Prices in the table are cumulative values; each upgrade costs only the difference from the previous tier. Fish move with the upgrade. All future footprints are reserved from the start. ADD FISH moves one bag fish into it; TO BAG reverses that transfer. A rejected transfer leaves the fish in its current location and explains the limiting condition.

| Habitat | Coins | Fish count | Maximum kg per fish | Total kg | Interior W x D x H (m) | Swim |
|---|---:|---:|---:|---:|---|---|
| Tidepool Cabinet | 250 | 3 | 0.8 | 1.8 | 2.2 x 1.4 x 1.2 | No |
| Reef Gallery | 700 | 6 | 4 | 14 | 4.8 x 2.8 x 2.2 | No |
| Lagoon Suite | 2,000 | 10 | 12 | 55 | 8 x 5 x 3.6 | Yes |
| Grand Ocean Gallery | 6,500 | 20 | 35 | 220 | 14 x 9 x 5 | Yes |
| Oceanarium | 18,000 | 40 | 120 | 1,000 | 26 x 16 x 7 | Yes |
| Courtyard Pond | 1,400 | 12 | 8 | 45 | 10 x 8 x 2.8 | Yes |
| Garden Lagoon | 5,000 | 25 | 35 | 240 | 18 x 14 x 4 | Yes |
| Sanctuary Lake | 15,000 | 50 | 120 | 1,200 | 32 x 24 x 6 | Yes |

Water volumes are slightly inset from walls. The ponds are sunk into actual ground-mesh openings, with stairs for walking out. Large aquarium stairways lead to access landings. At Home, climb the aquarium stairs or walk into the pond. When swimming, the mobile up button shifts upward and a down button appears beneath it. Both return to the land layout when swimming ends. Hold the up/down arrows to ascend/descend. On keyboard use Space to rise and Ctrl to descend. Water detection is bounded to owned, swimmable Home habitats and cannot make the whole dimension behave as ocean water.

Fish length grows with the cube root of weight. Habitat fish are normalized to `ShopCatalog.FishLength` (0.40 m at 1 kg, or 0.48 m for tuna), rather than shrinking every species to a fixed tank display size. Admission checks count, individual weight, total mass and turning length. Existing head-led body-trail swimming is preserved, with paths sized to each habitat. Distant habitat fish are disabled until approached to reduce animation cost.

## Updated menu and capacity

MENU / TRAVEL has three large destination cards: Fishing Island, Tideglass Quay and Home. Directory, water-access menu rows and the old FISH shortcut are removed. Shops are opened by walking to their counters/signs. Equipment and bag rows have larger controls and cached 3D item snapshots. Fish use a shared physical-size frame with the head anchored at the left, so bigger bodies crop off the frame. Clicking an already-open tab does not rebuild the UI.

The fish bag holds **50 fish**. New casts and withdrawals are blocked when full. Existing oversized saves retain all fish and can be sold or deposited until room is available. No migration truncates catches.

## Saves and migration

`OpenWorld.ShopWorld.v1` stores coins, the entire bag, gear ownership/equipment, bait and owned habitats with their residents in one JSON record. Sales and fish transfers mutate both sides before one save, avoiding separate remove/save/add/save operations. A previous-save backup is retained. Unreadable saves are not overwritten.

On the first Play after installation, existing coin and fish-bag saves are imported. Every fish from an old aquarium returns to the bag. Each old placed or unplaced aquarium refunds its original 250-coin price. This happens once; original legacy PlayerPrefs keys remain intact. Starting wealth is not otherwise increased. The existing fishing preview-grant behavior is unchanged.

An existing Quay save migrates once to one aquarium and one pond: the highest owned tier in each group survives, all residents are consolidated into it, and earlier smaller purchases are credited. If the combined residents exceed capacity they remain safe; new deposits are blocked until enough are withdrawn. Repeated installs/loads do not repeat the credit.

## Art and rendering

World-space signs use a front-only depth-tested font shader; secondary labels fit mounted boards. Sand beds and plinths no longer share a surface, stair blocks no longer overlap, and glass/water use single surfaces. The market hangs existing fish models from its canopy.

The generated shop uses limestone paving, oak counters and rafters, brass framing, glass skylights, planted borders, benches, lanterns, aquarium light strips, aquatic plants/stone beds and world-space signs. Dedicated URP surface/glass shaders provide textures, sunlight/shadows, local accent lighting and animated water highlights. Six shadowless point lights and one 128px reflection probe bound the additional lighting cost. Layout, rendering and performance still need to be judged in Unity on the target phone; procedural assets alone are not a claim of photorealistic final art.

## Verification

Run the actual commerce model tests with .NET 8:

```sh
dotnet run --project Tools/ShopWorldChecks/ShopWorldChecks.csproj
```

64 automated checks passed. They cover exact funds/insufficient funds, duplicate or out-of-order purchases, unowned equipment, consumable bait and free fallback, all capacity rules, duplicate deposit/withdraw/sale protection, fish-size growth, and save round trips with more than 150 bag fish. Additional checks cover ordered aquarium/pond upgrades, difference pricing, resident retention, the 50-fish withdrawal limit and idempotent migration credits. Changed C# files passed syntax parsing.

The Unity installer additionally checks world references, habitat IDs, walkable arrival ground, entries inside water volumes and the absence of floor colliders across pond openings before saving the shop scene.

Unity compilation, shader compilation, Play Mode, Android performance and final visual QA were not available in the execution environment. First local acceptance pass:

- Install twice; check there is one shop scene/player HUD and no island platform/old tanks.
- Confirm migrated fish/refunds; restart and confirm no second refund.
- Travel among all three destinations during/after a cast and while swimming; confirm island return position.
- Buy/equip each gear group, use a bait pack to zero, and verify range/reeling effects.
- Scroll a large fish bag; sell individual fish and review Sell All.
- Purchase a small tank, reject an oversized fish, reach its count/weight limits, then withdraw.
- Upgrade each Home slot; verify only its current tier is visible, all residents survive, and stairs/up/down controls work.
- Inspect signs from the front/back and behind walls; inspect sand beds and stairs for flickering.
- Inspect thumbnail framing for small/large fish and equipped-button stability.
- Restart and revisit the shop; confirm balances, bait, owned habitats and residents persist.
