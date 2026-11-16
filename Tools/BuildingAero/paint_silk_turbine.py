"""Hand-painted colour atlas for the Silk Turbine (the Wind Condenser, rebuilt 2026-11-15).

Requested with the choice of design 01: "make the machine details more hand paint". Like the dandelion greens
(paint_dandelion_greens.py), the texture is painted stroke by stroke with a bristle brush (soft elliptical stamps
whose alpha is streaked per bristle), so the pearl, azure and frosted parts read as brushwork, not flat colour.
Seeded: every run repaints the same texture.

  python paint_silk_turbine.py      ->  Assets/_Project/Models/Resources/Textures/SilkTurbinePaint.png

Atlas layout (UV v from the bottom, as in Blender and Unity). build_sky_workshop.py wraps each part's region around
it: u runs around the part (the texture repeats in u, so strokes wrap), v runs up it (0 = its foot).
  v 0.56 .. 0.98  pearl: tower, nacelle, pad, seat, cuffs, tray. Cool blue-lilac at the foot, warm cream at the top
  v 0.31 .. 0.52  azure trim: bands, collar, spinner, hub, rims. Navy to bright azure, cyan glints
  v 0.03 .. 0.27  blades: u from root (0) to tip (1), v across the chord (1 = leading edge). Frost white to cyan tip

It then paints the plinth's top medallion (requested 2026-11-15, "enhance the base with better visuals"):
  python paint_silk_turbine.py      ->  also Textures/SilkTurbineTop.png
a top-down disc mapped flat onto the plinth (centre = UV 0.5, 0.5; rim = radius 0.5). A sunny cream centre that shows
through the glass orb, five wind-swirl arms brushed in aqua, sky blue and lilac, small dandelion seeds blowing round
the swirl, and an azure rim band. The atlas is painted first, so adding the medallion leaves it unchanged.
"""
import math
import random
from pathlib import Path

import numpy as np
from PIL import Image

SIZE = 1024
OUT = Path(__file__).resolve().parents[2] / 'Assets/_Project/Models/Resources/Textures/SilkTurbinePaint.png'
PEARL, AZURE, BLADE = (.56, .98), (.31, .52), (.03, .27)
rng = random.Random(2026_11_15)
canvas = np.full((SIZE, SIZE, 3), (.62, .76, .9), np.float32)       # gaps between regions: a soft blue for mip bleeding


def px(u, v):
    return u * (SIZE - 1), (1 - v) * (SIZE - 1)


def fill(region, stops):
    """Base coat: a colour ramp up the region (v)."""
    y0, y1 = int((1 - region[1]) * SIZE), int((1 - region[0]) * SIZE)
    t = np.linspace(1, 0, y1 - y0)                                  # 1 at the top row of the region
    pos = [p for p, _ in stops]; cols = np.array([c for _, c in stops], np.float32)
    ramp = np.stack([np.interp(t, pos, cols[:, k]) for k in range(3)], -1)
    canvas[y0:y1] = ramp[:, None, :]


def fill_along_u(region, stops, across=None):
    """Base coat: a colour ramp along u (blades: root to tip), optionally shaded across v."""
    y0, y1 = int((1 - region[1]) * SIZE), int((1 - region[0]) * SIZE)
    us = np.linspace(0, 1, SIZE)
    pos = [p for p, _ in stops]; cols = np.array([c for _, c in stops], np.float32)
    ramp = np.stack([np.interp(us, pos, cols[:, k]) for k in range(3)], -1)
    block = np.repeat(ramp[None], y1 - y0, 0)
    if across:
        a = np.linspace(1, 0, y1 - y0)[:, None, None]
        block = block * (1 + across(a))
    canvas[y0:y1] = np.clip(block, 0, 1)


def bristles(seed):
    r = random.Random(seed); n = 32
    raw = [r.uniform(.35, 1) for _ in range(n)]
    smooth = [(raw[i - 1] + 2 * raw[i] + raw[(i + 1) % n]) / 4 for i in range(n)]
    return np.interp(np.linspace(0, n - 1, 64), np.arange(n), smooth)


