"""Essence nodes and blocks. Run inside the project's connected Blender (Blender MCP socket).

Essence nodes are grounded natural resources, like the stone cluster and sand mound: a spring pool, a
clump of water bubbles and a fallen cloud. Each has a base that stays during cooldown (pebbles, moss,
sky crystals) and an essence container that is gathered. Processors live in build_processor_family.py.

Metres, Z up, front faces -Y. FBX exports are Y up with baked axes, matching the sand/glass workshop.
Material names map to Unity materials through the importer remap in EssenceWorkshopAuthoring.
"""
import bpy, math, random, os
from mathutils import Vector, Euler

ROOT = 'D:/PersonalProjects/the-tower-of-babel'
OUT = ROOT + '/Assets/_Project/Models/Resources'
os.makedirs(OUT, exist_ok=True)
os.makedirs(ROOT + '/ArtSource/Resources', exist_ok=True)
old = bpy.data.scenes.get('Aero_EssenceWorkshop')
if old:
    # Re-runs replace the previous build so object and FBX names never gain .001 suffixes.
    for o in list(old.objects): bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.scenes.remove(old)
for block in (bpy.data.meshes, bpy.data.curves):
    for item in list(block):
        if item.users == 0: block.remove(item)
scene = bpy.data.scenes.new('Aero_EssenceWorkshop')
bpy.context.window.scene = scene
random.seed(4242)

PALETTE = {
    'Pearl': ((.84, .93, .98, 1), .18, .26, 0), 'Azure': ((.025, .3, .66, 1), .45, .25, 0),
    'Steel': ((.12, .19, .23, 1), .7, .32, 0), 'Glass': ((.14, .76, .89, 1), .25, .12, 0),
    'RiverGlass': ((.1, .74, .62, 1), .1, .05, 0), 'RiverGlow': ((.35, 1, .78, 1), 0, .2, 2),
    'WaterGlass': ((.18, .55, .98, 1), .1, .04, 0), 'WaterFoam': ((.9, .97, 1, 1), 0, .1, 0),
    'SkyGlass': ((.64, .83, 1, 1), .05, .05, 0), 'SkyGlow': ((.72, .78, 1, 1), 0, .2, 2),
    'Cloud': ((.97, .98, 1, 1), 0, .55, 0),
    'RiverStone': ((.58, .64, .68, 1), 0, .45, 0), 'Moss': ((.34, .58, .44, 1), 0, .7, 0),
}
materials = {}
for name, (color, metal, rough, emit) in PALETTE.items():
    m = bpy.data.materials.get('Workshop_' + name) or bpy.data.materials.new('Workshop_' + name)
    m.use_nodes = True
    node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = color
    node.inputs['Metallic'].default_value = metal; node.inputs['Roughness'].default_value = rough
    if emit:
        node.inputs['Emission Color'].default_value = color; node.inputs['Emission Strength'].default_value = emit
    materials[name] = m


def link(o):
    if o.name not in scene.collection.objects: scene.collection.objects.link(o)
    return o


def finish(o, name, material, parent=None, smooth=True, apply_rotation=False):
    o.name = name
    if material: o.data.materials.append(materials[material])
    if parent: o.parent = parent
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=apply_rotation, scale=True)
    if smooth and o.type == 'MESH':
        for p in o.data.polygons: p.use_smooth = True
    return o


def empty(name, parent=None, loc=(0, 0, 0)):
    o = link(bpy.data.objects.new(name, None)); o.location = loc
    if parent: o.parent = parent
    return o


def box(name, loc, size, material, parent=None, bevel=.04, segments=3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.dimensions = size
    finish(o, name, material, parent, smooth=False)
    if bevel:
        m = o.modifiers.new('Soft edges', 'BEVEL'); m.width = bevel; m.segments = segments
        bpy.ops.object.modifier_apply(modifier=m.name)
        m = o.modifiers.new('Weighted', 'WEIGHTED_NORMAL'); bpy.ops.object.modifier_apply(modifier=m.name)
    return o


def cylinder(name, loc, radius, depth, material, parent=None, vertices=32, top=None):
    if top is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=top, depth=depth, location=loc)
    o = finish(bpy.context.object, name, material, parent, smooth=False)
    for p in o.data.polygons: p.use_smooth = len(p.vertices) == 4
    return o


def sphere(name, loc, radius, material, parent=None, segments=24, rings=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=loc)
    return finish(bpy.context.object, name, material, parent)


