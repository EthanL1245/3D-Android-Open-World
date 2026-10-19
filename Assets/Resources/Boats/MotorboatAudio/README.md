# Motorboat engine audio

`MotorboatEngineAnimation` loads these spatial mono Vorbis clips automatically
for the motorboat (legacy ID `sailboat`). No prefab rewiring is required.

- `start.ogg`: plays once when a driver takes the helm.
- `stop.ogg`: plays once after the driver leaves and speed reaches 18 m/s or less.
  Taking the helm again cancels a pending shutdown.
- `idle.ogg`: loops while driving without forward input, or below 1 m/s.
- `slow.ogg`: forward driving at 1–12 m/s.
- `moderate_speed.ogg`: forward driving at 12–24 m/s.
- `fast.ogg`: forward driving at 24–27 m/s.

Adjacent driving loops use broad equal-power crossfades over 8–16 and 21–27 m/s,
centered on 12 and 24 m/s. Volume smoothing uses a 0.25 second time constant.
When unoccupied, a previously running engine coasts with speed-based audio
until shutdown. An unused boat stays silent. Multiplayer uses replicated helm
and input state. After leaving the helm, forward damping is reduced from 0.65 to about 0.50,
for roughly 30% more coasting range on unobstructed water. Shallow-water braking,
collisions, vertical damping, driven acceleration and motor animation are unchanged.

Driving clips were rebuilt from the supplied MP3s with shared tonal filtering,
gentle volume leveling, a shared 18% low engine texture, matched -20 dBFS RMS,
and 450 ms loop-seam crossfades. Use `Tools/BoatSystem/RebuildMotorboatAudio.py`
with the original upload directory to reproduce them. All processing is offline.
Idle/start/stop recordings are unchanged. All clips remain 44.1 kHz mono Vorbis.
Recordings play at original pitch; inaudible loops stop to save Android voices.

Driving layers use 1.4x gain (0.672 peak per-layer volume); idle and ignition
retain their previous 0.48 gain. Speed crossfades are unchanged.
