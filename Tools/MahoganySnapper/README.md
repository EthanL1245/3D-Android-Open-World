# Mahogany Snapper (save-safe ID 30)

Snapper Island only (biome 4). Chances equal Yellowtail Snapper in all nine
bait/lure rows. Existing Snapper Island catch odds are proportionally rebalanced
with largest remainder rounding to retain integer 100% rows.
The new species is very slightly harder than Yellowtail Snapper: base difficulty
0.48 vs 0.46; health 96–315 vs 90–300; swim speed 2.90 vs 2.85 m/s.
Catches weigh 0.25–4.1 kg. Sale range 40–250 coins.

Uses the shared fish visual and presentation factory, including index/shop,
inventory, held fish, aquarium, ambient previews, multiplayer and tournaments.

IMPORTANT: A textured **rigged, animated FBX** from the Blender source
is required for the authored appearance. An STL does not contain that rig,
UVs or swim animation. The supplied .blend/.stl/.png are not yet committed
as repo binary assets. Unity must have these exact files:
Assets/_Game/Reef/Source/MahoganySnapper.fbx
Assets/_Game/Reef/Source/MahoganySnapperTexture.png

When added, Unity regenerates Resources/Fishing/MahoganySnapper.prefab
automatically using SeaBassImporter, or run Tools > Open World >
Rebuild Mahogany Snapper. Without them, the generic fish shape is used rather
than failing a build. No special flatfish rotation or held struggle clip.
Unity Play Mode and visual animation verification remain outstanding.
