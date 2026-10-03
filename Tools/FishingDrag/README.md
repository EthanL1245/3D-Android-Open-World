# Fishing drag and swipe skill

Pull and Play; the existing FishingHUD and FishingBurstDamageRuntime build/use the controls automatically.

| Mode | Outward fish speed | Reel-burst damage | Positive tension gain | Passive effect |
|---|---:|---:|---:|---|
| Low | 2x | 0.5x | 0.5x | 1.3x release recovery |
| Medium | 1x | 1x | 1x | Existing behavior |
| High | 1x | 2x | 2x reeling; 1x passive resistance + 0.025 per second | No release recovery; one normal noncritical rod hit per second; skill charge |

Tension is internally 0–1, so 0.025 is 2.5 percentage points per second, in addition to ordinary fish resistance. Low scales positive resistance and reel tension, not recovery. The maximum-line-distance failure remains unchanged. Half damage retains fractional points between bursts; odd hits do not gain damage through rounding.

Low's 2x escape multiplier applies to base swim speed before temperament, initial-burst and surge multipliers. High doubles only the reeling tension term; passive resistance and the extra 0.025 per second remain unchanged.

The drag selector is a draggable slider with whole-number values 0/1/2, a handle, and labeled Low/Medium/High notches. It follows the top of the movement joystick in canvas coordinates. During a fight the skill meter replaces all hotbar slots; the normal hotbar returns on catch, escape, cancellation or component disable. Retrieval of an unconscious fish shows a subdued label and disables drag controls.

High charges the meter in 5 seconds of active fight time. Other modes preserve earned charge. A full meter activates immediately on any nonzero drag movement on TouchLookArea, in any direction. There is no speed, distance, duration, or finger-release requirement; even a tiny upward look qualifies. Joystick/reel/drag/menu pointer gestures cannot qualify. Existing camera look still works. The skill consumes its meter once and deals 10x a random **normal** damage roll of the equipped rod, independent of drag and crit multipliers.

All damage goes through the existing authoritative burst owner and its unconscious-fish presentation. Legacy frame damage is skipped while that owner is enabled to prevent premature KOs. No charge/passive damage while a menu covers the fight; no charge or skill use after unconsciousness. Every new fight starts Medium with an empty meter.

## Validation

Run `dotnet run --project Tools/FishingDrag/Checks` for tests executing the real damage owner with small Unity/scene stubs. Covers reel multipliers, fractional Low damage, passive High ticks, meter charge, small look gestures in all four directions, one-time consumption, finishing skill hits, menu coverage and fight reset.

C# syntax checked separately for all changed scripts. Unity/Android Play validation is still needed for layout, multitouch swipes while holding REEL, drag changes, line breaks, KO/retrieval and hotbar restoration.


## Purple FRAGILE status

A 15% initial temperament roll can select FRAGILE; the existing different-mood reroll also includes it. Purple phases last 3.5–5 seconds. Outward swimming is exactly 0.2x species base speed, overriding mood/surge/initial-burst and drag escape multipliers. Reeling still pulls inward normally.

While the live fish is purple, two consecutive seconds in Medium or High snaps the line. Low resets this countdown immediately, as does leaving the purple status. A non-interactive 6px red border traces the rectangular status box clockwise from its top centre in proportion to elapsed unsafe time. Standard distance/tension break rules still apply. Unconscious fish have no purple countdown.

The complete drag panel (including slider touch target, labels and handle) is scaled 1.3x; screen-edge clamping accounts for the enlarged size. Unity phone layout and rendering still require Play testing.