def _stroke(pts, width, color, alpha, region, taper, prof):
    dense = []
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        n = max(2, int(math.hypot(x1 - x0, y1 - y0) / max(1, width * .25)))
        dense += [(x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n) for k in range(n)]
    dense.append(pts[-1])
    xs = [p[0] for p in dense]; ys = [p[1] for p in dense]
    bx0, bx1 = int(max(0, min(xs) - width * 2)), int(min(SIZE, max(xs) + width * 2 + 1))
    by0, by1 = int(max(0, min(ys) - width * 2)), int(min(SIZE, max(ys) + width * 2 + 1))
    if bx1 <= bx0 or by1 <= by0: return
    gy, gx = np.mgrid[by0:by1, bx0:bx1].astype(np.float32)
    mask = np.zeros_like(gx); total = len(dense)
    for i, (x, y) in enumerate(dense):
        j, k = min(i + 1, total - 1), max(i - 1, 0)
        dx, dy = dense[j][0] - dense[k][0], dense[j][1] - dense[k][1]; L = math.hypot(dx, dy) or 1
        tx, ty = dx / L, dy / L; t = i / max(1, total - 1)
        w = width * (math.sin(math.pi * min(1, .15 + t * .85)) ** .5 if taper else 1)
        along = (gx - x) * tx + (gy - y) * ty; acr = (gx - x) * -ty + (gy - y) * tx
        disc = np.clip(1 - ((along / (w * .7)) ** 2 + (acr / w) ** 2), 0, 1) ** .6
        idx = np.clip(((acr / w) * .5 + .5) * 63, 0, 63).astype(int)
        mask = np.maximum(mask, disc * prof[idx])
    a = (mask * alpha)[..., None]
    lo, hi = px(0, region[1])[1], px(0, region[0])[1]
    rows = np.arange(by0, by1)[:, None, None]
    a = a * ((rows >= lo) & (rows <= hi))
    sub = canvas[by0:by1, bx0:bx1]
    canvas[by0:by1, bx0:bx1] = sub * (1 - a) + np.array(color, np.float32) * a


def stroke(points, width, color, alpha, region, taper=True, wrap=True):
    """One brush stroke along UV points. Regions that wrap around a part repeat in u, so a stroke crossing u = 0 or
    1 is painted again on the other side."""
    prof = bristles(rng.random())
    us = [u for u, _ in points]
    for shift in ([0, -1, 1] if wrap else [0]):
        if shift == -1 and max(us) <= 1: continue
        if shift == 1 and min(us) >= 0: continue
        _stroke([px(u + shift, v) for u, v in points], width, color, alpha, region, taper, prof)


def jitter(c, amount=.04):
    return tuple(max(0, min(1, x + rng.uniform(-amount, amount))) for x in c)


def line(u0, v0, u1, v1, bow=0., n=6):
    return [(u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * k / n + bow * math.sin(math.pi * k / n)) for k in range(n + 1)]


def rv(region, t):
    return region[0] + (region[1] - region[0]) * t


# ---------------------------------------------------------------- pearl: brushed down the tower, cool foot, warm top
fill(PEARL, [(0, (.7, .8, .93)), (.25, (.84, .9, .97)), (.7, (.93, .96, .99)), (1, (.99, .98, .95))])
COOL = [(.6, .75, .95), (.68, .88, .94), (.78, .74, .95), (.62, .86, .86), (.7, .8, 1)]   # sky blue, aqua, lilac, sea glass
WARM = [(1, .96, .86), (1, 1, 1), (.99, .92, .9), (.92, .98, 1), (1, .98, .8)]            # cream, white, shell blush, frost, sun
for _ in range(460):                                                              # long strokes running up the part
    u0 = rng.uniform(0, 1); t0 = rng.uniform(-.1, .95); length = rng.uniform(.15, .5)
    warm = .2 + .7 * min(1, t0 + length / 2)
    col = jitter(rng.choice(WARM if rng.random() < warm else COOL), .05)
    stroke(line(u0, rv(PEARL, t0), u0 + rng.uniform(-.03, .03), rv(PEARL, min(1.02, t0 + length)), rng.uniform(-.01, .01)),
           rng.uniform(6, 18), col, rng.uniform(.28, .55), PEARL)
for _ in range(170):                                                              # short sideways dabs: sheen and shade
    u0 = rng.uniform(0, 1); t0 = rng.uniform(0, 1)
    col = jitter(rng.choice([(1, 1, 1), (1, .94, .84), (.58, .72, .93), (.78, .72, .94), (.6, .88, .86)]), .05)
    stroke(line(u0, rv(PEARL, t0), u0 + rng.uniform(.04, .1), rv(PEARL, t0 + rng.uniform(-.02, .02)), 0, 3),
           rng.uniform(4, 9), col, rng.uniform(.25, .5), PEARL)
