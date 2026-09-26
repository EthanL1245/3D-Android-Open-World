# Standard fish implementation contract

This file is the permanent checklist for adding catchable fish to the game. New fish should follow this path unless there is a deliberate gameplay reason to do something different.

## Save-safe species data

- Append a new stable species ID in `FishCatalog`. Never reuse or renumber an existing/retired ID because catches are saved by ID.
- Add the ID to `FishCatalog.ActiveIds`.
- Add a real weight/length row to `FishSizeTable` in exactly the same ID order.
- Add raw species weights to every relevant `ReefCatalog` bait/lure table. The catalog normalizes those tables at runtime, so new species can be appended without forcing every raw table to total exactly 100.
- Set fight `Difficulty`, coastal min/max weight, sale value and fallback colors in `FishCatalog`.
- Keep the tiny-fish economy rule: fish at or below 15 cm stay nearly worthless regardless of rarity.
- Open-ocean catches may exceed the coastal max weight; `FishSizeTable` continues the species' final length/weight trend with its existing safety cap.

## Authored model contract

For a normal authored fish, the preferred source is a ZIP containing one `.blend`, one `.stl` and one texture image. The Blender file should contain:

- at least one skinned fish mesh;
- a five-bone chain named `Bone` through `Bone.004`;
- an armature;
- an authored looping swim action;
- UV0 texture coordinates.

Export the Blender rig/action to FBX, convert the texture to PNG, then pass it through `SeaBassImporter.InstallModel(assetName, prefabPath)`. Despite the historical class name, that importer is the standard generic five-bone fish prefab builder.

It normalizes the authored model, keeps the swim animation, creates the real lip/mouth anchor and adds `RedSnapperPresentation`.

## REQUIRED shared behavior

Every standard authored fish MUST use the same presentation path as the existing fish. Do not add species-specific aquarium or held-fish hacks when this generic path works.

`RedSnapperPresentation` is currently the shared authored-fish presentation contract. It provides both:

1. **Aquarium / pond train-track swimming** — `ReefFishTrail` calls `SetAquariumTrail(...)`, so the body/bones follow the habitat trail instead of sliding rigidly around the tank.
2. **Held fish flopping** — `FishingSystem` calls `SetHeld(true/false)`, so the held fish uses the same resting pose, sway and periodic frantic/flopping behavior as the other authored fish.

The prefab must also contain `AuthoredMouthAnchor` on the actual lips so held-fish line placement stays attached to the mouth.

## Visual routing

Register the prefab in `FishVisualFactory` under `Resources/Fishing/<FishName>`. That ensures the same authored fish appears in:

- the catch/fight presentation;
- held fish view;
- fish bag and shop previews;
- aquarium/pond residents;
- ambient fish where applicable.

Do not create a separate preview-only or aquarium-only model unless a feature explicitly requires it.

## Required verification for every new fish

Before considering a fish finished, verify all of these:

- it can actually be rolled/caught with the intended odds;
- its fight difficulty matches the requested balance;
- its min/max and open-ocean sizes look correct;
- Fish Index shows it and reports sensible size/rarity information;
- its real texture/model appears instead of the procedural fallback;
- the authored swim animation plays;
- aquarium/pond movement follows the train-track/head-trail deformation path;
- holding it uses the standard flop/rest/sway behavior;
- the fishing line terminates at its mouth anchor;
- inventory/shop previews render the real prefab;
- save/reload preserves the species correctly;
- very small specimens obey the low-value tiny-fish rule.

When future fish are added, this checklist is part of the task by default; it should not need to be requested again.
