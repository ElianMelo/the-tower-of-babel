"""Ten glossy white arches with luminous strand ribbons, Round05.

The user rejected the Round01-04 art directions and asked to return to glossy bright white with detail
like Frutiger Reference/Asset/Arch-Light-Ribbons-User.png: translucent ribbons of fine light strands
that twist, fold and loop. Maps per design: Normal, AO and an Emission colour map for the ribbons.

Round04 notes:

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
  blender --background --factory-startup --python Tools/BuildingAero/build_arch_designs_round05.py -- [--render] [--preview]

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
OUT = ROOT / 'ArtSource/Buildings/FrutigerAero/ArchDesigns/Round05'
SHOT = ROOT / '_Screenshots-and-previews-to-delete/ArchDesigns/Round05'
MARBLE = ROOT / 'Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble.png'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
PREVIEW = '--preview' in ARGS
TAU = math.tau
SPAN = 2 * 96 * math.sin(math.radians(1.25) / 2)
THICK, FOOT, TOP = .2, .14, 2.15
BEND = math.radians(1.25) * 96 / 27
UV_X0, UV_SIZE = -.2, 2.4
RES = 512 if PREVIEW else 1280

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

# ---------------------------------------------------------------- luminous strand ribbons
# Reference: Frutiger Reference/Asset/Arch-Light-Ribbons-User.png. A ribbon is a centreline curve with
# a signed width w(t); N fine strands run at offsets w(t) * (k / (N - 1) - .5) and cross wherever w
# changes sign, which reads as the ribbon twisting and folding. A faint silk fill sits between them.
# Palette kept to the building brief (cyan, aqua, blue, white) instead of the reference's green/yellow.
RIBBON_SCALE = 1.7      # ribbons sweep across the whole spandrel, as in the reference
CYAN, AQUA, BLUE, ICE = (.25, .95, 1.), (.10, .72, 1.), (.28, .45, 1.), (.75, .95, 1.)

def zeros(F): return np.zeros_like(F.X)

def offset(path, d):
    """Offset the opening path outward (into the spandrel) by d metres."""
    c = SPAN / 2; out = []
    for i, (x, z) in enumerate(path):
        x0, z0 = path[max(0, i - 1)]; x1, z1 = path[min(len(path) - 1, i + 1)]
        tx, tz = x1 - x0, z1 - z0; l = math.hypot(tx, tz) or 1
        nx, nz = -tz / l, tx / l
        if nx * (x - c) + nz * (z - .2) < 0: nx, nz = -nx, -nz
        out.append((x + nx * d, z + nz * d))
    return out

def curve_field(F, pts):
    """Signed distance to a polyline and the normalised parameter t of the nearest point."""
    P = np.asarray(pts, dtype=float); n = len(P) - 1
    best = np.full(F.X.shape, 1e3); sd = zeros(F); tp = zeros(F)
    for i in range(n):
        (x0, z0), (x1, z1) = P[i], P[i + 1]
        ex, ez = x1 - x0, z1 - z0; L2 = ex * ex + ez * ez or 1e-9
        t = np.clip(((F.X - x0) * ex + (F.Z - z0) * ez) / L2, 0, 1)
        dx, dz = F.X - x0 - t * ex, F.Z - z0 - t * ez
        d = np.hypot(dx, dz)
        closer = d < best
        best = np.where(closer, d, best)
        sd = np.where(closer, np.sign(ex * dz - ez * dx) * d, sd)
        tp = np.where(closer, (i + t) / n, tp)
    return sd, tp

def palette(t, colours):
    t = np.clip(t, 0, 1) * (len(colours) - 1)
    i = np.minimum(np.floor(t).astype(int), len(colours) - 2); f = (t - i)[..., None]
    C = np.array(colours)
    return C[i] * (1 - f) + C[i + 1] * f

def ribbon(F, pts, width, colours, strands=14, line=.0055, fill=.38, intensity=1., phase=0.):
    """Returns (rgb light, silk fill amount) for one twisting strand ribbon."""
    sd, t = curve_field(F, pts)
    w = width(t) * RIBBON_SCALE
    light = zeros(F)
    for k in range(strands):
        off = w * (k / (strands - 1) - .5)
        edge = 1. if k in (0, strands - 1) else .55 + .25 * math.sin(k * 2.1 + phase)
        light = light + edge * np.exp(-((sd - off) / line) ** 2)
    silk = np.exp(-(sd / (np.abs(w) / 2 + .004)) ** 6)
    ends = ss(0, .07, t) * ss(1, .93, t)
    amount = (np.clip(light, 0, 1.6) + silk * fill) * ends * intensity
    rgb = palette(t + .15 * np.sin(sd * 40 + phase), colours) * amount[..., None]
    return rgb, silk * ends

def func_curve(fn, x0=0., x1=SPAN, n=160):
    return [(x, fn(x)) for x in np.linspace(x0, x1, n)]

def frame(F):
    """Soft rounded intrados roll and top fillet in the glossy white body."""
    return bead(F.D, .055, .03) * .7 + plateau(F.T, 0, .045, .01)

def combine(F, parts, relief=.0035):
    rgb = sum(p[0] for p in parts); silk = sum(p[1] for p in parts)
    keep = ss(.0, .03, F.D)[..., None]
    return frame(F) + relief * np.clip(silk, 0, 1) * ss(0, .03, F.D), rgb * keep

def v01_silk_ribbon(F):
    pts = func_curve(lambda x: 1.74 + .15 * math.sin(math.pi * x / SPAN * 1.6 + .2))
    return combine(F, [ribbon(F, pts, lambda t: .17 * np.cos(math.pi * (t * 1.25 - .1)), [CYAN, AQUA, BLUE, ICE], 16)])

def v02_twin_twist(F):
    a = func_curve(lambda x: 1.66 + .19 * math.sin(TAU * x / SPAN * .85))
    b = func_curve(lambda x: 1.78 - .17 * math.sin(TAU * x / SPAN * .85 + .7))
    return combine(F, [ribbon(F, a, lambda t: .12 * np.cos(math.pi * t * 1.6), [CYAN, AQUA, ICE], 14),
                       ribbon(F, b, lambda t: .10 * np.sin(math.pi * (t * 1.4 + .2)), [BLUE, AQUA, CYAN], 12, phase=1.)])

def v03_arch_hugging(F):
    path = opening(*SHAPE)
    pts = [p for p in offset(path, .14)]
    return combine(F, [ribbon(F, pts, lambda t: .085 * np.sin(math.pi * (3 * t + .25)), [ICE, CYAN, AQUA, CYAN, ICE], 12)])

def v04_rising_flow(F):
    parts = []
    for side in (0, 1):
        def z(x, s=side):
            xs = x if s == 0 else SPAN - x
            return .45 + 1.45 / (1 + math.exp(-(xs - .5) / .16))
        xs = np.linspace(.02, SPAN / 2 + .1, 140) if side == 0 else np.linspace(SPAN - .02, SPAN / 2 - .1, 140)
        pts = [(x, z(x)) for x in xs]
        parts.append(ribbon(F, pts, lambda t: (.11 - .05 * t) * np.cos(math.pi * t * 1.3), [AQUA, CYAN, ICE], 12, phase=side))
    return combine(F, parts)

def v05_horizon(F):
    pts = func_curve(lambda x: TOP - .19 + .05 * math.sin(TAU * x / .95))
    return combine(F, [ribbon(F, pts, lambda t: .09 * np.cos(TAU * t * 1.5), [BLUE, CYAN, AQUA, CYAN, BLUE], 20, line=.0045)])

def v06_loop(F):
    ts = np.linspace(0, 1, 260); th = TAU * (ts - .5)
    pts = [(.06 + (SPAN - .12) * t - .38 * math.sin(a), 1.80 + .20 * math.cos(a) - .12 * (2 * t - 1) ** 2) for t, a in zip(ts, th)]
    return combine(F, [ribbon(F, pts, lambda t: .075 * np.cos(math.pi * t * 2.2), [CYAN, AQUA, ICE, AQUA, BLUE], 12)])

def v07_veils(F):
    parts = []
    for k, (z0, amp, per, ph, col) in enumerate(((1.55, .16, 1.4, 0., [AQUA, CYAN]), (1.72, .13, 1.1, 1.8, [BLUE, AQUA]),
                                                 (1.88, .10, .9, 3.1, [CYAN, ICE]))):
        pts = func_curve(lambda x, z0=z0, amp=amp, per=per, ph=ph: z0 + amp * math.sin(TAU * x / per + ph))
        parts.append(ribbon(F, pts, lambda t, k=k: (.16 - .03 * k) * np.cos(math.pi * (t * 1.1 + .15 * k)), col,
                            10, line=.0045, fill=.6, intensity=.8, phase=k))
    return combine(F, parts)

def v08_strand_fan(F):
    parts = []
    for k in range(13):
        end = (.55 * SPAN + (SPAN * .42) * k / 12, 2.04 - .02 * k)
        ctrl = (.25 + .05 * k, 1.35 + .03 * k)
        ts = np.linspace(0, 1, 70)
        pts = [((1 - t) ** 2 * .06 + 2 * (1 - t) * t * ctrl[0] + t * t * end[0],
                (1 - t) ** 2 * .45 + 2 * (1 - t) * t * ctrl[1] + t * t * end[1]) for t in ts]
        parts.append(ribbon(F, pts, lambda t: zeros(F) + .0, [CYAN, AQUA, ICE], 2, line=.005, fill=0, intensity=.9, phase=k))
    return combine(F, parts)

def v09_carved_channel(F):
    pts = func_curve(lambda x: 1.74 + .15 * math.sin(math.pi * x / SPAN * 1.6 + .2))
    rgb, silk = ribbon(F, pts, lambda t: .15 * np.cos(math.pi * (t * 1.25 - .1)), [CYAN, AQUA, BLUE], 14)
    sd, t = curve_field(F, pts)
    channel = -.012 * np.exp(-(sd / .11) ** 6) * ss(0, .08, t) * ss(1, .92, t)
    lips = .006 * (np.exp(-((sd - .115) / .01) ** 2) + np.exp(-((sd + .115) / .01) ** 2)) * ss(0, .08, t) * ss(1, .92, t)
    h, light = combine(F, [(rgb, silk)], relief=0)
    return h + channel + lips, light

def v10_aurora(F):
    top = 1.98 + .07 * np.sin(TAU * F.X / 1.1) + .03 * np.sin(TAU * F.X / .37 + 1)
    curtain = ss(top - .55, top - .1, F.Z) * (1 - ss(top - .02, top + .03, F.Z))
    streak = (.5 + .5 * np.sin(F.X * 38 + 3 * np.sin(F.X * 6.5) + 2 * np.sin(F.Z * 4))) ** 3
    amount = curtain * (.45 + .9 * streak) * ss(0, .06, F.X) * ss(0, .06, SPAN - F.X)
    rgb = palette((F.Z - (top - .55)) / .55, [BLUE, AQUA, CYAN, AQUA]) * np.clip(amount, 0, 1)[..., None] * .75
    return combine(F, [(rgb, curtain * .4)])

DESIGNS = [
    ('01', 'Silk Ribbon', v01_silk_ribbon),
    ('02', 'Twin Twist', v02_twin_twist),
    ('03', 'Arch Hugging Ribbon', v03_arch_hugging),
    ('04', 'Rising Flow', v04_rising_flow),
    ('05', 'Horizon Strands', v05_horizon),
    ('06', 'Loop Knot', v06_loop),
    ('07', 'Layered Veils', v07_veils),
    ('08', 'Strand Fan', v08_strand_fan),
    ('09', 'Carved Light Channel', v09_carved_channel),
    ('10', 'Aurora Curtain', v10_aurora),
]
SHAPE = ('ellipse', .30, .30, 1.30)

# ---------------------------------------------------------------- textures
def save_image(name, rgb, path, non_color=True):
    n = rgb.shape[0]
    rgba = np.concatenate([rgb, np.ones(rgb.shape[:2] + (1,))], axis=2)
    img = bpy.data.images.new(name, n, n, alpha=False, float_buffer=False)
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = str(path); img.file_format = 'PNG'; img.save()
    if non_color: img.colorspace_settings.name = 'Non-Color'
    return img

def blur(a, r):
    k = 2 * r + 1
    c = np.cumsum(np.pad(a, ((r + 1, r), (0, 0)), mode='edge'), axis=0); a = (c[k:] - c[:-k]) / k
    c = np.cumsum(np.pad(a, ((0, 0), (r + 1, r)), mode='edge'), axis=1); return (c[:, k:] - c[:, :-k]) / k

def maps(code, F, h, light):
    fade = ss(0, .03, F.X) * ss(0, .03, SPAN - F.X) * F.body
    h = h * fade; light = light * fade[..., None]
    gz, gx = np.gradient(h, F.px)
    n = np.stack([-gx, -gz, np.ones_like(h)], axis=2); n /= np.linalg.norm(n, axis=2, keepdims=True)
    normal = save_image(f'R5_{code}_Normal', n * .5 + .5, OUT / f'Arch_R5D{code}_Normal.png')
    cavity = np.clip((blur(blur(h, 5), 5) - h) * 50, 0, 1)
    ao = save_image(f'R5_{code}_AO', np.repeat(np.clip(1 - .5 * cavity, 0, 1)[..., None], 3, axis=2), OUT / f'Arch_R5D{code}_AO.png')
    # Clip rather than normalise: strand crossings at a twist would otherwise dim the whole ribbon.
    emission = save_image(f'R5_{code}_Emission', np.clip(light, 0, 1), OUT / f'Arch_R5D{code}_Emission.png', non_color=False)
    return normal, ao, emission, float(h.min()), float(h.max()), round(float((light.max(axis=2) > .05).mean() / max(F.body.mean(), 1e-6)), 4)

# ---------------------------------------------------------------- material: glossy bright white pearl + light ribbons
def glossy_white(name, normal, ao, emission):
    m = bpy.data.materials.new(name); nt = m.node_tree; L = nt.links
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    uv = nt.nodes.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
    tex = {}
    for k, img in (('n', normal), ('ao', ao), ('e', emission)):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = img; t.interpolation = 'Cubic'
        L.new(uv.outputs['UV'], t.inputs['Vector']); tex[k] = t
    nmap = nt.nodes.new('ShaderNodeNormalMap'); nmap.uv_map = 'UVMap'
    L.new(tex['n'].outputs['Color'], nmap.inputs['Color']); L.new(nmap.outputs['Normal'], b.inputs['Normal'])
    # Ribbons tint the glossy white like translucent coloured silk set into the surface.
    bw = nt.nodes.new('ShaderNodeRGBToBW'); L.new(tex['e'].outputs['Color'], bw.inputs['Color'])
    amt = nt.nodes.new('ShaderNodeMath'); amt.operation = 'MULTIPLY'; amt.use_clamp = True
    amt.inputs[1].default_value = 3.; L.new(bw.outputs['Val'], amt.inputs[0])
    hsv = nt.nodes.new('ShaderNodeHueSaturation'); hsv.inputs['Value'].default_value = 1.6; hsv.inputs['Saturation'].default_value = 1.25
    L.new(tex['e'].outputs['Color'], hsv.inputs['Color'])
    tint = nt.nodes.new('ShaderNodeMix'); tint.data_type = 'RGBA'; tint.inputs['A'].default_value = (.965, .972, .985, 1)
    L.new(hsv.outputs['Color'], tint.inputs['B']); L.new(amt.outputs['Value'], tint.inputs['Factor'])
    mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
    mix.inputs['Factor'].default_value = 1; L.new(tint.outputs['Result'], mix.inputs['A'])
    L.new(tex['ao'].outputs['Color'], mix.inputs['B']); L.new(mix.outputs['Result'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = .07
    b.inputs['Coat Weight'].default_value = 1.; b.inputs['Coat Roughness'].default_value = .02
    if 'Thin Film Thickness' in b.inputs: b.inputs['Thin Film Thickness'].default_value = 140
    L.new(tex['e'].outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 7.
    return m

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
    h, light = design(F)
    normal, ao, emission, hmin, hmax, cover = maps(code, F, h, light)
    name = 'Arch_R5D' + code + '_' + title.replace(' ', '')
    obj = body_mesh(name, path)
    obj.data.materials.append(glossy_white(name + '_GlossyWhite', normal, ao, emission))
    vs = [v.co for v in obj.data.vertices]
    lo = [min(v[k] for v in vs) for k in range(3)]; hi = [max(v[k] for v in vs) for k in range(3)]
    report[name] = {'title': title, 'reference': 'Frutiger Reference/Asset/Arch-Light-Ribbons-User.png',
                    'triangles': len(obj.data.polygons), 'reliefRangeMetres': [round(hmin, 4), round(hmax, 4)],
                    'lightCoverage': cover,
                    'bounds_min_xyz_blender': [round(v, 4) for v in lo], 'bounds_max_xyz_blender': [round(v, 4) for v in hi],
                    'endsAtNextPillarAxis': hi[0] <= SPAN + 1e-4,
                    'onlyCapBehindStartAxis': all(math.hypot(v.x, v.y) <= THICK + 1e-3 for v in vs if v.x < -1e-4),
                    'maps': [f'Arch_R5D{code}_{k}.png' for k in ('Normal', 'AO', 'Emission')]}
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
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ArchDesigns_Round05.blend'), compress=True)
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