for k in range(4):                                                                # a few long sunlit streaks
    u0 = .08 + k * .25 + rng.uniform(-.03, .03)
    stroke(line(u0, rv(PEARL, .25), u0 + .015, rv(PEARL, 1), 0, 8), rng.uniform(5, 8), (1, 1, .97), .5, PEARL)
stroke(line(-.05, rv(PEARL, .04), 1.05, rv(PEARL, .04), 0, 12), 14, (.6, .72, .9), .4, PEARL, taper=False, wrap=False)   # shaded foot

# ---------------------------------------------------------------- azure trim: brushed around the band
fill(AZURE, [(0, (.02, .17, .46)), (.4, (.03, .3, .68)), (.75, (.05, .4, .82)), (1, (.12, .55, .94))])
TRIM = [(.02, .16, .46), (.04, .32, .74), (.08, .45, .88), (.15, .58, .96), (.01, .1, .32), (.05, .36, .7)]
for _ in range(300):                                                              # long strokes around the band
    u0 = rng.uniform(0, 1); t0 = rng.uniform(0, 1); length = rng.uniform(.12, .4)
    stroke(line(u0, rv(AZURE, t0), u0 + length, rv(AZURE, min(1, max(0, t0 + rng.uniform(-.06, .06)))), rng.uniform(-.02, .02)),
           rng.uniform(5, 14), jitter(rng.choice(TRIM), .03), rng.uniform(.2, .45), AZURE)
for _ in range(60):                                                               # cyan and white glints on the lit edge
    u0 = rng.uniform(0, 1); t0 = rng.uniform(.62, .95)
    col = jitter(rng.choice([(.4, .82, 1), (.7, .92, 1), (.3, .7, 1)]))
    stroke(line(u0, rv(AZURE, t0), u0 + rng.uniform(.03, .08), rv(AZURE, t0), 0, 3), rng.uniform(3, 6), col, rng.uniform(.35, .6), AZURE)
stroke(line(-.05, rv(AZURE, .82), 1.05, rv(AZURE, .82), 0, 12), 7, (.5, .85, 1), .35, AZURE, taper=False, wrap=False)       # rim light

# ---------------------------------------------------------------- blades: frost white to a cyan tip, brushed lengthwise
fill_along_u(BLADE, [(0, (.82, .9, .98)), (.3, (.9, .96, 1)), (.75, (.86, .95, 1)), (1, (.5, .86, 1))],
             across=lambda a: .05 * (a - .5))
FROST = [(1, 1, 1), (.86, .95, 1), (.8, .9, 1), (.9, .88, 1), (.78, .96, .98), (.7, .88, 1)]
for _ in range(260):
    u0 = rng.uniform(-.05, .9); a = rng.uniform(0, 1); length = rng.uniform(.15, .45)
    col = jitter(rng.choice(FROST) if u0 + length < .85 else rng.choice([(.5, .86, 1), (.62, .9, 1), (.8, .95, 1)]), .03)
    stroke(line(u0, rv(BLADE, a), u0 + length, rv(BLADE, min(1, max(0, a + rng.uniform(-.04, .04)))), rng.uniform(-.01, .01)),
           rng.uniform(5, 14), col, rng.uniform(.2, .45), BLADE, wrap=False)
stroke(line(0, rv(BLADE, .86), 1, rv(BLADE, .86), 0, 12), 9, (1, 1, 1), .55, BLADE, taper=False, wrap=False)           # leading edge
stroke(line(0, rv(BLADE, .14), 1, rv(BLADE, .14), 0, 12), 8, (.66, .82, .96), .45, BLADE, taper=False, wrap=False)     # trailing edge
for _ in range(24):                                                               # cyan tip dabs
    a = rng.uniform(.1, .9); u0 = rng.uniform(.8, .94)
    stroke(line(u0, rv(BLADE, a), u0 + .06, rv(BLADE, a), 0, 3), rng.uniform(5, 10), jitter((.35, .8, 1)), rng.uniform(.3, .5), BLADE, wrap=False)

OUT.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray((np.clip(canvas, 0, 1) * 255 + .5).astype(np.uint8), 'RGB').save(OUT)
print(OUT)

# ================================================================ the plinth's top medallion (a separate texture)
TOP_OUT = OUT.parent / 'SilkTurbineTop.png'
FULL = (0, 1)


