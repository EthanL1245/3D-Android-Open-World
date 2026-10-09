# Fishing hotspots

Hotspots install automatically when the main FishingSystem starts. No scene rebuild
or terrain regeneration is required. Source art is copied unchanged from Ripple.png
and Bubble.png. Texture import limits both to 512px with alpha and mipmaps on Android.

- A pool of at most five effects, each 5.6m across; shared materials and ripple mesh.
- The entire effect stays within 60m of the player and at least 15m from terrain land
  and exposed static mesh rocks. No Palm Pond, shop, or home hotspots.
- At most eight placement probes and one new spot per second. Fewer than five is
  expected where insufficient valid water exists. Spots last 90–150 seconds.
- 2.5s ripple lifetime, 0.7 rings/sec, 2m initial size scaled from 40% to 250%.
- Horizontal subdivided mesh particles follow the SAME wave phases and spatial
  storm blend as OceanWater. This replaces a plain horizontal billboard so ring
  edges also conform to wave crests. Bubbles and splash droplets use billboards.
- Bubble clusters every 1.3–2.8s; a subtle five-droplet splash every 4–8s.
- Inactive/out-of-range spots stop and clear particles, and are reused.

A successful bait or lure landing inside the 2.8m radius locks +10% fish weight at
the edge through +50% at the centre. The bonus is applied once, before existing
cast-depth quality, temperament and health calculations. Existing species-to-length
rules determine the model size. This can exceed the normal biome weight maximum.
Species selection still uses the player's zone at cast release. Crossing hotspots
while retrieving, moving between zones, or a hotspot expiring cannot change the
locked catch. The waiting/reeling status displays the earned bonus.

The reusable resource prefab is Assets/Resources/Fishing/Hotspots/FishingHotspot.prefab.
The manager initializes its Ripple Particles, Bubble Particles and Splash Particles
children when creating the pool. Placement constants live in FishingHotspotManager;
particle settings live in FishingHotspotEffect.

## Unity play check

1. Pull main and allow Unity to compile/import. Play the saved fishing scene.
2. Near open water, verify up to five hotspots appear over the first few seconds.
   Observe rings conforming to both calm and Brinebreak waves, and clustered bubbles.
3. Land a worm/shrimp/squid cast and a lure cast at the centre, edge and outside.
   Only the first two show a bonus; centre is +50%, edge is +10%.
4. Retrieve an initially missed lure through a hotspot: no bonus should be awarded.
5. Hook inside a hotspot, then move away/let it expire: catch weight/species remain
   unchanged. Cross an island boundary during the fight to verify the same.
6. Travel to shop/home and back, and move more than 60m: old particles disappear,
   and the pool repopulates with no more than five objects active.
7. On Android, check visibility from 20–30m and that no material renders pink.

Source/asset and numerical checks were run outside Unity. Editor/Android rendering
and gameplay still require this play check; Unity is unavailable in the build workspace.
