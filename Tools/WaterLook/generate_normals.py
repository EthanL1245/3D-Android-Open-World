"""Build numerical, seamless normal-map data; no runtime texture generation.

Requires numpy and Pillow. Run from any directory. Integer Fourier frequencies
make wraparound continuous; many random phases/directions prevent a line lattice.
"""
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Game/Water/Textures"


def make_normal(seed, size=128):
    rng = np.random.default_rng(seed)
    k = np.fft.fftfreq(size) * size
    kx, ky = np.meshgrid(k, k)
    radius = np.hypot(kx, ky)
    # Broadband low and mid frequencies, not two dominant directional bands.
    envelope = (np.exp(-0.5 * ((radius - 6) / 3.0) ** 2)
                + 0.28 * np.exp(-0.5 * ((radius - 14) / 5.0) ** 2))
    envelope[radius < 2] = 0
    envelope[radius > 28] = 0
    spectrum = np.fft.fft2(rng.standard_normal((size, size))) * envelope
    dx = np.fft.ifft2(spectrum * (2j * np.pi * kx)).real
    dy = np.fft.ifft2(spectrum * (2j * np.pi * ky)).real
    scale = 0.28 / np.sqrt(np.mean(dx * dx + dy * dy))
    dx *= scale
    dy *= scale
    normals = np.stack((-dx, -dy, np.ones_like(dx)), axis=-1)
    normals /= np.linalg.norm(normals, axis=-1, keepdims=True)
    pixels = np.rint((normals * 0.5 + 0.5) * 255).clip(0, 255).astype(np.uint8)
    return pixels


if __name__ == "__main__":
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, seed in (("OceanRippleA", 48127), ("OceanRippleB", 91573)):
        destination = OUTPUT / (name + ".png")
        Image.fromarray(make_normal(seed)).save(destination, optimize=True)
        print(f"{destination.name}: {destination.stat().st_size} bytes, 128 x 128")
