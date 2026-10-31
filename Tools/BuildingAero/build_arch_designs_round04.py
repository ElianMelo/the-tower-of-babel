"""Ten Wave Scroll variations with Frutiger Aero details, Round04.

The user picked Round03 design 05 (Wave Scroll) and asked for bolder details matching the Frutiger
Aero references instead of small classical carving: glossy aqua inlays, light lines, water glass
with caustics, iridescent bubbles, light swooshes and ripples. A third map, the material mask,
marks those regions (R aqua enamel, G glow, B water glass, A caustic light).

Round03 notes:

The user rejected Round01 (surface decoration) and Round02 (form and material experiments) and asked
for marble and stone, with normal maps giving the impression of depth. Every arch here is a low-poly
body (about 1k triangles) whose carving lives in two generated textures:
  * a tangent-space normal map (OpenGL, green up) from a carved height field, and
  * a cavity/AO map from the same height field, darkening recesses.
Height fields are computed analytically from the arch geometry (distance to the opening, distance to
the top, stone coursing), so mouldings follow the curve exactly and are seamless at the pillar joints.

Run headless:
  blender --background --factory-startup --python Tools/BuildingAero/build_arch_designs_round04.py -- [--render] [--preview]

Fit contract (unchanged from Round01/02): local X spans exactly pillar axis to pillar axis
(SPAN = 2.0944 m) with the opening centred; only a half-round cap of radius 0.2 m sits behind x = 0;
thickness +/-0.2 m, feet at z 0.14, flat top at z 2.15.
UV: front/back faces are planar, u = (x + 0.2) / 2.4, v = z / 2.4. Other faces sample a flat patch.
Outputs: ArtSource/Buildings/FrutigerAero/ArchDesigns/Round03 (.blend, FBX, *_Normal.png, *_AO.png,
report) and one contact sheet in _Screenshots-and-previews-to-delete/ArchDesigns/Round03.
"""
import bpy, bmesh, math, json, sys
import numpy as np
from mathutils import Vector, Matrix
from pathlib import Path

ROOT = Path('D:/PersonalProjects/the-tower-of-babel')
OUT = ROOT / 'ArtSource/Buildings/FrutigerAero/ArchDesigns/Round04'
SHOT = ROOT / '_Screenshots-and-previews-to-delete/ArchDesigns/Round04'
MARBLE = ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble.png'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
PREVIEW = '--preview' in ARGS
TAU = math.tau
SPAN = 2 * 96 * math.sin(math.radians(1.25) / 2)
THICK, FOOT, TOP = .2, .14, 2.15
BEND = math.radians(1.25) * 96 / 27
UV_X0, UV_SIZE = -.2, 2.4
RES = 512 if PREVIEW else 1536

bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene
COLL = SCENE.collection
OUT.mkdir(parents=True, exist_ok=True); SHOT.mkdir(parents=True, exist_ok=True)

# ---------------------------------------------------------------- arch outline
def opening(kind, w, zs, crown, n=72):
    c, a = SPAN / 2, SPAN / 2 - w
    curve = []
    for i in range(1, n):
        phi = math.pi * i / n; u = -math.cos(phi)
        if kind == 'ellipse':
            f = math.sin(phi)
        elif kind == 'semicircle':
            f = math.sin(phi)
        elif kind == 'pointed':
            R = a * 1.7; cx = a - R
            f = math.sqrt(max(0, R * R - (abs(u) * a - cx) ** 2)) / math.sqrt(R * R - cx * cx)
        curve.append((c + a * u, zs + (crown - zs) * f))
    return [(w, FOOT), (w, zs)] + curve + [(SPAN - w, zs), (SPAN - w, FOOT)]

# ---------------------------------------------------------------- height-field toolkit (metres)
class Field:
    """Grid over the front-face UV square with distances used by the carvings."""
    def __init__(self, path, w, zs, crown):
        n = RES
        u = (np.arange(n) + .5) / n
        self.X, self.Z = np.meshgrid(UV_X0 + u * UV_SIZE, u * UV_SIZE)
        self.c, self.w, self.zs, self.crown, self.a = SPAN / 2, w, zs, crown, SPAN / 2 - w
        self.px = UV_SIZE / n
        P = np.array(path)
        # Unsigned distance to the opening outline, then signed: negative inside the opening.
        d = np.full(self.X.shape, 1e3)
        for (x0, z0), (x1, z1) in zip(P[:-1], P[1:]):
            ex, ez = x1 - x0, z1 - z0; L2 = ex * ex + ez * ez or 1e-9
            t = np.clip(((self.X - x0) * ex + (self.Z - z0) * ez) / L2, 0, 1)
            d = np.minimum(d, np.hypot(self.X - x0 - t * ex, self.Z - z0 - t * ez))
        curve = P[2:-2]
        zc = np.interp(self.X, curve[:, 0], curve[:, 1], left=-1, right=-1)
        self.open = (self.X > w) & (self.X < SPAN - w) & (self.Z < np.maximum(zc, zs))
        self.D = np.where(self.open, -d, d)
        self.T = TOP - self.Z                                   # distance below the top
        v = (self.Z - zs) / max(crown - zs, 1e-3)
        self.phi = np.arctan2(np.maximum(v, 0), (self.X - self.c) / self.a)   # 0 right .. pi left
        self.body = (~self.open) & (self.X >= 0) & (self.X <= SPAN) & (self.Z >= FOOT) & (self.Z <= TOP)

