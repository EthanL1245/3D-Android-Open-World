# Starter lure and gauge update

Pull main and press Play after Unity compiles. No installer rerun is required. Open the bait/lure shortcut below the island index, then select **Starter Lure**. Every player has it permanently, with no purchase or consumable charge; existing worms and specialty bait remain available.

Cast normally, then hold REEL to retrieve. Letting go stops retrieval and gives no bite opportunity. A lure bite immediately starts the existing fight, showing HP and tension while preserving the held REEL input. No release or HOOK tap is needed; lifting your finger during the fight still stops reeling normally. A retrieve without a bite returns to CAST. Canceling, menus, and swimming use the existing cancellation paths.

Each completed stationary 30 m retrieve has a 50% bite probability, not a guaranteed alternating catch. Short casts scale this probability linearly (5 m: about 8.3%; 15 m: 25%). The probability is integrated over retrieved water distance, so frame rate, pauses and reel speed do not change the full-retrieve chance. The usable water section is measured at landing and stops at the first shoreline; pauses cannot farm bites. Moving the player does not extend this cast's fixed retrieve path or bite budget.

Lure sizes use the ORIGINAL cast distance: longer casts shift the distribution toward larger fish. Worms/shrimp/squid retain depth-based sizes and passive bites. Palm Pond still produces 5–12 cm fish; lure distance biases sizes within that small range.

The index displays the actual equipped whole-percent species table, conditional on a bite. Natural rarity labels remain the same. Both tuna always retain their 2:1 ratio.

| Fish | Worms | Shrimp | Squid | Starter lure |
|---|---:|---:|---:|---:|
| Blue Mackerel | 20 | 11 | 14 | 4 |
| Red Snapper | 11 | 15 | 8 | 5 |
| Sea Bass | 13 | 8 | 10 | 5 |
| Yellowtail | 6 | 3 | 20 | 24 |
| Yellowfin Tuna | 2 | 1 | 6 | 18 |
| Yellow Goatfish | 22 | 30 | 15 | 4 |
| Black Spot Goatfish | 22 | 30 | 15 | 4 |
| Bigeye Tuna | 4 | 2 | 12 | 36 |
| Total | 100 | 100 | 100 | 100 |

The gauge is mirrored to CAST's upper-left and enlarged again: green at the lower-left endpoint, red at the top/right endpoint. Distance caption stays above it. The world fish indicator shrinks from 360×92 to 270×72 reference pixels while retaining the same 24-point text.

Validation: 55,619 C# rules/save assertions and project syntax parsing passed; commerce regression checks also passed. New checks cover zero stationary bite chance, 50% full-retrieve chance, time-step invariance, permanent-lure consumption, whole-percent equipped odds and distance/size monotonicity. Unity compilation, rendering, controls and Android performance still require local testing.

Local checks: equip lure and restart; confirm selection persists and no quantity depletes. Leave a cast idle for a minute (no bite); retrieve/pause/resume and observe strikes only while retrieving. Test a no-bite return, uninterrupted held-input transition into the fight, ordinary bait's passive bites, swimming cancellation, pond retrieval, and index odds after switching every bait. Check gauge/caption positioning and the smaller fish indicator on the phone.
