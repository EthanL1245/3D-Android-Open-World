# Motorboat engine audio

`MotorboatEngineAnimation` loads these spatial mono Vorbis clips automatically
for the motorboat (legacy ID `sailboat`). No prefab rewiring is required.

- `start.ogg`: plays once when a driver takes the helm.
- `stop.ogg`: plays once after the driver leaves and speed reaches 5 m/s or less.
  Taking the helm again cancels a pending shutdown.
- `idle.ogg`: loops while driving without forward input, or below 1 m/s.
- `slow.ogg`: forward driving at 1–8 m/s.
- `moderate_speed.ogg`: forward driving at 8–21 m/s.
- `fast.ogg`: forward driving at 21–27 m/s.

Adjacent driving loops crossfade only over 7.5–8.5 and 20.5–21.5 m/s.
When unoccupied, a previously running engine coasts with speed-based audio
until shutdown. An unused boat stays silent. Multiplayer uses replicated helm
and input state. Physics and motor animation are unchanged.

Clips were converted from the six supplied MP3s to 44.1 kHz mono Vorbis.
Loops have a 50 ms seam crossfade; one-shots preserve their complete duration.
Peaks exceeding 0.9 were reduced before encoding for headroom. Recordings play
at their original pitch. Inaudible loops stop to save Android audio voices.