def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def bead(d, d0, r):
    return np.sqrt(np.clip(r * r - (d - d0) ** 2, 0, None))

def step(d, d0, height, soft=.004):
    return height * ss(d0 - soft, d0 + soft, d)

def plateau(d, d0, d1, height, soft=.004):
    return height * ss(d0 - soft, d0 + soft, d) * (1 - ss(d1 - soft, d1 + soft, d))

def groove_lines(coord, period, width, depth, offset=0.):
    """V-grooves every `period` along `coord`."""
    m = np.abs(((coord - offset) / period + .5) % 1 - .5) * period
    return -depth * np.clip(1 - m / width, 0, 1)

def cushion(coord, period, edge, offset=0.):
    """0 at the block joints rising to 1 inside: a pillowed stone face."""
    m = np.abs(((coord - offset) / period + .5) % 1 - .5) * period
    return ss(0, edge, period / 2 - m)

def value_noise(F, cell, seed):
    rng = np.random.default_rng(seed)
    gx = int(UV_SIZE / cell) + 3
    g = rng.random((gx, gx))
    fx = (F.X - UV_X0) / cell; fz = F.Z / cell
    ix, iz = np.floor(fx).astype(int), np.floor(fz).astype(int)
    tx, tz = fx - ix, fz - iz
    tx, tz = tx * tx * (3 - 2 * tx), tz * tz * (3 - 2 * tz)
    a = g[iz, ix]; b = g[iz, ix + 1]; c = g[iz + 1, ix]; d = g[iz + 1, ix + 1]
    return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz

def fbm(F, cell, seed, octaves=4):
    h, amp, tot = 0, 1, 0
    for o in range(octaves):
        h = h + amp * value_noise(F, cell / 2 ** o, seed + o); tot += amp; amp *= .5
    return h / tot - .5

def arc_s(F):
    """Approximate arc length along the opening from the right springing."""
    return F.phi * (F.a + (F.crown - F.zs)) / 2

def ashlar(F, course, length, joint, depth, pillow, seed=0, irregular=0.):
    row = np.floor((F.Z - FOOT) / course)
    shift = (row % 2) * length / 2
    if irregular:
        shift = shift + np.vectorize(lambda r: (math.sin(r * 12.9898 + seed) * 43758.5453) % 1)(row) * length * irregular
    h = groove_lines(F.Z - FOOT, course, joint, depth) + groove_lines(F.X + shift, length, joint, depth)
    face = cushion(F.Z - FOOT, course, pillow) * cushion(F.X + shift, length, pillow)
    return h + depth * .6 * face

def keystone(F, half_bottom, half_top, height, relief, chamfer=.02):
    zb = F.crown - .03
    t = np.clip((F.Z - zb) / height, 0, 1)
    half = half_bottom + (half_top - half_bottom) * t
    edge = np.minimum.reduce([half - np.abs(F.X - F.c), F.Z - zb, zb + height - F.Z])
    return relief * ss(0, chamfer, edge) * (edge > -chamfer)

def rosette(F, cx, cz, R, petals=8, h=.018):
    r = np.hypot(F.X - cx, F.Z - cz) / R; ang = np.arctan2(F.Z - cz, F.X - cx)
    petal = h * (1 - r) * (.5 + .5 * np.cos(petals * ang)) ** .7 * (r < 1)
    centre = 1.2 * h * np.sqrt(np.clip(1 - (r / .22) ** 2, 0, None))
    ring = bead(r * R, R * 1.0, .012) * .9
    return np.maximum.reduce([petal, centre, ring])

# ---------------------------------------------------------------- Aero motifs
# Each design returns (height, mask): height in metres for the normal/AO maps; mask channels
#   R aqua enamel inlay (glossy, 0..1 also used as a gradient), G glow light line,
#   B water glass (glossy aqua with light), A caustic light inside the water glass.
def zeros(F): return np.zeros_like(F.X)