def torus(name, loc, major, minor, material, parent=None, tilt=(0, 0, 0), segments=40):
    bpy.ops.mesh.primitive_torus_add(major_segments=segments, minor_segments=8, location=loc,
                                     major_radius=major, minor_radius=minor, rotation=tilt)
    return finish(bpy.context.object, name, material, parent, apply_rotation=True)


def join(objects, name):
    objects = [o for o in objects if o is not None]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join(); objects[0].name = name
    return objects[0]


def reparent(o, parent):
    bpy.context.view_layer.update()
    world = o.matrix_world.copy(); o.parent = parent; o.matrix_world = world


def helix(name, origin, radius, height, turns, thickness, material, parent=None, taper=0.0, points=72):
    """Tube that spirals around the object's own Z axis; origin stays on the axis for spinning."""
    curve = bpy.data.curves.new(name, 'CURVE'); curve.dimensions = '3D'
    curve.bevel_depth = thickness; curve.bevel_resolution = 1; curve.use_fill_caps = True; curve.resolution_u = 2
    spline = curve.splines.new('NURBS'); spline.points.add(points - 1); spline.use_endpoint_u = True
    for i in range(points):
        t = i / (points - 1); a = t * turns * 2 * math.pi
        r = radius * (1 - taper * t)
        spline.points[i].co = (math.cos(a) * r, math.sin(a) * r, (t - .5) * height, 1)
        spline.points[i].radius = math.sin(min(1, t * 6) * math.pi / 2) * math.sin(min(1, (1 - t) * 6) * math.pi / 2) + .05
    o = link(bpy.data.objects.new(name, curve)); o.location = origin
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.object; o.name = name
    return finish(o, name, material, parent)


def teardrop(name, loc, radius, material, parent=None, stretch=1.35):
    o = sphere(name, loc, radius, material, parent, 28, 18)
    for v in o.data.vertices:
        z = v.co.z / radius
        if z > 0:
            pinch = 1 - .62 * z ** 1.6
            v.co.x *= pinch; v.co.y *= pinch; v.co.z *= stretch
    return o


def puff(name, loc, radius, parent=None, material='Cloud'):
    parts = []
    for i, (dx, dy, dz, r) in enumerate([(0, 0, 0, 1), (.7, .1, -.15, .72), (-.68, -.05, -.18, .7),
                                          (.2, .5, -.1, .62), (-.25, -.45, -.12, .6), (.05, 0, .45, .66)]):
        parts.append(sphere(name + str(i), (loc[0] + dx * radius, loc[1] + dy * radius, loc[2] + dz * radius),
                            r * radius, material, None, 16, 10))
    o = join(parts, name)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    if parent: reparent(o, parent)
    return o


def crystal(name, loc, radius, material, parent=None, stretch=1.7):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius, location=loc)
    o = bpy.context.object
    for v in o.data.vertices: v.co.z *= stretch
    return finish(o, name, material, parent, smooth=False)


def wave_strip(name, loc, length, amplitude, width, material, parent=None, waves=1.5, thickness=.02):
    curve = bpy.data.curves.new(name, 'CURVE'); curve.dimensions = '3D'
    curve.bevel_depth = thickness; curve.bevel_resolution = 1; curve.extrude = width; curve.resolution_u = 2
    spline = curve.splines.new('NURBS'); n = 40; spline.points.add(n - 1); spline.use_endpoint_u = True
    for i in range(n):
        t = i / (n - 1)
        spline.points[i].co = ((t - .5) * length, 0, math.sin(t * waves * 2 * math.pi) * amplitude, 1)
    o = link(bpy.data.objects.new(name, curve)); o.location = loc
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object, name, material, parent)


# ---------------------------------------------------------------- Essence nodes
def mound(name, radius, height, material, squash=.86, segments=40, rings=8, wobble=.05, seed=0):
    """Irregular low dome with its origin on the ground, so scaling stays anchored."""
    verts = [(0, 0, height)]; faces = []
    for ring in range(1, rings + 1):
        t = ring / rings
        for j in range(segments):
            a = 2 * math.pi * j / segments
            r = radius * t * (1 + wobble * math.sin(a * 3 + seed) + wobble * .7 * math.sin(a * 7 + ring * .3 + seed))
            z = height * (1 - t ** 1.6)
            verts.append((r * math.cos(a), r * math.sin(a) * squash, z))
    for j in range(segments): faces.append((0, 1 + j, 1 + (j + 1) % segments))
    for ring in range(rings - 1):
        for j in range(segments):
            p0 = 1 + ring * segments + j; p1 = 1 + ring * segments + (j + 1) % segments
            faces.append((p0, p0 + segments, p1 + segments, p1))
    faces.append(tuple(reversed(range(1 + (rings - 1) * segments, 1 + rings * segments))))
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    o = link(bpy.data.objects.new(name, mesh))
    return finish(o, name, material)


