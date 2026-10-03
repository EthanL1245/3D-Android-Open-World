# Fishing drag and swipe skill

Pull and Play; the existing FishingHUD and FishingBurstDamageRuntime build/use the controls automatically.

| Mode | Outward fish speed | Reel-burst damage | Positive tension gain | Passive effect |
|---|---:|---:|---:|---|
| Low | 1.5x | 0.5x | 0.5x | Normal release recovery |
| Medium | 1x | 1x | 1x | Existing behavior |
| High | 1x | 2x | 1x + 0.025 per second | No release recovery; one normal noncritical rod hit per second; skill charge |

Tension is internally 0–1, so 0.025 is 2.5 percentage points per second, in addition to ordinary fish resistance. Low scales positive resistance and reel tension, not recovery. The maximum-line-distance failure remains unchanged. Half damage retains fractional points between bursts; odd hits do not gain damage through rounding.

The drag selector follows the top of the movement joystick in canvas coordinates. During a fight the skill meter replaces all hotbar slots; the normal hotbar returns on catch, escape, cancellation or component disable. Retrieval of an unconscious fish shows a subdued label and disables drag controls.

High charges the meter in 12 seconds of active fight time. Other modes preserve earned charge. A full meter can be spent in any mode with a quick swipe beginning on TouchLookArea, completed in 0.04–0.35 seconds, at least 12% of the screen's shorter dimension (minimum 70 px), at a speed of at least 80% of that dimension per second. Joystick/reel/drag/menu pointer gestures cannot qualify. Existing camera look still works. The skill consumes its meter once and deals 10x a random **normal** damage roll of the equipped rod, independent of drag and crit multipliers.

All damage goes through the existing authoritative burst owner and its unconscious-fish presentation. Legacy frame damage is skipped while that owner is enabled to prevent premature KOs. No charge/passive damage while a menu covers the fight; no charge or skill use after unconsciousness. Every new fight starts Medium with an empty meter.

## Validation

Run `dotnet run --project Tools/FishingDrag/Checks` for tests executing the real damage owner with small Unity/scene stubs. Covers reel multipliers, fractional Low damage, passive High ticks, meter charge, swipe validation, one-time consumption, finishing skill hits, menu coverage and fight reset.

C# syntax checked separately for all changed scripts. Unity/Android Play validation is still needed for layout, multitouch swipes while holding REEL, drag changes, line breaks, KO/retrieval and hotbar restoration.
