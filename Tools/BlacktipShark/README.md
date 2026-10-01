# Blacktip shark and resting rear paddles

Pull and let Unity finish importing. `BlacktipSharkSetup` installs the prepared FBX,
texture, Animator and resource prefab automatically, with a Play/build gate.
`RowboatSetup` upgrades the existing generated rowboat to source version 3.
No Blender installation, ZIP selection or scene rebuild is needed on the player's PC.

- Stable species ID 15; retired ID 4 remains reserved.
- Brinebreak Isle: 5–14 kg. Deep Ocean: 12–30 kg. Suncrest: unavailable.
- 2% for every supported bait/lure in those two biomes except Bloody Bait
  (`lure:4`), temporarily 55%. Percentages are removed from the largest existing
  allocation first, then the next largest if needed. Each row remains 100%.
- Extreme fight tuning: 9,000–20,000 health, yellow movement 5.2 m/s, difficulty 1.
- Size curve is a gameplay approximation, kg = 5 × metres³, not a biological measurement.
- Uses the existing `RedSnapperPresentation` contract for aquarium trails and
  swimming, held flopping/rest, and the animated mouth anchor.
- Rear oars remain visible at their initial authored pose in solo play. Each pair
  advances only while its own rower supplies movement input, and pauses in its last pose.

Original shark authoring file: `ArtSources/BlacktipShark.blend`.
Prepared runtime sources: `Assets/_Game/Reef/Source/BlacktipShark*`.
Export preserves the supplied rig, texture UVs and 20-frame swim animation while
normalizing `.R` bone names to the shared fish naming contract.

Checks:

```sh
dotnet run --project Tools/BlacktipShark/Checks/Checks.csproj -- .
blender --background --python Tools/BlacktipShark/verify_export.py -- Assets/_Game/Reef/Source/BlacktipShark.fbx
```

The rules checks execute the actual runtime CSV parser and species selector over all
27 probability tables and verify stable IDs, weight bounds, sizes and fight tuning.
The Blender check reimports the committed FBX and checks rig names, UVs, skin weights,
head-attached detail, tail motion and loop closure. C# syntax is also checked.
Unity editor compilation, phone input, held appearance and aquarium Play Mode still
require a Unity run; these checks do not substitute for that.
