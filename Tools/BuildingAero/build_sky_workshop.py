"""Sky workshop: the Wind Condenser processor and the Cloud Dandelion sky essence node (production models).

Requested 2026-11-04. The user picked Wind Condenser variant 03A (Bliss Hill) for the sky processor and
sky essence concept 03 (Cloud Dandelions) for the resource, to be enhanced and animated in Unity.
Run inside the connected Blender (MCP socket). Builds its own scene, 'Aero_SkyWorkshop', exports:
  Assets/_Project/Models/Resources/WindCondenser.fbx
  Assets/_Project/Models/Resources/SkyDandelions.fbx
and saves the data to ArtSource/Resources/Sky_Workshop.blend without changing the open file.

Every part that moves in Unity is its own object with its pivot where it turns:
  WindCondenser  Base, Grass (vertex colour: albedo + wind weight in alpha), Daisy_0..n, MachineBody, Orb,
                 OrbGlow, OrbCloud, CloudMass (fill, pivot at its base), Rotor (pivot at the hub), CloudRing,
                 WindSpiral_0/1, Feed, StatusLight_0..2, BlockSocket
  SkyDandelions  SkyEssence (container: Bloom with child Stalk_1/2, Seed_0..4), Rosette, Grass
Materials are named Workshop_* so the Unity importer remaps them to the project's materials.
Front faces -Y (Unity -Z). Exported like build_processor_family.py (no baked space transform).
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

KIT = 'D:/PersonalProjects/the-tower-of-babel/Tools/BuildingAero/sky_concept_kit.py'
SCENE_NAME = 'Aero_SkyWorkshop'
exec(open(KIT, encoding='utf-8').read(), globals())
OUT = 'D:/PersonalProjects/the-tower-of-babel/Assets/_Project/Models/Resources/'
BLEND = 'D:/PersonalProjects/the-tower-of-babel/ArtSource/Resources/Sky_Workshop.blend'
CONDENSER_SCALE = .75


# ---------------------------------------------------------------- Workshop materials (preview look in Blender)
def W(name, color, metal=0., rough=.4, emit=0., alpha=1.):
    m = bpy.data.materials.get('Workshop_' + name) or bpy.data.materials.new('Workshop_' + name); m.use_nodes = True
    b = next(x for x in m.node_tree.nodes if x.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*color, 1); b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough; b.inputs['Alpha'].default_value = alpha
    if emit: b.inputs['Emission Color'].default_value = (*color, 1); b.inputs['Emission Strength'].default_value = emit
    return m


WM = {
    'Pearl': W('Pearl', (.84, .93, .98), rough=.26), 'Azure': W('Azure', (.025, .3, .66), .45, .25),
    'Chrome': W('Ingot', (.85, .88, .92), .95, .15), 'SkyGlass': W('SkyGlass', (.55, .78, 1), rough=.05, alpha=.5),
    'SkyGlow': W('SkyGlow', (.72, .78, 1), emit=2), 'Cloud': W('Cloud', (.97, .98, 1), rough=.55),
    'Meadow': W('Meadow', (.2, .66, .14), rough=.25), 'GrassBlade': W('GrassBlade', (1, 1, 1), rough=.5),
    'Frost': W('FrostBlade', (.85, .95, 1), rough=.2, alpha=.6), 'Wind': W('WindRibbon', (.85, .95, 1), emit=.4, alpha=.35),
    'Daisy': W('Daisy', (1, 1, 1), rough=.4), 'Pollen': W('Pollen', (1, .8, .15), emit=.3),
    'Lime': W('LeafGlow', (.45, 1, .35), emit=2), 'Leaf': W('Leaf', (.16, .6, .12), rough=.3),
    'Stem': W('Stem', (.42, .7, .25), rough=.4), 'RiverStone': W('RiverStone', (.58, .64, .68), rough=.45),
}
SWAP = {'Concept_Cloud': 'Cloud', 'Concept_SkyGlass': 'SkyGlass', 'Concept_SkyGlow': 'SkyGlow', 'Concept_Pearl': 'Pearl'}
for me in SKY.values():
    for i, m in enumerate(me.materials):
        if m and m.name.split('.')[0] in SWAP: me.materials[i] = WM[SWAP[m.name.split('.')[0]]]
MAT.update(WM)


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


def grass_vc(name, count, rmin, rmax, h, ground, seed, width=.022, lean=.35, avoid=None):
    """Grass blades with vertex colour: dark root to lime tip (RGB) and the shader's wind weight (alpha, 0 at the root)."""
    rng = random.Random(seed); bm = bmesh.new(); col = bm.loops.layers.color.new('Col')
    made = 0
    while made < count:
        r = math.sqrt(rng.uniform(rmin * rmin, rmax * rmax)); a = rng.uniform(0, TAU)
        x, y = r * math.cos(a), r * math.sin(a)
        if avoid and avoid(x, y): continue
        made += 1
        z = ground(x, y) - .01; hh = rng.uniform(*h); face = rng.uniform(0, TAU)
        lx, ly = math.cos(face) * lean * rng.uniform(.3, 1), math.sin(face) * lean * rng.uniform(.3, 1)
        side = Vector((-ly, lx, 0)).normalized()
        tip = (rng.uniform(.62, .82), 1, rng.uniform(.22, .4)) if rng.random() > .12 else (.85, .95, .3)
        rows = []
        for t in (0, .35, .7, 1):
            c = Vector((x + lx * hh * t * t, y + ly * hh * t * t, z + hh * t * (1 - .25 * t * t)))
            w = width * (1 - t) ** .8
            rows.append((bm.verts.new(c - side * w), bm.verts.new(c + side * w), t))
        for (a0, b0, t0), (a1, b1, t1) in zip(rows[:-1], rows[1:]):
            f = bm.faces.new((a0, b0, b1, a1))
            for loop, t in zip(f.loops, (t0, t0, t1, t1)):
                k = t ** .9
                loop[col] = (.04 + (tip[0] - .04) * k, .3 + (tip[1] - .3) * k, .05 + (tip[2] - .05) * k, t ** 1.5)
    o = make(name, bm, 'GrassBlade')
    return o


