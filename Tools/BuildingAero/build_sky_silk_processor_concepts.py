"""Sky processor Round03: ten Silk Clock propositions for the Wind Condenser, for review.

Requested 2026-11-15, after the sky essence became the Silk Clock dandelion. The written propositions are in
Documentation/FrutigerAero/Resources/SKY_PROCESSOR_CONCEPTS.md (Round 03). Every concept keeps the processor
contract (three status lights, a front tray with the real Sky Block, a hero part that spins) and the Silk Clock
principle: a real reference first, the work passed from part to part, Silk Clock parachute seeds as the input
(two puffs on a downwind arc instead of the cloud tuft), silk that becomes the cloud sealed in the block, and a
meadow painted like the dandelion node (painted greens atlas, grass in three sizes, white and aqua pebbles,
young dandelions instead of daisies).

  01 Silk Turbine         the current turbine with real proportions; seeds ride a silk spiral down into the orb
  02 Pinwheel Trio        three pinwheels at the dandelion stalk heights, worked in the node's pick order
  03 Pappus Rotor         a giant glass pappus over a glass achene, with a rolling cloud vortex ring above it
  04 Cup Anemometer       weather-mast cups scoop the seeds cup by cup; a seed-tailed wind vane
  05 Silk Spinning Wheel  a Saxony wheel turned by sail spokes; the silk is drafted into a thread on a bobbin
  06 Floss Spinner        a candy-floss machine: the silk is flung into a web and wound into a cloud on a wand
  07 Silk Sail Mill       a Dutch smock mill whose lattice sails are dressed with silk one by one
  08 Clock Rebuilder      seeds dock back onto a glass receptacle in golden-spiral order
  09 Silk Top             a glass spinning top winds a band of silk on its shoulder
  10 Kite Winch           seeds climb a tether to a silk kite that gathers a cloud; a drum reels it in

Static concepts at the condenser's concept scale (production is 0.75 of this). Builds the scene
Aero_SkyProcessorSilkConcepts with sky_concept_kit.py and dandelion_kit.py. Run headless (Blender 5.3 has a window
in background mode) with the render globals listed in SKY_PROCESSOR_CONCEPTS.md (Round 03):
  blender -b --factory-startup --python <runner>   (the runner execs this file, sets the globals, then execs
                                                   render_sky_processor_concepts.py)
Set ONLY = ('03', '07') before exec to build a subset.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler, Quaternion

ROOTP = 'D:/PersonalProjects/the-tower-of-babel'
SCENE_NAME = 'Aero_SkyProcessorSilkConcepts'
exec(open(ROOTP + '/Tools/BuildingAero/sky_concept_kit.py', encoding='utf-8').read(), globals())
exec(open(ROOTP + '/Tools/BuildingAero/dandelion_kit.py', encoding='utf-8').read(), globals())
TEX = ROOTP + '/Assets/_Project/Models/Resources/Textures/DandelionGreens.png'
LEAF_UV = lambda u, v: (u, .30 + .70 * v)          # atlas regions painted by paint_dandelion_greens.py
STEM_UV = lambda u, v: (u, .02 + .24 * v)


# ---------------------------------------------------------------- materials
def painted_greens():
    """The production dandelion greens: the brush-painted atlas on leaves, stems and buds."""
    m, n, l, b = _mat('Concept_DandelionGreens'); _set(b, rough=.42, coat=.6, coat_rough=.08)
    tex = n.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(TEX, check_existing=True)
    l.new(tex.outputs['Color'], b.inputs['Base Color'])
    return m


def vertex_colour(name, **kw):
    m, n, l, b = _mat(name); _set(b, **kw)
    a = n.new('ShaderNodeVertexColor'); a.layer_name = 'Col'; l.new(a.outputs['Color'], b.inputs['Base Color'])
    return m


def painted_meadow(name):
    """Glossy Bliss hill in the atlas palette: teal-emerald hollows to warm yellow-green crowns, in soft strokes."""
    m, n, l, b = _mat(name); _set(b, rough=.22, coat=1, coat_rough=.04)
    tc = n.new('ShaderNodeTexCoord')
    nz = n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3.2; nz.inputs['Detail'].default_value = 6
    nz.inputs['Distortion'].default_value = .8; l.new(tc.outputs['Object'], nz.inputs['Vector'])
    wv = n.new('ShaderNodeTexWave'); wv.inputs['Scale'].default_value = 3.5; wv.inputs['Distortion'].default_value = 4
    wv.inputs['Detail'].default_value = 1; l.new(tc.outputs['Object'], wv.inputs['Vector'])
    h = n.new('ShaderNodeMath'); h.operation = 'MULTIPLY'; h.inputs[1].default_value = .45
    l.new(axis_value(n, l, 'Z', 0, .45), h.inputs[0])
    a = n.new('ShaderNodeMath'); a.operation = 'MULTIPLY_ADD'; a.inputs[1].default_value = .6
    l.new(nz.outputs['Fac'], a.inputs[0]); l.new(h.outputs[0], a.inputs[2])
    c = n.new('ShaderNodeMath'); c.operation = 'MULTIPLY_ADD'; c.inputs[1].default_value = .07       # faint brush drag
    l.new(wv.outputs['Fac'], c.inputs[0]); l.new(a.outputs[0], c.inputs[2])
    d = n.new('ShaderNodeMath'); d.operation = 'SUBTRACT'; d.inputs[1].default_value = .1; d.use_clamp = True
    l.new(c.outputs[0], d.inputs[0])
    r = ramp(n, [(0, (.03, .26, .17)), (.28, (.05, .4, .12)), (.52, (.17, .6, .1)), (.76, (.48, .8, .16)), (1, (.76, .9, .3))])
    l.new(d.outputs[0], r.inputs[0]); l.new(r.outputs[0], b.inputs['Base Color'])
    return m


def vane(name, c):
    """Thin-film glass sheet (pinwheel vanes): tinted body, white rim, soft glow."""
    return fresnel_shell(name, (*c, 1), (1, 1, 1, 1), .95, .55, glow=.35, film=420, rough=.05, ior=1.2)


GREEN = painted_greens()
MAT.update({
    'DGreen': GREEN, 'StemGel': GREEN, 'Bract': GREEN,
    'PaintMeadow': painted_meadow('Concept_PaintedMeadow'),
    'GrassVC': vertex_colour('Concept_PaintedGrass', rough=.5, coat=.3),
    'WhitePebble': plain('Concept_WhitePebble', (.88, .94, .97), rough=.2, coat=1),
    'Silk': plain('Concept_Silk', (1, 1, 1), rough=.35, sss=.3, emit=(.92, .96, 1, 1), es=1.2),
    'SilkRibbon': fresnel_shell('Concept_SilkRibbon', (1, 1, 1, 1), (.8, .93, 1, 1), .7, .14, glow=.8, rough=.3),
    'SilkCloth': fresnel_shell('Concept_SilkCloth', (1, 1, 1, 1), (.82, .93, 1, 1), .95, .75, glow=.7, rough=.4),
    'KiteSilk': fresnel_shell('Concept_KiteSilk', (.62, .88, 1, 1), (.25, .7, 1, 1), .9, .45, glow=.25, film=380, rough=.3),
    'CloudVeil': fresnel_shell('Concept_CloudVeil', (1, 1, 1, 1), (.7, .88, 1, 1), .38, .06, glow=.2, rough=.5),
    # Clear shells instead of solid refracting glass, so the cloud inside an orb reads at a glance.
    'OrbShell': fresnel_shell('Concept_OrbShell', (.85, .95, 1, 1), (.5, .82, 1, 1), .6, .05, film=260, rough=0, ior=1.1),
    'Floss': plain('Concept_Floss', (1, 1, 1), rough=.5, emit=(.95, .97, 1, 1), es=1.6),
    'Glaze': plain('Concept_BowlGlaze', (.12, .5, .9), rough=.06, coat=1, coat_rough=0),
    'BladeGlow': gradient('Concept_BladeGlow', 'Z', .1, .9, [(0, (.86, .95, 1)), (.75, (.86, .95, 1)), (1, (.45, .88, 1))],
                          emit=.35, method='DITHERED', rough=.35, trans=.85, ior=1.4, coat=1),
    'VaneCyan': vane('Concept_VaneCyan', (.08, .62, 1)), 'VaneMint': vane('Concept_VaneMint', (.2, .9, .45)),
    'VaneWhite': vane('Concept_VaneWhite', (.95, .98, 1)),
})


# ---------------------------------------------------------------- shared helpers
HILL = bumps((0, .05, .3, .75), (.62, .45, .1, .42), (-.66, .32, .08, .45), (.1, -.7, .04, .4))
PATH = lambda x, y: abs(x) < .24 and y < -.4                      # pebble path to the tray stays clear
ROSETTES = [(-.74, -.3, .4), (.72, -.42, 2.2), (.3, .86, 4.0)]
TIPS = [((.62, 1, .3), 5), ((.82, .96, .26), 3), ((.28, .86, .34), 3), ((.32, .9, .62), 2), ((.86, .86, .32), 1),
        ((.42, .84, .76), 1), ((.52, .95, .18), 3)]
CLUMPS = [(math.cos(a) * r, math.sin(a) * r) for a, r in ((.4, .98), (1.3, 1.02), (2.2, .95), (2.9, 1.04), (3.5, .97), (5.9, 1.0))]


def bez(pts, t):
    P = [Vector(p) for p in pts]
    while len(P) > 1: P = [a.lerp(b, t) for a, b in zip(P[:-1], P[1:])]
    return P[0]


def meadow_grass(name, count, rmin, rmax, skip, seed):
    """Production grass (build_sky_dandelions.painted_grass) on the hill: short, medium and tall blades, tall ones in
    clumps, each blade its own tip shade in the vertex colour."""
    rng = random.Random(seed); bm = bmesh.new(); col = bm.loops.layers.color.new('Col'); uv = bm.loops.layers.uv.new('UVMap')
    made = tries = 0
    while made < count and tries < count * 8:
        tries += 1
        r = math.sqrt(rng.uniform(rmin * rmin, rmax * rmax)); a = rng.uniform(0, TAU); x, y = r * math.cos(a), r * math.sin(a)
        if skip(x, y): continue
        near = min(math.hypot(x - cx, y - cy) for cx, cy in CLUMPS); roll = rng.random()
        if near < .16 and roll < .8: hh = rng.uniform(.3, .45)
        elif roll < .5: hh = rng.uniform(.05, .12)
        elif roll < .85: hh = rng.uniform(.12, .24)
        else: hh = rng.uniform(.24, .34)
        made += 1
        width = .012 + hh * .036 + rng.uniform(-.002, .003); lean = .25 + hh * .6
        face = rng.uniform(0, TAU); lx, ly = math.cos(face) * lean * rng.uniform(.3, 1), math.sin(face) * lean * rng.uniform(.3, 1)
        side = Vector((-ly, lx, 0)).normalized()
        tip = rng.choices([c for c, _ in TIPS], [w for _, w in TIPS])[0]
        tip = tuple(min(1, max(0, c + rng.uniform(-.06, .06))) for c in tip)
        root = (rng.uniform(.03, .07), rng.uniform(.24, .34), rng.uniform(.04, .09))
        z0 = HILL(x, y) - .01; rows = []
        for t in (0, .3, .62, 1):
            c = Vector((x + lx * hh * t * t, y + ly * hh * t * t, z0 + hh * t * (1 - .25 * t * t)))
            w = width * (1 - t) ** .8
            rows.append((bm.verts.new(c - side * w), bm.verts.new(c + side * w), t))
        for (a0, b0, t0), (a1, b1, t1) in zip(rows[:-1], rows[1:]):
            f = bm.faces.new((a0, b0, b1, a1))
            for loop, t, u in zip(f.loops, (t0, t0, t1, t1), (0, 1, 1, 0)):
                k = t ** .9
                loop[col] = tuple(root[i] + (tip[i] - root[i]) * k for i in range(3)) + (1,)
                loop[uv].uv = (u, t)
    return make(name, bm, 'GrassVC')


def young_dandelion(pos, yaw, rng, s=.68):
    """A young rosette: toothed leaves in three sizes, the yellow bloom and a closed bud. No seed clock: the essence
    node stays the only place seeds grow."""
    g = Geo(); g.region = LEAF_UV
    for i in range(7):
        k = i % 3
        leaf(g, i * TAU / 7 + rng.uniform(-.15, .15), (.66, .5, .34)[k] * rng.uniform(.9, 1.1), (.11, .09, .065)[k], rng,
             teeth=(4, 3, 3)[k], z0=(0, .006, .012)[k], mat='DGreen', rib='DGreen', nt=40, lift=(1, 1.3, 1.9)[k])
    g.region = STEM_UV
    g.ball((0, 0, .045), .09, 'DGreen', scale=(1, 1, .45), seg=16, rings=8)
    bud(g); yellow_bloom(g, rng)
    g.region = None
    o = g.done('YoungDandelion')
    o.location = pos; o.rotation_euler = (0, 0, yaw); o.scale = (s, s, s)
    return o


def tray(x=0, y=-1.3):
    lathe('Tray', [(0, -.04), (.12, -.05), (.3, 0), (.34, .05), (.31, .07), (.27, .04), (0, .03)], 'Pearl', loc=(x, y, .3))
    torus('TrayRim', (x, y, .355), .325, .016, 'Azure', seg=32, minor=6)
    rod('TrayStem', (x, y, 0), (x, y, .27), .045, 'Chrome')
    lathe('TrayFoot', [(0, 0), (.13, 0), (.1, .04), (0, .04)], 'Pearl', loc=(x, y, HILL(x, y)))
    block((x, y, .55))


def front_lights():
    return [(x, -.93, HILL(x, -.93) + .055) for x in (-.16, 0, .16)]


def meadow(rng, clear=.4, rosettes=ROSETTES, lights=None, tray_at=(0, -1.3), grass=430, avoid=None):
    """The painted dandelion meadow every concept stands on, the pebble path, the tray and the status lights."""
    terrain('Hill', 1.2, HILL, 'PaintMeadow', rings=16, seg=64, subsurf=1)
    lights = lights or front_lights()
    keep_clear = [(x, y, .24) for x, y, _ in rosettes] + [(p[0], p[1], .09) for p in lights]

    def skip(x, y):
        return (PATH(x, y) or math.hypot(x, y) < clear or any(math.hypot(x - a, y - b) < r for a, b, r in keep_clear)
                or (avoid is not None and avoid(x, y)))
    meadow_grass('Grass', grass, .2, 1.17, skip, rng.randint(1, 999))
    for x, y, yaw in rosettes: young_dandelion((x, y, HILL(x, y) - .006), yaw, rng)
    for i, t in enumerate((0, .17, .34, .51, .68)):
        y = -1.12 + t * .95; x = .05 * math.sin(i * 1.7)
        sphere('PathStone', (x, y, HILL(x, y) + .005), .085 - .008 * i, ('WhitePebble', 'AquaPebble')[i % 2],
               scale=(1.2, .9, .22), rot=(0, 0, i), seg=12, rings=6)
    k = 0
    while k < 9:
        r = rng.uniform(.82, 1.12); a = rng.uniform(0, TAU); x, y = r * math.cos(a), r * math.sin(a)
        if PATH(x, y) or (abs(x) < .45 and y < -.7): continue
        s = rng.uniform(.04, .075)
        sphere('Pebble', (x, y, HILL(x, y) + s * .15), s, ('WhitePebble', 'WhitePebble', 'AquaPebble')[k % 3],
               scale=(1, rng.uniform(.7, 1), .5), rot=(0, 0, a), seg=14, rings=8)
        k += 1
    if tray_at: tray(*tray_at)
    for i, p in enumerate(lights): sphere('StatusLight_%d' % i, p, .035, 'Lime', seg=16, rings=8)


def chute(g, p, up, rng, size=.24, k=12, w=.0032):
    """One Silk Clock parachute seed, thicker than the node's so it reads at processor distance: aqua achene,
    beak and a pappus umbrella of curled bristles. size is the distance from the umbrella to the achene's foot."""
    p, up = Vector(p), Vector(up).normalized(); u, v = frame(up)
    a0, a1 = p - up * size, p - up * size * .62; aw = .008 * size / .16
    g.strand([a0, a0.lerp(a1, .5), a1], (aw * .3, aw, aw * .5), 'AcheneAqua', sides=4)
    g.prism(a1, p, w * .9, w * .8, 'Beak')
    for j in range(k):
        phi = j * TAU / k + rng.uniform(-.2, .2); th = math.radians(78 + rng.uniform(-8, 6))
        b = up * math.cos(th) + (u * math.cos(phi) + v * math.sin(phi)) * math.sin(th)
        L = size * .42 * rng.uniform(.9, 1.1); m = p + b * L * .55; e = m + (b * .8 + up * .2).normalized() * L * .48
        g.prism(p, m, w, w * .75, 'Pappus', 0, .55); g.prism(m, e, w * .75, w * .3, 'Pappus', .55, 1)


