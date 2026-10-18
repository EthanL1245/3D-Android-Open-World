# Angler levels and personal stats

Open **Menu / Travel > Personal Stats**. This uses the shared shop background and the five supplied artwork assets. The right panel reserves a 2-column, 3-row grid: four cards and two completely empty spaces. The charged yank stat is labeled **Skill**.

Progression is stored in `ShopLedger.angler`, alongside inventory and the saved player name, using the existing local save and backup mechanism. Existing saves start at level 1 with zero XP and bonuses. This does not add cloud accounts.

## Balance

- Every level awards one point uniformly among the six stats that are below 100. No capped stat wastes a level-up.
- Raw damage: +1 per point. Percent damage: +1% per point, applied after raw damage. Critical hits double the raw bonus too.
- Reeling speed: +1% per point, stacking multiplicatively with equipment; affects lure retrieval and live, stunned and unconscious fish retrieval.
- Tension control: positive tension gain divided by `1 + points / 100`. At +100%, gain is halved. Line-distance breaks and recovery are unchanged.
- Skill: +1% charged-yank damage per point, on top of the improved base damage.
- Charm: +1% fish weight per point. Multiplies the existing hotspot bonus and preserves depth-quality rules. Larger weights feed the existing health/difficulty calculation.
- Six 100-point caps are fully reached at level 601. This is the maximum level.
- Required XP: `ceil(250 * 1.015^(level - 1))`. The first level-up typically takes a few starter fish.
- Catch XP: `round(4 * sqrt(maximum fight HP) * (1 + 2 * effective difficulty))`, minimum 1. Uses actual fight HP after depth-quality adjustment and effective species/size difficulty, not damage dealt or remaining HP.
- Only a successful inventory catch grants XP. Missed bites, escaped fish, cancelled casts, full-bag releases, sales and transfers do not grant XP. Overflow carries into subsequent levels. Catch feedback shows earned XP and the new level.

## Abilities

At **every 20 levels** (20, 40, 60, 80, 100, ... 600), the player earns **one discretionary Special Ability upgrade point**. In **Personal Stats**, eligible ability cards pulse with a synchronized white border, white tone and lettering. Tap a card to open the enlarged, full-color preview and spend a point on **UNLOCK ABILITY** or **UPGRADE TO RANK N**; tap anywhere else to close. Unspent points accumulate in the saved `ShopLedger.angler` profile. The top level band says **Next special ability upgrade:**, or shows how many points are ready. The ordinary Fishing Attributes stat increase continues automatically at *every* level.

| Special Ability | Rank 1 effect | Each additional rank |
| --- | --- | --- |
| Lightning Strike | Passive effect on the **every charged Skill yank** during each fight; 5% max HP **total over 4 seconds**, capped at **2,000 total damage** (eight zaps: 50% immediately, the remaining 50% evenly split across seven later zaps by 4s) | +0.5 percentage points to total damage before the cap |
| Hookset Power | Automatic; 10% max HP when the fight starts | +2% max HP initial damage |
| Line Immortality | Passive; triggers once per fight at the equipped line's maximum distance. For 5 seconds, permits exceeding line length and prevents all positive tension gain and tension breaks, including when reeling. The screen edges, distance label and top status box pulse purple while active | +1 second |
| Siren's Call | Button; fish swims toward the player for 4 seconds, retaining existing water collision checks | +1 second |

A chosen rank 1 unlocks its ability in combat; extra ranks strengthen it. **Legacy saves are migrated without losing previous automatic unlocks:** those at levels 20/40/60/80 are assigned rank 1 in the corresponding abilities; any later unused milestone points remain available for new selections. The migration marker prevents granting the old unlocks twice.

Siren's Call is the only manually activated ability and is usable once per fish. Lightning Strike triggers on every skill use; Hookset triggers when hooking; Line Immortality triggers at maximum line distance once per fight. Immortality bypasses both FishingSystem's distance/tension snap logic and the later rendered-line range limiter while its timer is active; when it expires, normal distance and tension break rules resume immediately. Timers pause while combat is covered and reset when the fight ends. Hookset occurs after cast-quality health adjustment. All ability damage uses the existing authoritative damage owner and knockout presentation. The two future ability art slots stay empty.

Every unlocked charged Skill starts eight zaps across four seconds. Exactly one wave-conforming electric ripple spawns when the second numbered animation frame of each zap is displayed. Each ring keeps a random surface rotation, expands to a 10 m radius, and fades independently. No background ripple emitter runs between zaps.

All four supplied backdrops replace the previous art. Transparent margins are trimmed at runtime, each background fills the existing equal-sized card rectangle, and the text now uses the space freed by removing the old corner icons. The enlarged preview remains a uniformly scaled clone in the same fixed position for every ability.

Ability cards with an available upgrade retain their white flashing border/tint. The enlarged full-color preview intentionally **does not** flash. Rather than reconstructing its text, it clones and uniformly scales the selected small card itself (identical art geometry, labels, and text positions), recoloring the clone without the flashing overlay. The four small cards retain their current dimensions.

The caught-fish XP bar and the attribute-level-up notification each hold **2.25 seconds** before using the original 0.38-second slide-out animation.

## Validation

`Tools/AnglerProgression/Checks` compiles the actual progression model and combat owner with lightweight Unity stubs. Checks cover XP thresholds and overflow, all stat caps, legacy migration, selectable upgrade milestone boundaries, bonus damage, Hookset once-only behavior, burn damage/KO at coarse and 60 FPS steps, active timers, menu pause and reset between fish. Run with .NET 8: `dotnet run --project Tools/AnglerProgression/Checks/Checks.csproj`.

C# syntax was checked for Fishing and ShopWorld scripts. Unity is not available in the implementation environment; Play Mode/Android verification remains necessary for layout, touch interactions, save/relaunch, fish movement, and the full Unity build. No terrain, scene objects, or manually authored scenery was edited.

### Eight-zap Lightning Strike

Each unlocked skill use starts an independent eight-zap sequence at intervals of 4/7 seconds, beginning immediately and ending at 4 seconds. Half the ability damage is immediate; the other half is evenly shared by seven zaps with cumulative integer rounding. The 2,000 cap applies per activation. Repeated and overlapping skills retain their own complete sequences.

Each zap plays the nine frames once. First strike canvas: 9 m. Later strikes: 8.25 m (10% larger than the previous 7.5 m). Sound clips 1–4 map to the first four zaps; zaps 5–8 reuse clip 4. Spatial audio stays full volume within 12 m and attenuates logarithmically farther away. Each animation requests one ripple only when it actually displays frame 2.

Lightning damage labels use bright yellow, a black outline, and the bundled Bungee display font with an italic slant. Label-specific styling keeps simultaneous skill criticals separate, and pooled labels restore their ordinary font when reused. Font source: https://github.com/google/fonts/tree/main/ofl/bungee ; its supplied OFL license is included alongside the font.
