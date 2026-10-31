"""Ten Frutiger Aero pillar design proposals (Round03).

Run headless so the user's open Blender scene is untouched:
  blender --background --factory-startup --python Tools/BuildingAero/build_pillar_designs.py -- [--render] [--preview]

Every design keeps the current pillar contract: base pivot at the origin, height 0-4 m along
Blender Z (Unity Y), footprint inside the current +/-0.41 m bounds, flat base and flat bearing top.
Palette follows the building brief: pearl white, aqua/blue enamel, clear aqua glass, chrome. No green.
Outputs: ArtSource/.../PillarDesigns/Round03 (.blend, one FBX per design, report JSON) and one
review render under _Screenshots-and-previews-to-delete/PillarDesigns/Round03.
"""
import bpy, bmesh, math, json, random, sys
from mathutils import Vector, Matrix
from pathlib import Path

ROOT = Path('D:/PersonalProjects/the-tower-of-babel')
OUT = ROOT / 'ArtSource/Buildings/FrutigerAero/PillarDesigns/Round03'
SHOT = ROOT / '_Screenshots-and-previews-to-delete/PillarDesigns/Round03'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
TAU = 2 * math.pi

bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene
COLL = SCENE.collection
CUTTERS = []

# ---------------------------------------------------------------- materials
def bsdf_of(m):
    nt = m.node_tree
    b = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is None:
        b = nt.nodes.new('ShaderNodeBsdfPrincipled')
        out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None) or nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(b.outputs[0], out.inputs['Surface'])
    return b

def principled(name, base, rough=.2, metal=0., coat=0., coat_rough=.03, trans=0., ior=1.45,
               emit=None, emit_strength=0., film=0., film_ior=1.33):
    m = bpy.data.materials.new(name)
    b = bsdf_of(m)
    vals = {'Base Color': (*base, 1), 'Roughness': rough, 'Metallic': metal, 'Coat Weight': coat,
            'Coat Roughness': coat_rough, 'Transmission Weight': trans, 'IOR': ior,
            'Emission Color': (*(emit or base), 1), 'Emission Strength': emit_strength,
            'Thin Film Thickness': film, 'Thin Film IOR': film_ior}
    for k, v in vals.items():
        if k in b.inputs: b.inputs[k].default_value = v
    m.diffuse_color = (*base, 1)
    return m

def pearl_material():
    base, vein = (.95, .965, .98), (.80, .865, .93)
    m = principled('Aero_Pearl', base, rough=.16, coat=1., coat_rough=.025, film=160, film_ior=1.4)
    nt = m.node_tree; b = bsdf_of(m)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    noise = nt.nodes.new('ShaderNodeTexNoise')
    for k, v in (('Scale', 1.6), ('Detail', 9.), ('Distortion', 1.4)):
        if k in noise.inputs: noise.inputs[k].default_value = v
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0., (*base, 1)
    els[1].position, els[1].color = 1., (*base, 1)
    for pos, col in ((.488, base), (.5, vein), (.512, base)):
        e = els.new(pos); e.color = (*col, 1)
    nt.links.new(tc.outputs['Object'], noise.inputs['Vector'])
    nt.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    return m

PEARL = pearl_material()
ENAMEL = principled('Aero_AquaEnamel', (.02, .50, .86), rough=.12, coat=1., coat_rough=.02)
DEEP = principled('Aero_DeepBlueEnamel', (.01, .22, .68), rough=.12, coat=1., coat_rough=.02)
GLASS = principled('Aero_AquaGlass', (.62, .93, 1.), rough=.015, trans=1., ior=1.45, film=320)
WATER = principled('Aero_Water', (.22, .78, .97), rough=.03, trans=1., ior=1.33,
                   emit=(.15, .75, 1.), emit_strength=.35)
BUBBLE = principled('Aero_Bubble', (1., 1., 1.), rough=0., trans=1., ior=1.08, film=480, film_ior=1.33)
GLOW = principled('Aero_AquaGlow', (.35, .92, 1.), rough=.2, emit=(.30, .90, 1.), emit_strength=10.)
CHROME = principled('Aero_Chrome', (.90, .93, .97), rough=.06, metal=1.)

