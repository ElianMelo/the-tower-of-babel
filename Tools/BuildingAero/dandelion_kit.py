"""Shared Blender kit for the dandelion clocks: the Round02 designs (build_sky_dandelion_designs.py) and the
production Silk Clock node (build_sky_dandelions.py). exec() it after sky_concept_kit.py, which provides make(), MAT,
the material helpers and TAU.

Geo collects many small parts in one bmesh. Besides materials and UVs it can write, per corner:
  a colour (Geo(colors={material key: rgba}))           - albedo and emission weight for vertex-coloured shaders
  per-seed data (Geo(seed_data=True), g.seed = (pivot, rnd)) - the UV maps SeedPivot (x, y) and SeedInfo (z, rnd)
  atlas UVs (g.region = callable(u, v) -> (u, v))        - maps strand and leaf UVs into a painted texture atlas
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler

GOLD = math.pi * (3 - math.sqrt(5))
_az, _el = math.radians(-34), math.radians(16)          # the review close-up camera (render script)
CAM = Vector((math.sin(_az) * math.cos(_el), -math.cos(_az) * math.cos(_el), math.sin(_el)))
FACE_CAM = Vector((0, 0, 1)).rotation_difference(CAM).to_matrix()
RIGHT = Vector((0, 0, 1)).cross(CAM).normalized()               # screen right; seeds blow this way
BLOWN = (CAM * .8 + RIGHT * .6).normalized()                     # half-blown head: bare toward camera and wind
# Production stalks (build_sky_workshop.py): name, base, height, lean; head radius is new (pappus outer ~0.94 R).
STALKS = [('Bloom', (0, 0, .02), 1.0, (.05, .05), .2), ('Stalk_1', (.12, -.05, .02), .8, (.3, -.12), .175),
          ('Stalk_2', (-.1, .05, .02), .64, (-.28, .1), .165)]
# Drifting seeds: the production Seed_0..4 spots, moved clear of the larger heads.
SEEDS = [(.66, -.3, 1.1), (.74, .12, .86), (-.6, -.28, .9), (.24, .5, 1.02), (-.36, .42, 1.16)]


# ---------------------------------------------------------------- materials
def tip_mat(name, hue_stops, es_root=.4, es_tip=.9, power=1., base_scale=.3, **kw):
    """White at the bristle root (UV x = 0) fading to a per-seed hue (UV y) at the tip. The colour is carried mostly
    by emission over a dimmed base colour, so lit tips keep their hue instead of clipping to white."""
    m, n, l, b = _mat(name); _set(b, **kw)
    tc = n.new('ShaderNodeTexCoord'); sep = n.new('ShaderNodeSeparateXYZ'); l.new(tc.outputs['UV'], sep.inputs[0])
    hue = ramp(n, hue_stops); l.new(sep.outputs['Y'], hue.inputs[0])
    pw = n.new('ShaderNodeMath'); pw.operation = 'POWER'; pw.inputs[1].default_value = power; l.new(sep.outputs['X'], pw.inputs[0])
    sub = n.new('ShaderNodeVectorMath'); sub.operation = 'SUBTRACT'; sub.inputs[1].default_value = (1, 1, 1)
    l.new(hue.outputs[0], sub.inputs[0])
    fma = n.new('ShaderNodeVectorMath'); fma.operation = 'MULTIPLY_ADD'; fma.inputs[2].default_value = (1, 1, 1)
    l.new(sub.outputs[0], fma.inputs[0]); l.new(pw.outputs[0], fma.inputs[1])
    dim = n.new('ShaderNodeVectorMath'); dim.operation = 'SCALE'; dim.inputs['Scale'].default_value = base_scale
    l.new(fma.outputs[0], dim.inputs[0]); l.new(dim.outputs[0], b.inputs['Base Color'])
    l.new(fma.outputs[0], b.inputs['Emission Color'])
    es = n.new('ShaderNodeMath'); es.operation = 'MULTIPLY_ADD'; es.inputs[1].default_value = es_tip - es_root
    es.inputs[2].default_value = es_root; l.new(pw.outputs[0], es.inputs[0]); l.new(es.outputs[0], b.inputs['Emission Strength'])
    return m


def glow(name, color, es=4.):
    """Pure light: black base so the colour stays saturated under the review sun."""
    return plain(name, (0, 0, 0), emit=(*color, 1), es=es, rough=.5)


def leaf_mat(name, stops, es=.1, coat_rough=.04):
    """Glossy dandelion leaf: pale petiole to deep green to lime tip along UV x, darker chevron veins."""
    m, n, l, b = _mat(name); _set(b, rough=.28, coat=1, coat_rough=coat_rough, sss=.04)
    col = ramp(n, stops)
    tc = n.new('ShaderNodeTexCoord'); sep = n.new('ShaderNodeSeparateXYZ'); l.new(tc.outputs['UV'], sep.inputs[0])
    l.new(sep.outputs['X'], col.inputs[0])
    off = n.new('ShaderNodeMath'); off.operation = 'SUBTRACT'; off.inputs[1].default_value = .5; l.new(sep.outputs['Y'], off.inputs[0])
    ab = n.new('ShaderNodeMath'); ab.operation = 'ABSOLUTE'; l.new(off.outputs[0], ab.inputs[0])
    ch = n.new('ShaderNodeMath'); ch.operation = 'MULTIPLY_ADD'; ch.inputs[1].default_value = 7
    l.new(ab.outputs[0], ch.inputs[0]); l.new(sep.outputs['X'], ch.inputs[2])
    sc = n.new('ShaderNodeMath'); sc.operation = 'MULTIPLY'; sc.inputs[1].default_value = 22 * 2
    l.new(ch.outputs[0], sc.inputs[0])
    si = n.new('ShaderNodeMath'); si.operation = 'SINE'; l.new(sc.outputs[0], si.inputs[0])
    vein = ramp(n, [(0, (1, 1, 1)), (.82, (1, 1, 1)), (1, (.72, .86, .7))]); l.new(si.outputs[0], vein.inputs[0])
    mul = n.new('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY'
    l.new(col.outputs[0], mul.inputs[0]); l.new(vein.outputs[0], mul.inputs[1])
    l.new(mul.outputs[0], b.inputs['Base Color']); l.new(col.outputs[0], b.inputs['Emission Color'])
    b.inputs['Emission Strength'].default_value = es
    return m


def bokeh_mat(name, color, es=1.4):
    """Soft camera-facing bokeh disc: faint body, brighter ring, fading edge (UV x is the radius)."""
    m, n, l, b = _mat(name); _set(b, color=(*color, 1), emit=(*color, 1), es=es, rough=.5)
    a = ramp(n, [(0, (.1, .1, .1)), (.7, (.14, .14, .14)), (.87, (.32, .32, .32)), (1, (0, 0, 0))])
    l.new(axis_value(n, l, 'X', 0, 1, 'UV'), a.inputs[0]); l.new(a.outputs[0], b.inputs['Alpha'])
    _render(m, 'BLENDED')
    return m


def pits_mat(name):
    """Bare receptacle of a blown head: pearl dome dotted with azure seed pits."""
    m, n, l, b = _mat(name); _set(b, rough=.2, coat=1, coat_rough=.03)
    tc = n.new('ShaderNodeTexCoord'); vo = n.new('ShaderNodeTexVoronoi'); vo.inputs['Scale'].default_value = 95
    l.new(tc.outputs['Object'], vo.inputs['Vector'])
    r = ramp(n, [(0, (.03, .3, .65)), (.26, (.08, .42, .78)), (.34, (.93, .96, .92)), (1, (.96, .98, .95))])
    l.new(vo.outputs['Distance'], r.inputs[0]); l.new(r.outputs[0], b.inputs['Base Color'])
    return m


MAT.update({
    'Pappus': plain('Concept_Pappus', (1, 1, 1), rough=.4, sss=.2, emit=(.9, .95, 1, 1), es=.45),
    'PappusSoft': plain('Concept_PappusSoft', (1, 1, 1), rough=.3, coat=1, coat_rough=.05, emit=(.92, .96, 1, 1), es=.35),
    'FiberPappus': tip_mat('Concept_FiberPappus', [(0, (0, .72, 1)), (1, (0, .72, 1))], es_root=.35, es_tip=2.4, power=1.6,
                           base_scale=.35, rough=.3),
    'IriPappus': tip_mat('Concept_IriPappus', [(0, (.15, .78, 1)), (.25, (.55, .38, 1)), (.5, (1, .32, .74)),
                                               (.75, (.25, 1, .66)), (1, (.15, .78, 1))], es_root=.45, es_tip=1., power=.8, rough=.3),
    'FrostFiber': plain('Concept_FrostFiber', (.6, .85, 1), rough=.15, coat=1, coat_rough=.02, emit=(.45, .78, 1, 1), es=.75),
    'Crystal': plain('Concept_DandelionCrystal', (.4, .78, 1), 'DITHERED', rough=0, trans=.8, ior=1.5, film=380, film_ior=1.5,
                     coat=1, emit=(.3, .68, 1, 1), es=.7),
    'Dew': plain('Concept_Dew', (.85, .97, 1), 'DITHERED', rough=0, trans=.85, ior=1.33, coat=1, emit=(.55, .88, 1, 1), es=.25),
    'CyanTip': glow('Concept_CyanTip', (0, .75, 1), 5),
    'LimeTip': glow('Concept_LimeTip', (.45, 1, .1), 5),
    'Beak': plain('Concept_Beak', (.9, .97, 1), rough=.3, emit=(.85, .95, 1, 1), es=.3),
    'AquaPebble': plain('Concept_AquaPebble', (.55, .86, .95), rough=.12, coat=1, coat_rough=.02),
    'AcheneAqua': plain('Concept_AcheneAqua', (.04, .45, .8), rough=.18, coat=1, coat_rough=.02),
    'StemGel': gradient('Concept_StemGel', 'Z', 0, 1.1, [(0, (.1, .45, .1)), (.55, (.35, .78, .2)), (1, (.62, .95, .4))],
                        rough=.15, coat=1, coat_rough=.02, sss=.15),
    'Bract': plain('Concept_Bract', (.16, .55, .12), rough=.25, coat=1, coat_rough=.04),
    'DLeaf': leaf_mat('Concept_DandelionLeaf', [(0, (.7, .9, .55)), (.12, (.04, .36, .05)), (.6, (.1, .56, .07)), (1, (.36, .84, .14))], es=.06),
    'GelLeaf': leaf_mat('Concept_GelLeaf', [(0, (.6, 1, .5)), (.15, (.06, .72, .14)), (1, (.55, 1, .28))], es=.22, coat_rough=0),
    'Midrib': plain('Concept_Midrib', (.8, .96, .62), rough=.2, coat=1, coat_rough=.03, emit=(.6, .9, .4, 1), es=.12),
    'Yellow': plain('Concept_DandelionYellow', (1, .76, .04), rough=.3, coat=.8, coat_rough=.05, sss=.3, emit=(1, .65, 0, 1), es=.25),
    'FloretCentre': plain('Concept_FloretCentre', (1, .55, .02), rough=.3, coat=.8, emit=(1, .5, 0, 1), es=.2),
    'Halo': fresnel_shell('Concept_HeadHalo', (1, 1, 1, 1), (.6, .9, 1, 1), .32, 0, glow=.5, rough=.3),
    'Glint': plain('Concept_Glint', (1, 1, 1), emit=(1, 1, 1, 1), es=10),
    'BokehCyan': bokeh_mat('Concept_BokehCyan', (.3, .85, 1)),
    'BokehLime': bokeh_mat('Concept_BokehLime', (.55, 1, .3)),
    'BokehWhite': bokeh_mat('Concept_BokehWhite', (1, 1, 1), .9),
    'LimeJelly': plain('Concept_LimeJelly', (.4, .95, .25), 'DITHERED', rough=.04, trans=.6, ior=1.4, coat=1, coat_rough=0,
                       emit=(.3, .8, .15, 1), es=.15),
    'Pits': pits_mat('Concept_BlownReceptacle'),
})


# ---------------------------------------------------------------- one bmesh, many parts
def frame(d):
    d = d.normalized(); a = Vector((0, 0, 1)) if abs(d.z) < .9 else Vector((1, 0, 0))
    u = d.cross(a).normalized()
    return u, d.cross(u)


class Geo:
    """Accumulates thin prisms, balls, ribbons and billboards in one bmesh with per-face materials.
    UV x runs 0 -> 1 from root to tip along strands (bristle gradients); UV y carries a per-seed hue."""
    def __init__(self, colors=None, seed_data=False):
        self.bm = bmesh.new(); self.uv = self.bm.loops.layers.uv.new('UVMap'); self.mats = []
        self.colors = colors; self.col = self.bm.loops.layers.color.new('Col') if colors else None
        self.pivot_uv = self.bm.loops.layers.uv.new('SeedPivot') if seed_data else None
        self.info_uv = self.bm.loops.layers.uv.new('SeedInfo') if seed_data else None
        self.seed = (Vector(), 0.)          # (pivot, random) written to every corner while seed_data is on
        self.region = None                  # optional (u, v) -> atlas (u, v)

    def mi(self, key):
        if key not in self.mats: self.mats.append(key)
        return self.mats.index(key)

    def _loop(self, loop, mat, uv):
        loop[self.uv].uv = self.region(*uv) if self.region else uv
        if self.col is not None: loop[self.col] = self.colors.get(mat, (1, 1, 1, 0))
        if self.pivot_uv is not None:
            p, rnd = self.seed
            loop[self.pivot_uv].uv = (p.x, p.y); loop[self.info_uv].uv = (p.z, rnd)

    def prism(self, p0, p1, w0, w1, mat, t0=0., t1=1., hue=0., sides=3):
        p0, p1 = Vector(p0), Vector(p1)
        if (p1 - p0).length < 1e-6: return
        u, v = frame(p1 - p0); idx = self.mi(mat)
        ring = [[self.bm.verts.new(p + (u * math.cos(k * TAU / sides) + v * math.sin(k * TAU / sides)) * w) for k in range(sides)]
                for p, w in ((p0, w0), (p1, w1))]
        for k in range(sides):
            f = self.bm.faces.new((ring[0][k], ring[0][(k + 1) % sides], ring[1][(k + 1) % sides], ring[1][k]))
            f.material_index = idx
            for loop, tt in zip(f.loops, (t0, t0, t1, t1)): self._loop(loop, mat, (tt, hue))

    def strand(self, pts, widths, mat, sides=3, hue=0.):
        n = len(pts) - 1
        for i in range(n): self.prism(pts[i], pts[i + 1], widths[i], widths[i + 1], mat, i / n, (i + 1) / n, hue, sides)

    def tube(self, pts, r0, r1, mat, sides=8):
        """Smooth closed tube along pts with a UV seam: UV x along the tube, UV y around it."""
        pts = [Vector(p) for p in pts]; n = len(pts) - 1; idx = self.mi(mat); rows = []; last = None
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, n)] - pts[max(i - 1, 0)]).normalized(); r = r0 + (r1 - r0) * i / n
            if last is None: u, v = frame(d)
            else: u = (last[0].rotation_difference(d) @ last[1]).normalized(); v = d.cross(u)     # parallel transport: no twist
            last = (d, u)
            rows.append([self.bm.verts.new(p + (u * math.cos(k * TAU / sides) + v * math.sin(k * TAU / sides)) * r)
                         for k in range(sides + 1)])
        for i in range(n):
            for k in range(sides):
                quad = ((i, k), (i, k + 1), (i + 1, k + 1), (i + 1, k))
                f = self.bm.faces.new([rows[a][b] for a, b in quad]); f.material_index = idx
                for loop, (a, b) in zip(f.loops, quad): self._loop(loop, mat, (a / n, b / sides))
        bmesh.ops.remove_doubles(self.bm, verts=[r[0] for r in rows] + [r[-1] for r in rows], dist=1e-6)  # weld; UVs keep the seam

    def flat(self, pts, w0, w1, mat, up):
        """Flat strap along pts (a floret petal), facing up."""
        pts = [Vector(p) for p in pts]; n = len(pts) - 1; idx = self.mi(mat); rows = []
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, n)] - pts[max(i - 1, 0)]).normalized(); s = d.cross(up).normalized()
            w = w0 + (w1 - w0) * i / n
            rows.append((self.bm.verts.new(p - s * w), self.bm.verts.new(p + s * w)))
        for i in range(n):
            f = self.bm.faces.new((rows[i][0], rows[i + 1][0], rows[i + 1][1], rows[i][1])); f.material_index = idx
            for loop, uv in zip(f.loops, ((i / n, 0), ((i + 1) / n, 0), ((i + 1) / n, 1), (i / n, 1))): self._loop(loop, mat, uv)

    def _tag(self, verts, mat, hue=0., centre=None, r=1.):
        """Material (and colour/seed data) for an operator-made part. With centre, UVs wrap it: x up it, y around it."""
        idx = self.mi(mat)
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = idx
            for loop in f.loops:
                if centre is None: self._loop(loop, mat, (.5, hue)); continue
                d = loop.vert.co - centre
                self._loop(loop, mat, (min(1, max(0, d.z / r * .5 + .5)), math.atan2(d.y, d.x) / TAU + .5))

    def ball(self, c, r, mat, scale=(1, 1, 1), rot=(0, 0, 0), seg=12, rings=8):
        M = Matrix.Translation(Vector(c)) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
        self._tag(bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=r, matrix=M)['verts'], mat,
                  centre=Vector(c) if self.region else None, r=r * max(scale))

    def ico(self, c, r, mat, scale=(1, 1, 1), rot=(0, 0, 0), sub=0):
        M = Matrix.Translation(Vector(c)) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
        self._tag(bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=r, matrix=M)['verts'], mat)

    def octa(self, c, r, mat):
        c = Vector(c); idx = self.mi(mat)
        vs = [self.bm.verts.new(c + Vector(a) * r) for a in ((1, 0, 0), (0, 1, 0), (-1, 0, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
        for k in range(4):
            for pole, order in ((vs[4], 1), (vs[5], -1)):
                tri = (vs[k], vs[(k + 1) % 4], pole) if order > 0 else (vs[(k + 1) % 4], vs[k], pole)
                self.bm.faces.new(tri).material_index = idx

    def _fan(self, c, pts, mat):
        """Camera-facing fan (centre UV x = 0, rim UV x = 1) for stars and bokeh discs."""
        c = Vector(c); idx = self.mi(mat)
        mid = self.bm.verts.new(c); ring = [self.bm.verts.new(c + FACE_CAM @ p) for p in pts]
        for k in range(len(ring)):
            f = self.bm.faces.new((mid, ring[k], ring[(k + 1) % len(ring)])); f.material_index = idx
            for loop, x in zip(f.loops, (0, 1, 1)): loop[self.uv].uv = (x, .5)

    def disc(self, c, r, mat, seg=24):
        self._fan(c, [Vector((math.cos(k * TAU / seg) * r, math.sin(k * TAU / seg) * r, 0)) for k in range(seg)], mat)

    def star(self, c, r, mat='Glint', thin=.11, rot=.3):
        """Four-point glint with a smaller diagonal star behind it."""
        for rr, rt in ((r, rot), (r * .42, rot + math.pi / 4)):
            self._fan(c, [Vector((math.cos(rt + k * TAU / 8) * (rr if k % 2 == 0 else rr * thin),
                                  math.sin(rt + k * TAU / 8) * (rr if k % 2 == 0 else rr * thin), 0)) for k in range(8)], mat)

    def done(self, name, origin=(0, 0, 0), parent=None, loc=(0, 0, 0), smooth=True):
        # Keys that share one material (e.g. seed pappus, achene and beak colours) collapse into one slot.
        unique, remap = [], []
        for key in self.mats:
            m = MAT[key]; firsts = [MAT[k] for k in unique]
            if m not in firsts: unique.append(key)
            remap.append([MAT[k] for k in unique].index(m))
        if len(unique) < len(self.mats):
            for f in self.bm.faces: f.material_index = remap[f.material_index]
            self.mats = unique
        o = Vector(origin)
        bmesh.ops.translate(self.bm, vec=-o, verts=self.bm.verts)
        if self.pivot_uv is not None:                       # seed pivots follow the vertices into object space
            for f in self.bm.faces:
                for loop in f.loops:
                    a, b = loop[self.pivot_uv].uv.copy(), loop[self.info_uv].uv.copy()
                    loop[self.pivot_uv].uv = (a.x - o.x, a.y - o.y); loop[self.info_uv].uv = (b.x - o.z, b.y)
        return make(name, self.bm, self.mats, loc, smooth=smooth, parent=parent)


# ---------------------------------------------------------------- the dandelion parts
def edge(t, teeth, depth, phase, hook, L, lo=.15, hi=.84):
    """Margin of a runcinate leaf: backward-pointing teeth between the petiole and the terminal lobe.
    Returns the width factor and how far the margin is pulled back toward the base (the hook)."""
    if t <= lo or t >= hi: return 1., 0.
    u = (t - lo) / (hi - lo); fade = math.sin(math.pi * u) ** .6
    s = (u * teeth + phase) % 1.
    dip = (s / .76) ** 1.3 if s < .76 else 1 - ((s - .76) / .24) ** .7
    return 1 - depth * fade * dip, hook * (1 - dip) * fade * (hi - lo) / teeth * L


def leaf(g, yaw, L, W, rng, teeth=4, z0=0., mat='DLeaf', rib='Midrib', nt=110, start=.035, lift=1.):
    """Toothed dandelion leaf lying out from the crown, arched and cupped; returns point(t, v) on its surface."""
    ph = (rng.random(), rng.random()); depth = rng.uniform(.42, .56)
    Rz = Matrix.Rotation(yaw, 3, 'Z'); O = Vector((math.cos(yaw) * start, math.sin(yaw) * start, 0))
    fmax = (1.1 / 1.6) ** 1.1 * (.5 / 1.6) ** .5
    rise, droop, twist = rng.uniform(.04, .065) * lift, rng.uniform(0, .03), rng.uniform(-.15, .15)

    def point(t, v):
        env = W * max(t ** 1.1 * (1 - t) ** .5 / fmax, .1 * (1 - t))
        w, sh = edge(t, teeth, depth, ph[0 if v < 0 else 1], .18, L)
        y = v * env * w
        x = t * L - sh * v * v
        z = z0 + .028 + rise * math.sin(math.pi * t ** .8) * (1 - .3 * t) - droop * t * t + .3 * y * y / W + twist * y * t
        return O + Rz @ Vector((x, y, z))

    V = (-1, -.72, -.42, -.14, -.03, .03, .14, .42, .72, 1)
    rows = [[g.bm.verts.new(point(i / nt, v)) for v in V] for i in range(nt + 1)]
    li, ri = g.mi(mat), g.mi(rib)
    for i in range(nt):
        for j in range(len(V) - 1):
            quad = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
            f = g.bm.faces.new([rows[a][b] for a, b in quad]); f.material_index = ri if j == 4 else li
            for loop, (a, b) in zip(f.loops, quad): g._loop(loop, rib if j == 4 else mat, (a / nt, (V[b] + 1) / 2))
    return point


def bud(g):
    """Closed dandelion bud: green involucre, bracts hugging it and curling back, a yellow tip."""
    b0, top = Vector((.27, .25, .03)), Vector((.33, .3, .29))
    g.strand([b0, b0.lerp(top, .5) + Vector((.012, 0, 0)), top], (.008, .007, .006), 'StemGel', sides=6)
    c = top + Vector((0, 0, .05))
    g.ball(c, .033, 'StemGel', scale=(1, 1, 1.75), seg=14, rings=10)
    for j in range(8):
        o = Vector((math.cos(j * TAU / 8 + .2), math.sin(j * TAU / 8 + .2), 0))
        g.strand([c + o * .029 + Vector((0, 0, -.045)), c + o * .036 + Vector((0, 0, -.005)), c + o * .026 + Vector((0, 0, .038)),
                  c + o * .007 + Vector((0, 0, .062))], (.007, .008, .005, .001), 'Bract')
    for j in range(6):
        o = Vector((math.cos(j * TAU / 6), math.sin(j * TAU / 6), 0)); p = c + Vector((0, 0, -.05))
        g.strand([p + o * .018, p + o * .036 + Vector((0, 0, -.008)), p + o * .045 + Vector((0, 0, -.032))], (.006, .005, .001), 'Bract')
    g.ball(c + Vector((0, 0, .062)), .012, 'Yellow', seg=10, rings=6)


def yellow_bloom(g, rng, c=Vector((-.37, .18, .2)), facing=Vector((-.2, -.35, 1))):
    """Open dandelion flower: four rings of strap florets cupping up to an orange centre."""
    b0 = Vector((-.32, .15, .03)); n_ = facing.normalized(); u, v = frame(n_)
    g.strand([b0, (b0 + c) / 2 + Vector((-.012, .01, 0)), c - n_ * .012], (.008, .007, .0065), 'StemGel', sides=6)
    g.ball(c - n_ * .014, .02, 'Bract', scale=(1, 1, .8))
    for count, length, tilt in ((28, .052, 10), (22, .042, 28), (16, .03, 48), (10, .018, 68)):
        for j in range(count):
            a = j * TAU / count + rng.uniform(-.08, .08) + count; o = u * math.cos(a) + v * math.sin(a)
            el = math.radians(tilt + rng.uniform(-5, 5)); dv = o * math.cos(el) + n_ * math.sin(el)
            p0 = c + o * .006
            g.flat([p0, p0 + dv * length * .5, p0 + dv * length - n_ * (.004 if tilt < 20 else 0)], .0042, .0056, 'Yellow', n_)
    g.ball(c + n_ * .008, .011, 'FloretCentre', scale=(1, 1, .6))
    for j in range(10):
        o = u * math.cos(j * TAU / 10) + v * math.sin(j * TAU / 10); p = c - n_ * .02
        g.strand([p + o * .012, p + o * .03 - n_ * .01, p + o * .034 - n_ * .035], (.005, .004, .001), 'Bract')


def base(B, rng, leaf_mat='DLeaf', dew=0, dew_mat='ClearGlass', dew_r=(.007, .019), pebble_mats=('Pearl', 'Pearl', 'AquaPebble'),
         crystals=0, bubbles=0, grass=380):
    """Production Base: Rosette (leaves, crown, bud, yellow bloom, pebbles) and a wind-shader Grass ring."""
    g = Geo(); leaves = []
    for i in range(9):
        yaw = i * TAU / 9 + .2 + rng.uniform(-.08, .08); long = i % 2 == 0
        leaves.append(leaf(g, yaw, (.74 if long else .6) * rng.uniform(.94, 1.06), .1 if long else .085, rng,
                           teeth=4 if long else 3, z0=0 if long else .007, mat=leaf_mat))
    g.ball((0, 0, .04), .1, 'StemGel', scale=(1, 1, .42), seg=20, rings=10)
    bud(g); yellow_bloom(g, rng)
    for k in range(10):
        r = rng.uniform(.72, .96); a = rng.uniform(0, TAU); s = rng.uniform(.045, .085)
        g.ball((r * math.cos(a), r * math.sin(a), s * .22), s, pebble_mats[k % len(pebble_mats)],
               scale=(1, rng.uniform(.7, 1), .5), rot=(0, 0, a), seg=16, rings=8)
    for _ in range(dew):
        p = rng.choice(leaves)(rng.uniform(.25, .85), rng.uniform(-.55, .55)); r = rng.uniform(*dew_r)
        g.ball(p + Vector((0, 0, r * .6)), r, dew_mat, scale=(1, 1, .82), seg=16, rings=10)
    for k in range(crystals):                                  # small clusters of hexagonal sky crystals
        a = k * TAU / crystals + .9 + rng.uniform(-.25, .25); r = rng.uniform(.6, .8)
        c = Vector((r * math.cos(a), r * math.sin(a), 0))
        for j in range(rng.randint(3, 5)):
            out = Vector((math.cos(a), math.sin(a), 0)) * rng.uniform(.2, .7) + Vector((rng.uniform(-.4, .4), rng.uniform(-.4, .4), 1))
            out.normalize(); L = rng.uniform(.11, .24) * (1.4 if j == 0 else 1); w = L * .2
            p0 = c + Vector((rng.uniform(-.03, .03), rng.uniform(-.03, .03), -.01))
            g.prism(p0, p0 + out * L * .78, w, w, 'Crystal', sides=6); g.prism(p0 + out * L * .78, p0 + out * L, w, .0008, 'Crystal', sides=6)
    for k in range(bubbles):
        a = k * TAU / bubbles + 2.2; r = rng.uniform(.5, .7); s = rng.uniform(.04, .07)
        g.ball((r * math.cos(a), r * math.sin(a), s + rng.uniform(.04, .3)), s, 'Bubble', seg=32, rings=16)
    g.done('Rosette', parent=B)
    if grass: grass_patch('Grass', grass, .55, 1.0, h=(.08, .22), seed=rng.randint(1, 999), parent=B)


BASE_ST = dict(n=100, k=14, theta=62, ach='AcheneAqua', beak='Beak', pap='Pappus', rec='Azure', bw=.0015, curl=.36, pl=.4,
               ach_w=.014, ach_len=.42, ach_sides=4, beak_w=.0016, bottom=-.7, smooth=True, stem='StemGel', stem_r=.011, bract='Bract')


def clock(g, top, R, s, rng, keep=None):
    """A dandelion clock: receptacle, achenes, beaks and pappus umbrellas on a golden spiral.
    Returns the seeds (umbrella centre, direction, hue) and the bristle tips (tip, direction, hue)."""
    top = Vector(top); rec = s.get('rec_r', .17 * R)
    if not s.get('no_rec'): (g.ico if s.get('rec_ico') else g.ball)(top, rec, s['rec'])
    seeds, tips = [], []
    for i in range(s['n']):
        z = 1 - (1 - s['bottom']) * (i + .5) / s['n']; rr = math.sqrt(max(0., 1 - z * z)); a = GOLD * i
        d = (Vector((math.cos(a) * rr, math.sin(a) * rr, z)) + Vector([rng.uniform(-.06, .06) for _ in range(3)])).normalized()
        if keep and not keep(d, rng): continue
        hue = rng.random(); aw = s['ach_w'] * R / .2
        p0, pa, pb = top + d * rec * .8, top + d * R * s['ach_len'], top + d * R * .7
        g.seed = (pb.copy(), hue)                          # the seed turns and flies about its umbrella centre
        g.strand([p0, p0.lerp(pa, .45), pa], (aw * .45, aw, aw * .3), s['ach'], sides=s['ach_sides'], hue=hue)
        g.prism(pa, pb, s['beak_w'], s['beak_w'] * .8, s['beak'], hue=hue)
        u, v = frame(d); seeds.append((pb, d, hue))
        for j in range(s['k']):
            phi = j * TAU / s['k'] + rng.uniform(-.3, .3) + i; th = math.radians(s['theta']) + rng.uniform(-.12, .12)
            b = d * math.cos(th) + (u * math.cos(phi) + v * math.sin(phi)) * math.sin(th)
            L = R * s['pl'] * rng.uniform(.93, 1.05); m = pb + b * L * .55
            e = m + (b * (1 - s['curl']) + d * s['curl']).normalized() * L * .48; w = s['bw']
            g.prism(pb, m, w, w * .7, s['pap'], 0, .55, hue); g.prism(m, e, w * .7, w * .25, s['pap'], .55, 1, hue)
            tips.append((e, d, hue))
    return seeds, tips


def bracts(g, top, R, s, rng, length=1.):
    """Reflexed bracts hanging under the head, as on a ripe clock."""
    rec = s.get('rec_r', .17 * R)
    for j in range(9):
        a = j * TAU / 9 + rng.uniform(-.15, .15); o = Vector((math.cos(a), math.sin(a), 0)); L = length * R
        p0 = top + o * rec * .6 + Vector((0, 0, -rec * .75))
        g.strand([p0, p0 + o * L * .12 + Vector((0, 0, -L * .04)), p0 + o * L * .18 + Vector((0, 0, -L * .17)),
                  p0 + o * L * .16 + Vector((0, 0, -L * .32))], (.0045, .004, .003, .001), s['bract'])


def parachute(g, p, up, s, rng, size=.16, k=14, width=None):
    """One loose seed floating pappus-up: achene, beak and a flat umbrella of bristles.
    width (optional) thickens the bristles and beak so a seed reads at machine distance (the Silk Turbine)."""
    p, up = Vector(p), Vector(up).normalized(); u, v = frame(up); hue = rng.random(); tips = []
    g.seed = (p.copy(), hue)
    a0, a1 = p - up * size, p - up * size * .62; aw = .008 * size / .16
    g.strand([a0, a0.lerp(a1, .5), a1], (aw * .3, aw, aw * .5), s['ach'], sides=s['ach_sides'], hue=hue)
    bw = (.0017, .0012, .0004) if width is None else (width, width * .75, width * .3)
    g.prism(a1, p, .0018 if width is None else width * .9, .0015 if width is None else width * .8, s['beak'], hue=hue)
    for j in range(k):
        phi = j * TAU / k + rng.uniform(-.2, .2); th = math.radians(78 + rng.uniform(-8, 6))
        b = up * math.cos(th) + (u * math.cos(phi) + v * math.sin(phi)) * math.sin(th)
        L = size * .42 * rng.uniform(.9, 1.1); m = p + b * L * .55; e = m + (b * .8 + up * .2).normalized() * L * .48
        g.prism(p, m, bw[0], bw[1], s['pap'], 0, .55, hue); g.prism(m, e, bw[1], bw[2], s['pap'], .55, 1, hue)
        tips.append((e, up, hue))
    return tips
