# Cast-water splash animation

On a successful lure or bait landing, `FishingSystem.CastRoutine` triggers a one-shot, upright, camera-facing water-impact splash at the landing position. The animation follows the wave surface and uses the original water-landing sound.

## Frames

18 frames played at 20 FPS (~0.9 seconds), in this exact sequence:

`01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 15, 16, 18, 19, 20`.

Splash 14 and Splash 17 were deliberately removed. The original frame numbering is preserved so the source artwork matches the author's images.

All 18 PNGs and their `.meta` files are committed under `Assets/Resources/Fishing/CastSplash/`. They use 512px alpha textures suitable for Android. The animator loads them through `Resources.Load`, shares the unlit transparent material, and destroys each one-shot effect when playback finishes. No prefab or manual Unity wiring is necessary.

## Unity check

Pull `main` and allow the Unity Editor to import the PNGs. Cast worms, shrimp, squid and each lure to open ocean and pond water. Confirm a splash at the impact point and no splash on invalid land casts. Test on Android to verify transparency, orientation and performance. Existing hotspot bonuses and splash audio are unchanged.

## Bottom-aligned, smaller cast splash

Every selected PNG is vertically shifted within its original 512 × 512 transparent canvas so the lowest visible pixel stays at the bottom edge across all frames. The upright splash quad now anchors its bottom edge to the current water surface (0 baseline offset), preventing vertical bobbing caused by changing transparent margins. The world-space effect width is 0.8 m, one-third of its previous 2.4 m width. The splash remains non-looping and the original 18-frame order and sound are unchanged.
