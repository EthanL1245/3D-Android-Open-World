"""Rebuild sample-overlapped fishing WAVs. Requires ffmpeg and numpy."""
from pathlib import Path
import subprocess
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCES = Path(__file__).resolve().parent / "Sources"
OUTPUT = ROOT / "Assets/Resources/Fishing/Audio"
RATE = 48000
OVERLAP = round(0.035 * RATE)


def decode(path):
    raw = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le",
        "-acodec", "pcm_f32le", "-ar", str(RATE), "-ac", "2", "-",
    ])
    return np.frombuffer(raw, dtype="<f4").reshape(-1, 2).copy()


def overlap(tail, head):
    # Equal-power fade keeps the join audible despite tiny MP3 silent lead-ins.
    phase = np.linspace(0, np.pi / 2, OVERLAP)[:, None]
    return tail * np.cos(phase) + head * np.sin(phase)


def sequence(first, repeat):
    assert min(len(first), len(repeat)) > 2 * OVERLAP
    seam = overlap(repeat[-OVERLAP:], repeat[:OVERLAP])
    middle = repeat[OVERLAP:-OVERLAP]
    cycle = np.concatenate((seam, middle))
    # First repeat starts 35 ms before the initial recording finishes. At the
    # lead-in boundary we are ready for the repeat tail/head overlap in cycle.
    lead = np.concatenate((first[:-OVERLAP],
                           overlap(first[-OVERLAP:], repeat[:OVERLAP]), middle))
    return lead, cycle


def save(name, data):
    assert np.max(np.abs(data)) < 1, f"Clipping in {name}"
    pcm = np.rint(data * 32767).astype("<i2")
    with wave.open(str(OUTPUT / (name + ".wav")), "wb") as out:
        out.setnchannels(2)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(pcm.tobytes())
    print(f"{name}: {len(data)} frames, {len(data) / RATE:.6f} s")


if __name__ == "__main__":
    initial = decode(SOURCES / "ReelInitial.wav")
    repeat = decode(SOURCES / "ReelEndRepeat.mp3")
    pull = decode(OUTPUT / "LinePull.mp3")
    reel_lead, reel_loop = sequence(initial, repeat)
    _, pull_loop = sequence(pull, pull)
    pull_lead = pull[:-OVERLAP]
    fast = decode(SOURCES / "LinePullFast.wav")
    _, fast_loop = sequence(fast, fast)
    fast_lead = fast[:-OVERLAP]
    for name, data in [("ReelLeadIn", reel_lead), ("ReelLoop", reel_loop),
                       ("LinePullLeadIn", pull_lead), ("LinePullLoop", pull_loop),
                       ("LinePullFastLeadIn", fast_lead), ("LinePullFastLoop", fast_loop)]:
        save(name, data)


    for mood in ("Calm", "Irritated", "Angry"):
        recording = decode(SOURCES / ("LinePull" + mood + ".wav"))
        _, cycle = sequence(recording, recording)
        save("LinePull" + mood + "LeadIn", recording[:-OVERLAP])
        save("LinePull" + mood + "Loop", cycle)