def wave_line(F, top_offset, amp, period, phase=0.):
    """Height of a wave line measured down from the top (T coordinate)."""
    return top_offset + amp * np.sin(TAU * F.X / period + phase)

def caustic(F, scale=1., phase=0.):
    px, pz = F.X * 9 * scale + phase, F.Z * 6 * scale
    w = np.sin(px + 1.4 * np.sin(pz * 1.3)) + np.sin(pz * 1.1 - 1.2 * np.sin(px * .9 + 1.7))
    return np.clip(1 - np.abs(w * .5), 0, 1) ** 6

def curls(F, T0, amp, period, r, width):
    """Scroll curls under each wave crest: rings in the T/X plane."""
    xc = ((F.X - period / 4) % period) - period / 2
    return np.exp(-((np.hypot(xc, (F.T - T0 - amp * .2)) - r) / width) ** 2)

def bubbles_field(F, region, count, rmin, rmax, seed):
    """Non-overlapping bubbles inside a boolean region: (dome height, rim, highlight)."""
    rng = np.random.default_rng(seed)
    dome, rim, spot = zeros(F), zeros(F), zeros(F)
    placed = []
    for _ in range(count * 40):
        if len(placed) >= count: break
        r = rmin + (rmax - rmin) * rng.random() ** 2.2
        x, z = rng.uniform(r, SPAN - r), rng.uniform(FOOT + r, TOP - r)
        iz, ix = int(z / UV_SIZE * RES), int((x - UV_X0) / UV_SIZE * RES)
        if not region[min(iz, RES - 1), min(ix, RES - 1)]: continue
        if any(math.hypot(x - px, z - pz) < r + pr + .01 for px, pz, pr in placed): continue
        placed.append((x, z, r))
        q = np.hypot(F.X - x, F.Z - z) / r
        dome = np.maximum(dome, .016 * r / .08 * np.sqrt(np.clip(1 - q * q, 0, None)))
        rim = np.maximum(rim, np.exp(-((q - .9) / .08) ** 2) * (q < 1.05))
        spot = np.maximum(spot, np.exp(-(np.hypot(F.X - x + .35 * r, F.Z - z - .38 * r) / (.18 * r)) ** 2))
    return dome, rim, spot

def frame(F):
    """Shared Wave Scroll frame: a top fillet and a soft archivolt roll."""
    return plateau(F.T, 0, .05, .014) + bead(F.D, .06, .03) * .7

def w01_big_wave_inlay(F):
    # A tall running wave: glossy aqua enamel water below a white marble crest with curled scrolls.
    band = (F.T > .07) & (F.T < .47)
    below = F.T > wave_line(F, .27, .09, .52)
    crest = np.exp(-((F.T - wave_line(F, .27, .09, .52)) / .012) ** 2)
    h = frame(F) + band * (np.where(below, -.010, .012) + .010 * crest + .012 * curls(F, .27, .09, .52, .05, .012) * below)
    m = {'R': (band & below) * (1 - curls(F, .27, .09, .52, .05, .016))}
    return h + plateau(F.T, .47, .49, .01), m

def w02_glowing_crest(F):
    # Marble wave in relief with a glowing light line set into the crest, echoed around the opening.
    band = (F.T > .07) & (F.T < .42)
    line = wave_line(F, .25, .08, .50)
    h = frame(F) + band * (.016 * ss(.004, -.004, F.T - line) - .006 * np.exp(-((F.T - line) / .008) ** 2))
    glow = band * np.exp(-((F.T - line) / .009) ** 2) + np.exp(-((F.D - .11) / .009) ** 2) * (F.D > 0)
    return h - .005 * np.exp(-((F.D - .11) / .008) ** 2), {'G': np.clip(glow, 0, 1)}

def w03_water_glass(F):
    # The wave band is aqua water glass with moving-water caustics and small rising bubbles.
    band = (F.T > .07) & (F.T < .45)
    below = band & (F.T > wave_line(F, .22, .07, .46))
    dome, rim, spot = bubbles_field(F, below, 26, .012, .04, 3)
    cau = caustic(F, 1.4)
    h = frame(F) + below * (-.012 + .002 * cau) + .012 * np.exp(-((F.T - wave_line(F, .22, .07, .46)) / .01) ** 2) * band + dome
    return h, {'B': below * (1 - rim * .8), 'A': cau * below, 'G': spot * .6}