# ---------------------------------------------------------------- geometry helpers
def make_obj(name, bm, mats, sharp=38):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    o = bpy.data.objects.new(name, me); COLL.objects.link(o)
    for p in me.polygons: p.use_smooth = True
    if sharp:
        try: me.set_sharp_from_angle(angle=math.radians(sharp))
        except Exception: pass
    return o

def loft(name, rings, mats, matfn=None, cap=True, closed=False, sharp=38):
    bm = bmesh.new()
    vr = [[bm.verts.new(p) for p in ring] for ring in rings]
    if closed: vr.append(vr[0])
    n = len(rings[0])
    for a in range(len(vr) - 1):
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((vr[a][i], vr[a][j], vr[a + 1][j], vr[a + 1][i]))
            if matfn: f.material_index = matfn(a, i)
    if cap and not closed:
        bm.faces.new(list(reversed(vr[0]))); bm.faces.new(vr[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return make_obj(name, bm, mats, sharp)

def clean(pts, eps=1e-5):
    out = [pts[0]]
    for p in pts[1:]:
        if abs(p[0] - out[-1][0]) > eps or abs(p[1] - out[-1][1]) > eps: out.append(p)
    return out

def fillet(pts, rad=.03, steps=4):
    """Round every interior corner of a (z, r) polyline with a quadratic arc."""
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, p, b = Vector(pts[i - 1]), Vector(pts[i]), Vector(pts[i + 1])
        da, db = a - p, b - p
        if da.length < 1e-6 or db.length < 1e-6 or da.normalized().dot(db.normalized()) < -.999:
            out.append(pts[i]); continue
        d = min(rad, da.length * .5, db.length * .5)
        p1, p2 = p + da.normalized() * d, p + db.normalized() * d
        for k in range(steps + 1):
            t = k / steps
            q = (1 - t) ** 2 * p1 + 2 * (1 - t) * t * p + t * t * p2
            out.append((q.x, q.y))
    out.append(pts[-1])
    return clean(out)

def lathe(name, prof, mats, segs=64, zone=None, closed=False, offset=(0, 0, 0), sharp=38):
    """Revolve a (z, r) profile; r=0 end points become flat caps."""
    prof = [p for p in prof if p[1] > 1e-4]
    ox, oy, oz = offset
    rings = [[(ox + r * math.cos(TAU * i / segs), oy + r * math.sin(TAU * i / segs), oz + z)
              for i in range(segs)] for z, r in prof]
    matfn = None
    if zone:
        matfn = lambda a, i: zone(.5 * (prof[a][0] + prof[(a + 1) % len(prof)][0]))
    return loft(name, rings, mats, matfn, cap=not closed, closed=closed, sharp=sharp)

def mirror(prof, h=4.):
    return [(h - z, r) for z, r in reversed(prof)]

def torus(name, R, r, z, mat, segs=72, rsegs=16, offset=(0, 0)):
    rings = [[(offset[0] + (R + r * math.cos(TAU * j / rsegs)) * math.cos(TAU * i / segs),
               offset[1] + (R + r * math.cos(TAU * j / rsegs)) * math.sin(TAU * i / segs),
               z + r * math.sin(TAU * j / rsegs)) for j in range(rsegs)] for i in range(segs)]
    return loft(name, rings, [mat], cap=False, closed=True, sharp=0)

def sphere(name, c, r, mat, segs=20):
    prof = [(r * math.sin(t), r * math.cos(t)) for t in
            [-math.pi / 2 + math.pi * k / (segs // 2) for k in range(1, segs // 2)]]
    return lathe(name, prof, [mat], segs=segs, offset=c, sharp=0)

def tube(name, pts, radius, mat, res=4):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = res; cu.use_fill_caps = True
    sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts): p.co = (*c, 1)
    cu.materials.append(mat)
    o = bpy.data.objects.new(name, cu); COLL.objects.link(o)
    return o

def boolean(target, cutter):
    mod = target.modifiers.new('Cut_' + cutter.name, 'BOOLEAN')
    mod.operation = 'DIFFERENCE'; mod.object = cutter
    try: mod.solver = 'EXACT'
    except Exception: pass
    cutter.hide_render = True; cutter.display_type = 'WIRE'
    CUTTERS.append(cutter)

def bevel(o, width, segs=2, angle=30):
    mod = o.modifiers.new('Bevel', 'BEVEL')
    mod.width = width; mod.segments = segs
    mod.limit_method = 'ANGLE'; mod.angle_limit = math.radians(angle)

def bake(o, sharp=38):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(o.matrix_world)
    if o.type == 'CURVE':
        for p in me.polygons: p.use_smooth = True
        try: me.set_sharp_from_angle(angle=math.radians(sharp))
        except Exception: pass
    n = bpy.data.objects.new(o.name + '_baked', me); COLL.objects.link(n)
    bpy.data.objects.remove(o)
    return n

def join(objs, name):
    objs = [bake(o) for o in objs]
    for o in CUTTERS: bpy.data.objects.remove(o)
    CUTTERS.clear()
    bpy.context.view_layer.update()
    for o in COLL.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active
    j.name = name; j.data.name = name
    return j

def linspace(a, b, n):
    return [a + (b - a) * k / (n - 1) for k in range(n)]

def end_round(z, r, rr=.025, h=4.):
    """Quarter-circle rounding of a loft's outer edge at the base and top."""
    e = min(z, h - z)
    if e >= rr: return r
    return r - rr + math.sqrt(max(0., rr * rr - (rr - e) ** 2))

def end_samples(n_mid, h=4., rr=.025):
    ends = [0., .002, .006, .012, .02, rr]
    mid = linspace(rr + .03, h - rr - .03, n_mid)
    return ends + mid + [h - e for e in reversed(ends)]

def bubbles(prefix, rng, count, rmax, z0, z1, sizes, mat=BUBBLE):
    out = []
    for k in range(count):
        s = sizes[0] + (sizes[1] - sizes[0]) * rng.random() ** 2.2
        rho = (rmax - s) * math.sqrt(rng.random()); a = rng.random() * TAU
        out.append(sphere(f'{prefix}_bubble{k}', (rho * math.cos(a), rho * math.sin(a), rng.uniform(z0 + s, z1 - s)),
                          s, mat, segs=14))
    return out

# ---------------------------------------------------------------- designs
def d01_aquarium_core():
    prof = fillet([(0, 0), (0, .395), (.02, .41), (.17, .41), (.21, .37), (.31, .36), (.42, .345),
                   (3.58, .345), (3.69, .36), (3.79, .37), (3.83, .41), (3.98, .41), (4, .395), (4, 0)], .022, 4)
    shell = lathe('D01_shell', prof, [PEARL, ENAMEL], segs=96,
                  zone=lambda z: 1 if (.205 < z < .315 or 3.685 < z < 3.795) else 0)
    chamber = lathe('D01_chamber', fillet([(.5, 0), (.5, .272), (3.5, .272), (3.5, 0)], .06, 5), [PEARL], segs=96)
    boolean(shell, chamber)
    for k in range(3):
        a = k * TAU / 3 + math.pi / 6
        hw, z0, z1 = .165, .66, 3.34
        d, t = Vector((math.cos(a), math.sin(a), 0)), Vector((-math.sin(a), math.cos(a), 0))
        outline = [(hw * math.cos(s), z0 + hw + hw * math.sin(s)) for s in linspace(math.pi, TAU, 17)]
        outline += [(hw * math.cos(s), z1 - hw + hw * math.sin(s)) for s in linspace(0, math.pi, 17)]
        rings = [[tuple(d * rho + t * u + Vector((0, 0, z))) for u, z in outline] for rho in (.12, .6)]
        boolean(shell, loft(f'D01_window{k}', rings, [PEARL]))
    bevel(shell, .007, 2)
    water = lathe('D01_water', fillet([(.52, 0), (.52, .262), (3.48, .262), (3.48, 0)], .05, 4), [WATER], segs=64)
    glow = [torus('D01_glowLow', .268, .012, .56, GLOW), torus('D01_glowHigh', .268, .012, 3.44, GLOW)]
    return [shell, water] + glow + bubbles('D01', random.Random(1), 34, .24, .6, 3.4, (.012, .05))

def d02_helix_flute():
    bprof = fillet([(0, 0), (0, .41), (.13, .41), (.17, .375), (.24, .375), (.29, .35), (.46, .34), (.46, 0)], .025, 4)
    base = lathe('D02_base', bprof, [PEARL, ENAMEL], segs=96, zone=lambda z: 1 if .165 < z < .245 else 0)
    cap = lathe('D02_cap', mirror(bprof), [PEARL, ENAMEL], segs=96, zone=lambda z: 1 if 3.755 < z < 3.835 else 0)
    nth, twist, rings, thetas = 120, math.radians(120), [], [TAU * i / 120 for i in range(120)]
    zs = linspace(.40, 3.60, 72)
    for z in zs:
        t = (z - .40) / 3.2; ent = 1 + .035 * math.sin(math.pi * t); tw = twist * t
        ring = []
        for th in thetas:
            r = ent * (.30 - .034 * max(0., math.cos(10 * th)) ** 2)
            ring.append((r * math.cos(th + tw), r * math.sin(th + tw), z))
        rings.append(ring)
    matfn = lambda a, i: 1 if math.cos(10 * (thetas[i] + math.pi / nth)) > .55 else 0
    shaft = loft('D02_shaft', rings, [PEARL, ENAMEL], matfn, sharp=60)
    glow = [torus('D02_glowLow', .312, .016, .47, GLOW), torus('D02_glowHigh', .312, .016, 3.53, GLOW)]
    return [base, cap, shaft] + glow

def superellipse_r(t, R, R2, p=2.8):
    """Rounded pebble profile point for parameter t in (-pi/2, pi/2)."""
    c, s = math.cos(t), math.sin(t)
    return (math.copysign(abs(s) ** (2 / p), s) * R2, abs(c) ** (2 / p) * R)

def d03_bead_stack():
    bprof = fillet([(0, 0), (0, .41), (.19, .41), (.25, .335), (.30, .33), (.30, 0)], .04, 5)
    objs = [lathe('D03_core', [(.1, 0), (.1, .085), (3.9, .085), (3.9, 0)], [CHROME], segs=32),
            lathe('D03_base', bprof, [PEARL], segs=96), lathe('D03_cap', mirror(bprof), [PEARL], segs=96)]
    hs, n_small, n_big = .17, 6, 5
    hb = (3.40 - n_small * hs) / n_big
    z = .30
    for k in range(n_small + n_big):
        small = k % 2 == 0; h = hs if small else hb
        R = .265 if small else .372
        ts = [-math.pi / 2 + math.pi * j / 26 for j in range(1, 26)]
        prof = [(z + h / 2 + zz, rr) for zz, rr in (superellipse_r(t, R, h / 2, 2.4 if small else 2.9) for t in ts)]
        objs.append(lathe(f'D03_bead{k}', prof, [GLASS if small else PEARL], segs=72, sharp=0))
        if small: objs.append(torus(f'D03_beadGlow{k}', .11, .012, z + h / 2, GLOW, segs=40, rsegs=10))
        z += h
    return objs

def d04_bubble_tube():
    bprof = fillet([(0, 0), (0, .41), (.18, .41), (.24, .37), (.40, .37), (.44, .35), (.44, 0)], .03, 4)
    objs = [lathe('D04_base', bprof, [PEARL, DEEP], segs=96, zone=lambda z: 1 if .28 < z < .36 else 0),
            lathe('D04_cap', mirror(bprof), [PEARL, DEEP], segs=96, zone=lambda z: 1 if 3.64 < z < 3.72 else 0),
            torus('D04_chromeLow', .355, .022, .45, CHROME), torus('D04_chromeHigh', .355, .022, 3.55, CHROME),
            lathe('D04_glass', [(.42, .33), (.42, .362), (3.58, .362), (3.58, .33)], [GLASS], segs=96, closed=True, sharp=60),
            lathe('D04_water', [(.44, 0), (.44, .325), (3.56, .325), (3.56, 0)], [WATER], segs=72),
            torus('D04_glowLow', .30, .013, .48, GLOW), torus('D04_glowHigh', .30, .013, 3.52, GLOW)]
    spine = [(z, .065 + .028 * math.sin(math.pi * (z - .44) / .624) ** 2) for z in linspace(.44, 3.56, 90)]
    objs.append(lathe('D04_spine', [(.44, 0)] + spine + [(3.56, 0)], [PEARL], segs=40, sharp=0))
    return objs + bubbles('D04', random.Random(4), 70, .31, .5, 3.5, (.01, .055))

def d05_hourglass_glow():
    nth, rings, thetas = 128, [], [TAU * i / 128 for i in range(128)]
    zs = end_samples(96)
    band = lambda z: max(0., 1 - abs(z - 2.) / .13)
    channel = lambda th: max(0., math.cos(4 * th)) ** 14
    for z in zs:
        u = abs(z - 2.) / 2.
        base_r = .245 + .09 * u ** 1.6
        n = 2. + 3.5 * u ** 1.5
        base_r = end_round(z, base_r, .025) * (1 - .06 * min(1., band(z) * 2.2))
        fade = max(0., min(1., (z - .30) / .12, (3.70 - z) / .12)) * (1 - min(1., band(z) * 1.6))
        ring = []
        for th in thetas:
            c, s = abs(math.cos(th)), abs(math.sin(th))
            r = base_r * (c ** n + s ** n) ** (-1 / n) - .018 * fade * channel(th)
            ring.append((r * math.cos(th), r * math.sin(th), z))
        rings.append(ring)
    def matfn(a, i):
        z = .5 * (zs[a] + zs[a + 1])
        if 1.90 < z < 2.10: return 2
        if .14 < z < .19 or 3.81 < z < 3.86: return 1
        if .34 < z < 3.66 and not 1.80 < z < 2.20 and channel(thetas[i] + math.pi / nth) > .25: return 3
        return 0
    body = loft('D05_body', rings, [PEARL, ENAMEL, GLOW, DEEP], matfn, sharp=50)
    return [body, torus('D05_waistGlass', .25, .05, 2., GLASS, segs=80, rsegs=20)]

def d06_tri_strand():
    bprof = fillet([(0, 0), (0, .41), (.16, .41), (.22, .36), (.30, .36), (.34, .30), (.34, 0)], .03, 4)
    objs = [lathe('D06_base', bprof, [PEARL, ENAMEL], segs=96, zone=lambda z: 1 if .19 < z < .31 else 0),
            lathe('D06_cap', mirror(bprof), [PEARL, ENAMEL], segs=96, zone=lambda z: 1 if 3.69 < z < 3.81 else 0),
            lathe('D06_rod', [(.3, 0), (.3, .12), (3.7, .12), (3.7, 0)], [GLASS], segs=64),
            lathe('D06_thread', [(.3, 0), (.3, .035), (3.7, .035), (3.7, 0)], [GLOW], segs=24)]
    for k in range(3):
        pts = []
        for z in linspace(.22, 3.78, 160):
            ph = k * TAU / 3 + TAU * (z - .22) / 3.56
            pts.append((.245 * math.cos(ph), .245 * math.sin(ph), z))
        objs.append(tube(f'D06_strand{k}', pts, .095, PEARL, res=6))
    return objs

def d07_halo_rings():
    def r_of(z):
        return .165 + .245 * max(0., 1 - (z - .08) / .62) ** 2.2 + .245 * max(0., 1 - (3.92 - z) / .62) ** 2.2
    prof = [(0, 0), (0, .41), (.08, .41)] + [(z, r_of(z)) for z in linspace(.10, 3.90, 120)] + [(3.92, .41), (4, .41), (4, 0)]
    prof = fillet(prof, .02, 3)
    objs = [lathe('D07_shaft', prof, [PEARL, ENAMEL], segs=96, sharp=50,
                  zone=lambda z: 1 if (.045 < z < .075 or 3.925 < z < 3.955) else 0)]
    for k, z in enumerate((1.12, 1.76, 2.40, 3.04)):
        objs += [torus(f'D07_halo{k}', .325, .03, z, GLOW), torus(f'D07_collar{k}', .17, .028, z, ENAMEL, segs=48)]
        for s in range(3):
            a = s * TAU / 3 + k * .5
            objs.append(tube(f'D07_spoke{k}_{s}', [(.16 * math.cos(a), .16 * math.sin(a), z),
                                                   (.30 * math.cos(a), .30 * math.sin(a), z)], .009, CHROME, res=2))
    return objs

def d08_scallop_shell():
    nth, rings, thetas = 144, [], [TAU * i / 144 for i in range(144)]
    zs = end_samples(110)
    for z in zs:
        rr = .265 + .115 * math.exp(-z / .32) + .115 * math.exp(-(4 - z) / .32) + .005 * math.sin(TAU * z * 6)
        rr = end_round(z, rr, .025)
        ring = []
        for th in thetas:
            r = rr * (.86 + .14 * abs(math.cos(3 * th)) ** .6)
            ring.append((r * math.cos(th), r * math.sin(th), z))
        rings.append(ring)
    def matfn(a, i):
        z = .5 * (zs[a] + zs[a + 1])
        if z < .05 or z > 3.95: return 0
        return 1 if abs(math.cos(3 * (thetas[i] + math.pi / nth))) < .28 else 0
    body = loft('D08_body', rings, [PEARL, ENAMEL], matfn, sharp=55)
    return [body, torus('D08_glowLow', .335, .014, .62, GLOW), torus('D08_glowHigh', .335, .014, 3.38, GLOW)]

def squircle_loft(name, z0, z1, half, n=4., rr=.025, mats=None, matfn=None, nth=96):
    zs = [z0 + e for e in (0, .002, .006, .012, .02, rr)] + [z1 - e for e in (rr, .02, .012, .006, .002, 0)]
    rings, thetas = [], [TAU * i / nth for i in range(nth)]
    for z in zs:
        e = min(z - z0, z1 - z)
        h = half - rr + math.sqrt(max(0., rr * rr - (rr - e) ** 2)) if e < rr else half
        ring = []
        for th in thetas:
            c, s = abs(math.cos(th)), abs(math.sin(th))
            r = h * (c ** n + s ** n) ** (-1 / n)
            ring.append((r * math.cos(th), r * math.sin(th), z))
        rings.append(ring)
    return loft(name, rings, mats or [PEARL], (lambda a, i: matfn(.5 * (zs[a] + zs[a + 1]))) if matfn else None, sharp=50)

def d09_aero_ionic():
    objs = [squircle_loft('D09_plinth', 0, .14, .38)]
    bprof = fillet([(.13, 0), (.13, .33), (.19, .36), (.245, .33), (.26, .29), (.29, .29), (.31, .315), (.35, .30),
                    (.38, .285), (.38, 0)], .02, 4)
    objs.append(lathe('D09_base', bprof, [PEARL, ENAMEL], segs=96, zone=lambda z: 1 if .25 < z < .30 else 0))
    nth, rings, thetas = 160, [], [TAU * i / 160 for i in range(160)]
    zs = linspace(.36, 3.42, 70)
    for z in zs:
        t = (z - .36) / 3.06
        R = .285 * (1 - .06 * t ** 1.5) * (1 + .015 * math.sin(math.pi * t))
        fade = max(0., min(1., (z - .40) / .14, (3.36 - z) / .14))
        fade = fade * fade * (3 - 2 * fade)
        ring = []
        for th in thetas:
            r = R - .017 * fade * (.5 + .5 * math.cos(20 * th)) ** 1.6
            ring.append((r * math.cos(th), r * math.sin(th), z))
        rings.append(ring)
    objs.append(loft('D09_shaft', rings, [PEARL], sharp=70))
    objs.append(torus('D09_necking', .272, .02, 3.42, GLOW))
    objs.append(lathe('D09_echinus', fillet([(3.40, 0), (3.40, .27), (3.45, .30), (3.53, .33), (3.60, .32), (3.62, 0)], .02, 4),
                      [ENAMEL], segs=96))
    objs.append(lathe('D09_cushion', [(3.56, 0), (3.56, .25), (3.78, .25), (3.78, 0)], [PEARL], segs=64))
    for sx in (-1, 1):
        bol = lathe(f'D09_bolster{sx}', fillet([(-.30, 0), (-.30, .112), (-.10, .098), (.10, .098), (.30, .112), (.30, 0)], .015, 3),
                    [ENAMEL], segs=64, sharp=50)
        bol.rotation_euler = (math.radians(90), 0, 0); bol.location = (sx * .268, 0, 3.645)
        objs.append(bol)
        for sy in (-1, 1):
            pts = []
            for k in range(140):
                f = k / 139; phi = sx * sy * TAU * 2.3 * f + math.pi / 2
                rho = .102 * (1 - f) + .024 * f
                pts.append((sx * .268 + rho * math.cos(phi), sy * .304, 3.645 + rho * math.sin(phi)))
            objs.append(tube(f'D09_volute{sx}{sy}', pts, .0135, PEARL, res=4))
            objs.append(sphere(f'D09_eye{sx}{sy}', (sx * .268, sy * .30, 3.645), .032, GLOW, segs=16))
    objs.append(squircle_loft('D09_abacus', 3.76, 4.0, .38, mats=[PEARL, DEEP],
                              matfn=lambda z: 1 if 3.85 < z < 3.90 else 0))
    return objs

def d10_wave_fins():
    bprof = fillet([(0, 0), (0, .41), (.14, .41), (.20, .36), (.23, .30), (.23, 0)], .03, 4)
    objs = [lathe('D10_base', bprof, [PEARL], segs=96), lathe('D10_cap', mirror(bprof), [PEARL], segs=96),
            lathe('D10_core', [(.2, 0), (.2, .14), (3.8, .14), (3.8, 0)], [GLASS], segs=64),
            lathe('D10_thread', [(.2, 0), (.2, .04), (3.8, .04), (3.8, 0)], [GLOW], segs=24)]
    na, alphas, ri = 36, [TAU * j / 36 for j in range(36)], .10
    for k in range(4):
        rings = []
        for z in linspace(.10, 3.90, 100):
            t = (z - .10) / 3.8
            ro = .40 - .13 * math.sin(math.pi * t)
            tw = math.radians(80) * t + k * math.pi / 2
            mid, hl = (ri + ro) / 2, (ro - ri) / 2
            ring = []
            for al in alphas:
                rho = mid + hl * math.cos(al)
                q = (rho - ri) / max(ro - ri, 1e-3)
                u = .036 * (1 - .45 * q) * math.sin(al) + .22 * (rho - ri) ** 2
                ring.append((rho * math.cos(tw) - u * math.sin(tw), rho * math.sin(tw) + u * math.cos(tw), z))
            rings.append(ring)
        matfn = lambda a, i: 1 if math.cos(alphas[i] + math.pi / na) > .72 else 0
        objs.append(loft(f'D10_fin{k}', rings, [PEARL, ENAMEL], matfn, sharp=60))
    return objs

DESIGNS = [
    ('01', 'Aquarium Core', d01_aquarium_core),
    ('02', 'Helix Flute', d02_helix_flute),
    ('03', 'Pearl Bead Stack', d03_bead_stack),
    ('04', 'Bubble Tube', d04_bubble_tube),
    ('05', 'Hourglass Glow', d05_hourglass_glow),
    ('06', 'Tri-Strand Spiral', d06_tri_strand),
    ('07', 'Halo Rings', d07_halo_rings),
    ('08', 'Scallop Shell', d08_scallop_shell),
    ('09', 'Aero Ionic', d09_aero_ionic),
    ('10', 'Wave Fins', d10_wave_fins),
]

# ---------------------------------------------------------------- build, report, export
OUT.mkdir(parents=True, exist_ok=True); SHOT.mkdir(parents=True, exist_ok=True)
report, pillars = {}, []
for code, title, fn in DESIGNS:
    name = 'Pillar_D' + code + '_' + title.replace(' ', '').replace('-', '')
    obj = join(fn(), name)
    pts = [Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    report[name] = {'title': title, 'triangles': tris,
                    'bounds_min_xyz_blender': [round(v, 4) for v in lo], 'bounds_max_xyz_blender': [round(v, 4) for v in hi],
                    'materials': [m.name for m in obj.data.materials]}
    bpy.context.view_layer.update()
    for o in COLL.objects: o.select_set(False)
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    try:
        bpy.ops.export_scene.fbx(filepath=str(OUT / (name + '.fbx')), use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                                 apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False, bake_anim=False,
                                 use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
                                 path_mode='STRIP', embed_textures=False)
        report[name]['fbx'] = str((OUT / (name + '.fbx')).relative_to(ROOT)).replace('\\', '/')
    except Exception as exc:
        report[name]['fbx_error'] = repr(exc)
    pillars.append((code, title, obj))

# ---------------------------------------------------------------- review studio
SPACING = 1.45
font = None
try: font = bpy.data.fonts.load(str(ROOT / 'Assets/_Project/Fonts/Frutiger.ttf'))
except Exception: pass
LABEL = principled('Review_Label', (.02, .20, .52), rough=.4)
for idx, (code, title, obj) in enumerate(pillars):
    x = (idx - 4.5) * SPACING
    obj.location = (x, 0, 0)
    cu = bpy.data.curves.new('Label_' + code, 'FONT'); cu.body = f'{code}\n{title}'
    cu.align_x = 'CENTER'; cu.size = .17; cu.space_line = .95; cu.extrude = .004
    if font: cu.font = font
    cu.materials.append(LABEL)
    lab = bpy.data.objects.new('Label_' + code, cu); COLL.objects.link(lab)
    lab.location = (x * (14.2 / 16.5), -2.3, .19); lab.rotation_euler = (math.radians(80), 0, 0)

floor_bm = bmesh.new()
bmesh.ops.create_circle(floor_bm, cap_ends=True, radius=90, segments=96)
floor = make_obj('Review_Floor', floor_bm,
                 [principled('Review_Floor', (.80, .92, .985), rough=.07, coat=.6, coat_rough=.04)], sharp=0)

world = bpy.data.worlds.new('Review_Sky'); SCENE.world = world
wn = world.node_tree.nodes; wl = world.node_tree.links
bg = next(n for n in wn if n.type == 'BACKGROUND')
tc = wn.new('ShaderNodeTexCoord'); sep = wn.new('ShaderNodeSeparateXYZ'); ramp = wn.new('ShaderNodeValToRGB')
wl.new(tc.outputs['Generated'], sep.inputs[0]); wl.new(sep.outputs['Z'], ramp.inputs['Fac'])
ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = .0, (.78, .94, 1., 1)
ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = .16, (.04, .42, 1., 1)
wl.new(ramp.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 1.35

sun = bpy.data.objects.new('Review_Sun', bpy.data.lights.new('Review_Sun', 'SUN')); COLL.objects.link(sun)
sun.data.energy = 4.0; sun.data.color = (1., .97, .92); sun.data.angle = math.radians(3)
sun.rotation_euler = (math.radians(52), 0, math.radians(-32))
for nm, loc, energy, col, size in (('Review_Fill', (7, -9, 6), 900, (.88, .95, 1.), 8),
                                   ('Review_Rim', (-3, 8, 7), 700, (.85, .95, 1.), 8)):
    L = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'AREA')); COLL.objects.link(L)
    L.data.energy = energy; L.data.color = col; L.data.size = size; L.location = loc
    L.rotation_euler = (Vector((0, 0, 1.8)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()

cam = bpy.data.objects.new('Review_Camera', bpy.data.cameras.new('Review_Camera')); COLL.objects.link(cam)
cam.data.lens = 37; cam.location = (0, -16.5, 2.9)
cam.rotation_euler = (Vector((0, 0, 1.75)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
SCENE.camera = cam

SCENE.render.engine = 'CYCLES'
device = 'CPU'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI'):
        try:
            prefs.compute_device_type = kind
            (prefs.refresh_devices if hasattr(prefs, 'refresh_devices') else prefs.get_devices)()
            if any(d.type == kind for d in prefs.devices):
                for d in prefs.devices: d.use = d.type == kind
                SCENE.cycles.device = 'GPU'; device = kind; break
        except Exception:
            continue
except Exception:
    pass
preview = '--preview' in ARGS
SCENE.cycles.samples = 24 if preview else (192 if device != 'CPU' else 96)
SCENE.cycles.use_denoising = True
for k, v in (('max_bounces', 16), ('transmission_bounces', 16), ('transparent_max_bounces', 16), ('glossy_bounces', 8)):
    setattr(SCENE.cycles, k, v)
SCENE.render.resolution_x, SCENE.render.resolution_y = 3200, 1350
SCENE.render.resolution_percentage = 40 if preview else 100
SCENE.view_settings.view_transform = 'AgX'
SCENE.view_settings.exposure = .2
for look in ('AgX - Punchy', 'Punchy', 'AgX - Medium High Contrast'):
    try: SCENE.view_settings.look = look; break
    except Exception: continue
SCENE.render.image_settings.file_format = 'PNG'

report['_render'] = {'device': device, 'samples': SCENE.cycles.samples, 'look': SCENE.view_settings.look}
(OUT / 'PillarDesignsReport.json').write_text(json.dumps(report, indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'PillarDesigns_Round03.blend'), compress=True)
print('PILLAR_REPORT', json.dumps({k: (v.get('triangles'), v.get('bounds_min_xyz_blender'), v.get('bounds_max_xyz_blender'))
                                   for k, v in report.items() if not k.startswith('_')}))
print('RENDER_DEVICE', device)

if '--render' in ARGS:
    target = (Path(ARGS[ARGS.index('--out') + 1]) if '--out' in ARGS else SHOT / 'Pillar-Designs-Lineup.png')
    SCENE.render.filepath = str(target)
    bpy.ops.render.render(write_still=True)
    print('RENDERED', target)