def flower(base, height, petal_mat, centre_mat, s=1., tilt=(0, 0, 0)):
    """A small flower on a stem; returns its objects (stem base at base)."""
    with Capture() as c:
        top = (base[0], base[1], base[2] + height)
        rod('Stem', base, top, .006 * s, 'Stem', seg=5)
        head = empty('Head', top); head.rotation_euler = tilt
        for i in range(8):
            a = i * TAU / 8
            sphere('Petal', (math.cos(a) * .028 * s, math.sin(a) * .028 * s, 0), .026 * s, petal_mat, scale=(1, .45, .18),
                   rot=(0, 0, a), seg=6, rings=4, parent=head)
        sphere('Centre', (0, 0, .006 * s), .013 * s, centre_mat, scale=(1, 1, .6), seg=8, rings=5, parent=head)
    return c.objects


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


# ================================================================ Wind Condenser (03A Bliss Hill, enhanced)
root = begin('WindCondenser', 0)
g = bumps((0, 0, .36, .72), (.62, .45, .12, .42), (-.66, .32, .1, .45), (.1, -.7, .05, .4))
path = lambda x, y: abs(x) < .26 and y < -.42                   # pebble path to the tray stays clear of grass
with Capture() as base:
    terrain('Hill', 1.2, g, 'Meadow', rings=16, seg=64, subsurf=0)
    for i, t in enumerate([.0, .17, .34, .51, .68]):
        y = -1.12 + t * .95; x = .05 * math.sin(i * 1.7)
        sphere('PathStone', (x, y, g(x, y) + .005), .085 - .008 * i, 'Pearl', scale=(1.2, .9, .22), rot=(0, 0, i), seg=12, rings=6)
    torus('OrbSeat', (0, 0, g(0, 0) - .04), .32, .045, 'Pearl', seg=40, minor=8)
    lathe('Tray', [(0, -.04), (.12, -.05), (.3, 0), (.34, .05), (.31, .07), (.27, .04), (0, .03)], 'Pearl', loc=(0, -1.3, .3))
    torus('TrayRim', (0, -1.3, .355), .325, .016, 'Azure', seg=32, minor=6)
    rod('TrayStem', (0, -1.3, 0), (0, -1.3, .27), .045, 'Chrome')
    lathe('TrayFoot', [(0, 0), (.13, 0), (.1, .04), (0, .04)], 'Pearl', loc=(0, -1.3, g(0, -1.3)))
    rng = random.Random(77)
    for i in range(7):                                         # glossy dew bubbles in the grass
        r = rng.uniform(.55, 1.05); a = rng.uniform(.3, 2.9) if i % 2 else rng.uniform(3.4, 6.0)
        x, y = r * math.cos(a), r * math.sin(a)
        if path(x, y): continue
        sphere('Dew', (x, y, g(x, y) + .05), rng.uniform(.03, .05), 'SkyGlass', seg=10, rings=6)