def wind_up(rng, lean=(.3, -.1, 1)):
    return (Vector(lean) + Vector((rng.uniform(-.3, .3), rng.uniform(-.3, .3), 0))).normalized()


def seed_path(name, pts, rng, puffs=(.32, .8), n=7, spread=.12, size=.22, trail=4):
    """The input: two puffs of parachute seeds on a one-sided arc from the player's side, the first further along."""
    g = Geo()
    for tc in puffs:
        c = bez(pts, tc)
        for _ in range(n):
            off = Vector((rng.gauss(0, spread), rng.gauss(0, spread), rng.gauss(0, spread * .8)))
            chute(g, c + off, wind_up(rng), rng, size * rng.uniform(.85, 1.1))
    for _ in range(trail):
        c = bez(pts, rng.uniform(.05, .98)) + Vector([rng.uniform(-.05, .05) for _ in range(3)])
        chute(g, c, wind_up(rng), rng, size * .85)
    return g.done(name)


def sheet(g, fn, nu, nv, mat):
    """A curved sheet fn(u, v) -> point, u and v in 0..1 (vanes, sails, cloth, kite)."""
    idx = g.mi(mat)
    rows = [[g.bm.verts.new(fn(i / nu, j / nv)) for j in range(nv + 1)] for i in range(nu + 1)]
    for i in range(nu):
        for j in range(nv):
            quad = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
            f = g.bm.faces.new([rows[a][b] for a, b in quad]); f.material_index = idx
            for loop, (a, b) in zip(f.loops, quad): g._loop(loop, mat, (a / nu, b / nv))


