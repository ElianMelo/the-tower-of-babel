"""Hand-painted colour atlas for the green parts of the Cloud Dandelions (Silk Clock) node.

Requested 2026-11-15: "handpaint with hand brush some more colors to the green parts of the model". This script
paints the texture stroke by stroke with a bristle brush (soft elliptical stamps whose alpha is streaked by
per-bristle noise), so the result reads as brushwork rather than a smooth gradient. It is seeded, so it repaints
the same texture every run.

  python paint_dandelion_greens.py      ->  Assets/_Project/Models/Resources/Textures/DandelionGreens.png

Atlas layout (UV v measured from the bottom, as in Blender and Unity):
  v 0.30 .. 1.00  leaf: u runs from the petiole (0) to the tip (1); across the blade, v 0.65 is the midrib
  v 0.02 .. 0.26  stems, bud and crown: u runs from the ground (0) up the stem (1); across is around the stem
build_sky_dandelions.py maps the leaves, stems, bracts, bud and crown into these regions.
"""
import math
import random
from pathlib import Path

import numpy as np
from PIL import Image

SIZE = 1024
OUT = Path(__file__).resolve().parents[2] / 'Assets/_Project/Models/Resources/Textures/DandelionGreens.png'
LEAF = (.30, 1.0)
STEM = (.02, .26)
rng = random.Random(1115)
canvas = np.full((SIZE, SIZE, 3), (.2, .52, .15), np.float32)       # gaps stay green for mip bleeding


def px(u, v):
    """UV (v from the bottom) to pixel x, y (y from the top)."""
    return u * (SIZE - 1), (1 - v) * (SIZE - 1)


def region_v(region, t):
    return region[0] + (region[1] - region[0]) * t


def fill_gradient(region, stops_u, across=None):
    """Base coat: colour ramp along u, optionally shaded across the region."""
    y0, y1 = int((1 - region[1]) * SIZE), int((1 - region[0]) * SIZE)
    us = np.linspace(0, 1, SIZE)
    pos = [p for p, _ in stops_u]; cols = np.array([c for _, c in stops_u], np.float32)
    ramp = np.stack([np.interp(us, pos, cols[:, k]) for k in range(3)], -1)
    block = np.repeat(ramp[None], y1 - y0, 0)
    if across:
        a = np.linspace(1, 0, y1 - y0)[:, None, None]          # 1 at the top row of the region (v high)
        block = block * (1 + across(a))
    canvas[y0:y1] = np.clip(block, 0, 1.2)


def bristles(width, seed):
    """Alpha profile across a brush: uneven bristle streaks."""
    r = random.Random(seed); n = 32
    raw = [r.uniform(.35, 1) for _ in range(n)]
    smooth = [(raw[i - 1] + 2 * raw[i] + raw[(i + 1) % n]) / 4 for i in range(n)]
    return np.interp(np.linspace(0, n - 1, 64), np.arange(n), smooth)


def stroke(points, width, color, alpha, region=None, taper=True, seed=None):
    """One brush stroke along points (UV). The stroke's alpha is the max of its stamps, then composited once."""
    pts = [px(u, v) for u, v in points]
    if region:                                                   # keep paint inside its atlas region
        lo, hi = px(0, region[1])[1], px(0, region[0])[1]
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
    mask = np.zeros_like(gx)
    prof = bristles(width, seed if seed is not None else rng.random())
    total = len(dense)
    for i, (x, y) in enumerate(dense):
        j = min(i + 1, total - 1); k = max(i - 1, 0)
        dx, dy = dense[j][0] - dense[k][0], dense[j][1] - dense[k][1]; L = math.hypot(dx, dy) or 1
        tx, ty = dx / L, dy / L
        t = i / max(1, total - 1)
        w = width * (math.sin(math.pi * min(1, .15 + t * .85)) ** .5 if taper else 1)
        along = (gx - x) * tx + (gy - y) * ty
        acr = (gx - x) * -ty + (gy - y) * tx
        d = (along / (w * .7)) ** 2 + (acr / w) ** 2
        disc = np.clip(1 - d, 0, 1) ** .6
        idx = np.clip(((acr / w) * .5 + .5) * 63, 0, 63).astype(int)
        mask = np.maximum(mask, disc * prof[idx])
    a = (mask * alpha)[..., None]
    if region:
        rows = np.arange(by0, by1)[:, None, None]
        a = a * ((rows >= lo) & (rows <= hi))
    sub = canvas[by0:by1, bx0:bx1]
    canvas[by0:by1, bx0:bx1] = sub * (1 - a) + np.array(color, np.float32) * a


def jitter(c, amount=.06):
    return tuple(max(0, min(1, x + rng.uniform(-amount, amount))) for x in c)


def curve(u0, v0, u1, v1, bow=0., n=6):
    return [(u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * k / n + bow * math.sin(math.pi * k / n)) for k in range(n + 1)]


# ---------------------------------------------------------------- leaf
L0, L1 = LEAF
MID = .5
lv = lambda t: region_v(LEAF, t)                                 # across-blade fraction -> atlas v
# Base coat: cool emerald-teal at the base, green through the middle, warm sunlit yellow-green at the tip.
fill_gradient(LEAF, [(0, (.78, .84, .56)), (.07, (.36, .58, .4)), (.16, (.05, .4, .26)), (.45, (.14, .54, .16)), (.75, (.36, .7, .12)),
                     (1, (.6, .8, .12))], across=lambda a: -.18 * np.abs(a - MID) * 2)