merge('Base', base.objects)
grass_vc('Grass', 470, .44, 1.18, (.09, .22), g, 11, avoid=path)
rng = random.Random(5); placed = 0
while placed < 8:
    r = rng.uniform(.5, 1.06); a = rng.uniform(0, TAU); x, y = r * math.cos(a), r * math.sin(a)
    if path(x, y) or (abs(x) < .4 and y < -.78): continue
    z = g(x, y)
    merge('Daisy_%d' % placed, flower((x, y, z - .01), rng.uniform(.09, .14), 'Daisy', 'Pollen',
                                      rng.uniform(1.15, 1.5), (rng.uniform(-.3, .3), rng.uniform(-.3, .3), 0)), (x, y, z))
    placed += 1
orb_z = g(0, 0) - .06 + .42
with Capture() as body:
    torus('Collar', (0, 0, orb_z + .41), .13, .045, 'Azure', seg=32, minor=8)
    cyl('Tower', (0, 0, orb_z + .42 + .73), .085, 1.46, 'Pearl', top=.05, seg=20)
    for z, r in [(orb_z + .85, .08), (orb_z + 1.45, .066)]: torus('Band', (0, 0, z), r, .014, 'Azure', seg=24, minor=6)
    sphere('Nacelle', (0, .02, 2.65), .13, 'Pearl', scale=(1, 2.4, 1), seg=24, rings=12)
    torus('NacelleBand', (0, -.12, 2.65), .118, .014, 'Azure', rot=(math.pi / 2, 0, 0), seg=24, minor=6)
    rbox('TailVane', (0, .38, 2.7), (.018, .2, .17), 'Pearl', .008)
merge('MachineBody', body.objects)
sphere('Orb', (0, 0, orb_z), .42, 'SkyGlass', seg=40, rings=20)
torus('OrbGlow', (0, 0, orb_z), .425, .016, 'SkyGlow', seg=56, minor=6)
oc = cloud((0, 0, orb_z - .24), .3, name='OrbCloud'); decimate(merge('OrbCloud', [oc], (0, 0, orb_z - .24)), .3)
cm = cloud((0, 0, orb_z - .36), .34, name='CloudMass'); cm.scale = (.34, .34, .55); decimate(merge('CloudMass', [cm], (0, 0, orb_z - .36)), .3)
hub = (0, -.34, 2.65)
with Capture() as rotor:
    sphere('Hub', (0, -.33, 2.65), .1, 'Azure', scale=(1, .8, 1), seg=20, rings=10)
    sphere('HubTip', (0, -.42, 2.65), .045, 'Pearl')
    for i in range(3):
        a = i * TAU / 3 + .3; L = .5
        sphere('Blade', (math.sin(a) * L, -.36, 2.65 + math.cos(a) * L), L, 'Frost', scale=(.16, .03, 1), rot=(0, a, 0), seg=16, rings=14)
        cyl('Cuff', (math.sin(a) * .13, -.36, 2.65 + math.cos(a) * .13), .035, .08, 'Pearl', rot=(0, a, 0), seg=12)
merge('Rotor', rotor.objects, hub)
with Capture() as ring:
    for i in range(8):
        a = i * TAU / 8; r = .8
        tuft((math.cos(a) * r, math.sin(a) * r, 1.95 + math.sin(a) * .16), .5 + .14 * (i % 3), (0, 0, a))
decimate(merge('CloudRing', ring.objects, (0, 0, 1.95)), .22)
for k in range(2):
    w = wind_ribbon('WindSpiral', spiral((0, 0), .55, .3, 1.05, 2.45, 1.2, 12, phase=k * math.pi), .07)
    merge('WindSpiral_%d' % k, [w], (0, 0, 1.7))
f = tuft((0, -.52, 2.62), 1.25); decimate(merge('Feed', [f], (0, -.52, 2.62)), .35)
for i, x in enumerate((-.16, 0, .16)):
    sphere('StatusLight_%d' % i, (x, -.93, g(x, -.93) + .055), .035, 'Lime', seg=16, rings=8)
empty('BlockSocket', (0, -1.3, .36))
bake_scale(root, CONDENSER_SCALE)
export(root, OUT + 'WindCondenser.fbx')