def quat_to(d, up_from=(0, 0, 1)):
    return Vector(up_from).rotation_difference(Vector(d).normalized())


def orient(o, q):
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = q
    return o


def helix_on_torus(c, R, r, wraps, n, phase=0.):
    c = Vector(c); pts = []
    for i in range(n):
        al = TAU * i / (n - 1); be = wraps * al + phase
        pts.append((c.x + (R + r * math.cos(be)) * math.cos(al), c.y + (R + r * math.cos(be)) * math.sin(al), c.z + r * math.sin(be)))
    return pts


def glass_orb(z_seat, r=.42, cloud_s=.34):
    """The condenser's glass cloud orb on its pearl seat."""
    torus('OrbSeat', (0, 0, z_seat - .04), .32, .045, 'Pearl', seg=40, minor=8)
    oz = z_seat - .06 + r
    sphere('Orb', (0, 0, oz), r, 'OrbShell', seg=40, rings=20)
    torus('OrbGlow', (0, 0, oz), r + .005, .016, 'SkyGlow', seg=56, minor=6)
    cloud((0, 0, oz - r * .76), cloud_s, name='CloudMass')
    return oz


# ---------------------------------------------------------------- 01 Silk Turbine
def blade_bm(L, r0=.07, n=16, m=12, thick=.2, twist=(18, 2)):
    """A slender twisted airfoil along +Z: narrow root, widest near 15 %, tapering to a narrow tip."""
    def chord(t):
        return .06 + .07 * (t / .15) ** .7 if t < .15 else .13 - .095 * ((t - .15) / .85) ** .9
    bm = bmesh.new(); rows = []
    for i in range(n + 1):
        t = i / n; c = chord(t); tw = math.radians(twist[0] + (twist[1] - twist[0]) * t); ring = []
        for k in range(m):
            a = k * TAU / m; x = math.cos(a) * c * .5
            y = math.sin(a) * c * thick * .5 * (1.25 - .5 * (math.cos(a) * .5 + .5))      # thicker toward the leading edge
            ring.append(bm.verts.new((x * math.cos(tw) - y * math.sin(tw), x * math.sin(tw) + y * math.cos(tw), r0 + t * L)))
        rows.append(ring)
    for a_, b_ in zip(rows[:-1], rows[1:]):
        for k in range(m): bm.faces.new((a_[k], a_[(k + 1) % m], b_[(k + 1) % m], b_[k]))
    bm.faces.new(rows[0][::-1]); bm.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def c01_silk_turbine():
    rng = random.Random(101); meadow(rng, clear=.38)
    oz = glass_orb(HILL(0, 0))
    tuft((.05, .02, oz - .02), .9, (0, 0, .8))
    torus('Collar', (0, 0, oz + .41), .12, .04, 'Azure', seg=32, minor=8)
    hz = 2.38
    lathe('Tower', [(.09, oz + .38), (.075, oz + .9), (.055, hz - .3), (.048, hz - .1), (0, hz - .1)], 'Pearl', seg=24)
    torus('YawRing', (0, .02, hz - .1), .062, .014, 'Azure', seg=24, minor=6)
    sphere('Nacelle', (0, .1, hz), .1, 'Pearl', scale=(1, 2.7, 1.05), seg=24, rings=12)
    rbox('Vane', (0, .42, hz + .04), (.016, .16, .13), 'Pearl', .006)
    lathe('Spinner', [(.105, 0), (.1, .05), (.07, .12), (.03, .17), (0, .185)], 'Azure', loc=(0, -.16, hz), rot=(math.pi / 2, 0, 0), seg=24)
    for k in range(3):
        make('Blade_%d' % k, blade_bm(.8), 'BladeGlow', loc=(0, -.22, hz), rot=(0, k * TAU / 3 + .35, 0))
    seed_path('SeedsIn', [(-.85, -1.55, .5), (-1.05, -1.0, 1.6), (-.2, -.5, hz - .05)], rng)
    sp = spiral((0, 0), .22, .17, hz - .22, oz + .48, 1.7, 16, phase=-1.0)
    wind_ribbon('SilkSpiral', sp, .03, 'SilkRibbon')
    g = Geo()
    for i in range(1, 15, 2): chute(g, Vector(sp[i]) + Vector((0, 0, .05)), wind_up(rng), rng, .14)
    for k in range(5):                                          # silk knitting into the cloud inside the orb
        pts = [Vector((math.cos(k * 1.3 + t * 4) * .2 * (1 - t * .5), math.sin(k * 1.3 + t * 4) * .2 * (1 - t * .5), oz + .35 - t * .45))
               for t in (i / 8 for i in range(9))]
        g.strand(pts, [.004] * 9, 'Silk')
    g.done('SilkRide')
    g = Geo()                                                   # the orbiting cloud ring becomes drifting seeds
    for i in range(10):
        a = i * TAU / 10 + .2
        chute(g, (math.cos(a) * .85, math.sin(a) * .85, 1.75 + math.sin(a) * .14), wind_up(rng), rng, .17)
    g.done('SeedRing')
    return 3.3