def w04_rising_bubbles(F):
    # Iridescent glossy bubbles rise from the arch into the spandrel, under an aqua wave.
    band = (F.T > .07) & (F.T < .36)
    below = band & (F.T > wave_line(F, .2, .06, .44))
    spand = (F.D > .14) & (F.T > .42) & (F.X > .06) & (F.X < SPAN - .06)
    dome, rim, spot = bubbles_field(F, spand, 14, .035, .12, 7)
    h = frame(F) + below * -.008 + .01 * np.exp(-((F.T - wave_line(F, .2, .06, .44)) / .01) ** 2) * band + dome
    return h, {'R': np.clip(below + rim * .7 + (dome > 0) * .4, 0, 1), 'G': spot * 1.2}

def w05_double_wave(F):
    # Two broad aqua waves sweep across the whole spandrel, edged with white marble beads.
    h, R = frame(F), zeros(F)
    for top, amp, per, ph in ((.18, .07, 1.05, 0.), (.40, .09, 1.05, 1.6)):
        line = wave_line(F, top, amp, per, ph)
        ribbon = np.exp(-((F.T - line) / .035) ** 4) * (F.D > .1)
        R = np.maximum(R, ribbon)
        h = h - .008 * ribbon + .010 * (np.exp(-((F.T - line - .042) / .009) ** 2) + np.exp(-((F.T - line + .042) / .009) ** 2)) * (F.D > .1)
    return h, {'R': R * (F.X > .02) * (F.X < SPAN - .02)}

def w06_swoosh(F):
    # Aero light swooshes: glossy aqua ribbons rise from each pillar and curl over the arch.
    h, R, G = frame(F), zeros(F), zeros(F)
    for side in (1, -1):
        xs = F.X if side == 1 else SPAN - F.X
        for k, (z0, lift, width) in enumerate(((.42, 1.50, .075), (.34, 1.42, .04))):
            centre = z0 + lift / (1 + np.exp(-(xs - .62 - .06 * k) / .15))
            ribbon = np.exp(-((F.Z - centre) / width) ** 4) * (F.D > .04) * (xs < SPAN / 2 + .02) * (F.T > .05)
            grad = np.clip(xs / (SPAN / 2), 0, 1)
            R = np.maximum(R, ribbon * (1 - .55 * grad) * (1 if k == 0 else .7))
            G = np.maximum(G, np.exp(-((F.Z - centre - width) / .004) ** 2) * (ribbon > .01) * (k == 0))
            h = h - .006 * ribbon
    return h, {'R': R, 'G': G * .8}

def w07_ring_light(F):
    # A recessed light strip follows the opening like the ring lamps in sample 2; marble wave above.
    band = (F.T > .07) & (F.T < .32)
    line = wave_line(F, .18, .055, .40)
    strip = (F.D > .03) & (F.D < .085)
    h = frame(F) * 0 + plateau(F.T, 0, .05, .014) + band * .014 * ss(.004, -.004, F.T - line)
    h = h - .01 * strip + bead(F.D, .12, .018) + bead(F.D, .0, .02) * .6
    return h, {'G': strip * ss(.03, .036, F.D) * ss(.085, .079, F.D), 'R': band * (F.T > line) * .8}

def w08_ocean_gradient(F):
    # Water rises from the arch: an aqua-to-white gradient with foam crest relief and bubbles.
    grad = np.clip(1 - F.D / 1.15, 0, 1) ** 1.1 * (F.D > .02)
    foam = (F.T > .06) & (F.T < .3)
    crest = .012 * ss(.004, -.004, F.T - wave_line(F, .18, .05, .34)) * foam
    dome, rim, spot = bubbles_field(F, (F.D > .08) & (F.D < .55) & (F.T > .3), 18, .015, .05, 11)
    return frame(F) + crest + dome, {'R': np.clip(grad * (1 - rim * .7), 0, 1), 'G': spot * .5}

def w09_ripple_rings(F):
    # Concentric water ripples spread from a glowing drop in each spandrel corner.
    h, R, G = frame(F), zeros(F), zeros(F)
    for cx in (.40, SPAN - .40):
        r = np.hypot(F.X - cx, F.Z - 1.58)
        ripple = np.cos(TAU * r / .065) * np.exp(-r / .3) * (r < .44) * (F.D > .1)
        h = h + .011 * ripple
        R = np.maximum(R, np.exp(-r / .3) * .85 * (F.D > .1) * (r < .44))
        G = np.maximum(G, np.exp(-(r / .04) ** 2))
    band = (F.T > .07) & (F.T < .3)
    h = h + band * .012 * ss(.004, -.004, F.T - wave_line(F, .18, .05, .38))
    return h, {'R': R, 'G': G}

