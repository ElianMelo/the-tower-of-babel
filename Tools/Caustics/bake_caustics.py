"""Bakes a seamless, looping caustics flipbook by photon splatting through a periodic wave surface.

Rays fall straight down through a height field made of integer-wavevector waves (tiles in space) with
integer temporal harmonics (loops in time). Each ray bends by the surface slope and lands on the floor;
the landing density is the caustic. Output: one atlas PNG (cols x rows frames), 8-bit grey.
"""
import sys
import numpy as np
from PIL import Image

def bake(out, size=512, frames=32, cols=8, supersample=3, waves=28, depth=0.055, kmax=7, seed=7, blur=0.65, gain=0.55):
    rng = np.random.default_rng(seed)
    ks = []
    while len(ks) < waves:
        k = rng.integers(-kmax, kmax + 1, 2)
        n = np.hypot(*k)
        if n < 1.5 or any((k == q).all() or (k == -q).all() for q in ks):
            continue
        ks.append(k)
    ks = np.array(ks, float)
    kn = np.hypot(ks[:, 0], ks[:, 1])
    amp = kn ** -1.6 * rng.uniform(.6, 1.0, waves)
    amp /= np.sqrt((amp ** 2 * kn ** 2).sum())  # unit RMS slope
    harmonics = rng.integers(1, 3, waves) * rng.choice([-1, 1], waves)
    phase0 = rng.uniform(0, 2 * np.pi, waves)

    m = size * supersample
    g = (np.arange(m) + .5) / m
    X, Y = np.meshgrid(g, g)
    X = X.ravel(); Y = Y.ravel()
    freq = np.fft.fftfreq(size)
    fx, fy = np.meshgrid(freq, freq)
    kernel = np.exp(-2 * (np.pi * blur) ** 2 * (fx ** 2 + fy ** 2))

    rows = frames // cols
    atlas = np.zeros((rows * size, cols * size), np.uint8)
    for f in range(frames):
        t = f / frames
        gx = np.zeros_like(X); gy = np.zeros_like(Y)
        for i in range(waves):
            arg = 2 * np.pi * (ks[i, 0] * X + ks[i, 1] * Y) + phase0[i] + 2 * np.pi * harmonics[i] * t
            s = -np.sin(arg) * amp[i] * 2 * np.pi
            gx += s * ks[i, 0]; gy += s * ks[i, 1]
        lx = (X - depth * gx) * size; ly = (Y - depth * gy) * size
        x0 = np.floor(lx - .5); y0 = np.floor(ly - .5)
        fxw = lx - .5 - x0; fyw = ly - .5 - y0
        img = np.zeros(size * size)
        for dx, dy, w in ((0, 0, (1 - fxw) * (1 - fyw)), (1, 0, fxw * (1 - fyw)), (0, 1, (1 - fxw) * fyw), (1, 1, fxw * fyw)):
            ix = (x0 + dx).astype(np.int64) % size; iy = (y0 + dy).astype(np.int64) % size
            img += np.bincount(iy * size + ix, w, size * size)
        img = img.reshape(size, size) / supersample ** 2  # mean 1
        img = np.real(np.fft.ifft2(np.fft.fft2(img) * kernel))
        v = 1 - np.exp(-np.maximum(img, 0) * gain)
        v = np.clip(v / (1 - np.exp(-6 * gain)), 0, 1) ** 1.15
        r, c = divmod(f, cols)
        atlas[r * size:(r + 1) * size, c * size:(c + 1) * size] = (v * 255 + .5).astype(np.uint8)
        print(f"frame {f + 1}/{frames} mean {v.mean():.3f} p99 {np.percentile(v, 99):.3f}", flush=True)
    Image.fromarray(atlas, 'L').save(out, optimize=True)

if __name__ == '__main__':
    # Settings used for Assets/_Project/Environment/Lighting/Caustics/LavaLampCaustics.png (needs numpy and pillow).
    bake(sys.argv[1], size=512, frames=32, cols=8, depth=0.013, kmax=5, waves=20, supersample=3, gain=0.55)