# ---------------------------------------------------------------- 02 Pinwheel Trio
# Production stalk order: Stalk_2 (small, left) first, Stalk_1 (right), then the tall centre Bloom as the finale.
PINS = [('Pinwheel_Small', (-.26, .14), (-.74, .3, 1.28), .3, ('VaneMint', 'VaneWhite')),
        ('Pinwheel_Right', (.28, -.02), (.8, -.16, 1.6), .33, ('VaneCyan', 'VaneWhite')),
        ('Pinwheel_Centre', (0, .12), (.06, .2, 2.15), .4, ('VaneCyan', 'VaneMint'))]


def pinwheel(name, top, R, normal, mats, rng, spin=0., silk=False):
    """A four-vane garden pinwheel facing `normal`: each vane spans a corner of the square sheet, its trailing edge
    lifted toward the wind like the folded flap."""
    pv = orient(empty(name, top), quat_to(normal))
    g = Geo()

    def vane_pt(phi, u, v):
        # The flat half of the vane lies in the sheet; toward its trailing edge the paper curls up and back in
        # toward the pin, like the folded corner of a real pinwheel.
        al = math.radians(66) * v; rad = R * .7071 / math.cos(math.radians(45) - min(al, math.radians(89))) * (.06 + .94 * u)
        curl = v ** 2.2 * math.sin(math.pi * min(u, 1) ** .8)
        rad *= 1 - .38 * curl
        return Vector((rad * math.cos(phi + al), rad * math.sin(phi + al), R * .62 * curl))
    for k in range(4):
        phi = spin + k * TAU / 4
        sheet(g, lambda u, v, phi=phi: vane_pt(phi, u, v), 9, 7, mats[k % len(mats)])
    g.done('Vanes', parent=pv)
    sphere('Hub', (0, 0, .05), R * .11, 'Azure', seg=20, rings=10, parent=pv)
    sphere('Pin', (0, 0, .1), R * .05, 'Pearl', seg=12, rings=8, parent=pv)
    torus('HubGlow', (0, 0, .04), R * .16, R * .015, 'SkyGlow', seg=32, minor=6, parent=pv)
    if silk:                                                    # silk caught on the vanes, and a few seeds
        g = Geo()
        for _ in range(16):
            phi = spin + rng.randrange(4) * TAU / 4; u, v = rng.uniform(.35, .92), rng.uniform(.1, .85)
            pts = [vane_pt(phi, u + .05 * math.sin(i), v + i * .06) + Vector((0, 0, .012)) for i in range(5)]
            g.strand(pts, [.004, .005, .005, .004, .002], 'Silk')
        for _ in range(4):
            phi = spin + rng.randrange(4) * TAU / 4
            chute(g, vane_pt(phi, rng.uniform(.55, .9), rng.uniform(.3, .8)) + Vector((0, 0, .1)), (0, .2, 1), rng, .14)
        g.done('CaughtSilk', parent=pv)
    return pv


def c02_pinwheel_trio():
    rng = random.Random(202)
    lights = [(-.3, -.04, HILL(-.3, -.04) + .05), (.3, -.18, HILL(.3, -.18) + .05), (0, -.25, HILL(0, -.25) + .23)]
    meadow(rng, clear=.0, lights=lights, avoid=lambda x, y: math.hypot(x, y - .12) < .36)
    face = Vector((-1, -.42, .08)).normalized()                # into the wind, three-quarters to the player
    for name, b, top, R, mats in PINS:
        b3 = Vector((b[0], b[1], HILL(*b) - .02)); t = Vector(top)
        mid = b3.lerp(t, .55) + Vector(((t.x - b3.x) * .12, (t.y - b3.y) * .12, 0))
        ribbon(name + '_Stick', [tuple(b3), tuple(mid), tuple(t - face * .05)], .02, 'Pearl')
        pinwheel(name, top, R, face, mats, rng, spin=rng.uniform(0, 1.5), silk=name == 'Pinwheel_Small')
    L, Rt, C = (Vector(p[2]) for p in PINS)
    bz = HILL(0, .12)
    lathe('Bowl', [(.07, bz + .02), (.2, bz + .04), (.3, bz + .12), (.33, bz + .22)], 'OrbShell', loc=(0, .12, 0), seg=48)
    torus('BowlRim', (0, .12, bz + .22), .33, .012, 'Azure', seg=48, minor=6)
    cloud((0, .12, bz + .04), .22, name='CloudMass')
    seed_path('SeedsIn', [(-.95, -1.55, .55), (-1.15, -.85, .9), (-.79, .14, 1.29)], rng)
    wind_ribbon('SilkPass_0', [tuple(L + Vector((.12, -.12, -.06))), (-.02, -.42, 1.18), tuple(Rt + Vector((-.14, -.06, -.06)))], .028, 'SilkRibbon')
    wind_ribbon('SilkPass_1', [tuple(Rt + Vector((-.06, -.1, .1))), (.52, -.12, 2.12), tuple(C + Vector((.14, -.08, -.02)))], .032, 'SilkRibbon')
    down = [tuple(C.lerp(Vector((0, .12, bz + .2)), t) + Vector((math.cos(5 * t + 1) * .2, math.sin(5 * t + 1) * .2, 0)))
            for t in (i / 10 for i in range(1, 11))]
    wind_ribbon('SilkDown', down, .024, 'SilkRibbon')
    return 2.6


