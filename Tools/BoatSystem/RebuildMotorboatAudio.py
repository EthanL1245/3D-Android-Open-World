"""Rebuild driving loops from the supplied MP3s. Requires ffmpeg, numpy, scipy.
Usage: python Tools/BoatSystem/RebuildMotorboatAudio.py /path/to/upload
"""
from pathlib import Path
import sys, subprocess
import numpy as np
from scipy.ndimage import gaussian_filter1d
from scipy.signal import butter, sosfilt

RATE = 44100
source = Path(sys.argv[1])
dest = Path(__file__).resolve().parents[2] / 'Assets/Resources/Boats/MotorboatAudio'
files = {'slow': 'Motorboat slow(1).mp3',
         'moderate_speed': 'Motorboat moderate speed(1).mp3',
         'fast': 'Motorboat fast(1).mp3'}

def rms(x):
    return np.sqrt(np.mean(x*x) + 1e-12)

def seam(x, seconds=0.45):
    n = min(int(seconds*RATE), len(x)//4)
    t = np.linspace(0, np.pi/2, n)
    return np.concatenate((x[n:-n], x[-n:]*np.cos(t)+x[:n]*np.sin(t)))

loops = {}
for name, filename in files.items():
    data = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(source/filename),
                                   '-f', 'f32le', '-ac', '1', '-ar', str(RATE), '-'])
    x = np.frombuffer(data, dtype='<f4').astype(np.float64)
    # Shared tonal shaping and gentle offline leveling, no runtime DSP cost.
    x = sosfilt(butter(2, [55, 4800], btype='bandpass', fs=RATE, output='sos'), x)
    # Calculate the envelope at 100 Hz to avoid expensive full-rate smoothing.
    block = RATE//100
    energy = np.array([np.mean(x[i:i+block]**2) for i in range(0, len(x), block)])
    envelope = np.sqrt(gaussian_filter1d(energy, 20) + 1e-9)
    gain = np.clip(np.median(envelope)/envelope, 0.65, 1.6)
    x *= np.interp(np.arange(len(x)), np.arange(len(gain))*block, gain)
    x = seam(x)
    loops[name] = x/rms(x)

# A quiet common low engine texture reduces the tonal jump between recordings.
bed = sosfilt(butter(2, 650, fs=RATE, output='sos'), loops['moderate_speed'])
bed = seam(bed)
bed /= rms(bed)
for name, x in loops.items():
    common = np.resize(bed, len(x))
    x = seam(0.82*x + 0.18*common)
    x *= 0.1/rms(x)  # Same -20 dBFS RMS across all three driving recordings.
    if np.max(np.abs(x)) > 0.9:
        raise ValueError('Unexpected peak: reduce common RMS target for all clips')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ac', '1',
                    '-ar', str(RATE), '-i', '-', '-c:a', 'libvorbis', '-q:a', '5',
                    str(dest/(name+'.ogg'))], input=x.astype('<f4').tobytes(), check=True)
    print(name, 'duration', round(len(x)/RATE, 2), 'RMS', round(float(rms(x)), 4),
          'peak', round(float(np.max(np.abs(x))), 3))