WARM = [((.62, .82, .1), 4), ((.74, .84, .2), 2), ((.5, .78, .12), 3), ((.82, .9, .42), 1)]       # yellow-green, lime-gold
MID_GREENS = [((.2, .6, .15), 4), ((.07, .38, .12), 3), ((.04, .28, .08), 2), ((.32, .66, .2), 2)]
COOL = [((.05, .5, .3), 4), ((.08, .55, .48), 3), ((.2, .6, .62), 2), ((.42, .78, .66), 1)]       # emerald, teal, blue-green


def leaf_colour(u, a):
    """Warm toward the tip and the sunlit margins, cool toward the base and the midrib shadow."""
    warm = .15 + .65 * u + .25 * abs(a - MID) * 2; cool = .15 + .7 * (1 - u) + .2 * (1 - abs(a - MID) * 2)
    family = rng.choices([WARM, MID_GREENS, COOL], [warm, .55, cool])[0]
    return jitter(rng.choices([c for c, _ in family], [w for _, w in family])[0], .04)


for _ in range(360):                                             # long strokes following the blade
    u0 = rng.uniform(-.05, .9); length = rng.uniform(.12, .38); a = rng.uniform(0, 1)
    col = leaf_colour(min(1, u0 + length / 2), a)
    v0 = lv(a); slope = (a - MID) * rng.uniform(.02, .12)        # strokes fan slightly toward the margins
    stroke(curve(u0, v0, u0 + length, lv(min(1, max(0, a + slope))), rng.uniform(-.015, .015)),
           rng.uniform(5, 16), col, rng.uniform(.25, .55), LEAF)
for _ in range(110):                                             # short dabs: warm and cool accents
    col = jitter(rng.choice([(.76, .82, .26), (.62, .84, .1), (.12, .58, .5), (.2, .6, .62), (.86, .96, .62), (.05, .3, .1)]))
    u0 = rng.uniform(.1, .95); a = rng.uniform(.05, .95)
    stroke(curve(u0, lv(a), u0 + rng.uniform(.03, .07), lv(a + rng.uniform(-.03, .03)), 0, 2), rng.uniform(4, 9), col, rng.uniform(.18, .4), LEAF)
for k in range(13):                                              # lateral veins: loose chevrons pointing to the tip
    for side in (-1, 1):
        u = .13 + k * .062 + rng.uniform(-.018, .018); reach = rng.uniform(.28, .43)
        stroke(curve(u, lv(MID + side * .02), u + rng.uniform(.07, .11), lv(MID + side * reach), side * rng.uniform(.01, .04), 5),
               rng.uniform(1.8, 2.8), jitter((.6, .85, .42), .05), rng.uniform(.16, .3), LEAF)
stroke(curve(0, lv(MID), 1, lv(MID), 0, 10), 7, (.86, .95, .68), .75, LEAF, taper=False)          # midrib
stroke(curve(0, lv(MID), .26, lv(MID), 0, 6), 9, (.66, .36, .5), .55, LEAF)                        # reddish base
for side in (.03, .97):                                          # darker margins with a few lime glints
    stroke(curve(.1, lv(side), 1, lv(side), 0, 10), 6, (.05, .28, .09), .35, LEAF, taper=False)
for _ in range(14):
    u0 = rng.uniform(.2, .9); side = rng.choice((.06, .94))
    stroke(curve(u0, lv(side), u0 + .06, lv(side), 0, 3), 3, (.75, .95, .35), .45, LEAF)

# ---------------------------------------------------------------- stems, bud and crown
sv = lambda t: region_v(STEM, t)
fill_gradient(STEM, [(0, (.74, .58, .58)), (.1, (.6, .7, .42)), (.25, (.42, .68, .28)), (.7, (.34, .64, .22)), (1, (.5, .8, .3))],
              across=lambda a: .2 * np.cos((a - .3) * math.pi * 2) * .5)
STEM_PALETTE = [(.24, .55, .17), (.5, .8, .3), (.14, .45, .32), (.62, .88, .4), (.36, .58, .14), (.72, .62, .55)]
for _ in range(150):
    col = jitter(rng.choice(STEM_PALETTE)); u0 = rng.uniform(-.05, .9); a = rng.uniform(0, 1)
    stroke(curve(u0, sv(a), u0 + rng.uniform(.15, .4), sv(a + rng.uniform(-.03, .03)), 0, 5), rng.uniform(4, 10), col, rng.uniform(.15, .35), STEM)
stroke(curve(0, sv(.3), 1, sv(.3), 0, 10), 10, (.8, .96, .62), .45, STEM, taper=False)            # sheen along the stem
stroke(curve(0, sv(.78), 1, sv(.78), 0, 10), 12, (.12, .36, .1), .35, STEM, taper=False)          # shaded side
stroke(curve(0, sv(.55), .14, sv(.55), 0, 4), 14, (.7, .42, .52), .5, STEM)                        # reddish foot

OUT.parent.mkdir(parents=True, exist_ok=True)
img = (np.clip(canvas, 0, 1) ** (1 / 1.0) * 255 + .5).astype(np.uint8)
Image.fromarray(img, 'RGB').save(OUT)
print(OUT)