# ================================================================ Cloud Dandelions (sky essence 03, enhanced)
node = begin('SkyDandelions', 0)
container = empty('SkyEssence', (0, 0, 0))
with Capture() as rosette:
    for i in range(9):
        a = i * TAU / 9 + .2; r = .33 if i % 2 else .42; s = .34 if i % 2 else .42
        sphere('RosetteLeaf', (math.cos(a) * r, math.sin(a) * r, .035), s, 'Leaf', scale=(1, .36, .07), rot=(0, -.16, a), seg=20, rings=10)
        sphere('LeafRib', (math.cos(a) * r, math.sin(a) * r, .06), s * .9, 'Stem', scale=(1, .04, .03), rot=(0, -.16, a), seg=12, rings=4)
    sphere('Crown', (0, 0, .05), .12, 'Leaf', scale=(1, 1, .45), seg=16, rings=8)
    rod('BudStem', (.28, .26, .04), (.33, .3, .3), .009, 'Stem', seg=6)
    sphere('Bud', (.33, .3, .33), .045, 'Leaf', scale=(1, 1, 1.6), seg=12, rings=8)
    sphere('BudTip', (.33, .3, .39), .022, 'Cloud', seg=10, rings=6)
    with Capture() as yb: flower((-.34, .16, .04), .2, 'Pollen', 'Pollen', 1.6, (.2, -.2, 0))
    pebbles(10, .72, .95, (.05, .1), seed=31, mat='RiverStone')
merge('Rosette', rosette.objects)
grass_vc('Grass', 380, .55, 1.0, (.08, .22), lambda x, y: 0, 23)


def stalk(name, base, height, lean, r, seeds, parent):
    top = Vector((base[0] + lean[0], base[1] + lean[1], height))
    with Capture() as c:
        ribbon('Stem', [base, ((base[0] + top.x) / 2 + lean[0] * .2, (base[1] + top.y) / 2 + lean[1] * .2, height * .55), tuple(top)], .012, 'Stem')
        sphere('Receptacle', top, .045, 'SkyGlow', seg=14, rings=8)
        sphere('CloudHead', top, r * .86, 'Cloud', seg=20, rings=10)
        gold = math.pi * (3 - math.sqrt(5))
        for i in range(seeds):
            z = 1 - 2 * (i + .5) / seeds; rr = math.sqrt(1 - z * z); a = gold * i
            tip = top + Vector((math.cos(a) * rr, math.sin(a) * rr, z)) * r
            rod('Pappus', top, tip, .003, 'Daisy', seg=4)
            sphere('Fluff', tip, .036, 'Cloud', scale=(1, 1, .8), seg=8, rings=5)
    o = merge(name, c.objects, base, parent)
    return o


into(container)
bloom = stalk('Bloom', (0, 0, .02), 1.0, (.05, .05), .17, 30, container)
stalk('Stalk_1', (.12, -.05, .02), .8, (.3, -.12), .15, 26, bloom)
stalk('Stalk_2', (-.1, .05, .02), .64, (-.28, .1), .14, 24, bloom)
for i, (p, s) in enumerate([((.55, -.2, .95), .42), ((.7, .1, .82), .36), ((-.5, -.25, .78), .38), ((.2, .45, .9), .34), ((-.35, .35, 1.0), .3)]):
    t = tuft(p, s, (0, 0, i * 1.3)); decimate(merge('Seed_%d' % i, [t], p, container), .3)
export(node, OUT + 'SkyDandelions.fbx')
node.location.x = 4

bpy.data.libraries.write(BLEND, {scene}, fake_user=True, compress=True)
report = {}
for r in (root, node):
    meshes = [o for o in r.children_recursive if o.type == 'MESH']
    report[r.name] = (sum(len(o.data.polygons) for o in meshes), sorted({o.name for o in r.children_recursive})[:60])
for k, (faces, names) in report.items(): print(k, faces, 'faces;', ', '.join(names))
def bb(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return tuple(round(min(p[i] for p in pts), 2) for i in range(3)), tuple(round(max(p[i] for p in pts), 2) for i in range(3))
for n in ('Base', 'MachineBody', 'Rotor', 'Orb', 'CloudMass', 'Feed', 'Bloom', 'Stalk_1', 'Rosette', 'Seed_0', 'Grass'):
    o = bpy.data.objects.get(n)
    if o: print(n, 'origin', tuple(round(v, 2) for v in o.matrix_world.translation), 'bounds', bb(o))