def polar(r, a):
    """Normalised radius (1 = the medallion's rim) and angle to UV."""
    return .5 + .5 * r * math.cos(a), .5 + .5 * r * math.sin(a)


ys, xs = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
radius = np.hypot(xs / (SIZE - 1) - .5, (1 - ys / (SIZE - 1)) - .5) / .5
STOPS = [(0, (1, .96, .86)), (.22, (.99, .96, .92)), (.5, (.92, .94, .99)), (.78, (.8, .86, .97)), (.885, (.72, .79, .95)),
         (.9, (.12, .5, .9)), (.96, (.05, .34, .74)), (1, (.03, .22, .56))]
pos = [p for p, _ in STOPS]; cols = np.array([c for _, c in STOPS], np.float32)
canvas = np.stack([np.interp(np.clip(radius, 0, 1), pos, cols[:, k]) for k in range(3)], -1).astype(np.float32)

for _ in range(260):                                             # brushed pearl field: short strokes around the disc
    r = math.sqrt(rng.uniform(.01, .78)); a = rng.uniform(0, TAU := math.tau); span = rng.uniform(.12, .35) / max(r, .2)
    col = jitter(rng.choice([(1, 1, 1), (1, .96, .88), (.86, .9, 1), (.88, .86, .98), (.84, .95, .96)]), .04)
    stroke([polar(r + rng.uniform(-.01, .01), a + span * k / 6) for k in range(7)], rng.uniform(6, 14), col, rng.uniform(.18, .38), FULL, wrap=False)
ARMS, TWIST = 5, -1.25                                           # a clockwise wind swirl, like the silk spirals
SWIRL = [(.62, .86, .94), (.66, .78, .97), (.78, .74, .95), (.58, .82, .92), (1, 1, 1)]
for arm in range(ARMS):
    a0 = arm * TAU / ARMS
    for _ in range(60):
        r0 = rng.uniform(.1, .74); r1 = min(.84, r0 + rng.uniform(.08, .22)); off = rng.uniform(-.035, .035)
        pts = [polar(r, a0 + TWIST * math.log(r / .08) + off / r) for r in np.linspace(r0, r1, 7)]
        stroke(pts, rng.uniform(7, 20) * (.6 + .5 * r0), jitter(rng.choice(SWIRL), .03), rng.uniform(.22, .48), FULL, wrap=False)
for _ in range(16):                                              # dandelion seeds blowing round the swirl, seen from above
    r = rng.uniform(.3, .8); a = rng.uniform(0, TAU); cu, cv = polar(r, a)
    wind = (math.sin(a), -math.cos(a))                           # clockwise, along the swirl
    for k in range(10):                                          # the pappus: a star of fine silk
        b = k * TAU / 10 + rng.uniform(-.15, .15); L = rng.uniform(.012, .017)
        stroke([(cu, cv), (cu + math.cos(b) * L, cv + math.sin(b) * L)], 1.6, (1, 1, 1), .85, FULL, taper=False, wrap=False)
    tail = (cu - wind[0] * .03, cv - wind[1] * .03)              # the beak and achene trail behind the umbrella
    stroke([(cu, cv), tail], 1.4, (.85, .95, 1), .7, FULL, taper=False, wrap=False)
    stroke([tail, (tail[0] - wind[0] * .014, tail[1] - wind[1] * .014)], 3.2, (.1, .5, .85), .9, FULL, wrap=False)
for _ in range(170):                                             # azure rim band, brushed around the disc
    r = rng.uniform(.905, .99); a = rng.uniform(0, TAU); span = rng.uniform(.15, .45)
    stroke([polar(r, a + span * k / 8) for k in range(9)], rng.uniform(4, 9), jitter(rng.choice(TRIM), .03), rng.uniform(.25, .5), FULL, wrap=False)
for _ in range(36):                                              # cyan glints on the band
    r = rng.uniform(.92, .96); a = rng.uniform(0, TAU)
    stroke([polar(r, a + .05 * k / 3) for k in range(4)], rng.uniform(2.5, 4.5), jitter((.5, .88, 1)), rng.uniform(.4, .65), FULL, wrap=False)
stroke([polar(.895, k * TAU / 96) for k in range(97)], 3, (.96, .98, 1), .7, FULL, taper=False, wrap=False)   # rim light
Image.fromarray((np.clip(canvas, 0, 1) * 255 + .5).astype(np.uint8), 'RGB').save(TOP_OUT)
print(TOP_OUT)