def w10_pearl_wave(F):
    # A string of glossy pearls flows along the wave over a deep aqua band.
    band = (F.T > .07) & (F.T < .4)
    line = wave_line(F, .23, .08, .52)
    s = F.X / .1
    size = np.where((np.floor(s) % 3) == 0, .048, .034)
    q = np.hypot((s % 1 - .5) * .1, F.T - (.23 + .08 * np.sin(TAU * (np.floor(s) + .5) * .1 / .52))) / size
    pearls = .03 * np.sqrt(np.clip(1 - q * q, 0, None))
    return frame(F) + band * -.006 + pearls * band, {'R': band * (q > 1), 'G': np.exp(-(np.hypot(q - .0, 0) / .25) ** 2) * band * .0}

DESIGNS = [
    ('01', 'Big Wave Inlay', w01_big_wave_inlay),
    ('02', 'Glowing Crest', w02_glowing_crest),
    ('03', 'Water Glass Band', w03_water_glass),
    ('04', 'Rising Bubbles', w04_rising_bubbles),
    ('05', 'Double Wave', w05_double_wave),
    ('06', 'Aero Swoosh', w06_swoosh),
    ('07', 'Ring Light', w07_ring_light),
    ('08', 'Ocean Gradient', w08_ocean_gradient),
    ('09', 'Ripple Rings', w09_ripple_rings),
    ('10', 'Pearl Wave', w10_pearl_wave),
]
SHAPE = ('ellipse', .30, .30, 1.30)      # Wave Scroll's opening, kept for every variation

# ---------------------------------------------------------------- textures
def save_image(name, rgba, path, non_color=True):
    n = rgba.shape[0]
    if rgba.shape[2] == 3: rgba = np.concatenate([rgba, np.ones(rgba.shape[:2] + (1,))], axis=2)
    img = bpy.data.images.new(name, n, n, alpha=True, float_buffer=False)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = str(path); img.file_format = 'PNG'; img.save()
    if non_color: img.colorspace_settings.name = 'Non-Color'
    img.alpha_mode = 'CHANNEL_PACKED'
    return img

def blur(a, r):
    k = 2 * r + 1
    c = np.cumsum(np.pad(a, ((r + 1, r), (0, 0)), mode='edge'), axis=0); a = (c[k:] - c[:-k]) / k
    c = np.cumsum(np.pad(a, ((0, 0), (r + 1, r)), mode='edge'), axis=1); return (c[:, k:] - c[:, :-k]) / k

def maps(code, F, h, m):
    # Fade everything out within 3 cm of the pillar axes so neighbouring arches join seamlessly.
    fade = ss(0, .03, F.X) * ss(0, .03, SPAN - F.X) * F.body
    h = h * fade
    gz, gx = np.gradient(h, F.px)
    n = np.stack([-gx, -gz, np.ones_like(h)], axis=2); n /= np.linalg.norm(n, axis=2, keepdims=True)
    normal = save_image(f'R4_{code}_Normal', n * .5 + .5, OUT / f'Arch_R4D{code}_Normal.png')
    cavity = np.clip((blur(blur(h, 6), 6) - h) * 55, 0, 1) + np.clip((blur(h, 24) - h) * 18, 0, 1) * .5
    ao = np.clip(1 - .6 * cavity, 0, 1)
    aoimg = save_image(f'R4_{code}_AO', np.repeat(ao[:, :, None], 3, axis=2), OUT / f'Arch_R4D{code}_AO.png')
    chans = [np.clip(np.asarray(m.get(k, 0.)) * np.ones_like(h) * fade, 0, 1) for k in 'RGBA']
    mask = save_image(f'R4_{code}_Mask', np.stack(chans, axis=2), OUT / f'Arch_R4D{code}_Mask.png')
    cover = {k: round(float((c > .5).mean() / max(F.body.mean(), 1e-6)), 4) for k, c in zip('RGBA', chans)}
    return normal, aoimg, mask, float(h.min()), float(h.max()), cover

# ---------------------------------------------------------------- material
MARBLE_IMG = bpy.data.images.load(str(MARBLE))

