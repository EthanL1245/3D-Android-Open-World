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
| Burning Fish | Button; 5% max HP **total over 5 seconds**, capped at **2,000 total damage** (ten half-second ticks) | +0.5 percentage points to total damage before the cap |
| Hookset Power | Automatic; 10% max HP when the fight starts | +2% max HP initial damage |
| Line Immortality | Button; 5 seconds without the reeling-specific tension increase | +1 second |
| Siren's Call | Button; fish swims toward the player for 4 seconds, retaining existing water collision checks | +1 second |

A chosen rank 1 unlocks its ability in combat; extra ranks strengthen it. **Legacy saves are migrated without losing previous automatic unlocks:** those at levels 20/40/60/80 are assigned rank 1 in the corresponding abilities; any later unused milestone points remain available for new selections. The migration marker prevents granting the old unlocks twice.

Active abilities are usable once per fish. Timers pause while combat is covered, and reset when the fight ends. Hookset occurs after cast-quality health adjustment. All ability damage uses the existing authoritative damage owner and knockout presentation. Line Immortality does not remove passive drag/mood tension or prevent a line-distance break. The two future ability art slots stay empty.

Burning Fish turns the rendered fishing line red for its active five-second duration and surrounds the bobber/fight-panel fish status icons with a randomized, rapid, looping animation of the six supplied fire frames. The normal line color and icons return as soon as the effect ends or the fight is cancelled. The six frames are packaged in a compact `Resources/Fishing/StatusIcons/BurningFlamesAtlas.txt` PNG atlas (base64-encoded for TextAsset loading), so no scene changes or manual imports are needed.

Ability cards with an available upgrade retain their white flashing border/tint. The enlarged full-color preview intentionally **does not** flash, uses the exact same normalized text positions as the small cards, and the four small cards are taller (widths and horizontal spacing unchanged).

The caught-fish XP bar and the attribute-level-up notification each hold **2.25 seconds** before using the original 0.38-second slide-out animation.

## Validation

`Tools/AnglerProgression/Checks` compiles the actual progression model and combat owner with lightweight Unity stubs. Checks cover XP thresholds and overflow, all stat caps, legacy migration, selectable upgrade milestone boundaries, bonus damage, Hookset once-only behavior, burn damage/KO at coarse and 60 FPS steps, active timers, menu pause and reset between fish. Run with .NET 8: `dotnet run --project Tools/AnglerProgression/Checks/Checks.csproj`.

C# syntax was checked for Fishing and ShopWorld scripts. Unity is not available in the implementation environment; Play Mode/Android verification remains necessary for layout, touch interactions, save/relaunch, fish movement, and the full Unity build. No terrain, scene objects, or manually authored scenery was edited.
