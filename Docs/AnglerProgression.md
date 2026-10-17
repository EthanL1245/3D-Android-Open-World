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

| Level | Ability | Initial balance |
| --- | --- | --- |
| 20 | Burning Fish | Button; 2% max HP per second for 5 seconds, in ten half-second ticks |
| 40 | Hookset Power | Automatic; 10% max HP when the fight starts |
| 60 | Line Immortality | Button; 5 seconds without the reeling-specific tension increase |
| 80 | Siren's Call | Button; fish swims toward the player for 4 seconds, using existing water collision checks |

Active abilities are usable once per fish. Timers pause while combat is covered, and reset when the fight ends. Hookset occurs after cast-quality health adjustment. All ability damage uses the existing authoritative damage owner and knockout presentation. Line Immortality does not remove passive drag/mood tension or prevent a line-distance break. No new abilities are invented for levels 100/120; those two visual slots remain empty.

## Validation

`Tools/AnglerProgression/Checks` compiles the actual progression model and combat owner with lightweight Unity stubs. Checks cover XP thresholds and overflow, all stat caps, migration, unlock boundaries, bonus damage, Hookset once-only behavior, burn damage/KO at coarse and 60 FPS steps, active timers, menu pause and reset between fish. Run with .NET 8: `dotnet run --project Tools/AnglerProgression/Checks/Checks.csproj`.

C# syntax was checked for Fishing and ShopWorld scripts. Unity is not available in the implementation environment; Play Mode/Android verification remains necessary for layout, touch interactions, save/relaunch, fish movement, and the full Unity build. No terrain, scene objects, or manually authored scenery was edited.