def aero_marble(name, normal, ao, mask):
    """Carrara marble with glossy aqua enamel, glowing light lines and water glass from the mask."""
    m = bpy.data.materials.new(name); nt = m.node_tree; L = nt.links
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    def node(kind, **kw):
        n = nt.nodes.new(kind)
        for k, v in kw.items(): setattr(n, k, v)
        return n
    tc = node('ShaderNodeTexCoord'); mp = node('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (.42, .42, .42)
    mt = node('ShaderNodeTexImage', image=MARBLE_IMG, projection='BOX', projection_blend=.25)
    L.new(tc.outputs['Object'], mp.inputs['Vector']); L.new(mp.outputs['Vector'], mt.inputs['Vector'])
    uv = node('ShaderNodeUVMap', uv_map='UVMap')
    texs = {k: node('ShaderNodeTexImage', image=img, interpolation='Cubic') for k, img in (('n', normal), ('ao', ao), ('m', mask))}
    for t in texs.values(): L.new(uv.outputs['UV'], t.inputs['Vector'])
    nmap = node('ShaderNodeNormalMap', uv_map='UVMap'); L.new(texs['n'].outputs['Color'], nmap.inputs['Color'])
    L.new(nmap.outputs['Normal'], b.inputs['Normal'])
    sep = node('ShaderNodeSeparateColor'); L.new(texs['m'].outputs['Color'], sep.inputs['Color'])
    def mix(a, bcol, fac, blend='MIX'):
        x = node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        for sock, val in (('A', a), ('B', bcol), ('Factor', fac)):
            if isinstance(val, tuple): x.inputs[sock].default_value = val
            elif isinstance(val, (int, float)): x.inputs[sock].default_value = val
            else: L.new(val, x.inputs[sock])
        return x.outputs['Result']
    marble = mix(mt.outputs['Color'], (.96, .965, .975, 1), 1, 'MULTIPLY')
    col = mix(marble, (.03, .50, .90, 1), sep.outputs['Red'])
    col = mix(col, (.10, .60, .92, 1), sep.outputs['Blue'])
    col = mix(col, texs['ao'].outputs['Color'], 1, 'MULTIPLY')
    L.new(col, b.inputs['Base Color'])
    gloss = node('ShaderNodeMath', operation='MAXIMUM'); L.new(sep.outputs['Red'], gloss.inputs[0]); L.new(sep.outputs['Blue'], gloss.inputs[1])
    rough = node('ShaderNodeMapRange'); rough.inputs['To Min'].default_value = .22; rough.inputs['To Max'].default_value = .05
    L.new(gloss.outputs['Value'], rough.inputs['Value']); L.new(rough.outputs['Result'], b.inputs['Roughness'])
    coat = node('ShaderNodeMapRange'); coat.inputs['To Min'].default_value = .35; coat.inputs['To Max'].default_value = 1.
    L.new(gloss.outputs['Value'], coat.inputs['Value']); L.new(coat.outputs['Result'], b.inputs['Coat Weight'])
    b.inputs['Coat Roughness'].default_value = .04
    light = node('ShaderNodeMath', operation='MULTIPLY'); L.new(sep.outputs['Blue'], light.inputs[0]); L.new(texs['m'].outputs['Alpha'], light.inputs[1])
    glow = mix((0, 0, 0, 1), (.30, .90, 1., 1), sep.outputs['Green'])
    water = mix((0, 0, 0, 1), (.55, .95, 1., 1), light.outputs['Value'])
    emis = mix(glow, water, 1, 'ADD')
    L.new(emis, b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 5.
    return m

def body_mesh_r4(name, path):
    return body_mesh(name, path)

# ---------------------------------------------------------------- low-poly body with planar UVs
def body_mesh(name, path):
    pts = [(0, FOOT)] + path + [(SPAN, FOOT), (SPAN, TOP), (0, TOP)]
    bm = bmesh.new()
    front = [bm.verts.new((x, -THICK, z)) for x, z in pts]
    f = bm.faces.new(front)
    if f.normal.y > 0: f.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in [g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)]: v.co.y = THICK
    # Half-round cap behind the start axis closes the joint for any tower bend.
    cap = [(THICK * math.cos(a), THICK * math.sin(a)) for a in [math.pi / 2 + math.pi * k / 12 for k in range(13)]]
    cb = [bm.verts.new((x, y, FOOT + .002)) for x, y in cap]
    cf = bm.faces.new(cb)
    if cf.normal.z > 0: cf.normal_flip()
    cext = bmesh.ops.extrude_face_region(bm, geom=[cf])
    for v in [g for g in cext['geom'] if isinstance(g, bmesh.types.BMVert)]: v.co.z = TOP - .002
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    uv = bm.loops.layers.uv.new('UVMap')
    flat_u, flat_v = (SPAN / 2 - UV_X0) / UV_SIZE, .3 / UV_SIZE      # inside the opening: no carving
    for face in bm.faces:
        planar = abs(face.normal.y) > .7 and face.calc_center_median().x > .001
        for l in face.loops:
            p = l.vert.co
            l[uv].uv = ((p.x - UV_X0) / UV_SIZE, p.z / UV_SIZE) if planar else \
                       (flat_u + (p.x + p.y) * .01, flat_v + p.z * .01)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    o = bpy.data.objects.new(name, me); COLL.objects.link(o)
    return o

# ---------------------------------------------------------------- build
report, arches = {}, []
for code, title, design in DESIGNS:
    kind, w, zs, crown = SHAPE
    path = opening(kind, w, zs, crown)
    F = Field(path, w, zs, crown)
    h, m = design(F)
    normal, ao, mask, hmin, hmax, cover = maps(code, F, h, m)
    name = 'Arch_R4D' + code + '_' + title.replace(' ', '')
    obj = body_mesh(name, path)
    obj.data.materials.append(aero_marble(name + '_AeroMarble', normal, ao, mask))
    vs = [v.co for v in obj.data.vertices]
    lo = [min(v[k] for v in vs) for k in range(3)]; hi = [max(v[k] for v in vs) for k in range(3)]
    report[name] = {'title': title, 'basedOn': 'Round03 05 Wave Scroll', 'triangles': len(obj.data.polygons),
                    'reliefRangeMetres': [round(hmin, 4), round(hmax, 4)], 'maskCoverage': cover,
                    'bounds_min_xyz_blender': [round(v, 4) for v in lo], 'bounds_max_xyz_blender': [round(v, 4) for v in hi],
                    'endsAtNextPillarAxis': hi[0] <= SPAN + 1e-4,
                    'onlyCapBehindStartAxis': all(math.hypot(v.x, v.y) <= THICK + 1e-3 for v in vs if v.x < -1e-4),
                    'maps': [f'Arch_R4D{code}_{k}.png' for k in ('Normal', 'AO', 'Mask')]}
    for o in COLL.objects: o.select_set(False)
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(OUT / (name + '.fbx')), use_selection=True, object_types={'MESH'},
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', use_triangles=True,
                             path_mode='STRIP', embed_textures=False)
    arches.append((code, title, obj))

