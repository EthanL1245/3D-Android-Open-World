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
| Lightning Strike | Passive effect on the **every charged Skill yank** during each fight; 5% max HP **total over 5 seconds**, capped at **2,000 total damage** (four zaps: 50% immediately, the remaining 50% split across 1.67s, 3.33s and 5s) | +0.5 percentage points to total damage before the cap |
| Hookset Power | Automatic; 10% max HP when the fight starts | +2% max HP initial damage |
| Line Immortality | Button; 5 seconds without the reeling-specific tension increase | +1 second |
| Siren's Call | Button; fish swims toward the player for 4 seconds, retaining existing water collision checks | +1 second |

A chosen rank 1 unlocks its ability in combat; extra ranks strengthen it. **Legacy saves are migrated without losing previous automatic unlocks:** those at levels 20/40/60/80 are assigned rank 1 in the corresponding abilities; any later unused milestone points remain available for new selections. The migration marker prevents granting the old unlocks twice.

The manually activated abilities are usable once per fish; Lightning Strike triggers on every skill use. Timers pause while combat is covered, and reset when the fight ends. Hookset occurs after cast-quality health adjustment. All ability damage uses the existing authoritative damage owner and knockout presentation. Line Immortality does not remove passive drag/mood tension or prevent a line-distance break. The two future ability art slots stay empty.

When any charged Skill yank triggers Lightning Strike, the supplied electric ripple appears on the water at the current line endpoint every 0.35 seconds during the five-second damage window (about 15 rings). Each ring expands to a 10 m radius and fades to zero alpha over 1.6 seconds; the final rings finish fading naturally. The shared hotspot shader conforms the subdivided ring mesh to ocean waves. Texture UVs trim the supplied image's transparent canvas and undo its baked-in foreshortening before projecting it onto water. No flame icon overlays or red line tint remain. Existing ability ranks, resource keys, and component references are preserved.

All four supplied backdrops replace the previous art. Transparent margins are trimmed at runtime, each background fills the existing equal-sized card rectangle, and the text now uses the space freed by removing the old corner icons. The enlarged preview remains a uniformly scaled clone in the same fixed position for every ability.

Ability cards with an available upgrade retain their white flashing border/tint. The enlarged full-color preview intentionally **does not** flash. Rather than reconstructing its text, it clones and uniformly scales the selected small card itself (identical art geometry, labels, and text positions), recoloring the clone without the flashing overlay. The four small cards retain their current dimensions.

The caught-fish XP bar and the attribute-level-up notification each hold **2.25 seconds** before using the original 0.38-second slide-out animation.

## Validation

`Tools/AnglerProgression/Checks` compiles the actual progression model and combat owner with lightweight Unity stubs. Checks cover XP thresholds and overflow, all stat caps, legacy migration, selectable upgrade milestone boundaries, bonus damage, Hookset once-only behavior, burn damage/KO at coarse and 60 FPS steps, active timers, menu pause and reset between fish. Run with .NET 8: `dotnet run --project Tools/AnglerProgression/Checks/Checks.csproj`.

C# syntax was checked for Fishing and ShopWorld scripts. Unity is not available in the implementation environment; Play Mode/Android verification remains necessary for layout, touch interactions, save/relaunch, fish movement, and the full Unity build. No terrain, scene objects, or manually authored scenery was edited.

### Above-water Lightning Strike animation and sound

Each unlocked skill use starts an independent four-zap sequence at 0, 5/3, 10/3 and 5 seconds. Half of the total damage is immediate; the remaining half is divided between the later three zaps using cumulative rounding so the exact total (capped at 2,000 per activation) is paid. Overlapping activations do not erase previous pending damage. Combat events trigger both the visible zap and its matching sound, including the final event at the end of the timer.

Each zap plays all nine numbered PNGs once, in order, on an upright quad facing the player at the fish line endpoint. The first zap uses a 9 m canvas (+50%); later zaps use 7.5 m (+25%). All frames retain a common waterline anchor. The four supplied MP3s map to zaps 1–4. Each uses a spatial AudioSource at full volume through 12 m, logarithmic attenuation beyond 12 m, and no Doppler pitch shift. Visuals and sounds pause with combat menus; disable cleans up active strikes. Expanding water ripples retain their independent cadence and fixed random yaw.