# ---------------------------------------------------------------- 03 Pappus Rotor
def c03_pappus_rotor():
    rng = random.Random(303); meadow(rng, clear=.36)
    s0 = HILL(0, 0)
    torus('Seat', (0, 0, s0 - .03), .27, .045, 'Pearl', seg=40, minor=8)
    cz, rx, rz = s0 + .38, .3, .435
    sphere('Achene', (0, 0, cz), rx, 'OrbShell', scale=(1, 1, rz / rx), seg=40, rings=24)
    g = Geo()                                                   # ribs along the glass achene, like a real seed's ridges
    for k in range(10):
        a = k * TAU / 10
        pts = [Vector((math.cos(a) * rx * 1.012 * math.sin(th), math.sin(a) * rx * 1.012 * math.sin(th), cz + rz * 1.012 * math.cos(th)))
               for th in [.3 + i * (2.55 / 12) for i in range(13)]]
        g.strand(pts, [.006] * 13, 'Azure', sides=4)
    g.done('AcheneRibs')
    cloud((0, 0, cz - .36), .26, name='CloudMass'); tuft((0, 0, cz - .05), .8)
    top, hz = cz + rz, 2.12
    torus('Collar', (0, 0, top - .01), .07, .025, 'Azure', seg=24, minor=6)
    lathe('Beak', [(.035, top - .02), (.03, (top + hz) / 2), (.022, hz - .04), (0, hz - .04)], 'Pearl', seg=16)
    torus('BeakBand', (0, 0, (top + hz) / 2), .036, .01, 'Azure', seg=20, minor=6)
    sphere('PappusHub', (0, 0, hz), .055, 'Pearl', seg=20, rings=10)
    hub = Vector((0, 0, hz)); g = Geo()
    for i in range(56):                                         # the giant pappus: frosted fibres with silky tips and barbs
        a = i * TAU / 56 + rng.uniform(-.04, .04); th = math.radians(rng.uniform(58, 70))
        d = Vector((math.cos(a) * math.sin(th), math.sin(a) * math.sin(th), math.cos(th))); L = .7 * rng.uniform(.94, 1.04)
        m = hub + d * L * .55; e = m + (d * .7 + Vector((0, 0, .3))).normalized() * L * .48
        g.prism(hub + d * .05, m, .011, .008, 'FrostFiber', sides=4); g.prism(m, e, .008, .003, 'Pappus', sides=4)
        side = d.cross(Vector((0, 0, 1))).normalized()
        for j, f in enumerate((.25, .55, .8)):
            p = m.lerp(e, f); s = side * (1 if j % 2 else -1)
            g.prism(p, p + (s * .55 + d * .45).normalized() * .055, .003, .0008, 'Pappus')
    g.done('Pappus')
    rc = Vector((0, 0, hz + .48)); R, r = .42, .11
    torus('VortexRing', tuple(rc), R, r, 'CloudVeil', seg=64, minor=20)
    for k in range(7):                                          # a few cloud puffs riding the ring as it rolls
        a = k * TAU / 7 + .4
        tuft((math.cos(a) * R, math.sin(a) * R, rc.z + r * .35), .34, (0, 0, a))
    wind_ribbon('Roll', helix_on_torus(rc, R, r * 1.15, 6, 90), .012, 'SilkRibbon')
    seed_path('SeedsIn', [(-.85, -1.55, .55), (-1.2, -.9, 1.9), (-.42, -.22, rc.z)], rng)
    g = Geo()
    for k in range(6):
        a = 3.7 + k * .38; chute(g, (math.cos(a) * R, math.sin(a) * R, rc.z + r * .9), wind_up(rng), rng, .15)
    g.done('RingSeeds')
    ribbon('SilkDown', spiral((0, 0), .06, .045, hz - .07, top + .03, 3, 24), .006, 'Silk')
    return 2.85


# ---------------------------------------------------------------- 04 Cup Anemometer
CUP_R = .19
CUP_PROF = ([(CUP_R * math.sin(t), -CUP_R * math.cos(t)) for t in [i * math.pi / 16 for i in range(9)]] +
            [((CUP_R - .012) * math.sin(t), -(CUP_R - .012) * math.cos(t)) for t in [i * math.pi / 16 for i in range(8, -1, -1)]])


def c04_cup_anemometer():
    rng = random.Random(404); meadow(rng, clear=.32)
    s0 = HILL(0, 0)
    lathe('ColumnFoot', [(.27, s0 - .03), (.27, s0 + .05), (.22, s0 + .1), (0, s0 + .1)], 'Pearl', seg=40)
    torus('FootBand', (0, 0, s0 + .03), .272, .012, 'Azure', seg=40, minor=6)
    lathe('Column', [(.2, s0 + .1), (.2, 1.32)], 'OrbShell', seg=40, subsurf=0)
    lathe('ColumnCap', [(0, 1.42), (.16, 1.41), (.23, 1.36), (.23, 1.31), (0, 1.31)], 'Pearl', seg=40)
    torus('CapBand', (0, 0, 1.335), .232, .012, 'Azure', seg=40, minor=6)
    cloud((0, 0, s0 + .1), .2, name='CloudMass'); tuft((0, 0, s0 + .45), .9, (0, 0, .5)); tuft((.03, -.02, s0 + .66), .7, (0, 0, 2))
    hz = 1.8
    rod('Mast', (0, 0, 1.4), (0, 0, hz), .028, 'Chrome'); torus('MastCollar', (0, 0, 1.45), .05, .018, 'Azure', seg=24, minor=6)
    sphere('Hub', (0, 0, hz), .075, 'Pearl', scale=(1, 1, .8), seg=24, rings=12)
    sphere('HubCap', (0, 0, hz + .055), .045, 'Azure', seg=16, rings=8)
    for k, (deg, fill) in enumerate(((215, .38), (335, .58), (95, 0))):   # scooping now, already full, empty next
        a = math.radians(deg); end = Vector((math.cos(a) * .55, math.sin(a) * .55, hz))
        rod('Arm_%d' % k, (0, 0, hz), tuple(end), .016, 'Chrome')
        tang = Vector((-math.sin(a), math.cos(a), 0)); q = quat_to(-tang)
        orient(lathe('Cup_%d' % k, CUP_PROF, 'ClearGlass', loc=tuple(end), seg=32, subsurf=0), q)
        orient(torus('CupRim_%d' % k, tuple(end), CUP_R - .006, .012, 'Azure', seg=32, minor=6), q)
        if fill: tuft(tuple(end + tang * .08), fill, (0, 0, a))
    wind_ribbon('SilkIn', spiral((0, 0), .48, .14, hz - .06, 1.46, 1.6, 18, phase=math.radians(215)), .022, 'SilkRibbon')
    vz = 2.08
    rod('VaneMast', (0, 0, hz + .08), (0, 0, vz), .014, 'Chrome')
    rod('VaneArm', (-.42, 0, vz), (.36, 0, vz), .014, 'Pearl')
    lathe('VaneHead', [(0, 0), (.05, .02), (0, .14)], 'Azure', loc=(-.42, 0, vz), rot=(0, -math.pi / 2, 0), seg=16)
    g = Geo(); c = Vector((.36, 0, vz))                          # the vane's tail is a parachute seed, downwind
    g.strand([c + Vector((-.1, 0, 0)), c + Vector((-.05, 0, 0)), c], (.006, .014, .006), 'AcheneAqua', sides=4)
    for j in range(15):
        ang = math.radians(-55 + j * 110 / 14); d = Vector((math.cos(ang), 0, math.sin(ang)))
        m = c + d * .12; e = m + (d * .8 + Vector((1, 0, 0)) * .2).normalized() * .11
        g.prism(c, m, .004, .003, 'Pappus'); g.prism(m, e, .003, .001, 'Pappus')
    g.done('VaneTail')
    seed_path('SeedsIn', [(-.9, -1.55, .55), (-1.1, -1.0, 1.4), (-.48, -.36, hz)], rng)
    return 2.3