def pebble(name, loc, size, material='RiverStone', flat=.45, spin=0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=size, location=loc)
    o = bpy.context.object
    for v in o.data.vertices:
        v.co.z *= flat; v.co.x *= 1.25
    o.rotation_euler[2] = spin
    return finish(o, name, material, apply_rotation=True)


def shard(name, loc, height, material, lean=(0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=height * .22, radius2=0, depth=height, location=(loc[0], loc[1], loc[2] + height * .4),
                                    rotation=(lean[0], lean[1], 0))
    return finish(bpy.context.object, name, material, smooth=False, apply_rotation=True)


random.seed(4242)
# River Essence: a spring pool ringed by smooth pebbles, with a glowing current and drifting wisps.
river = empty('RiverPool')
bed = [mound('PoolFloor', .92, .03, 'RiverStone', wobble=.04)]
for i in range(12):
    a = i * 2 * math.pi / 12 + random.uniform(-.12, .12)
    size = random.uniform(.13, .22)
    bed.append(pebble('Pebble', (math.cos(a) * .98, math.sin(a) * .84, size * .25), size, spin=a))
for x, y, sz in [(.55, .2, .07), (-.4, -.35, .06), (.1, .48, .05)]:
    bed.append(pebble('BedPebble', (x, y, .02), sz))
reparent(join(bed, 'PoolBed'), river)
riverEssence = empty('RiverEssence', river)
reparent(mound('Water', .86, .16, 'RiverGlass', wobble=.04, seed=1.3), riverEssence)
helix('Current', (0, 0, .17), .52, .02, 1.6, .02, 'RiverGlow', riverEssence, taper=.75)
for i, (x, y, z) in enumerate([(.3, .18, .42), (-.34, .08, .52), (.02, -.32, .36)]):
    sphere('Wisp_' + str(i), (x, y, z), .05, 'RiverGlow', riverEssence, 12, 8)

# Water Essence: a wobbling clump of clear water bubbles resting on a moss patch.
water = empty('WaterBubbles')
patch = [mound('MossPatch', 1.0, .06, 'Moss', wobble=.08, seed=2.1)]
for x, y, sz in [(.78, .3, .1), (-.72, -.28, .12), (.2, -.7, .08), (-.3, .66, .09)]:
    patch.append(pebble('MossPebble', (x, y, .04), sz))
reparent(join(patch, 'MossBed'), water)
waterEssence = empty('WaterEssence', water)
clump = [sphere('Big', (0, 0, .4), .42, 'WaterGlass', None, 32, 18)]
for x, y, z, r in [(.42, .1, .22, .26), (-.38, .16, .2, .25), (.06, -.4, .2, .24), (-.12, .38, .18, .2), (.3, -.28, .14, .16)]:
    clump.append(sphere('Lobe', (x, y, z), r, 'WaterGlass', None, 24, 14))
for x, y, z, r in [(.1, -.06, .42, .07), (-.12, .08, .55, .05), (.05, .12, .3, .04), (.28, .02, .26, .035)]:
    clump.append(sphere('Inner', (x, y, z), r, 'WaterFoam', None, 12, 8))
body = join(clump, 'Bubbles')
bpy.context.scene.cursor.location = (0, 0, 0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
reparent(body, waterEssence)
for i, (x, y, z, r) in enumerate([(.7, -.15, .14, .11), (-.62, -.3, .12, .09), (.2, .66, .1, .08), (-.05, -.1, .9, .07)]):
    sphere('Droplet_' + str(i), (x, y, z), r, 'WaterGlass', waterEssence, 16, 10)

# Sky Essence: a fallen cloud resting on glowing sky crystals.
sky = empty('SkyCloud')
stones = []
for i, (x, y, h, lean) in enumerate([(.75, .25, .42, (.35, .2)), (-.7, .3, .36, (-.3, .25)), (.15, -.72, .3, (.3, -.2)),
                                      (-.35, -.62, .26, (-.25, -.3)), (.55, -.45, .22, (.4, 0))]):
    stones.append(shard('Shard', (x, y, -.02), h, 'SkyGlow' if i % 2 == 0 else 'SkyGlass', lean))
for x, y, sz in [(.9, -.1, .09), (-.85, -.1, .08), (.1, .8, .07)]:
    stones.append(pebble('SkyPebble', (x, y, .03), sz, 'Cloud'))
reparent(join(stones, 'SkyStones'), sky)
skyEssence = empty('SkyEssence', sky)
cloud = [sphere('C', (x, y, z), r, 'Cloud', None, 20, 12) for x, y, z, r in [
    (0, 0, .36, .42), (.45, .1, .26, .32), (-.44, .05, .25, .33), (.1, .4, .24, .3), (-.1, -.38, .24, .3),
    (.32, -.3, .2, .24), (-.32, .32, .2, .24), (.15, .05, .62, .26), (-.18, -.05, .58, .22)]]
body = join(cloud, 'Cloud')
bpy.context.scene.cursor.location = (0, 0, 0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
reparent(body, skyEssence)
for i, (x, y, z) in enumerate([(.72, .25, .62), (-.66, -.2, .7), (.05, -.66, .78), (-.15, .6, .9)]):
    p = puff('Tuft_' + str(i), (x, y, z), .1 + (i % 2) * .03)
    reparent(p, skyEssence)

# ---------------------------------------------------------------- Processed blocks
def block_shell(name, glass):
    bpy.ops.mesh.primitive_cube_add(size=.42, location=(0, 0, 0)); o = bpy.context.object
    finish(o, name, glass, smooth=False)
    m = o.modifiers.new('Rounded', 'BEVEL'); m.width = .06; m.segments = 4; bpy.ops.object.modifier_apply(modifier=m.name)
    m = o.modifiers.new('Weighted', 'WEIGHTED_NORMAL'); bpy.ops.object.modifier_apply(modifier=m.name)
    return o


blocks = {}
rb = empty('RiverBlock')
inner_parts = [wave_strip('Wave', (0, 0, .03), .32, .05, .1, 'RiverGlow'),
               wave_strip('Wave2', (0, 0, -.07), .3, .035, .08, 'RiverGlow', waves=2.0)]
inner_parts.append(box('Bed', (0, 0, -.15), (.32, .32, .03), 'Pearl', bevel=.012))
reparent(join([block_shell('Block', 'RiverGlass')] + inner_parts, 'Block'), rb); blocks['RiverBlock'] = rb

wb = empty('WaterBlock')
inner_parts = [sphere('B', (x, y, z), r, 'WaterFoam', None, 14, 9)
               for x, y, z, r in [(.03, .02, -.02, .085), (-.08, -.05, .08, .05), (.09, -.07, .1, .035), (-.06, .08, -.1, .04)]]
inner_parts.append(box('Bed', (0, 0, -.15), (.32, .32, .03), 'Azure', bevel=.012))
reparent(join([block_shell('Block', 'WaterGlass')] + inner_parts, 'Block'), wb); blocks['WaterBlock'] = wb

sb = empty('SkyBlock')
cl = puff('C', (0, 0, -.02), .075)
star = crystal('Star', (.06, -.04, .1), .045, 'SkyGlow', stretch=1.4)
inner_parts = [cl, star, box('Bed', (0, 0, -.15), (.32, .32, .03), 'Pearl', bevel=.012)]
reparent(join([block_shell('Block', 'SkyGlass')] + inner_parts, 'Block'), sb); blocks['SkyBlock'] = sb

mote = empty('EssenceMote')
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=.5)
finish(bpy.context.object, 'EssenceMoteMesh', 'WaterFoam', mote)

exports = [river, water, sky, mote] + list(blocks.values())
for root in exports:
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for child in root.children_recursive: child.select_set(True)
    bpy.ops.export_scene.fbx(filepath=OUT + '/' + root.name + '.fbx', use_selection=True, object_types={'MESH', 'EMPTY'},
                             bake_space_transform=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_anim=False, add_leaf_bones=False)

# Spread source models out for an easy Blender review, after export.
for i, root in enumerate(exports): root.location.x = (i - len(exports) / 2) * 2.6
bpy.ops.wm.save_as_mainfile(filepath=ROOT + '/ArtSource/Resources/Essence_Nodes_And_Blocks.blend')
faces = {r.name: sum(len(o.data.polygons) for o in r.children_recursive if o.type == 'MESH') for r in exports}
print('Exported', faces)