# ---------------------------------------------------------------- review scene
def principled(name, base, rough=.2, metal=0., trans=0., ior=1.45, emit=None, strength=0., coat=0.):
    m = bpy.data.materials.new(name); b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    for k, v in {'Base Color': (*base, 1), 'Roughness': rough, 'Metallic': metal, 'Transmission Weight': trans,
                 'IOR': ior, 'Emission Color': (*(emit or base), 1), 'Emission Strength': strength, 'Coat Weight': coat}.items():
        if k in b.inputs: b.inputs[k].default_value = v
    return m

PEARL = principled('Pearl', (.95, .965, .98), .16, coat=1.)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/BubbleTubePillar.fbx'))
pillar = next(o for o in bpy.context.selected_objects if o.type == 'MESH')
pillar.data.transform(pillar.matrix_world); pillar.matrix_world = Matrix()
pillar.data.materials.clear()
for m in (PEARL, principled('Enamel', (.01, .22, .68), .12, coat=1.), principled('Chrome', (.9, .93, .97), .06, 1.),
          principled('Glow', (.35, .92, 1.), emit=(.3, .9, 1.), strength=8.), principled('Water', (.22, .78, .97), .03, trans=1., ior=1.33)):
    pillar.data.materials.append(m)
pal = pillar.data.uv_layers[1] if len(pillar.data.uv_layers) > 1 else pillar.data.uv_layers[0]
for p in pillar.data.polygons: p.material_index = min(4, int(pal.data[p.loop_indices[0]].uv[0] * 8))
pillar.hide_render = True

tiles_origin = []
for idx, (code, title, arch) in enumerate(arches):
    origin = Vector((idx * 40., 0, 0)); tiles_origin.append(origin)
    R = SPAN / (2 * math.sin(BEND / 2)); centre = origin + Vector((0, R, 0))
    pts = [centre + Vector((R * math.sin(k * BEND), -R * math.cos(k * BEND), 0)) for k in (-1, 0, 1)]
    for p in pts:
        o = pillar.copy(); o.hide_render = False; o.location = p; COLL.objects.link(o)
    for k in range(2):
        a, b = pts[k], pts[k + 1]
        o = arch.copy(); o.location = a + Vector((0, 0, 3.85)); o.rotation_euler = (0, 0, math.atan2(b.y - a.y, b.x - a.x))
        COLL.objects.link(o)
    arch.hide_render = True
    slab = bpy.data.objects.new('Slab', bpy.data.meshes.new('Slab')); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1); bm.to_mesh(slab.data); bm.free()
    slab.scale = (5.6, 3.0, .2); slab.location = origin + Vector((0, 1.05, 6.1)); slab.data.materials.append(PEARL)
    COLL.objects.link(slab)

