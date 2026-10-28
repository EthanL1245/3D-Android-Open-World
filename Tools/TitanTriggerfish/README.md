# Titan Triggerfish — stable species ID 32

**Location:** Suncrest Beach, represented by the existing Suncrest coastal
fishing biome ID `suncrest-reef` (index 0). All four other fishing biomes
have zero catch chance and unavailable weight ranges for this species.

**Bait odds:** With shrimp on Suncrest Beach, **Titan Triggerfish = 20%**
and **Picasso Triggerfish = 20%**. The remaining species share 60% in the
editable CSV. Both triggerfish have 3% chance with other Suncrest baits/lures.
Every complete biome/bait row sums to 100%. The old Snapper Island, Bluewater,
Brinebreak and Deep Ocean distributions remain unchanged. Existing species
IDs and saved records remain stable.

**Fighting mechanics:** Twice Picasso Triggerfish's difficulty by base
difficulty (0.84 vs 0.42) and hit points at corresponding weight percentiles
(230–684 vs 115–342). Both share 3.152 m/s baseline yellow swim speed;
difficulty does not double the fish's travel speed or override its own
natural physical length. The normal fight, hook, reel, damage, skills,
inventory, ambient fish, fish index, aquariums and multiplayer presentation
use the shared fish system. Sell endpoints are 80–600 coins.

**Real-world physical size:** *Balistoides viridescens* (titan triggerfish).
FishBase lists 75 cm TL as its maximum and the Bayesian relationship

    W in grams = 0.03631 × (total length in cm)^2.93

The dedicated `FishSizeTable` curve uses 5 cm steps from 5–75 cm.
Its chosen catch range 0.10–11.32 kg corresponds to roughly 15–75 cm
total length, not Picasso's much smaller 0.06–0.56 kg. This is a
species-specific model estimate; not a measured weight for every animal.
Reference: https://fishbase.org/summary/6026

**Unity model pipeline:** Supplied `Titan Triggerfish.fbx` (animated
FBX), `Titan Triggerfish Texture.png`, and optional original Blender
`Titan Triggerfish.blend`. No Blender installation or manual export is
required with the FBX. `Assets/_Game/Editor/TitanTriggerfishInstaller.cs`
auto-discovers local FBX and PNG in this project's Assets or the user's
Downloads/Desktop/Documents and common OneDrive folders. It copies them to:

- `Assets/_Game/Reef/Source/TitanTriggerfish.fbx`
- `Assets/_Game/Reef/Source/TitanTriggerfishTexture.png`

Then `SeaBassImporter.InstallModel` generates:
`Assets/Resources/Fishing/TitanTriggerfish.prefab` with textured skinned
rig, the single supplied swimming take, standard procedural held-fish motion, correct mouth pin and
`RedSnapperPresentation`. The supplied FBX contains **one** `ArmatureAction.001` animation stack,
so the normal Swim controller is generated from that take. As with other
snappers, `CaughtFishFlop` supplies held-fish struggling motion; do not try
to import a second nonexistent HeldStruggle take.
Manual fallback: **Tools > Open World > Locate Titan Triggerfish Files**;
forced rebuild: **Tools > Open World > Rebuild Titan Triggerfish**.

These binary assets stay on the user's PC until they are committed to Git.
Pulling the GitHub code alone cannot transfer assets only present in
a ChatGPT upload. If necessary, download the accompanying Unity source ZIP
and unpack its Assets directory into the project before reopening Unity.

Server island tournament cap: 11.32 kg normal max × 1.5 hotspot bonus =
16.98 kg. Server deployment must receive the new `Server/runtime` code
to recognize the added species in tournaments.

Validation: `node Tools/TitanTriggerfish/verify_rules.mjs`
and `node --test Server/tests/*.test.mjs`. Full Unity Editor import
and Play Mode verification require Unity locally.
