"""Ten carved marble / stone arch proposals, Round03.

The user rejected Round01 (surface decoration) and Round02 (form and material experiments) and asked
for marble and stone, with normal maps giving the impression of depth. Every arch here is a low-poly
body (about 1k triangles) whose carving lives in two generated textures:
  * a tangent-space normal map (OpenGL, green up) from a carved height field, and
  * a cavity/AO map from the same height field, darkening recesses.
Height fields are computed analytically from the arch geometry (distance to the opening, distance to
the top, stone coursing), so mouldings follow the curve exactly and are seamless at the pillar joints.

Run headless:
  blender --background --factory-startup --python Tools/BuildingAero/build_arch_designs_round03.py -- [--render] [--preview]

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
OUT = ROOT / 'ArtSource/Buildings/FrutigerAero/ArchDesigns/Round03'
SHOT = ROOT / '_Screenshots-and-previews-to-delete/ArchDesigns/Round03'
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

# ---------------------------------------------------------------- the ten carvings
def c01_classical(F):
    h = plateau(F.D, 0, .06, .010) + plateau(F.D, .06, .12, .016) + plateau(F.D, .12, .19, .022)
    h = np.maximum(h, bead(F.D, .205, .014) + .006)
    h = np.maximum(h, keystone(F, .085, .12, .34, .034))
    cornice = plateau(F.T, 0, .16, .018) + bead(F.T, .17, .012)
    dentil = plateau(F.T, .085, .14, .012) * cushion(F.X, .06, .006) * (np.abs(((F.X / .06) % 1) - .5) < .3)
    return np.maximum(h, cornice + dentil)

def c02_rusticated(F):
    ring = (F.D > 0) & (F.D < .30)
    v = groove_lines(arc_s(F), .155, .014, .016) + .012 * cushion(arc_s(F), .155, .03)
    v = v * ss(-.002, .01, F.D) + groove_lines(F.D, .30 * 2, .012, .014, offset=.30)
    blocks = ashlar(F, .19, .46, .012, .014, .04)
    h = np.where(ring, v + .012 * ss(.0, .02, .30 - F.D), blocks)
    return h + .0018 * fbm(F, .05, 2)

def c03_rosettes(F):
    h = bead(F.D, .045, .016) + plateau(F.D, .08, .15, .010) + bead(F.D, .16, .008)
    for cx in (.38, SPAN - .38):
        h = np.maximum(h, rosette(F, cx, 1.70, .17, 8))
    band = (F.T > .11) & (F.T < .21)
    eggx = ((F.X / .09) % 1 - .5) * .09
    egg = .013 * np.sqrt(np.clip(1 - (eggx / .028) ** 2 - ((F.T - .16) / .038) ** 2, 0, None))
    dart = .008 * np.clip(1 - np.abs(np.abs(eggx) - .043) / .006, 0, 1) * band
    h = np.maximum(h, np.where(band, np.maximum(egg, dart), 0))
    return h + plateau(F.T, 0, .1, .012) + bead(F.T, .105, .008)

def c04_fluted(F):
    inside = np.minimum.reduce([F.D - .12, F.T - .12, F.X - .09, SPAN - .09 - F.X])
    panel = -.014 * ss(0, .02, inside)
    flutes = -.008 * (.5 + .5 * np.cos(TAU * F.X / .055)) * ss(.02, .05, inside)
    return bead(F.D, .055, .022) + panel + flutes + bead(F.T, .055, .02)

def c05_wave_scroll(F):
    band = (F.T > .05) & (F.T < .25)
    line = (F.T - .15) - .055 * np.sin(TAU * F.X / .30)
    curl = np.hypot(((F.X - .075) % .30) - .15 + .0, (F.T - .15) * 1.0)
    wave = .014 * ss(.004, -.004, line) * band + .006 * (np.abs(curl - .045) < .008) * band
    s = arc_s(F)
    reel = ((s / .06) % 1 - .5) * .06
    beads = .012 * np.sqrt(np.clip(1 - (reel / .022) ** 2 - ((F.D - .045) / .02) ** 2, 0, None))
    return wave + beads + plateau(F.D, .08, .13, .008) + plateau(F.T, 0, .045, .012) + plateau(F.T, .25, .27, .01)

def c06_scales(F):
    region = ss(0, .02, np.minimum.reduce([F.D - .13, F.T - .09, F.X - .04, SPAN - .04 - F.X]))
    h = np.zeros_like(F.X)
    for k in (0, 1):
        row = np.floor(F.Z / .08) - k
        sx = ((F.X + (row % 2) * .05) % .10) - .05
        sz = F.Z - row * .08
        h = np.maximum(h, .012 * np.sqrt(np.clip(1 - (sx / .052) ** 2 - ((sz - .085) / .10) ** 2, 0, None)))
    return h * region + bead(F.D, .06, .02) + plateau(F.D, .09, .12, .008) + plateau(F.T, 0, .08, .014)

def c07_coffers(F):
    inside = np.minimum.reduce([F.D - .14, F.T - .11, F.X - .06, SPAN - .06 - F.X])
    cx = np.abs(((F.X / .25) % 1) - .5) * .25; cz = np.abs((((F.Z - .03) / .23) % 1) - .5) * .23
    edge = np.minimum(np.minimum(.125 - cx, .115 - cz), inside)
    coffer = -.010 * ss(.01, .025, edge) - .008 * ss(.03, .045, edge)
    dot = .008 * np.sqrt(np.clip(1 - (np.hypot(cx, cz) / .025) ** 2, 0, None)) * (inside > .03)
    return coffer + dot + plateau(F.D, 0, .12, .014) + keystone(F, .07, .1, .3, .028) + plateau(F.T, 0, .09, .014)

def c08_tracery(F):
    h = bead(F.D, .03, .018) + bead(F.D, .085, .014) + plateau(F.D, .11, .13, .006)
    for cx in (.40, SPAN - .40):
        r = np.hypot(F.X - cx, F.Z - 1.66); ang = np.arctan2(F.Z - 1.66, F.X - cx)
        rq = .19 * (.66 + .34 * np.abs(np.cos(2 * ang)))
        rib = .014 * np.exp(-((r - rq) / .011) ** 2) + .012 * np.exp(-((r - .215) / .01) ** 2)
        h = np.maximum(h, rib - .010 * (r < rq - .01))
    return h + plateau(F.T, 0, .08, .012)

def c09_shell(F):
    cx, cz, R = F.c, F.crown - .02, .36
    r = np.hypot(F.X - cx, F.Z - cz); ang = np.arctan2(F.Z - cz, F.X - cx)
    fan = (ang > .3) & (ang < math.pi - .3)
    scallop = R * (.93 + .07 * np.abs(np.cos(9 * (ang - .3))))
    shell = .02 * (r / R) ** .35 * (.55 + .45 * np.cos(18 * (ang - .3))) * (r < scallop) * fan
    s = arc_s(F)
    pearl = ((s / .045) % 1 - .5) * .045
    pearls = .012 * np.sqrt(np.clip(1 - (pearl / .016) ** 2 - ((F.D - .05) / .016) ** 2, 0, None))
    return np.maximum(shell, pearls + plateau(F.D, .08, .12, .01)) + plateau(F.T, 0, .08, .012)

def c10_weathered(F):
    blocks = ashlar(F, .21, .52, .02, .02, .05, seed=4, irregular=.35)
    ring = (F.D > 0) & (F.D < .27)
    v = groove_lines(arc_s(F), .17, .02, .02) + .014 * cushion(arc_s(F), .17, .04)
    h = np.where(ring, v + groove_lines(F.D, .54, .018, .018, offset=.27), blocks)
    return h + .006 * fbm(F, .09, 10) + .003 * fbm(F, .02, 20)

# stone: base tint, vein tint, roughness, normal strength
STONES = {
    'carrara': ((.95, .955, .965), 1., .22),
    'blue_veined': ((.90, .93, .97), 1., .24),
    'celeste': ((.82, .89, .95), 1., .30),
    'blue_grey': ((.74, .79, .86), 1., .55),
}
DESIGNS = [
    ('01', 'Classical Archivolt', c01_classical, ('ellipse', .30, .30, 1.38), 'carrara'),
    ('02', 'Rusticated Voussoirs', c02_rusticated, ('semicircle', .30, .40, 1.15), 'celeste'),
    ('03', 'Rosette Spandrels', c03_rosettes, ('ellipse', .30, .30, 1.36), 'carrara'),
    ('04', 'Fluted Panels', c04_fluted, ('ellipse', .30, .30, 1.36), 'blue_veined'),
    ('05', 'Wave Scroll', c05_wave_scroll, ('ellipse', .30, .30, 1.30), 'carrara'),
    ('06', 'Shell Scales', c06_scales, ('ellipse', .30, .30, 1.36), 'blue_veined'),
    ('07', 'Coffered', c07_coffers, ('ellipse', .30, .30, 1.36), 'carrara'),
    ('08', 'Gothic Tracery', c08_tracery, ('pointed', .30, .35, 1.60), 'celeste'),
    ('09', 'Shell Keystone', c09_shell, ('ellipse', .30, .30, 1.30), 'blue_veined'),
    ('10', 'Weathered Ashlar', c10_weathered, ('semicircle', .30, .40, 1.15), 'blue_grey'),
]

# ---------------------------------------------------------------- textures
def save_image(name, rgb, path, non_color=True):
    n = rgb.shape[0]
    img = bpy.data.images.new(name, n, n, alpha=False, float_buffer=False)
    rgba = np.concatenate([rgb, np.ones(rgb.shape[:2] + (1,))], axis=2).astype(np.float32)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = str(path); img.file_format = 'PNG'; img.save()
    if non_color: img.colorspace_settings.name = 'Non-Color'
    return img

def blur(a, r):
    k = 2 * r + 1
    c = np.cumsum(np.pad(a, ((r + 1, r), (0, 0)), mode='edge'), axis=0); a = (c[k:] - c[:-k]) / k
    c = np.cumsum(np.pad(a, ((0, 0), (r + 1, r)), mode='edge'), axis=1); return (c[:, k:] - c[:, :-k]) / k

def relief_maps(code, F, h):
    # Fade the carving out within 3 cm of the pillar axes so neighbouring arches join seamlessly.
    fade = ss(0, .03, F.X) * ss(0, .03, SPAN - F.X) * F.body
    h = h * fade
    gz, gx = np.gradient(h, F.px)
    n = np.stack([-gx, -gz, np.ones_like(h)], axis=2)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    normal = save_image(f'R3_{code}_Normal', n * .5 + .5, OUT / f'Arch_R3D{code}_Normal.png')
    cavity = np.clip((blur(blur(h, 6), 6) - h) * 55, 0, 1) + np.clip((blur(h, 24) - h) * 18, 0, 1) * .5
    ao = np.clip(1 - .65 * cavity, 0, 1)
    aoimg = save_image(f'R3_{code}_AO', np.repeat(ao[:, :, None], 3, axis=2), OUT / f'Arch_R3D{code}_AO.png')
    return normal, aoimg, float(h.max()), float(h.min())

# ---------------------------------------------------------------- materials
MARBLE_IMG = bpy.data.images.load(str(MARBLE))

def stone_material(name, stone, normal, ao):
    base, strength, rough = STONES[stone]
    m = bpy.data.materials.new(name)
    nt = m.node_tree; b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    tc = nt.nodes.new('ShaderNodeTexCoord'); mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (.42, .42, .42)
    mt = nt.nodes.new('ShaderNodeTexImage'); mt.image = MARBLE_IMG; mt.projection = 'BOX'; mt.projection_blend = .25
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector']); nt.links.new(mp.outputs['Vector'], mt.inputs['Vector'])
    uv = nt.nodes.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
    nm = nt.nodes.new('ShaderNodeTexImage'); nm.image = normal; nm.interpolation = 'Cubic'
    am = nt.nodes.new('ShaderNodeTexImage'); am.image = ao
    for t in (nm, am): nt.links.new(uv.outputs['UV'], t.inputs['Vector'])
    nmap = nt.nodes.new('ShaderNodeNormalMap'); nmap.uv_map = 'UVMap'; nmap.inputs['Strength'].default_value = strength
    nt.links.new(nm.outputs['Color'], nmap.inputs['Color']); nt.links.new(nmap.outputs['Normal'], b.inputs['Normal'])
    tint = nt.nodes.new('ShaderNodeMix'); tint.data_type = 'RGBA'; tint.blend_type = 'MULTIPLY'
    tint.inputs['Factor'].default_value = 1; tint.inputs['B'].default_value = (*base, 1)
    nt.links.new(mt.outputs['Color'], tint.inputs['A'])
    shade = nt.nodes.new('ShaderNodeMix'); shade.data_type = 'RGBA'; shade.blend_type = 'MULTIPLY'
    shade.inputs['Factor'].default_value = 1
    nt.links.new(tint.outputs['Result'], shade.inputs['A']); nt.links.new(am.outputs['Color'], shade.inputs['B'])
    nt.links.new(shade.outputs['Result'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = rough
    b.inputs['Coat Weight'].default_value = .35 if rough < .4 else 0
    b.inputs['Coat Roughness'].default_value = .08
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
for code, title, carve, (kind, w, zs, crown), stone in DESIGNS:
    path = opening(kind, w, zs, crown)
    F = Field(path, w, zs, crown)
    normal, ao, hmax, hmin = relief_maps(code, F, carve(F))
    name = 'Arch_R3D' + code + '_' + title.replace(' ', '')
    obj = body_mesh(name, path)
    obj.data.materials.append(stone_material(name + '_Stone', stone, normal, ao))
    vs = [v.co for v in obj.data.vertices]
    lo = [min(v[k] for v in vs) for k in range(3)]; hi = [max(v[k] for v in vs) for k in range(3)]
    report[name] = {'title': title, 'stone': stone, 'triangles': len(obj.data.polygons),
                    'reliefRangeMetres': [round(hmin, 4), round(hmax, 4)],
                    'bounds_min_xyz_blender': [round(v, 4) for v in lo], 'bounds_max_xyz_blender': [round(v, 4) for v in hi],
                    'endsAtNextPillarAxis': hi[0] <= SPAN + 1e-4,
                    'onlyCapBehindStartAxis': all(math.hypot(v.x, v.y) <= THICK + 1e-3 for v in vs if v.x < -1e-4),
                    'normalMap': f'Arch_R3D{code}_Normal.png', 'aoMap': f'Arch_R3D{code}_AO.png'}
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
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ArchDesigns_Round03.blend'), compress=True)
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