floor_bm = bmesh.new(); bmesh.ops.create_circle(floor_bm, cap_ends=True, radius=2000, segments=96)
fl = bpy.data.meshes.new('Floor'); floor_bm.to_mesh(fl); floor_bm.free()
fl.materials.append(principled('Floor', (.80, .92, .985), .07, coat=.6)); COLL.objects.link(bpy.data.objects.new('Floor', fl))
world = bpy.data.worlds.new('Sky'); SCENE.world = world
wn, wl = world.node_tree.nodes, world.node_tree.links
bg = next(n for n in wn if n.type == 'BACKGROUND')
tc = wn.new('ShaderNodeTexCoord'); sep = wn.new('ShaderNodeSeparateXYZ'); ramp = wn.new('ShaderNodeValToRGB')
wl.new(tc.outputs['Generated'], sep.inputs[0]); wl.new(sep.outputs['Z'], ramp.inputs['Fac'])
ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = 0., (.78, .94, 1., 1)
ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .16, (.04, .42, 1., 1)
wl.new(ramp.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 1.1
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); COLL.objects.link(sun)
# Lower, raking sun so the carved relief reads.
sun.data.energy = 4.5; sun.data.color = (1., .97, .92); sun.data.angle = math.radians(2)
sun.rotation_euler = (math.radians(62), 0, math.radians(-48))
cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); COLL.objects.link(cam)
cam.data.lens = 40; SCENE.camera = cam
SCENE.render.engine = 'CYCLES'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('OPTIX', 'CUDA'):
        prefs.compute_device_type = kind
        (prefs.refresh_devices if hasattr(prefs, 'refresh_devices') else prefs.get_devices)()
        if any(d.type == kind for d in prefs.devices):
            for d in prefs.devices: d.use = d.type == kind
            SCENE.cycles.device = 'GPU'; break
except Exception: pass
SCENE.cycles.samples = 16 if PREVIEW else 96
SCENE.cycles.use_denoising = True
SCENE.render.resolution_x, SCENE.render.resolution_y = (480, 400) if PREVIEW else (1000, 820)
SCENE.view_settings.view_transform = 'AgX'; SCENE.view_settings.exposure = .1
try: SCENE.view_settings.look = 'AgX - Punchy'
except Exception: pass
font = None
try: font = bpy.data.fonts.load(str(ROOT / 'Assets/_Project/Fonts/Frutiger.ttf'))
except Exception: pass
LABEL = principled('Label', (1., 1., 1.), .4, emit=(1., 1., 1.), strength=2.5)

(OUT / 'ArchDesignsReport.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ArchDesigns_Round04.blend'), compress=True)
print('ARCH_REPORT', json.dumps({k: (v['triangles'], v['endsAtNextPillarAxis'], v['onlyCapBehindStartAxis'], v['reliefRangeMetres'])
                                 for k, v in report.items()}))

if '--render' in ARGS:
    tiles = []
    for idx, (code, title, arch) in enumerate(arches):
        origin = tiles_origin[idx]
        lab = bpy.data.objects.new('L' + code, bpy.data.curves.new('L' + code, 'FONT'))
        lab.data.body = f'{code}  {title}'; lab.data.align_x = 'CENTER'; lab.data.size = .26
        if font: lab.data.font = font
        lab.data.materials.append(LABEL); COLL.objects.link(lab)
        # Close view of the arches and pillar tops, where the carving is.
        cam.location = origin + Vector((-2.9, -6.9, 3.25))
        target = origin + Vector((.25, 0, 4.75))
        cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
        lab.location = origin + Vector((-.2, -.6, 6.55)); lab.rotation_euler = (math.radians(88), 0, math.radians(-22))
        path = SHOT / f'tile_{code}.png'
        SCENE.render.filepath = str(path); bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(lab)
        img = bpy.data.images.load(str(path)); w, h = img.size
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4))
    h, w = tiles[0].shape[:2]
    sheet = np.ones((h * 2, w * 5, 4), dtype=np.float32)
    for idx, t in enumerate(tiles):
        r, c = idx // 5, idx % 5
        sheet[(1 - r) * h:(2 - r) * h, c * w:(c + 1) * w] = t
    out = bpy.data.images.new('Sheet', w * 5, h * 2, alpha=False)
    out.pixels.foreach_set(sheet.ravel())
    target = SHOT / ('Arch-Designs-Lineup-preview.png' if PREVIEW else 'Arch-Designs-Lineup.png')
    out.filepath_raw = str(target); out.file_format = 'PNG'; out.save()
    print('RENDERED', target)
