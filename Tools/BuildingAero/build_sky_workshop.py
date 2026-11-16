"""Sky workshop: the Wind Condenser processor (production model), rebuilt on 2026-11-15 as the Silk Turbine.

History: on 2026-11-04 the user picked Wind Condenser variant 03A (Bliss Hill). On 2026-11-15, after the sky essence
became the Silk Clock dandelion, the user chose proposition 01 Silk Turbine from the Round 03 concepts
(Documentation/FrutigerAero/Resources/SKY_PROCESSOR_CONCEPTS.md) and asked to remove the green base and make the
machine details more hand-painted. The Cloud Dandelions node moved to build_sky_dandelions.py.

The Silk Turbine:
  - a real three-blade turbine: tapered tower, streamlined nacelle with a tail vane, spinner and slender twisted
    blades; no hill, grass or daisies: it stands on a small painted pad that carries the glass cloud orb;
  - hand-painted: every pearl, azure and blade surface samples Textures/SilkTurbinePaint.png, brushed stroke by stroke
    by paint_silk_turbine.py (run it first). paint_uv() wraps each part's atlas region around it;
  - Silk Clock seeds instead of cloud: the input (Feed) is two puffs of parachute seeds, a loose ring of seeds orbits
    the tower where the cloud ring was, and two silk spirals carry seeds down the tower into the orb, where silk
    knits into the cloud (OrbSilk).
Seeds use the node's Dandelion Seeds material (vertex colours, per-seed pivot UVs); with _Release at 0 they stay put.
Enhanced the same day ("enhance the base with better visuals and make the inside cloud better"):
  - the pad became a two-tier plinth: a petal-scalloped foot with glass dew beads, a glass sky channel with drifting
    clouds (ChannelClouds), a drum with the lamps in azure bezels, the painted wind-swirl medallion on top
    (Textures/SilkTurbineTop.png, also in the tray's dish) and a painted walkway to the tray;
  - the orb's clouds are fluffy painted cumulus meshes (fluffy_cloud) for the Orb Cloud shader, with silk ribbons
    (OrbSilk) and a few seeds (OrbSeeds) sinking into the idle cloud.

Hierarchy (every part that moves in Unity is its own object, pivot where it turns):
  WindCondenser  Base, ChannelClouds, MachineBody, Orb, OrbGlow, OrbCloud (children OrbSilk, OrbSeeds),
                 CloudMass (fill, pivot at its base), Rotor (pivot at the hub), SeedRing,
                 SilkSpiral_0/1 (children SilkSeeds_0/1), Feed, StatusLight_0..2, BlockSocket
Materials are named Workshop_* so the Unity importer remaps them. Front faces -Y (Unity -Z).
Run headless:  blender -b --factory-startup --python build_sky_workshop.py      (REVIEW = True renders a still)
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, noise

ROOTP = 'D:/PersonalProjects/the-tower-of-babel'
SCENE_NAME = 'Aero_SkyWorkshop'
exec(open(ROOTP + '/Tools/BuildingAero/sky_concept_kit.py', encoding='utf-8').read(), globals())
exec(open(ROOTP + '/Tools/BuildingAero/dandelion_kit.py', encoding='utf-8').read(), globals())
OUT = ROOTP + '/Assets/_Project/Models/Resources/'
TEX = OUT + 'Textures/SilkTurbinePaint.png'
TOP_TEX = OUT + 'Textures/SilkTurbineTop.png'                   # the plinth's painted medallion
BLEND = ROOTP + '/ArtSource/Resources/Sky_Workshop.blend'
REVIEW = globals().get('REVIEW', os.environ.get('SKY_WORKSHOP_REVIEW') == '1')
CONDENSER_SCALE = .75
PEARL, AZURE, BLADE = (.56, .98), (.31, .52), (.03, .27)          # atlas regions painted by paint_silk_turbine.py


# ---------------------------------------------------------------- Workshop materials (preview look in Blender)
def W(name, color, metal=0., rough=.4, emit=0., alpha=1.):
    m = bpy.data.materials.get('Workshop_' + name) or bpy.data.materials.new('Workshop_' + name); m.use_nodes = True
    b = next(x for x in m.node_tree.nodes if x.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*color, 1); b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough; b.inputs['Alpha'].default_value = alpha
    if emit: b.inputs['Emission Color'].default_value = (*color, 1); b.inputs['Emission Strength'].default_value = emit
    return m


def painted(name, rough, alpha=1., texture=TEX):
    """Preview of a Unity material that samples a painted texture (the atlas, or the plinth's medallion)."""
    m, n, l, b = _mat('Workshop_' + name); _set(b, rough=rough, coat=.5, coat_rough=.06, alpha=alpha)
    tex = n.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(texture, check_existing=True)
    l.new(tex.outputs['Color'], b.inputs['Base Color'])
    return m


def vertex_coloured(name, emit=0., **kw):
    """Preview of the Dandelion Seeds material: albedo in the vertex colour, emission weight in its alpha."""
    m, n, l, b = _mat('Workshop_' + name); _set(b, **kw)
    attr = n.new('ShaderNodeVertexColor'); attr.layer_name = 'Col'
    l.new(attr.outputs['Color'], b.inputs['Base Color'])
    if emit:
        l.new(attr.outputs['Color'], b.inputs['Emission Color'])
        k = n.new('ShaderNodeMath'); k.operation = 'MULTIPLY'; k.inputs[1].default_value = emit
        l.new(attr.outputs['Alpha'], k.inputs[0]); l.new(k.outputs[0], b.inputs['Emission Strength'])
    return m


WM = {
    'Paint': painted('SilkTurbinePaint', .32), 'Blade': painted('SilkTurbineBlade', .2, .8),
    'Top': painted('SilkTurbineTop', .3, texture=TOP_TEX), 'OrbCloud': vertex_coloured('OrbCloud', emit=.6, rough=.6, sss=.3),
    'SkyGlass': W('SkyGlass', (.55, .78, 1), rough=.05, alpha=.5), 'SkyGlow': W('SkyGlow', (.72, .78, 1), emit=2),
    'OrbGlass': W('OrbGlass', (.82, .93, 1), rough=.02, alpha=.2),          # crystal clear, so the cloud inside reads
    'Cloud': W('Cloud', (.97, .98, 1), rough=.55), 'Wind': W('WindRibbon', (.85, .95, 1), emit=.4, alpha=.35),
    'Lime': W('LeafGlow', (.45, 1, .35), emit=2), 'Seeds': vertex_coloured('DandelionSeeds', emit=1.2, rough=.4, sss=.15),
}
SWAP = {'Concept_Cloud': 'Cloud', 'Concept_SkyGlass': 'SkyGlass', 'Concept_SkyGlow': 'SkyGlow', 'Concept_Pearl': 'Paint'}
for me in SKY.values():
    for i, m in enumerate(me.materials):
        if m and m.name.split('.')[0] in SWAP: me.materials[i] = WM[SWAP[m.name.split('.')[0]]]
MAT.update(WM)
for key in ('Pappus', 'AcheneAqua', 'Beak'): MAT[key] = WM['Seeds']           # colour keys of the seed material
SEED_COLORS = {'Pappus': (1, 1, 1, .75), 'AcheneAqua': (.22, .64, .92, .12), 'Beak': (.92, .97, 1, .45)}   # as the node
ST = {**BASE_ST}


# ---------------------------------------------------------------- assembly helpers
class Capture:
    """Collects every object a block of helper calls creates."""
    def __enter__(self): self.before = set(COLL.objects); return self
    def __exit__(self, *a): self.objects = [o for o in COLL.objects if o not in self.before]


def merge(name, objs, pivot=(0, 0, 0), parent=None):
    """Join objects (modifiers and curves evaluated) into one mesh whose origin is pivot; deletes the sources."""
    dg = bpy.context.evaluated_depsgraph_get(); pivot = Vector(pivot)
    bm = bmesh.new(); mats = []
    for o in objs:
        if o.type not in ('MESH', 'CURVE'): continue
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        tmp = bmesh.new(); tmp.from_mesh(me)
        bmesh.ops.transform(tmp, matrix=Matrix.Translation(-pivot) @ o.matrix_world, verts=tmp.verts)
        slots = [s.material for s in o.material_slots] or [None]
        for f in tmp.faces:
            mat = slots[min(f.material_index, len(slots) - 1)]
            if mat not in mats: mats.append(mat)
            f.material_index = mats.index(mat)
        part = bpy.data.meshes.new('_tmp'); tmp.to_mesh(part); tmp.free(); ev.to_mesh_clear()
        bm.from_mesh(part); bpy.data.meshes.remove(part)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    o = bpy.data.objects.new(name, me); COLL.objects.link(o); o.parent = parent or ROOTOBJ
    bpy.context.view_layer.update(); o.matrix_world = Matrix.Translation(pivot)
    for src in objs:
        if src.name in bpy.data.objects: bpy.data.objects.remove(src, do_unlink=True)
    return o


def decimate(o, ratio):
    """Clouds come from the 960/2160-face SkyCloud meshes; collapse them to a game budget on export."""
    m = o.modifiers.new('Decimate', 'DECIMATE'); m.ratio = ratio
    return o


def paint_uv(o, region):
    """Wrap a painted atlas region around a part (local space): u around its Z axis, v up it, so brush strokes run
    up towers and around bands, and every part is shaded from a cool foot to a warm, sunlit top."""
    me = o.data; uv = me.uv_layers.get('UVMap') or me.uv_layers.new(name='UVMap')
    zs = [v.co.z for v in me.vertices]; lo, hi = min(zs), max(zs); span = max(hi - lo, 1e-6)
    for poly in me.polygons:
        cos = [me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
        us = [math.atan2(c.y, c.x) / TAU + .5 for c in cos]
        if max(us) - min(us) > .5: us = [u + 1 if u < .5 else u for u in us]       # the texture repeats in u
        for li, c, u in zip(poly.loop_indices, cos, us):
            t = (c.z - lo) / span
            uv.data[li].uv = (u, region[0] + (region[1] - region[0]) * (.02 + .96 * t))
    return o


def export(root, path):
    bpy.ops.object.select_all(action='DESELECT'); root.select_set(True)
    for c in root.children_recursive: c.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
                             bake_space_transform=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False, colors_type='SRGB')


def bake_scale(root, s):
    """Scale the whole hierarchy and apply it, so every object exports with scale 1 and its pivot kept."""
    done = set()
    for o in root.children_recursive:                      # parents carry no scale, so local offsets scale exactly
        o.location = o.location * s
        if o.type == 'MESH' and o.data.name not in done: o.data.transform(Matrix.Scale(s, 4)); done.add(o.data.name)


top = bpy.data.collections.new('SkyWorkshop'); scene.collection.children.link(top)


def begin(name, x):
    global COLL, ROOTOBJ
    COLL = bpy.data.collections.new(name); top.children.link(COLL)
    ROOTOBJ = bpy.data.objects.new(name, None); COLL.objects.link(ROOTOBJ); ROOTOBJ.location = (x, 0, 0)
    return ROOTOBJ


def blade_bm(L, r0=.07, n=16, m=12, thick=.2, twist=(18, 2)):
    """A slender twisted airfoil along +Z: narrow root, widest near 15 %, tapering to a narrow tip. UVs map the
    painted blade region: u from root to tip, v across the chord (1 = the leading edge)."""
    def chord(t):
        return .06 + .07 * (t / .15) ** .7 if t < .15 else .13 - .095 * ((t - .15) / .85) ** .9
    bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap'); rows = []; param = {}
    for i in range(n + 1):
        t = i / n; c = chord(t); tw = math.radians(twist[0] + (twist[1] - twist[0]) * t); ring = []
        for k in range(m):
            a = k * TAU / m; x = math.cos(a) * c * .5
            y = math.sin(a) * c * thick * .5 * (1.25 - .5 * (math.cos(a) * .5 + .5))      # thicker toward the leading edge
            v = bm.verts.new((x * math.cos(tw) - y * math.sin(tw), x * math.sin(tw) + y * math.cos(tw), r0 + t * L))
            param[v] = (t, .5 + .5 * math.cos(a)); ring.append(v)
        rows.append(ring)
    for a_, b_ in zip(rows[:-1], rows[1:]):
        for k in range(m): bm.faces.new((a_[k], a_[(k + 1) % m], b_[(k + 1) % m], b_[k]))
    bm.faces.new(rows[0][::-1]); bm.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        for loop in f.loops:
            t, w = param[loop.vert]; loop[uv].uv = (t, BLADE[0] + (BLADE[1] - BLADE[0]) * (.04 + .92 * w))
    return bm


def seed(g, p, rng, size=.2, up=None):
    """One Silk Clock parachute seed, thickened to read at machine distance (production scale 0.75 brings the bristles
    to the node's 2.4 mm)."""
    up = up or (Vector((.3, -.1, 1)) + Vector((rng.uniform(-.3, .3), rng.uniform(-.3, .3), 0))).normalized()
    parachute(g, p, up, ST, rng, size=size, k=12, width=.0032)


def tower_r(z, prof):
    for (r0, z0), (r1, z1) in zip(prof[:-1], prof[1:]):
        if z0 <= z <= z1: return r0 + (r1 - r0) * (z - z0) / max(z1 - z0, 1e-6)
    return prof[-1][0]


def quat_to(d, up_from=(0, 0, 1)):
    return Vector(up_from).rotation_difference(Vector(d).normalized())


def orient(o, q):
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = q
    return o


def scallop_lathe(name, prof, mat, lobes=12, depth=.03, scalloped=None, seg=72):
    """A lathe whose profile points (all, or the indices in `scalloped`) swell into petal lobes round the axis."""
    bm = bmesh.new(); rings = []
    for i, (r, z) in enumerate(prof):
        swell = scalloped is None or i in scalloped
        rings.append([bm.verts.new(((r * (1 + depth * math.cos(lobes * a)) if swell else r) * math.cos(a),
                                    (r * (1 + depth * math.cos(lobes * a)) if swell else r) * math.sin(a), z))
                      for a in (k * TAU / seg for k in range(seg))])
    for a_, b_ in zip(rings[:-1], rings[1:]):
        for k in range(seg): bm.faces.new((a_[k], a_[(k + 1) % seg], b_[(k + 1) % seg], b_[k]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return make(name, bm, mat)


def medallion(name, centre, R, dome, rings=8, seg=48):
    """A gently domed disc mapped flat onto the painted medallion (UV 0.5, 0.5 at the centre, the rim at radius 0.5)."""
    bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap'); c = Vector(centre)
    mid = bm.verts.new(c + Vector((0, 0, dome))); prev = None
    for i in range(1, rings + 1):
        r = R * i / rings
        ring = [bm.verts.new(c + Vector((r * math.cos(a), r * math.sin(a), dome * (1 - (r / R) ** 2)))) for a in (k * TAU / seg for k in range(seg))]
        for k in range(seg):
            f = bm.faces.new((mid, ring[k], ring[(k + 1) % seg]) if prev is None else (prev[k], ring[k], ring[(k + 1) % seg], prev[(k + 1) % seg]))
            for loop in f.loops:
                d = loop.vert.co - c; loop[uv].uv = (.5 + d.x / (2 * R), .5 + d.y / (2 * R))
        prev = ring
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return make(name, bm, 'Top')


def fluffy_cloud(name, base, lobes, faces=1800, voxel=.011, seed=0, parent=None):
    """A soft cumulus: overlapping lobes fused by a voxel remesh, smoothed, given a flat base and decimated. Its
    vertex colours are painted like a Frutiger cloud: lilac-blue underneath, white through the middle, warm cream on
    the sunlit top, with soft lilac and aqua patches; alpha is the glow weight (the top glows most). Pivot at base."""
    bm = bmesh.new(); base = Vector(base)
    for c, r in lobes: bmesh.ops.create_icosphere(bm, subdivisions=3, radius=r, matrix=Matrix.Translation(base + Vector(c)))
    me = bpy.data.meshes.new(name + '_src'); bm.to_mesh(me); bm.free()
    src = bpy.data.objects.new(name + '_src', me); COLL.objects.link(src)
    rm = src.modifiers.new('Remesh', 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = voxel; rm.adaptivity = 0
    sm = src.modifiers.new('Smooth', 'SMOOTH'); sm.factor = .9; sm.iterations = 10
    dg = bpy.context.evaluated_depsgraph_get(); dense = len(src.evaluated_get(dg).data.polygons)
    dc = src.modifiers.new('Decimate', 'DECIMATE'); dc.ratio = min(1., faces / max(2 * dense, 1))     # quads -> triangles
    dg = bpy.context.evaluated_depsgraph_get(); out = bpy.data.meshes.new_from_object(src.evaluated_get(dg))
    bpy.data.objects.remove(src, do_unlink=True); bpy.data.meshes.remove(me)
    flat = base.z + .03                                          # cumulus clouds sit on a flat base
    for v in out.vertices:
        if v.co.z < flat: v.co.z = flat + (v.co.z - flat) * .25
    out.transform(Matrix.Translation(-base)); out.update()
    out.shade_smooth()
    zs = [v.co.z for v in out.vertices]; z0, z1 = min(zs), max(zs)
    sun = Vector((-.45, -.55, .7)).normalized(); off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    col = out.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    under, white, cream = Vector((.64, .7, .92)), Vector((.97, .98, 1)), Vector((1, .95, .84))
    lilac, aqua = Vector((.82, .78, .97)), Vector((.78, .93, .98))
    for loop in out.loops:
        v = out.vertices[loop.vertex_index]; n = v.normal; t = (v.co.z - z0) / max(z1 - z0, 1e-6)
        c = under.lerp(white, min(1, max(0, (t - .12) / .5)) ** .8)
        c = c.lerp(cream, .55 * max(0, n.dot(sun)) * min(1, t * 1.6))
        nz = noise.noise(v.co * 9 + off)
        if nz > .25: c = c.lerp(lilac, min(.45, (nz - .25) * 1.2))
        elif nz < -.3: c = c.lerp(aqua, min(.4, (-.3 - nz) * 1.2))
        col.data[loop.index].color_srgb = (c.x, c.y, c.z, .25 + .55 * t)
    out.materials.append(MAT['OrbCloud'])
    o = bpy.data.objects.new(name, out); COLL.objects.link(o); o.parent = parent or ROOTOBJ; o.location = base
    return o


# ================================================================ Silk Turbine (Round 03 proposition 01)
root = begin('WindCondenser', 0)
rng = random.Random(2026_11_15)
P = lambda o: paint_uv(o, PEARL)
A = lambda o: paint_uv(o, AZURE)

# The plinth (enhanced 2026-11-15): a two-tier painted pearl plinth instead of the meadow hill. Its foot swells into
# twelve petal lobes like a flower, each tipped with a glass dew bead, round a glass sky channel where small clouds
# drift. The upper drum carries the lamps in azure bezels and is topped by the painted wind-swirl medallion, which
# shows through the glass orb. A painted walkway joins it to the tray.
FOOT = [(.36, 0), (.6, 0), (.625, .012), (.632, .028), (.618, .043), (.59, .052), (.572, .052), (.568, .034), (.472, .034),
        (.468, .052), (.46, .052)]
LAMPS = (-104, -90, -76)                                         # degrees round the drum; -90 faces the player
with Capture() as base:
    P(scallop_lathe('Foot', FOOT, 'Paint', scalloped={1, 2, 3, 4, 5}))
    A(scallop_lathe('FootBand', [(.627, .016), (.637, .022), (.637, .033), (.627, .039)], 'Paint'))
    for k in range(12):                                          # a glass dew bead on each petal tip
        a = k * TAU / 12; sphere('Dew', (math.cos(a) * .606 * 1.03, math.sin(a) * .606 * 1.03, .058), .017, 'SkyGlass', seg=10, rings=6)
    lathe('SkyChannel', [(.474, .038), (.566, .038), (.566, .047), (.474, .047)], 'SkyGlass', seg=72, subsurf=0, closed=True)
    P(lathe('Drum', [(.46, .05), (.465, .062), (.466, .078), (.462, .095), (.455, .106), (.446, .112), (.44, .114)], 'Paint', seg=64, subsurf=0))
    A(torus('DrumBand', (0, 0, .06), .466, .008, 'Paint', seg=64, minor=6))
    medallion('Medallion', (0, 0, .114), .442, .008)
    P(torus('OrbSeat', (0, 0, .16), .3, .045, 'Paint', seg=40, minor=8))
    for deg in LAMPS:                                            # azure bezels round the lamps
        d = Vector((math.cos(math.radians(deg)), math.sin(math.radians(deg)), 0))
        orient(A(torus('Bezel', tuple(d * .464 + Vector((0, 0, .082))), .034, .008, 'Paint', seg=24, minor=6)), quat_to(d))
    P(rbox('Walkway', (0, -.885, .012), (.2, .5, .024), 'Paint', .01))
    A(rbox('WalkwayInlay', (0, -.885, .025), (.036, .46, .006), 'Paint', .003))
    P(lathe('Tray', [(0, -.04), (.12, -.05), (.3, 0), (.34, .05), (.31, .07), (.27, .04), (0, .03)], 'Paint', loc=(0, -1.3, .3)))
    medallion('TrayMedallion', (0, -1.3, .334), .27, .004, rings=5, seg=40)   # a small swirl in the dish under the block
    A(torus('TrayRim', (0, -1.3, .355), .325, .016, 'Paint', seg=32, minor=6))
    A(rod('TrayStem', (0, -1.3, 0), (0, -1.3, .27), .045, 'Paint'))
    P(lathe('TrayFoot', [(0, 0), (.13, 0), (.1, .04), (0, .04)], 'Paint', loc=(0, -1.3, 0), subsurf=0))
merge('Base', base.objects)
with Capture() as puffs:                                         # small clouds drifting round the sky channel
    for k in range(6):
        a = k * TAU / 6 + .3; tuft((math.cos(a) * .52, math.sin(a) * .52, .052), .21, (0, 0, a * 2.3))
decimate(merge('ChannelClouds', puffs.objects, (0, 0, .05)), .18)

oz = .16 + .045 + math.sqrt(.42 ** 2 - .3 ** 2)                  # the orb rests on its seat ring
sphere('Orb', (0, 0, oz), .42, 'OrbGlass', seg=40, rings=20)
torus('OrbGlow', (0, 0, oz), .425, .016, 'SkyGlow', seg=56, minor=6)
# The cloud inside (enhanced 2026-11-15): a fluffy painted cumulus instead of the decimated cloud mound. It drifts
# and billows in the Orb Cloud shader; silk ribbons and a few seeds sink into it and turn with it.
oc = fluffy_cloud('OrbCloud', (0, 0, oz - .17), [
    ((0, 0, .12), .13), ((.11, .02, .08), .1), ((-.12, .03, .08), .1), ((.03, .1, .08), .095), ((-.02, -.1, .085), .1),
    ((.06, -.03, .19), .085), ((-.06, .05, .18), .08), ((.19, -.04, .05), .06), ((-.2, .02, .05), .06), ((0, .17, .05), .06),
    ((.05, -.17, .05), .055)], faces=1300, seed=1)
silk = [wind_ribbon('OrbSilkRibbon', [(math.cos(k * TAU / 3 + t * 7.5) * (.13 - .09 * t), math.sin(k * TAU / 3 + t * 7.5) * (.13 - .09 * t),
                                 oz + .36 - .3 * t) for t in (i / 9 for i in range(10))], .016) for k in range(3)]
bpy.context.view_layer.update()
merge('OrbSilk', silk, (0, 0, oz - .17), oc)
g = Geo(colors=SEED_COLORS, seed_data=True)
for k in range(5):
    a = k * TAU / 5 + .5; r = rng.uniform(.05, .13)
    seed(g, (math.cos(a) * r, math.sin(a) * r, oz + .07 + rng.uniform(0, .1)), rng, .085)
g.done('OrbSeeds', (0, 0, oz - .17), oc)
fluffy_cloud('CloudMass', (0, 0, oz - .38), [
    ((0, 0, .08), .12), ((.14, .03, .14), .1), ((-.14, .02, .14), .1), ((.02, .14, .14), .095), ((0, -.14, .14), .1),
    ((.18, .08, .2), .085), ((-.17, -.09, .2), .085), ((-.08, .18, .2), .08), ((.1, -.18, .2), .08), ((0, 0, .2), .11)],
    faces=1100, seed=2)

hz = 2.2
TOWER = [(.09, oz + .38), (.075, oz + .85), (.055, hz - .3), (.048, hz - .1), (0, hz - .1)]
with Capture() as body:
    A(torus('Collar', (0, 0, oz + .41), .12, .04, 'Paint', seg=32, minor=8))
    P(lathe('Tower', TOWER, 'Paint', seg=24))
    for z in (oz + .7, hz - .45): A(torus('Band', (0, 0, z), tower_r(z, TOWER) + .004, .013, 'Paint', seg=24, minor=6))
    A(torus('YawRing', (0, .02, hz - .1), .062, .014, 'Paint', seg=24, minor=6))
    P(sphere('Nacelle', (0, .1, hz), .1, 'Paint', scale=(1, 2.7, 1.05), seg=24, rings=12))
    A(torus('NacelleBand', (0, -.1, hz), .07, .012, 'Paint', rot=(math.pi / 2, 0, 0), seg=24, minor=6))
    P(rbox('TailVane', (0, .42, hz + .04), (.016, .16, .13), 'Paint', .006))
merge('MachineBody', body.objects)

hub = (0, -.22, hz)
with Capture() as rotor:
    A(lathe('Spinner', [(.105, 0), (.1, .05), (.07, .12), (.03, .17), (0, .185)], 'Paint', loc=(0, -.16, hz), rot=(math.pi / 2, 0, 0), seg=24))
    P(cyl('HubDisc', (0, -.2, hz), .1, .06, 'Paint', rot=(math.pi / 2, 0, 0), seg=24))
    for k in range(3):
        a = k * TAU / 3 + .35; radial = Vector((math.sin(a), 0, math.cos(a)))
        make('Blade', blade_bm(.8), 'Blade', loc=hub, rot=(0, a, 0))
        P(cyl('Cuff', tuple(Vector(hub) + radial * .12), .032, .07, 'Paint', rot=(0, a, 0), seg=12))
merge('Rotor', rotor.objects, hub)

# Silk Clock seeds: the input, the drifting ring and the two silk spirals that carry seeds down into the orb.
F = Vector((0, -.5, hz - .06)); trail = Vector((0, -.22, -.27))   # the second puff trails toward the player
g = Geo(colors=SEED_COLORS, seed_data=True)
for c, n in ((F, 7), (F + trail, 6)):
    for _ in range(n): seed(g, c + Vector((rng.gauss(0, .07), rng.gauss(0, .07), rng.gauss(0, .05))), rng, rng.uniform(.17, .22))
g.done('Feed', F, loc=tuple(F))
ring_z = 1.55
g = Geo(colors=SEED_COLORS, seed_data=True)
for i in range(10):
    a = i * TAU / 10 + .2; seed(g, (math.cos(a) * .85, math.sin(a) * .85, ring_z + math.sin(a) * .12), rng, .18)
g.done('SeedRing', (0, 0, ring_z), loc=(0, 0, ring_z))
for k in range(2):
    sp = spiral((0, 0), .22, .17, hz - .25, oz + .5, 1.7, 16, phase=-1.0 + k * math.pi)
    mid = (0, 0, (hz - .25 + oz + .5) / 2)
    ribbon_obj = merge('SilkSpiral_%d' % k, [wind_ribbon('SilkSpiral', sp, .035, 'Wind')], mid)
    g = Geo(colors=SEED_COLORS, seed_data=True)
    for i in range(1 + k, 15, 2): seed(g, Vector(sp[i]) + Vector((0, 0, .05)), rng, .15)
    g.done('SilkSeeds_%d' % k, mid, ribbon_obj)

for i, deg in enumerate(LAMPS):                                  # three green lamps set in the drum's bezels
    d = Vector((math.cos(math.radians(deg)), math.sin(math.radians(deg)), 0))
    sphere('StatusLight_%d' % i, tuple(d * .468 + Vector((0, 0, .082))), .027, 'Lime', seg=16, rings=8)
empty('BlockSocket', (0, -1.3, .36))
bake_scale(root, CONDENSER_SCALE)
export(root, OUT + 'WindCondenser.fbx')

bpy.data.libraries.write(BLEND, {scene}, fake_user=True, compress=True)
meshes = [o for o in root.children_recursive if o.type == 'MESH']
print('WindCondenser', sum(len(o.data.polygons) for o in meshes), 'faces;', ', '.join(sorted(o.name for o in root.children_recursive)))


def bb(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return tuple(round(min(p[i] for p in pts), 2) for i in range(3)), tuple(round(max(p[i] for p in pts), 2) for i in range(3))


bpy.context.view_layer.update()
for n in ('Base', 'ChannelClouds', 'MachineBody', 'Rotor', 'Orb', 'OrbCloud', 'CloudMass', 'Feed', 'SeedRing', 'SilkSpiral_0'):
    o = bpy.data.objects.get(n)
    if o: print(n, 'origin', tuple(round(v, 2) for v in o.matrix_world.translation), 'bounds', bb(o))

if REVIEW:                                                       # a quick Blender still of the exported model
    SHOT = ROOTP + '/_Screenshots-and-previews-to-delete/SilkTurbine/Blender-Review.png'
    os.makedirs(os.path.dirname(SHOT), exist_ok=True)
    world = bpy.data.worlds.new('Review'); world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.55, .75, 1, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .9
    scene.world = world
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); scene.collection.objects.link(sun)
    sun.data.energy = 3.5; sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.lens = 50; target = Vector((0, -.2, 1.05)); a, e = math.radians(-34), math.radians(14)
    cam.location = target + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * 4.6
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.engine = 'BLENDER_EEVEE'; scene.eevee.taa_render_samples = 48
    scene.render.resolution_x, scene.render.resolution_y = 900, 1100
    scene.view_settings.view_transform = 'Standard'; scene.view_settings.exposure = -.3
    scene.render.filepath = SHOT; bpy.ops.render.render(write_still=True, scene=scene.name)
    print('Review', SHOT)
