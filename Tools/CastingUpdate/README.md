# Casting, records and Quay refinement

Pull main, allow Unity to compile, and press Play. No scene or model installer rerun is required for this update. Existing Home/Quay scenes receive the counter/stair adjustments at runtime.

## Casting contract

- All loadouts: 5 m minimum cast, 30 m maximum cast, 40 m line. Existing line upgrades retain their tension tolerance; their names now describe strength instead of obsolete range bonuses.
- Sample water/depth/flight clearance from 5 to 30 m every 0.5 m, four times per second, and recheck on both button presses. Actual airborne collision checks remain active.
- Any continuous clear interval of at least two adjacent samples enables CAST. Neither the 5 m nor the 30 m endpoint is required to be clear. All invalid intervals are grey on the gauge; no valid interval disables idle CAST completely.
- In cast mode the quarter-circle starts green at the top and progresses through yellow/orange to red on the right. A white needle accelerates near red and oscillates back. The larger arc sits above CAST; the label is above the arc on its own backing.
- Releasing in a grey sector, or after the range becomes invalid, exits immediately with a message, without animation or bait consumption. Jump becomes a red CANCEL CAST button at its land position until mode ends. Menus, unequipping and underwater cancellation also restore Jump.
- Palm Pond can be cast into whenever any continuous valid interval lies within 5–30 m.

## HUD, bait and records

Hotbar slots grow from 82 to 110 reference pixels. A pictured bait button below the top-left island index shows equipped bait, with ∞ for worms and a count for consumables. It opens a standalone bait selector showing worms and owned specialty bait; bait selection is removed from Equipment. Worms replace the reusable lure as the free, infinite default; there are no worm packs to buy. Existing shrimp/squid quantities remain saved, and empty specialty bait falls back to worms. This update selects worms once on existing saves.

The island index no longer has a Return to Destinations row. Each species shows total landed catches and personal best weight/length. Stats initialize to zero once for this update, regardless of existing bag/tank fish. Only successful landed catches count; gifts, previews, deposits and withdrawals do not. Later loads/updates preserve stats. These records use the existing local player save and backup; this project has no online account service or cross-device cloud sync.

## Difficulty and fixtures

- Fish HP: 2.5× the prior formula (rounding to integer HP).
- Base reeling damage: 8.5 HP/s instead of 10, before mood/reel modifiers.
- Tension buildup while reeling: +12% before equipment mitigation.
- Fresh Catch Market working surface: 0.80 m instead of 1.35 m. Counter collision, trays, ice, fish and labels move together.
- Swimmable aquarium stairs: thin translucent aqua treads, dark metal nosings/stringers/handrails and a glass landing. Top heights and approach positions are preserved. Pond exit stairs are unchanged.

## Local acceptance

53,171 automated C# rules/save assertions and syntax parsing passed. 1,865 commerce regression assertions passed. The checks are included in `Tools/TunaUpdate/Checks`; commerce regression checks are in `Tools/ShopWorldChecks`. Unity compilation, visuals, physics and Android frame timing still require local testing.

1. Aim at all-land, clear water, land-prefix/water-suffix, and a rock splitting the range. Verify the correct disabled/grey/available states.
2. Release in grey: no rod swing or bait loss. Cancel: Jump returns. Move/look during charging and confirm eligibility updates.
3. With each line tier, verify 5–30 m casting and 40 m line limit. Test pond casts with land beyond the far bank.
4. Land two fish of one species; count becomes two and best retains the heavier. Transfer and sell them; records stay. Restart; records remain.
5. Verify infinite worms and specialty bait depletion. Check the larger hotbar and cancel/gauge layout on the phone.
6. Travel to Quay: inspect ice fish from standing height. At Home, climb the translucent aquarium stairs, enter/exit the water and test different habitat tiers.

After testing, save any deliberate scene changes, then `git add Assets ProjectSettings`, commit and push. Runtime-generated fixture objects should not be saved from Play Mode.

Swimming immediately stows the rod and cancels any current cast/fight, including surface swimming with up/down controls. Rod equip and cast are refused while swimming. The fish indicator retains LINE but removes DIST.