# ---------------------------------------------------------------- 05 Silk Spinning Wheel
def c05_spinning_wheel():
    rng = random.Random(505)
    meadow(rng, clear=.25, rosettes=[(-.82, -.48, .4), (.8, -.5, 2.2), (.2, .9, 4.0)])
    tilt = math.radians(-9); bc = Vector((0, .1, .66))
    rbox('Bench', tuple(bc), (1.4, .3, .07), 'Pearl', .025, rot=(0, tilt, 0))
    bz = lambda x: bc.z + .035 + (x - bc.x) * math.tan(-tilt)
    for x, y in ((-.58, -.02), (-.58, .22), (.58, .1)):
        rod('Leg', (x * 1.08, y + (y - .1) * .4, HILL(x, y) - .02), (x, y, bz(x) - .07), .022, 'Pearl')
    W = Vector((.36, .1, 1.33))
    for y in (-.03, .23): rod('Maiden', (W.x, y, bz(W.x)), (W.x, y, W.z + .04), .02, 'Pearl')
    rod('Axle', (W.x, -.06, W.z), (W.x, .26, W.z), .015, 'Chrome')
    torus('WheelRim', tuple(W), .5, .022, 'Azure', rot=(math.pi / 2, 0, 0), seg=72, minor=8)
    torus('WheelRing', tuple(W), .47, .01, 'Pearl', rot=(math.pi / 2, 0, 0), seg=72, minor=6)
    cyl('WheelHub', tuple(W), .06, .1, 'Pearl', rot=(math.pi / 2, 0, 0))
    g = Geo()                                                   # spokes are small frosted sails, so the breeze turns it
    for k in range(8):
        a = k * TAU / 8 + .2; rh = Vector((math.sin(a), 0, math.cos(a))); th = Vector((math.cos(a), 0, -math.sin(a)))
        cd = th * math.cos(math.radians(28)) + Vector((0, 1, 0)) * math.sin(math.radians(28))
        sheet(g, lambda u, v, rh=rh, cd=cd: W + rh * (.08 + u * .39) + cd * (v - .5) * .17 * math.sin(math.pi * (.12 + u * .86)) ** .6,
              8, 3, 'Frost')
        g.prism(W + rh * .06, W + rh * .48, .006, .006, 'Chrome', sides=6)
    g.done('SailSpokes')
    F = Vector((-.42, .1, .9))
    for x in (-.64, -.2): rod('FlyerMaiden', (x, .1, bz(x)), (x, .1, F.z + .06), .022, 'Pearl')
    rod('Spindle', (-.66, .1, F.z), (-.2, .1, F.z), .011, 'Chrome')
    for y in (.015, .185): rod('FlyerArm', (-.58, y, F.z), (-.3, y, F.z), .009, 'Chrome')
    rod('FlyerBridge', (-.3, .015, F.z), (-.3, .185, F.z), .009, 'Chrome')
    cyl('Whorl', (-.25, .1, F.z), .05, .03, 'Azure', rot=(0, math.pi / 2, 0))
    cyl('Bobbin', (-.44, .1, F.z), .03, .24, 'SkyGlass', rot=(0, math.pi / 2, 0))
    for x in (-.56, -.32): cyl('BobbinFlange', (x, .1, F.z), .075, .012, 'Pearl', rot=(0, math.pi / 2, 0))
    sphere('Cop', (-.44, .1, F.z), .068, 'Cloud', scale=(1.55, 1, 1), seg=24, rings=12)
    for x in (-.52, -.47, -.42, -.37): torus('CopThread', (x, .1, F.z), .066, .004, 'Silk', rot=(0, math.pi / 2, 0), seg=32, minor=4)
    lathe('Orifice', [(.03, 0), (.02, .04), (.008, .07)], 'Azure', loc=(-.66, .1, F.z), rot=(0, -math.pi / 2, 0), seg=16)
    ribbon('DriveBand_0', [(W.x - .02, .1, W.z + .5), (-.25, .1, F.z + .05)], .004, 'Azure')
    ribbon('DriveBand_1', [(W.x - .02, .1, W.z - .5), (-.25, .1, F.z - .05)], .004, 'Azure')
    D = Vector((-.72, -.05, 1.5))
    rod('Distaff', (D.x, D.y, bz(D.x)), (D.x, D.y, 1.74), .016, 'Pearl')
    lathe('DistaffCage', [(0, 1.8), (.12, 1.66), (.17, 1.5), (.12, 1.36), (.03, 1.3)], 'Dome', loc=(D.x, D.y, 0), seg=32, subsurf=0)
    for z, s in ((1.42, .62), (1.53, .75), (1.64, .55)): tuft((D.x, D.y, z), s, (0, 0, z * 4))
    ribbon('Thread', [(D.x + .02, D.y, 1.33), (-.75, .0, 1.12), (-.7, .08, .96), (-.67, .1, .91)], .004, 'Floss')
    seed_path('SeedsIn', [(-1.0, -1.6, .6), (-1.25, -.95, 1.5), (-.8, -.2, 1.64)], rng)
    return 1.95


# ---------------------------------------------------------------- 06 Floss Spinner
def c06_floss_spinner():
    rng = random.Random(606); meadow(rng, clear=.38)
    s0 = HILL(0, 0)
    lathe('Pedestal', [(.34, s0 - .04), (.34, s0 + .04), (.26, s0 + .1), (.13, s0 + .2), (.1, .62), (.15, .7), (0, .7)], 'Pearl', seg=40)
    torus('PedestalBand', (0, 0, s0 + .05), .342, .014, 'Azure', seg=40, minor=6)
    lathe('Bowl', [(0, .68), (.42, .7), (.66, .82), (.77, .99), (.79, 1.03), (.76, 1.04), (.66, .9), (.42, .78), (0, .76)], 'Pearl', seg=64)
    lathe('Glaze', [(0, .765), (.42, .785), (.655, .905), (.752, 1.03)], 'Glaze', seg=64)
    torus('BowlRim', (0, 0, 1.035), .78, .018, 'Azure', seg=64, minor=8)
    lathe('Shield', [(.8, 1.04), (.82, 1.16), (.8, 1.28)], 'Dome', seg=64, subsurf=0)
    lathe('Head', [(0, .78), (.09, .79), (.13, .84), (.13, .9), (.1, .94), (0, .96)], 'Candy', seg=32)
    torus('HeadRing', (0, 0, .87), .133, .01, 'Chrome', seg=32, minor=6)
    g = Geo()                                                   # the silk flung out as threads, webbing outward in a spiral
    for s in range(42):
        a0 = s * TAU / 42 + rng.uniform(-.05, .05)
        pts = [Vector((math.cos(a0 + 1.5 * t) * (.14 + .52 * t), math.sin(a0 + 1.5 * t) * (.14 + .52 * t), .9 + .06 * t + .03 * math.sin(t * 9 + s)))
               for t in (i / 10 for i in range(11))]
        g.strand(pts, [.0055] * 11, 'Floss')
    g.done('FlossWeb')
    for k in range(6):                                          # floss gathering against the far wall
        a = k * TAU / 6 + 1.2; tuft((math.cos(a) * .62, math.sin(a) * .62, .96), .36, (0, 0, a))
    P = Vector((.95, .4, 1.72)); W = Vector((.12, .08, 1.72))
    rod('Post', (P.x, P.y, HILL(P.x, P.y) - .02), tuple(P), .03, 'Pearl')
    sphere('Joint', tuple(P), .05, 'Azure', seg=16, rings=8)
    rod('Arm', tuple(P), tuple(W), .022, 'Pearl'); sphere('WandJoint', tuple(W), .04, 'Azure', seg=16, rings=8)
    rod('Wand', tuple(W), (W.x, W.y, 1.0), .014, 'SkyGlass')
    for c, s, rz in (((W.x, W.y, 1.22), .78, 0), ((W.x + .04, W.y - .03, 1.34), .6, 1.4), ((W.x - .03, W.y + .03, 1.11), .55, 2.6)):
        tuft(c, s, (0, 0, rz))
    seed_path('SeedsIn', [(-.95, -1.55, .55), (-.8, -.75, 1.65), (-.06, -.04, 1.02)], rng)
    return 1.85


