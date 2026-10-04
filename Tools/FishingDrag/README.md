# Fishing drag and swipe skill

Pull and Play; the existing FishingHUD and FishingBurstDamageRuntime build/use the controls automatically.

| Mode | Outward fish speed | Reel-burst damage | Positive tension gain | Passive effect |
|---|---:|---:|---:|---|
| Low | 2x | 0.5x | 0.5x | 1.3x release recovery |
| Medium | 1x | 1x | 1x | Existing behavior |
| High | 1x | 2x | 1x reeling; 1x passive resistance + 0.225 per second | No release recovery; one normal noncritical rod hit per second; skill charge |

Tension is internally 0–1, so 0.225 is 22.5 percentage points per second, in addition to ordinary fish resistance. Low scales positive resistance and reel tension, not recovery. The maximum-line-distance failure remains unchanged. Half damage retains fractional points between bursts; odd hits do not gain damage through rounding.

Low's 2x escape multiplier applies to base swim speed before temperament, initial-burst and surge multipliers. High uses Medium’s reeling tension multiplier and adds passive tension at half the purple post-grace rate.

The drag selector is a draggable slider with whole-number values 0/1/2, a handle, and labeled Low/Medium/High notches. It follows the top of the movement joystick in canvas coordinates. During a fight the skill meter replaces all hotbar slots; the normal hotbar returns on catch, escape, cancellation or component disable. Retrieval of an unconscious fish shows a subdued label and disables drag controls.

High charges the meter in 5 seconds of active fight time. Other modes preserve earned charge. A full meter activates immediately on any nonzero drag movement on TouchLookArea, in any direction. There is no speed, distance, duration, or finger-release requirement; even a tiny upward look qualifies. Joystick/reel/drag/menu pointer gestures cannot qualify. Existing camera look still works. The skill consumes its meter once and deals 20x a random **normal** damage roll of the equipped rod, independent of drag and crit multipliers.

All damage goes through the existing authoritative burst owner and its unconscious-fish presentation. Legacy frame damage is skipped while that owner is enabled to prevent premature KOs. No charge/passive damage while a menu covers the fight; no charge or skill use after unconsciousness. Every new fight starts Medium with an empty meter.

## Validation

Run `dotnet run --project Tools/FishingDrag/Checks` for tests executing the real damage owner with small Unity/scene stubs. Covers reel multipliers, fractional Low damage, passive High ticks, meter charge, small look gestures in all four directions, one-time consumption, finishing skill hits, menu coverage and fight reset.

C# syntax checked separately for all changed scripts. Unity/Android Play validation is still needed for layout, multitouch swipes while holding REEL, drag changes, line breaks, KO/retrieval and hotbar restoration.


## Purple FRAGILE status

A 15% initial temperament roll can select FRAGILE; the existing different-mood reroll also includes it. Purple phases last 3.5–5 seconds. Outward swimming is exactly 0.2x species base speed, overriding mood/surge/initial-burst and drag escape multipliers. Reeling still pulls inward normally.

While the live fish is purple, Medium or High starts a two-second warning period. After that, tension rises by 45 percentage points per second, plus normal tension gains; passive recovery cannot cancel the surge. There is no direct timer-triggered snap: standard maximum-distance and sustained-full-tension rules still apply. Low stops the extra rise and clears the warning timer immediately, as do leaving purple or subduing the fish. The red circular status outline fills over the first two seconds, then flashes white at 8 Hz. Red, 30px LOOSEN DRAG text appears below the fish health bar with a white outline; that outline flashes red in the same phase after two seconds. The warning hides on Low.

The complete drag panel (including slider touch target, labels and handle) is scaled 1.3x; screen-edge clamping accounts for the enlarged size. Unity phone layout and rendering still require Play testing.

The drag thumb and its touch target are circular, with a 108-unit diameter (3× the original thumb height). The rail and slider track retain their original dimensions; the panel provides extra vertical space so the thumb does not cover the title or notch labels.


Skill hits deal 20× the base rod damage roll. Surviving fish are stunned for three seconds: no escape or resistance/tension generation, reeling remains possible. Purple danger resets and is suspended during stun. The supplied Stunned icon uses a white circular outline flashing progressively from 2 to 10 Hz; KO always takes precedence. Five translucent upward arrowheads ripple upward at the right of the skill bar only when charged. Existing any-direction skill gestures remain supported.

Skill activation plays SkillYank once, including lethal hits, and flashes a non-interactive soft white screen border for two seconds. High drag uses the same reeling tension multiplier as Medium, plus passive tension at 0.225 per second (half the purple post-grace ramp), whether reeling or resting. Temporary stun still suspends tension buildup.

Line-pull audio follows actual horizontal line payout speed relative to normal-drag angry speed (1–3x pitch; low+angry nominally 2x). Green and purple always play at exactly 1x regardless of drag. Both pre-overlapped lead-in and cycle receive 2x sample gain on isolated child sources. Pending intro joins follow pitch changes; cycle overlap remains baked into PCM. Existing pulling-only, release, stun and focus gates are preserved.

Fast payout (>1x pitch) uses the supplied LinePullFast recording with its own 35 ms sample-overlapped lead-in/cycle. At normal speed, including green and purple at any drag, the original recording plays. Both variants retain dynamic pitch, 2x gain and existing immediate-stop gates; only one variant is active at a time.