# ---------------------------------------------------------------- 07 Silk Sail Mill
def c07_sail_mill():
    rng = random.Random(707); s0 = HILL(0, 0)
    lights = [(x, -.5, s0 + .1) for x in (-.14, 0, .14)]
    meadow(rng, clear=.5, lights=lights)
    oct8 = (0, 0, math.radians(22.5))                            # a face of the octagon looks at the front
    lathe('Plinth', [(.53, s0 - .06), (.53, s0 + .06), (.49, s0 + .08), (0, s0 + .08)], 'WhitePebble', seg=8, subsurf=0, rot=oct8)
    z0, z1, r0, r1 = s0 + .08, 1.62, .45, .3
    rz = lambda z: r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    lathe('Smock', [(r0, z0), (r1, z1), (r1 + .03, z1 + .02), (r1 + .03, 1.68), (0, 1.68)], 'Pearl', seg=8, subsurf=0, rot=oct8)
    for z in (z0 + .1, 1.0): torus('Trim', (0, 0, z), rz(z) + .006, .016, 'Azure', seg=8, minor=6, rot=oct8)
    cosf = math.cos(math.radians(22.5)); lean = math.atan((r0 - r1) / (z1 - z0))
    rbox('Door', (0, -rz(.56) * cosf - .01, .56), (.17, .03, .28), 'Azure', .02, rot=(-lean, 0, 0))
    for z in (1.12, 1.38): rbox('Window', (0, -rz(z) * cosf - .01, z), (.08, .02, .1), 'SkyGlass', .015, rot=(-lean, 0, 0))
    lathe('Cap', [(.34, 1.66), (.34, 1.72), (.27, 1.86), (.15, 1.95), (0, 1.98)], 'Azure', scale=(1, 1.22, 1), seg=32)
    H = Vector((0, -.5, 1.86))
    rod('Windshaft', (0, -.15, 1.84), tuple(H), .035, 'Pearl'); sphere('Poll', tuple(H), .08, 'Azure', seg=20, rings=10)
    g = Geo(); cloth = (1., 1., .6, 0.)                          # sails dressed one by one: full, full, half, bare
    L0, L1, SW = .2, .98, .24
    for k in range(4):
        a = math.radians(25) + k * math.pi / 2
        rh = Vector((math.sin(a), 0, math.cos(a))); th = Vector((math.cos(a), 0, -math.sin(a)))
        g.prism(H + rh * .05, H + rh * L1, .02, .014, 'Pearl', sides=8)
        for o in (.11, SW): g.prism(H + rh * L0 + th * o, H + rh * L1 + th * o, .008, .008, 'Frost', sides=4)
        for i in range(10):
            r = L0 + (L1 - L0) * i / 9; g.prism(H + rh * r, H + rh * r + th * SW, .007, .007, 'Frost', sides=4)
        if cloth[k]:
            reach = L0 + (L1 - L0) * cloth[k]
            sheet(g, lambda u, v, rh=rh, th=th, reach=reach: H + rh * (L0 + .01 + u * (reach - L0 - .02)) + th * (.02 + v * (SW - .04))
                  + Vector((0, -.035 * math.sin(math.pi * v), 0)), 8, 4, 'SilkCloth')
    g.done('Sails')
    for sx in (-1, 1): rod('FanFrame', (sx * .1, .3, 1.72), (0, .68, 1.84), .012, 'Pearl')
    F = Vector((0, .7, 1.86)); sphere('FanHub', tuple(F), .035, 'Azure', seg=16, rings=8)
    g = Geo()                                                   # the fantail keeps the cap turned to the wind
    for j in range(6):
        b = j * TAU / 6; rb = Vector((0, math.sin(b), math.cos(b))); tb = Vector((0, math.cos(b), -math.sin(b)))
        cd = Vector((1, 0, 0)) * math.cos(math.radians(30)) + tb * math.sin(math.radians(30))
        sheet(g, lambda u, v, rb=rb, cd=cd: F + rb * (.03 + u * .14) + cd * (v - .5) * .07, 4, 2, 'Frost')
    g.done('Fantail')
    c0, c1 = Vector((0, -.42, .48)), Vector((0, -1.12, .44))
    q = quat_to(c1 - c0, (0, 1, 0))
    orient(rbox('Chute', tuple((c0 + c1) / 2), (.2, (c1 - c0).length, .025), 'Pearl', .01), q)
    for sx in (-1, 1): rod('ChuteRail', (sx * .1, c0.y, c0.z + .03), (sx * .1, c1.y, c1.z + .03), .012, 'Azure')
    for t, s in ((.2, .5), (.55, .55), (.88, .45)): tuft(tuple(c0.lerp(c1, t) + Vector((0, 0, .06))), s, (0, 0, t * 5))
    seed_path('SeedsIn', [(-1.0, -1.6, .6), (-1.1, -1.0, 1.55), (-.32, -.62, 1.36)], rng)
    return 2.9


# ---------------------------------------------------------------- 08 Clock Rebuilder
def c08_clock_rebuilder():
    rng = random.Random(808); meadow(rng, clear=.36)
    s0 = HILL(0, 0)
    lathe('Foot', [(.3, s0 - .04), (.3, s0 + .04), (.2, s0 + .1), (.1, s0 + .14), (0, s0 + .14)], 'Pearl', seg=40)
    torus('FootBand', (0, 0, s0 + .04), .302, .014, 'Azure', seg=40, minor=6)
    lathe('Stalk', [(.075, s0 + .12), (.06, 1.0), (.048, 1.55), (0, 1.6)], 'Pearl', seg=24)
    for z, r in ((.75, .068), (1.25, .056)): torus('StalkBand', (0, 0, z), r, .012, 'Azure', seg=24, minor=6)
    torus('Collar', (0, 0, 1.62), .07, .03, 'Azure', seg=24, minor=8)
    lathe('Cradle', [(.04, 1.6), (.12, 1.68), (.15, 1.73)], 'Pearl', seg=32)
    C = Vector((0, 0, 1.88))
    sphere('Receptacle', tuple(C), .17, 'OrbShell', seg=40, rings=20)
    sphere('Core', tuple(C), .06, 'SkyGlow', seg=20, rings=10)
    tuft(tuple(C + Vector((0, 0, -.02))), .5)
    st = {**BASE_ST, 'n': 84, 'k': 12, 'no_rec': True, 'rec_r': .17, 'bw': .004, 'beak_w': .0035, 'ach_w': .008, 'pl': .42}
    open_dirs = []

    def docked(d, r):                                           # the far half is rebuilt; the near half still waits
        if d.dot(BLOWN) < -.05: return True
        open_dirs.append(d.copy()); return False
    g = Geo(); clock(g, C, .6, st, rng, keep=docked); g.done('RebuiltClock')
    g = Geo()
    for d in open_dirs: g.ball(C + d * .168, .014, 'Azure', seg=8, rings=6)
    g.done('Sockets')
    IN = Vector((-.75, -.55, .25)).normalized(); g = Geo()
    for k, d in enumerate(sorted(open_dirs, key=lambda d: -d.dot(IN))[:10]):   # docking next, in spiral order
        chute(g, C + d * .42 + IN * (.1 + .09 * k) + Vector((0, 0, .02 * k)), d, rng, .26)
    g.done('Docking')
    seed_path('SeedsIn', [(-.95, -1.55, .55), (-1.25, -.95, 1.6), (-.85, -.62, 2.0)], rng)
    return 2.6


# ---------------------------------------------------------------- 09 Silk Top
TOP_PROF = [(0, 0), (.04, .05), (.15, .16), (.36, .34), (.46, .47), (.44, .56), (.34, .68), (.2, .78), (.09, .83), (0, .84)]


def c09_silk_top():
    rng = random.Random(909); meadow(rng, clear=.62)
    s0 = HILL(0, 0)
    lathe('Dish', [(0, s0 - .02), (.4, s0 - .01), (.66, s0 + .04), (.74, s0 + .1), (.71, s0 + .12), (.62, s0 + .07), (.36, s0 + .03), (0, s0 + .02)],
          'Pearl', seg=56)
    torus('DishRim', (0, 0, s0 + .11), .725, .016, 'Azure', seg=56, minor=6)
    S = 1.2
    pv = empty('Top', (0, .02, s0 + .025)); pv.rotation_euler = (math.radians(6), math.radians(-5), 0)
    lathe('TopBody', [(r * S, z * S) for r, z in TOP_PROF], 'OrbShell', seg=48, parent=pv)
    sphere('TopTip', (0, 0, .03), .04, 'Azure', parent=pv)
    torus('Shoulder', (0, 0, .47 * S), .462 * S, .02, 'Azure', seg=56, minor=10, parent=pv)
    cyl('Stem', (0, 0, .84 * S + .07), .05, .16, 'Azure', parent=pv)
    sphere('Knob', (0, 0, .84 * S + .17), .065, 'Pearl', seg=20, rings=10, parent=pv)
    use_mesh('CloudCore', SKY['Cloud'], (0, 0, .2 * S), (0, 0, .4), .3, parent=pv)
    use_mesh('CloudTuft', SKY['Tuft'], (0, 0, .5 * S), (0, 0, 1), .9, parent=pv)

    def body_r(z):
        for (r_a, z_a), (r_b, z_b) in zip(TOP_PROF[:-1], TOP_PROF[1:]):
            if z_a <= z <= z_b: return (r_a + (r_b - r_a) * (z - z_a) / max(z_b - z_a, 1e-6)) * S
        return 0
    g = Geo()
    for j in range(6):                                          # small crown vanes catch the breeze
        b = j * TAU / 6; out = Vector((math.cos(b), math.sin(b), 0)); tng = Vector((-math.sin(b), math.cos(b), 0))
        base = Vector((0, 0, .8 * S)) + out * .06; rd = (out * .85 + Vector((0, 0, .5))).normalized()
        cd = (tng * .8 + Vector((0, 0, .6))).normalized()
        sheet(g, lambda u, v, base=base, rd=rd, cd=cd: base + rd * (u * .22) + cd * (v - .5) * .09 * math.sin(math.pi * (.15 + u * .83)) ** .5,
              6, 2, 'VaneCyan')
    g.done('CrownVanes', parent=pv)
    g = Geo(); pts = []                                         # the silk band wound on the shoulder, one turn per puff
    for i in range(161):
        t = i / 160; z = .36 + .16 * t; th = t * 4.5 * TAU
        rr = body_r(z) + .012; pts.append(Vector((math.cos(th) * rr, math.sin(th) * rr, z * S)))
    g.strand(pts, [.007] * len(pts), 'Silk')
    g.done('SilkBand', parent=pv)
    seed_path('SeedsIn', [(-.95, -1.55, .55), (-1.05, -.6, .95), (-.55, .4, 1.0), (.25, .66, .92)], rng, puffs=(.3, .72))
    for k, (a0, z) in enumerate(((200, .72), (300, .95), (40, .62))):
        pts = [(math.cos(math.radians(a0 + i * 22)) * .8, math.sin(math.radians(a0 + i * 22)) * .8, z) for i in range(7)]
        wind_ribbon('SpinLine_%d' % k, pts, .03, 'Wind')
    return 1.45


# ---------------------------------------------------------------- 10 Kite Winch
def c10_kite_winch():
    rng = random.Random(1010); meadow(rng, clear=.35)
    gz = HILL(.12, .12); D = Vector((.12, .12, gz + .38))
    for sx in (-1, 1): rbox('StandPlate', (D.x + sx * .27, D.y, (gz + D.z + .05) / 2), (.04, .34, D.z - gz + .2), 'Pearl', .015)
    cyl('Drum', tuple(D), .17, .46, 'Pearl', rot=(0, math.pi / 2, 0))
    for sx in (-1, 1): cyl('Flange', (D.x + sx * .23, D.y, D.z), .27, .025, 'Azure', rot=(0, math.pi / 2, 0))
    for i in range(7): torus('LineCoil', (D.x - .18 + i * .06, D.y, D.z), .182, .011, 'Silk', rot=(0, math.pi / 2, 0), seg=40, minor=6)
    sphere('Crank', (D.x + .31, D.y, D.z), .05, 'Azure', seg=16, rings=8)
    P = Vector((-.22, -.12, 1.38))
    rod('PulleyPost', (P.x, P.y, HILL(P.x, P.y) - .02), (P.x, P.y, P.z - .08), .03, 'Pearl')
    torus('Pulley', tuple(P), .07, .02, 'Azure', rot=(math.pi / 2, 0, math.radians(35)), seg=32, minor=8)
    Kc = Vector((.85, .55, 2.72)); q = quat_to(Vector((-.7, -.65, .3)), (0, -1, 0))
    N, Lt, Rt, T = Vector((0, 0, .52)), Vector((-.62, 0, -.1)), Vector((.62, 0, -.1)), Vector((0, 0, -.16))
    world = lambda p: Kc + q @ p
    K = world(Vector((0, -.14, .06)))
    ribbon('TetherLow', [tuple(D + Vector((0, 0, .18))), tuple(P + Vector((0, 0, -.06)))], .006, 'Silk')
    mid = P.lerp(K, .5) + Vector((0, 0, -.12))
    ribbon('Tether', [tuple(P + Vector((.03, .02, .06))), tuple(mid), tuple(K)], .006, 'Silk')
    pv = orient(empty('Kite', tuple(Kc)), q); g = Geo()
    for E in (Lt, Rt):                                          # a delta kite of silk, billowing toward the wind
        sheet(g, lambda u, v, E=E: N.lerp(T, u).lerp(N.lerp(E, u), v) + Vector((0, -.06 * math.sin(math.pi * v) * u ** .5, 0)),
              8, 5, 'KiteSilk')
    for a, b in ((N, T), (N, Lt), (N, Rt), (N.lerp(Lt, .55) + Vector((0, .03, 0)), N.lerp(Rt, .55) + Vector((0, .03, 0)))):
        g.prism(a, b, .014, .011, 'Azure', sides=6)
    g.done('KiteSail', parent=pv)
    for c, s in (((-.22, -.12, .06), 1.0), ((.18, -.12, .02), 1.1), ((0, -.15, .24), .9)):   # the cloud it gathers
        use_mesh('KiteCloud', SKY['Tuft'], c, (math.pi / 2, 0, c[0] * 6), s, parent=pv)
    Tw = world(T)
    tail = [Tw, Tw + Vector((.15, .05, -.3)), Tw + Vector((.35, .1, -.55)), Tw + Vector((.6, .12, -.7))]
    ribbon('Tail', [tuple(p) for p in tail], .004, 'Silk')
    g = Geo()
    for i in range(1, 6): chute(g, bez(tail, i / 6) + Vector((0, 0, .02)), (.4, 0, 1), rng, .12)
    for i in range(7): chute(g, bez([P, mid, K], .15 + .1 * i) + Vector((0, 0, .05)), wind_up(rng), rng, .16)
    g.done('ClimbingSeeds')
    seed_path('SeedsIn', [(-.95, -1.55, .55), (-.85, -.9, 1.0), (P.x - .05, P.y - .08, P.z - .12)], rng)
    return 3.3


CONCEPTS = [('01', 'Silk Turbine', c01_silk_turbine), ('02', 'Pinwheel Trio', c02_pinwheel_trio),
            ('03', 'Pappus Rotor', c03_pappus_rotor), ('04', 'Cup Anemometer', c04_cup_anemometer),
            ('05', 'Silk Spinning Wheel', c05_spinning_wheel), ('06', 'Floss Spinner', c06_floss_spinner),
            ('07', 'Silk Sail Mill', c07_sail_mill), ('08', 'Clock Rebuilder', c08_clock_rebuilder),
            ('09', 'Silk Top', c09_silk_top), ('10', 'Kite Winch', c10_kite_winch)]
ONLY = globals().get('ONLY')
layout([c for c in CONCEPTS if not ONLY or c[0] in ONLY], 'SilkProcessorConcepts', 'SilkProcessor', spacing=3.4)
